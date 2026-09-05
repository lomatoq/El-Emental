using System.Collections.Generic;
using Elemental.Runtime.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Runtime.Geometry
{
    /// <summary>Cached render-only chamfers. Collider/bond geometry is never changed.</summary>
    public static class EarthFractureBevelMeshBuilder
    {
        private sealed class Corner
        {
            public Vector3 Point;
            public float ShortestEdge = float.PositiveInfinity;
            public readonly List<Vector3> Normals = new();
            public readonly List<Vector3> Inset = new();
        }
        private readonly struct Edge
        {
            public Edge(int a, int b, Vector3 pa, Vector3 pb, Vector3 normal, int material)
            { A = a; B = b; PA = pa; PB = pb; Normal = normal; Material = material; }
            public readonly int A, B, Material;
            public readonly Vector3 PA, PB, Normal;
        }

        public static Mesh Create(Mesh source, EarthStoneBevelProfile profile) => Create(source,
            profile != null ? profile.Width : EarthStoneBevelProfile.DefaultWidth,
            profile != null ? profile.MaxLocalEdgeFraction : EarthStoneBevelProfile.DefaultMaxLocalEdgeFraction);

        public static Mesh Create(Mesh source, float width = 0.02f, float edgeFraction = 0.08f)
        {
            if (source == null || !source.isReadable) return source;
            Vector3[] points = source.vertices;
            Vector3[] sourceNormals = source.normals;
            Vector2[] sourceUv = source.uv;
            var sourceExtraUv = new List<Vector4>[7];
            var extraUv = new List<Vector4>[7];
            for (int channel = 1; channel < 8; channel++)
            {
                var values = new List<Vector4>();
                source.GetUVs(channel, values);
                if (values.Count != points.Length) continue;
                sourceExtraUv[channel - 1] = values;
                extraUv[channel - 1] = new List<Vector4>();
            }
            Color[] sourceColors = source.colors;
            var corners = new List<Corner>();
            var welded = new Dictionary<Vector3Int, int>();
            var ids = new int[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 p = points[i];
                var key = new Vector3Int(Mathf.RoundToInt(p.x * 100000f),
                    Mathf.RoundToInt(p.y * 100000f), Mathf.RoundToInt(p.z * 100000f));
                if (!welded.TryGetValue(key, out int id))
                { id = corners.Count; welded.Add(key, id); corners.Add(new Corner { Point = p }); }
                ids[i] = id;
            }
            int[][] triangles = new int[source.subMeshCount][];
            for (int s = 0; s < triangles.Length; s++)
            {
                triangles[s] = source.GetTriangles(s);
                for (int t = 0; t + 2 < triangles[s].Length; t += 3)
                {
                    int a = triangles[s][t], b = triangles[s][t + 1], c = triangles[s][t + 2];
                    Vector3 n = Vector3.Cross(points[b] - points[a], points[c] - points[a]).normalized;
                    AddUnique(corners[ids[a]].Normals, n);
                    AddUnique(corners[ids[b]].Normals, n);
                    AddUnique(corners[ids[c]].Normals, n);
                    float ab = Vector3.Distance(points[a], points[b]);
                    float bc = Vector3.Distance(points[b], points[c]);
                    float ca = Vector3.Distance(points[c], points[a]);
                    corners[ids[a]].ShortestEdge = Mathf.Min(corners[ids[a]].ShortestEdge, Mathf.Min(ab, ca));
                    corners[ids[b]].ShortestEdge = Mathf.Min(corners[ids[b]].ShortestEdge, Mathf.Min(ab, bc));
                    corners[ids[c]].ShortestEdge = Mathf.Min(corners[ids[c]].ShortestEdge, Mathf.Min(bc, ca));
                }
            }
            width = float.IsFinite(width) ? Mathf.Max(0f, width) : EarthStoneBevelProfile.DefaultWidth;
            edgeFraction = float.IsFinite(edgeFraction) ? Mathf.Clamp(edgeFraction, 0f, .25f)
                : EarthStoneBevelProfile.DefaultMaxLocalEdgeFraction;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var colors = new List<Color>();
            var output = new List<int>[triangles.Length];
            for (int s = 0; s < output.Length; s++) output[s] = new List<int>();
            var edges = new Dictionary<ulong, Edge>();
            for (int s = 0; s < triangles.Length; s++)
            {
                for (int t = 0; t + 2 < triangles[s].Length; t += 3)
                {
                    int a = triangles[s][t], b = triangles[s][t + 1], c = triangles[s][t + 2];
                    Vector3 n = Vector3.Cross(points[b] - points[a], points[c] - points[a]).normalized;
                    Vector3 pa = Inset(corners[ids[a]], n, width, edgeFraction);
                    Vector3 pb = Inset(corners[ids[b]], n, width, edgeFraction);
                    Vector3 pc = Inset(corners[ids[c]], n, width, edgeFraction);
                    AddSource(a, pa, n, s); AddSource(b, pb, n, s); AddSource(c, pc, n, s);
                    Stitch(new Edge(ids[a], ids[b], pa, pb, n, s));
                    Stitch(new Edge(ids[b], ids[c], pb, pc, n, s));
                    Stitch(new Edge(ids[c], ids[a], pc, pa, n, s));
                }
            }
            for (int i = 0; i < corners.Count; i++)
            {
                Corner corner = corners[i];
                if (corner.Inset.Count < 3) continue;
                Vector3 n = Vector3.zero;
                for (int k = 0; k < corner.Normals.Count; k++) n += corner.Normals[k];
                n.Normalize();
                Vector3 tangent = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 side = Vector3.Cross(n, tangent);
                Vector3 center = Vector3.zero;
                for (int k = 0; k < corner.Inset.Count; k++) center += corner.Inset[k];
                center /= corner.Inset.Count;
                corner.Inset.Sort((a, b) => Mathf.Atan2(Vector3.Dot(a - center, side), Vector3.Dot(a - center, tangent))
                    .CompareTo(Mathf.Atan2(Vector3.Dot(b - center, side), Vector3.Dot(b - center, tangent))));
                for (int k = 0; k < corner.Inset.Count; k++)
                    AddBevelTriangle(center, corner.Inset[k], corner.Inset[(k + 1) % corner.Inset.Count], n, 0);
            }
            var mesh = new Mesh { name = source.name + " Beveled Render",
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetColors(colors);
            for (int channel = 1; channel < 8; channel++)
                if (extraUv[channel - 1] != null) mesh.SetUVs(channel, extraUv[channel - 1]);
            mesh.subMeshCount = output.Length;
            for (int s = 0; s < output.Length; s++) mesh.SetTriangles(output[s], s, false);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;

            void AddSource(int id, Vector3 point, Vector3 fallback, int submesh)
            {
                output[submesh].Add(vertices.Count); vertices.Add(point);
                normals.Add(sourceNormals.Length == points.Length ? sourceNormals[id] : fallback);
                uv.Add(sourceUv.Length == points.Length ? sourceUv[id] : new Vector2(point.x, point.z));
                colors.Add(sourceColors.Length == points.Length ? sourceColors[id] : Color.white);
                for (int channel = 0; channel < extraUv.Length; channel++)
                    extraUv[channel]?.Add(sourceExtraUv[channel][id]);
            }
            void AddBevelTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 n, int submesh)
            {
                if (Vector3.Cross(b - a, c - a).sqrMagnitude < 0.0000000001f) return;
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) < 0f) (b, c) = (c, b);
                Add(a); Add(b); Add(c);
                void Add(Vector3 p)
                {
                    output[submesh].Add(vertices.Count); vertices.Add(p); normals.Add(n);
                    uv.Add(new Vector2(p.x, p.z));
                    colors.Add(sourceColors.Length == points.Length ? new Color(1f, 0f, 0f, 0.1f) : Color.white);
                    for (int channel = 0; channel < extraUv.Length; channel++) extraUv[channel]?.Add(Vector4.zero);
                }
            }
            void Stitch(Edge edge)
            {
                ulong key = ((ulong)(uint)Mathf.Min(edge.A, edge.B) << 32) | (uint)Mathf.Max(edge.A, edge.B);
                if (!edges.TryGetValue(key, out Edge other)) { edges.Add(key, edge); return; }
                if (Vector3.Dot(edge.Normal, other.Normal) > 0.9995f) return;
                Vector3 oa = edge.A == other.A ? other.PA : other.PB;
                Vector3 ob = edge.B == other.B ? other.PB : other.PA;
                Vector3 n = (edge.Normal + other.Normal).normalized;
                AddBevelTriangle(edge.PA, edge.PB, ob, n, Mathf.Min(edge.Material, other.Material));
                AddBevelTriangle(edge.PA, ob, oa, n, Mathf.Min(edge.Material, other.Material));
            }
        }

        private static Vector3 Inset(Corner corner, Vector3 faceNormal, float width, float edgeFraction)
        {
            Vector3 inward = Vector3.zero;
            for (int i = 0; i < corner.Normals.Count; i++)
                inward -= Vector3.ProjectOnPlane(corner.Normals[i], faceNormal);
            Vector3 result = corner.Point + inward.normalized * Mathf.Min(width, corner.ShortestEdge * edgeFraction);
            bool exists = false;
            for (int i = 0; i < corner.Inset.Count; i++)
                if ((corner.Inset[i] - result).sqrMagnitude < 0.0000000001f) { exists = true; break; }
            if (!exists) corner.Inset.Add(result);
            return result;
        }

        private static void AddUnique(List<Vector3> normals, Vector3 normal)
        {
            if (normal.sqrMagnitude < 0.5f) return;
            for (int i = 0; i < normals.Count; i++)
                if (Vector3.Dot(normals[i], normal) > 0.9995f) return;
            normals.Add(normal);
        }
    }
}
