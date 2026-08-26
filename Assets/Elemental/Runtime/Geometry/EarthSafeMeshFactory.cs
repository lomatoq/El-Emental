using System.Collections.Generic;
using UnityEngine;

namespace Elemental.Runtime.Geometry
{
    public static class EarthSafeMeshFactory
    {
        private static readonly Color MainStoneVertex = new Color(0.60f, 0.60f, 0.60f, 0.38f);
        private static readonly Color BevelStoneVertex = new Color(0.62f, 0.62f, 0.62f, 0.72f);
        private static readonly Color CornerStoneVertex = new Color(0.64f, 0.64f, 0.64f, 0.82f);

        /// <summary>
        /// Creates a closed visual stone block with real face, edge and corner
        /// chamfers. Gameplay can keep a simple BoxCollider; this mesh is intended
        /// for the renderer only.
        /// </summary>
        public static Mesh CreateBeveledBox(
            string name,
            Bounds requestedBounds,
            float bevel,
            uint seed = 1u)
        {
            var vertices = new List<Vector3>(96);
            var normals = new List<Vector3>(96);
            var colors = new List<Color>(96);
            var triangles = new List<int>(132);
            AppendBeveledBox(vertices, normals, colors, triangles, requestedBounds, bevel, seed);
            var mesh = new Mesh
            {
                name = string.IsNullOrWhiteSpace(name) ? "EarthBeveledStone" : name,
                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static void AppendBeveledBox(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color> colors,
            List<int> triangles,
            Bounds requestedBounds,
            float bevel,
            uint seed = 1u)
        {
            Vector3 size = requestedBounds.size;
            size.x = Mathf.Max(0.05f, Mathf.Abs(size.x));
            size.y = Mathf.Max(0.05f, Mathf.Abs(size.y));
            size.z = Mathf.Max(0.05f, Mathf.Abs(size.z));
            Vector3 center = requestedBounds.center;
            Vector3 half = size * 0.5f;
            float maximumBevel = Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.42f;
            float width = Mathf.Clamp(bevel, 0.004f, Mathf.Max(0.004f, maximumBevel));
            Vector3 inner = new Vector3(
                Mathf.Max(0.004f, half.x - width),
                Mathf.Max(0.004f, half.y - width),
                Mathf.Max(0.004f, half.z - width));
            float tone = Mathf.Lerp(0.965f, 1.035f, Hash01(seed ^ 0x51A71E5Du));
            Color main = MainStoneVertex * tone;
            Color edge = BevelStoneVertex * tone;
            Color corner = CornerStoneVertex * tone;
            main.a = MainStoneVertex.a;
            edge.a = BevelStoneVertex.a;
            corner.a = CornerStoneVertex.a;

            AppendSurface(vertices, normals, colors, triangles, new[]
            {
                center + new Vector3(half.x, -inner.y, -inner.z), center + new Vector3(half.x, -inner.y, inner.z),
                center + new Vector3(half.x, inner.y, inner.z), center + new Vector3(half.x, inner.y, -inner.z)
            }, Vector3.right, main);
            AppendSurface(vertices, normals, colors, triangles, new[]
            {
                center + new Vector3(-half.x, -inner.y, inner.z), center + new Vector3(-half.x, -inner.y, -inner.z),
                center + new Vector3(-half.x, inner.y, -inner.z), center + new Vector3(-half.x, inner.y, inner.z)
            }, Vector3.left, main);
            AppendSurface(vertices, normals, colors, triangles, new[]
            {
                center + new Vector3(-inner.x, half.y, -inner.z), center + new Vector3(inner.x, half.y, -inner.z),
                center + new Vector3(inner.x, half.y, inner.z), center + new Vector3(-inner.x, half.y, inner.z)
            }, Vector3.up, main);
            AppendSurface(vertices, normals, colors, triangles, new[]
            {
                center + new Vector3(-inner.x, -half.y, inner.z), center + new Vector3(inner.x, -half.y, inner.z),
                center + new Vector3(inner.x, -half.y, -inner.z), center + new Vector3(-inner.x, -half.y, -inner.z)
            }, Vector3.down, main);
            AppendSurface(vertices, normals, colors, triangles, new[]
            {
                center + new Vector3(-inner.x, -inner.y, half.z), center + new Vector3(-inner.x, inner.y, half.z),
                center + new Vector3(inner.x, inner.y, half.z), center + new Vector3(inner.x, -inner.y, half.z)
            }, Vector3.forward, main);
            AppendSurface(vertices, normals, colors, triangles, new[]
            {
                center + new Vector3(inner.x, -inner.y, -half.z), center + new Vector3(inner.x, inner.y, -half.z),
                center + new Vector3(-inner.x, inner.y, -half.z), center + new Vector3(-inner.x, -inner.y, -half.z)
            }, Vector3.back, main);

            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                AppendSurface(vertices, normals, colors, triangles, new[]
                {
                    center + new Vector3(sx * half.x, sy * inner.y, -inner.z),
                    center + new Vector3(sx * inner.x, sy * half.y, -inner.z),
                    center + new Vector3(sx * inner.x, sy * half.y, inner.z),
                    center + new Vector3(sx * half.x, sy * inner.y, inner.z)
                }, new Vector3(sx, sy, 0f).normalized, edge);
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                AppendSurface(vertices, normals, colors, triangles, new[]
                {
                    center + new Vector3(sx * half.x, -inner.y, sz * inner.z),
                    center + new Vector3(sx * inner.x, -inner.y, sz * half.z),
                    center + new Vector3(sx * inner.x, inner.y, sz * half.z),
                    center + new Vector3(sx * half.x, inner.y, sz * inner.z)
                }, new Vector3(sx, 0f, sz).normalized, edge);
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                AppendSurface(vertices, normals, colors, triangles, new[]
                {
                    center + new Vector3(-inner.x, sy * half.y, sz * inner.z),
                    center + new Vector3(-inner.x, sy * inner.y, sz * half.z),
                    center + new Vector3(inner.x, sy * inner.y, sz * half.z),
                    center + new Vector3(inner.x, sy * half.y, sz * inner.z)
                }, new Vector3(0f, sy, sz).normalized, edge);
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                AppendSurface(vertices, normals, colors, triangles, new[]
                {
                    center + new Vector3(sx * half.x, sy * inner.y, sz * inner.z),
                    center + new Vector3(sx * inner.x, sy * half.y, sz * inner.z),
                    center + new Vector3(sx * inner.x, sy * inner.y, sz * half.z)
                }, new Vector3(sx, sy, sz).normalized, corner);
        }

        private static void AppendSurface(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color> colors,
            List<int> triangles,
            Vector3[] polygon,
            Vector3 outward,
            Color color)
        {
            int start = vertices.Count;
            bool reverse = polygon.Length >= 3 &&
                           Vector3.Dot(
                               Vector3.Cross(polygon[1] - polygon[0], polygon[2] - polygon[0]),
                               outward) < 0f;
            if (reverse)
            {
                for (int index = polygon.Length - 1; index >= 0; index--)
                {
                    vertices.Add(polygon[index]);
                    normals.Add(outward);
                    colors.Add(color);
                }
            }
            else
            {
                for (int index = 0; index < polygon.Length; index++)
                {
                    vertices.Add(polygon[index]);
                    normals.Add(outward);
                    colors.Add(color);
                }
            }
            for (int index = 1; index < polygon.Length - 1; index++)
            {
                triangles.Add(start);
                triangles.Add(start + index);
                triangles.Add(start + index + 1);
            }
        }

        /// <summary>
        /// Creates a low-poly convex block with a deterministic top/bottom skew.
        /// Unlike the plain box fallback this still reads as an irregular stone,
        /// while its eight-vertex closed topology is safe for a convex collider.
        /// </summary>
        public static Mesh CreateSkewedBlock(string name, Bounds requestedBounds, uint seed)
        {
            Vector3 size = requestedBounds.size;
            size.x = Mathf.Max(0.05f, Mathf.Abs(size.x));
            size.y = Mathf.Max(0.05f, Mathf.Abs(size.y));
            size.z = Mathf.Max(0.05f, Mathf.Abs(size.z));
            Vector3 center = requestedBounds.center;
            Vector3 h = size * 0.5f;

            float sx0 = Mathf.Lerp(0.82f, 0.96f, Hash01(seed ^ 0xA341316Cu));
            float sz0 = Mathf.Lerp(0.84f, 0.98f, Hash01(seed ^ 0xC8013EA4u));
            float sx1 = Mathf.Lerp(0.80f, 0.97f, Hash01(seed ^ 0xAD90777Du));
            float sz1 = Mathf.Lerp(0.82f, 0.96f, Hash01(seed ^ 0x7E95761Eu));
            float offsetX = (Hash01(seed ^ 0x9E3779B9u) - 0.5f) * h.x * 0.18f;
            float offsetZ = (Hash01(seed ^ 0x85EBCA6Bu) - 0.5f) * h.z * 0.18f;

            Vector3 bottom = center + new Vector3(-offsetX * 0.35f, -h.y, -offsetZ * 0.35f);
            Vector3 top = center + new Vector3(offsetX, h.y, offsetZ);
            var vertices = new[]
            {
                bottom + new Vector3(-h.x * sx0, 0f, -h.z * sz0),
                bottom + new Vector3( h.x * sx0, 0f, -h.z * sz0),
                bottom + new Vector3( h.x * sx0, 0f,  h.z * sz0),
                bottom + new Vector3(-h.x * sx0, 0f,  h.z * sz0),
                top + new Vector3(-h.x * sx1, 0f, -h.z * sz1),
                top + new Vector3( h.x * sx1, 0f, -h.z * sz1),
                top + new Vector3( h.x * sx1, 0f,  h.z * sz1),
                top + new Vector3(-h.x * sx1, 0f,  h.z * sz1)
            };
            int[] triangles =
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                3, 6, 2, 3, 7, 6,
                0, 4, 5, 0, 5, 1,
                1, 5, 6, 1, 6, 2,
                0, 3, 7, 0, 7, 4
            };
            var mesh = new Mesh
            {
                name = string.IsNullOrWhiteSpace(name) ? "EarthSafeSkewedFallback" : name,
                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor,
                vertices = vertices,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh CreateBox(string name, Bounds requestedBounds)
        {
            Vector3 size = requestedBounds.size;
            size.x = Mathf.Max(0.05f, Mathf.Abs(size.x));
            size.y = Mathf.Max(0.05f, Mathf.Abs(size.y));
            size.z = Mathf.Max(0.05f, Mathf.Abs(size.z));
            Vector3 center = requestedBounds.center;
            Vector3 h = size * 0.5f;

            Vector3[] vertices =
            {
                center + new Vector3(-h.x, -h.y,  h.z), center + new Vector3( h.x, -h.y,  h.z), center + new Vector3( h.x,  h.y,  h.z), center + new Vector3(-h.x,  h.y,  h.z),
                center + new Vector3( h.x, -h.y, -h.z), center + new Vector3(-h.x, -h.y, -h.z), center + new Vector3(-h.x,  h.y, -h.z), center + new Vector3( h.x,  h.y, -h.z),
                center + new Vector3( h.x, -h.y,  h.z), center + new Vector3( h.x, -h.y, -h.z), center + new Vector3( h.x,  h.y, -h.z), center + new Vector3( h.x,  h.y,  h.z),
                center + new Vector3(-h.x, -h.y, -h.z), center + new Vector3(-h.x, -h.y,  h.z), center + new Vector3(-h.x,  h.y,  h.z), center + new Vector3(-h.x,  h.y, -h.z),
                center + new Vector3(-h.x,  h.y,  h.z), center + new Vector3( h.x,  h.y,  h.z), center + new Vector3( h.x,  h.y, -h.z), center + new Vector3(-h.x,  h.y, -h.z),
                center + new Vector3(-h.x, -h.y, -h.z), center + new Vector3( h.x, -h.y, -h.z), center + new Vector3( h.x, -h.y,  h.z), center + new Vector3(-h.x, -h.y,  h.z)
            };
            Vector3[] normals =
            {
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
                Vector3.back, Vector3.back, Vector3.back, Vector3.back,
                Vector3.right, Vector3.right, Vector3.right, Vector3.right,
                Vector3.left, Vector3.left, Vector3.left, Vector3.left,
                Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                Vector3.down, Vector3.down, Vector3.down, Vector3.down
            };
            Vector4[] tangents = new Vector4[24];
            Vector2[] uvs = new Vector2[24];
            for (int face = 0; face < 6; face++)
            {
                Vector3 tangent = face is 2 or 3 ? Vector3.forward : Vector3.right;
                for (int corner = 0; corner < 4; corner++)
                {
                    tangents[face * 4 + corner] = new Vector4(tangent.x, tangent.y, tangent.z, 1f);
                }
                uvs[face * 4] = Vector2.zero;
                uvs[face * 4 + 1] = Vector2.right;
                uvs[face * 4 + 2] = Vector2.one;
                uvs[face * 4 + 3] = Vector2.up;
            }
            int[] triangles =
            {
                0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7,
                8, 9, 10, 8, 10, 11, 12, 13, 14, 12, 14, 15,
                16, 17, 18, 16, 18, 19, 20, 21, 22, 20, 22, 23
            };
            var mesh = new Mesh
            {
                name = string.IsNullOrWhiteSpace(name) ? "EarthSafeFallback" : name,
                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor,
                vertices = vertices,
                normals = normals,
                tangents = tangents,
                uv = uvs,
                triangles = triangles
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }
    }
}
