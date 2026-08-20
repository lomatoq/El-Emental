using UnityEngine;

namespace Elemental.Runtime.Geometry
{
    public static class EarthSafeMeshFactory
    {
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
