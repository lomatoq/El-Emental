using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Online
{
    /// <summary>Cold-path exact generated geometry transfer, preserving bevel normal/color/UV/material submeshes.</summary>
    public static class OnlineMeshCodec
    {
        public const int MaximumRawBytes = 8 * 1024 * 1024;
        public static byte[] Encode(Mesh mesh, out int rawLength, out ulong hash)
        {
            if (mesh == null || !mesh.isReadable) throw new InvalidOperationException("Uncatalogued online geometry must be CPU readable.");
            using var raw = new MemoryStream();
            using (var writer = new BinaryWriter(raw, System.Text.Encoding.UTF8, true))
            {
                Vector3[] vertices = mesh.vertices, normals = mesh.normals;
                Vector4[] tangents = mesh.tangents; Vector2[] uv = mesh.uv, uv2 = mesh.uv2; Color32[] colors = mesh.colors32;
                writer.Write(1); writer.Write(vertices.Length); writer.Write(mesh.subMeshCount);
                byte channels = (byte)((normals.Length == vertices.Length ? 1 : 0) | (tangents.Length == vertices.Length ? 2 : 0) |
                    (uv.Length == vertices.Length ? 4 : 0) | (uv2.Length == vertices.Length ? 8 : 0) | (colors.Length == vertices.Length ? 16 : 0));
                writer.Write(channels);
                for (int i = 0; i < vertices.Length; i++)
                {
                    Write(writer, vertices[i]);
                    if ((channels & 1) != 0) Write(writer, normals[i]);
                    if ((channels & 2) != 0) { Write(writer, (Vector3)tangents[i]); writer.Write(tangents[i].w); }
                    if ((channels & 4) != 0) { writer.Write(uv[i].x); writer.Write(uv[i].y); }
                    if ((channels & 8) != 0) { writer.Write(uv2[i].x); writer.Write(uv2[i].y); }
                    if ((channels & 16) != 0) { writer.Write(colors[i].r); writer.Write(colors[i].g); writer.Write(colors[i].b); writer.Write(colors[i].a); }
                }
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    if (mesh.GetTopology(sub) != MeshTopology.Triangles) throw new InvalidOperationException("Only triangle meshes belong in the Earth geometry registry.");
                    int[] indices = mesh.GetIndices(sub); writer.Write(indices.Length);
                    for (int i = 0; i < indices.Length; i++) writer.Write(indices[i]);
                }
            }
            byte[] bytes = raw.ToArray(); rawLength = bytes.Length;
            if (rawLength > MaximumRawBytes) throw new InvalidOperationException("Online mesh exceeds the per-mesh geometry budget.");
            hash = Hash(bytes);
            using var compressed = new MemoryStream();
            using (var deflate = new DeflateStream(compressed, System.IO.Compression.CompressionLevel.Fastest, true)) deflate.Write(bytes, 0, bytes.Length);
            return compressed.ToArray();
        }
        public static Mesh Decode(byte[] compressed, int expectedRawLength, ulong expectedHash)
        {
            if (expectedRawLength <= 0 || expectedRawLength > MaximumRawBytes) throw new InvalidOperationException("Invalid mesh size.");
            byte[] raw = new byte[expectedRawLength];
            using (var source = new MemoryStream(compressed, false))
            using (var deflate = new DeflateStream(source, CompressionMode.Decompress))
            {
                int offset = 0;
                while (offset < raw.Length) { int read = deflate.Read(raw, offset, raw.Length - offset); if (read == 0) break; offset += read; }
                if (offset != raw.Length || deflate.ReadByte() != -1) throw new InvalidOperationException("Geometry payload length differs from its declaration.");
            }
            if (Hash(raw) != expectedHash) throw new InvalidOperationException("Geometry checksum mismatch.");
            using var reader = new BinaryReader(new MemoryStream(raw, false));
            if (reader.ReadInt32() != 1) throw new InvalidOperationException("Unknown mesh format.");
            int count = reader.ReadInt32(), subCount = reader.ReadInt32(); byte channels = reader.ReadByte();
            if (count < 3 || count > 100000 || subCount < 1 || subCount > 32 || (channels & ~31) != 0)
                throw new InvalidOperationException("Invalid mesh dimensions.");
            var vertices = new Vector3[count]; var normals = (channels & 1) != 0 ? new Vector3[count] : null;
            var tangents = (channels & 2) != 0 ? new Vector4[count] : null;
            var uv = (channels & 4) != 0 ? new Vector2[count] : null; var uv2 = (channels & 8) != 0 ? new Vector2[count] : null;
            var colors = (channels & 16) != 0 ? new Color32[count] : null;
            for (int i = 0; i < count; i++)
            {
                vertices[i] = Read(reader);
                if (normals != null) normals[i] = Read(reader);
                if (tangents != null) { Vector3 xyz = Read(reader); tangents[i] = new Vector4(xyz.x, xyz.y, xyz.z, Finite(reader.ReadSingle())); }
                if (uv != null) uv[i] = new Vector2(Finite(reader.ReadSingle()), Finite(reader.ReadSingle()));
                if (uv2 != null) uv2[i] = new Vector2(Finite(reader.ReadSingle()), Finite(reader.ReadSingle()));
                if (colors != null) colors[i] = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
            }
            var mesh = new Mesh { name = "Online canonical geometry", indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            try
            {
                mesh.vertices = vertices;
                if (normals != null) mesh.normals = normals;
                if (tangents != null) mesh.tangents = tangents;
                if (uv != null) mesh.uv = uv;
                if (uv2 != null) mesh.uv2 = uv2;
                if (colors != null) mesh.colors32 = colors;
                mesh.subMeshCount = subCount;
                for (int sub = 0; sub < subCount; sub++)
                {
                    int length = reader.ReadInt32();
                    if (length < 0 || length > 600000 || length % 3 != 0) throw new InvalidOperationException("Invalid mesh triangle count.");
                    int[] indices = new int[length];
                    for (int i = 0; i < length; i++)
                    { int index = reader.ReadInt32(); if (index < 0 || index >= count) throw new InvalidOperationException("Triangle index is out of bounds."); indices[i] = index; }
                    mesh.SetTriangles(indices, sub, false);
                }
                if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidOperationException("Unexpected mesh payload suffix.");
                if (normals == null) mesh.RecalculateNormals();
                mesh.RecalculateBounds(); return mesh;
            }
            catch { UnityEngine.Object.Destroy(mesh); throw; }
        }
        private static void Write(BinaryWriter writer, Vector3 value) { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); }
        private static Vector3 Read(BinaryReader reader) => new Vector3(Finite(reader.ReadSingle()), Finite(reader.ReadSingle()), Finite(reader.ReadSingle()));
        private static float Finite(float value) => float.IsFinite(value) ? value : throw new InvalidOperationException("Non-finite mesh attribute.");
        public static ulong Hash(byte[] bytes)
        { ulong value = 14695981039346656037UL; for (int i = 0; i < bytes.Length; i++) { value ^= bytes[i]; value *= 1099511628211UL; } return value; }
    }
}
