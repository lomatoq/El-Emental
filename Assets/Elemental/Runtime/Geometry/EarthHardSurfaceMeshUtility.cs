using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Runtime.Geometry
{
    /// <summary>
    /// Builds render-only fracture meshes with explicit normal ownership for
    /// authored exterior surfaces and newly exposed cut faces.
    /// </summary>
    public static class EarthHardSurfaceMeshUtility
    {
        private static readonly ProfilerMarker FractureShadingMarker =
            new ProfilerMarker("Elemental.Earth.FractureShading.Build");

        public static Mesh CreateFlatShadedCopy(Mesh source, string meshName = null)
        {
            if (source == null) return null;

            Vector3[] sourceVertices = source.vertices;
            Color32[] sourceColors = source.colors32;
            Vector2[] sourceUv = source.uv;
            var vertices = new List<Vector3>(source.triangles.Length);
            var normals = new List<Vector3>(source.triangles.Length);
            var colors = sourceColors.Length == sourceVertices.Length
                ? new List<Color32>(source.triangles.Length)
                : null;
            var uv = sourceUv.Length == sourceVertices.Length
                ? new List<Vector2>(source.triangles.Length)
                : null;
            var submeshTriangles = new List<int>[Mathf.Max(1, source.subMeshCount)];

            for (int submesh = 0; submesh < submeshTriangles.Length; submesh++)
            {
                int[] triangles = source.GetTriangles(submesh);
                var destination = new List<int>(triangles.Length);
                for (int triangle = 0; triangle + 2 < triangles.Length; triangle += 3)
                {
                    int ia = triangles[triangle];
                    int ib = triangles[triangle + 1];
                    int ic = triangles[triangle + 2];
                    if ((uint)ia >= sourceVertices.Length ||
                        (uint)ib >= sourceVertices.Length ||
                        (uint)ic >= sourceVertices.Length) continue;

                    Vector3 a = sourceVertices[ia];
                    Vector3 b = sourceVertices[ib];
                    Vector3 c = sourceVertices[ic];
                    Vector3 normal = Vector3.Cross(b - a, c - a);
                    normal = normal.sqrMagnitude > 0.00000001f
                        ? normal.normalized
                        : Vector3.up;
                    AppendCorner(ia, a, normal, sourceColors, sourceUv,
                        vertices, normals, colors, uv, destination);
                    AppendCorner(ib, b, normal, sourceColors, sourceUv,
                        vertices, normals, colors, uv, destination);
                    AppendCorner(ic, c, normal, sourceColors, sourceUv,
                        vertices, normals, colors, uv, destination);
                }
                submeshTriangles[submesh] = destination;
            }

            var result = new Mesh
            {
                name = string.IsNullOrWhiteSpace(meshName)
                    ? source.name + " Flat"
                    : meshName,
                indexFormat = vertices.Count > ushort.MaxValue
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16
            };
            result.SetVertices(vertices);
            result.SetNormals(normals);
            if (colors != null) result.SetColors(colors);
            if (uv != null) result.SetUVs(0, uv);
            result.subMeshCount = submeshTriangles.Length;
            for (int submesh = 0; submesh < submeshTriangles.Length; submesh++)
                result.SetTriangles(submeshTriangles[submesh], submesh, false);
            if (uv != null && uv.Count == vertices.Count) result.RecalculateTangents();
            result.RecalculateBounds();
            return result;
        }

        /// <summary>
        /// Creates a render copy with split triangle corners. Exterior corners
        /// sample the intact mesh's authored normals in intact-local space while
        /// freshly exposed fracture submeshes use hard geometric face normals.
        /// This keeps the intact silhouette/detail response across the proxy swap
        /// without smoothing a cut face into the old surface.
        /// </summary>
        public static Mesh CreateFractureShadedCopy(
            Mesh piece,
            Mesh intact,
            Matrix4x4 pieceToIntact,
            int exteriorSubmesh,
            int interiorSubmesh,
            string meshName = null)
        {
            if (piece == null) return null;
            if (intact == null || intact.normals.Length != intact.vertexCount)
                return CreateFlatShadedCopy(piece, meshName);

            using (FractureShadingMarker.Auto())
            {
                Vector3[] sourceVertices = piece.vertices;
                Vector3[] sourceNormals = piece.normals;
                Color32[] sourceColors = piece.colors32;
                var sourceUv = new List<Vector4>[8];
                for (int channel = 0; channel < sourceUv.Length; channel++)
                {
                    var values = new List<Vector4>(sourceVertices.Length);
                    piece.GetUVs(channel, values);
                    sourceUv[channel] = values.Count == sourceVertices.Length ? values : null;
                }

                var sampler = new AuthoredNormalSampler(intact);
                int cornerCapacity = piece.triangles.Length;
                var vertices = new List<Vector3>(cornerCapacity);
                var normals = new List<Vector3>(cornerCapacity);
                var colors = sourceColors.Length == sourceVertices.Length
                    ? new List<Color32>(cornerCapacity)
                    : null;
                var uv = new List<Vector4>[sourceUv.Length];
                for (int channel = 0; channel < uv.Length; channel++)
                    if (sourceUv[channel] != null) uv[channel] = new List<Vector4>(cornerCapacity);
                var submeshTriangles = new List<int>[Mathf.Max(1, piece.subMeshCount)];
                Matrix4x4 intactNormalToPiece = pieceToIntact.transpose;
                Matrix4x4 pieceNormalToIntact = pieceToIntact.inverse.transpose;

                for (int submesh = 0; submesh < submeshTriangles.Length; submesh++)
                {
                    int[] triangles = piece.GetTriangles(submesh);
                    var destination = new List<int>(triangles.Length);
                    bool preserveExterior = submesh == exteriorSubmesh;
                    bool hardInterior = submesh == interiorSubmesh;
                    for (int triangle = 0; triangle + 2 < triangles.Length; triangle += 3)
                    {
                        int ia = triangles[triangle];
                        int ib = triangles[triangle + 1];
                        int ic = triangles[triangle + 2];
                        if ((uint)ia >= sourceVertices.Length ||
                            (uint)ib >= sourceVertices.Length ||
                            (uint)ic >= sourceVertices.Length) continue;

                        Vector3 a = sourceVertices[ia];
                        Vector3 b = sourceVertices[ib];
                        Vector3 c = sourceVertices[ic];
                        Vector3 faceNormal = SafeNormal(Vector3.Cross(b - a, c - a), Vector3.up);
                        Vector3 intactFaceNormal = SafeNormal(
                            pieceNormalToIntact.MultiplyVector(faceNormal),
                            Vector3.up);
                        AppendFractureCorner(
                            ia, a, faceNormal, intactFaceNormal, preserveExterior, hardInterior,
                            sourceNormals, sourceColors, sourceUv, pieceToIntact,
                            pieceNormalToIntact, intactNormalToPiece, sampler,
                            vertices, normals, colors, uv, destination);
                        AppendFractureCorner(
                            ib, b, faceNormal, intactFaceNormal, preserveExterior, hardInterior,
                            sourceNormals, sourceColors, sourceUv, pieceToIntact,
                            pieceNormalToIntact, intactNormalToPiece, sampler,
                            vertices, normals, colors, uv, destination);
                        AppendFractureCorner(
                            ic, c, faceNormal, intactFaceNormal, preserveExterior, hardInterior,
                            sourceNormals, sourceColors, sourceUv, pieceToIntact,
                            pieceNormalToIntact, intactNormalToPiece, sampler,
                            vertices, normals, colors, uv, destination);
                    }
                    submeshTriangles[submesh] = destination;
                }

                var result = new Mesh
                {
                    name = string.IsNullOrWhiteSpace(meshName)
                        ? piece.name + " Fracture Shaded"
                        : meshName,
                    indexFormat = vertices.Count > ushort.MaxValue
                        ? IndexFormat.UInt32
                        : IndexFormat.UInt16
                };
                result.SetVertices(vertices);
                result.SetNormals(normals);
                if (colors != null) result.SetColors(colors);
                for (int channel = 0; channel < uv.Length; channel++)
                    if (uv[channel] != null) result.SetUVs(channel, uv[channel]);
                result.subMeshCount = submeshTriangles.Length;
                for (int submesh = 0; submesh < submeshTriangles.Length; submesh++)
                    result.SetTriangles(submeshTriangles[submesh], submesh, false);
                if (uv[0] != null && uv[0].Count == vertices.Count) result.RecalculateTangents();
                result.RecalculateBounds();
                return result;
            }
        }

        private static void AppendFractureCorner(
            int sourceIndex,
            Vector3 vertex,
            Vector3 faceNormal,
            Vector3 intactFaceNormal,
            bool preserveExterior,
            bool hardInterior,
            Vector3[] sourceNormals,
            Color32[] sourceColors,
            IReadOnlyList<Vector4>[] sourceUv,
            Matrix4x4 pieceToIntact,
            Matrix4x4 pieceNormalToIntact,
            Matrix4x4 intactNormalToPiece,
            AuthoredNormalSampler sampler,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color32> colors,
            IList<Vector4>[] uv,
            List<int> triangles)
        {
            Vector3 normal = faceNormal;
            if (preserveExterior)
            {
                Vector3 hint = sourceNormals.Length > sourceIndex
                    ? SafeNormal(
                        pieceNormalToIntact.MultiplyVector(sourceNormals[sourceIndex]),
                        intactFaceNormal)
                    : intactFaceNormal;
                Vector3 intactPoint = pieceToIntact.MultiplyPoint3x4(vertex);
                Vector3 intactNormal = sampler.Sample(intactPoint, hint);
                normal = SafeNormal(intactNormalToPiece.MultiplyVector(intactNormal), faceNormal);
            }
            else if (!hardInterior && sourceNormals.Length > sourceIndex)
            {
                normal = SafeNormal(sourceNormals[sourceIndex], faceNormal);
            }

            int destinationIndex = vertices.Count;
            vertices.Add(vertex);
            normals.Add(normal);
            if (colors != null) colors.Add(sourceColors[sourceIndex]);
            for (int channel = 0; channel < uv.Length; channel++)
                if (uv[channel] != null) uv[channel].Add(sourceUv[channel][sourceIndex]);
            triangles.Add(destinationIndex);
        }

        private static Vector3 SafeNormal(Vector3 value, Vector3 fallback) =>
            value.sqrMagnitude > 0.00000001f ? value.normalized : fallback.normalized;

        private sealed class AuthoredNormalSampler
        {
            private readonly Vector3[] _vertices;
            private readonly Vector3[] _normals;
            private readonly int[] _triangles;

            public AuthoredNormalSampler(Mesh intact)
            {
                _vertices = intact.vertices;
                _normals = intact.normals;
                _triangles = intact.triangles;
            }

            public Vector3 Sample(Vector3 point, Vector3 normalHint)
            {
                float bestDistance = float.PositiveInfinity;
                float bestHintDot = float.NegativeInfinity;
                Vector3 bestNormal = normalHint;
                for (int triangle = 0; triangle + 2 < _triangles.Length; triangle += 3)
                {
                    int ia = _triangles[triangle];
                    int ib = _triangles[triangle + 1];
                    int ic = _triangles[triangle + 2];
                    if ((uint)ia >= _vertices.Length ||
                        (uint)ib >= _vertices.Length ||
                        (uint)ic >= _vertices.Length) continue;
                    Vector3 barycentric;
                    Vector3 closest = ClosestPointOnTriangle(
                        point, _vertices[ia], _vertices[ib], _vertices[ic], out barycentric);
                    float distance = (closest - point).sqrMagnitude;
                    Vector3 candidate = SafeNormal(
                        _normals[ia] * barycentric.x +
                        _normals[ib] * barycentric.y +
                        _normals[ic] * barycentric.z,
                        normalHint);
                    float hintDot = Vector3.Dot(candidate, normalHint);
                    if (distance > bestDistance + 0.00000001f ||
                        Mathf.Abs(distance - bestDistance) <= 0.00000001f && hintDot <= bestHintDot)
                        continue;
                    bestDistance = distance;
                    bestHintDot = hintDot;
                    bestNormal = candidate;
                }
                return bestNormal;
            }

            private static Vector3 ClosestPointOnTriangle(
                Vector3 point,
                Vector3 a,
                Vector3 b,
                Vector3 c,
                out Vector3 barycentric)
            {
                Vector3 ab = b - a;
                Vector3 ac = c - a;
                Vector3 ap = point - a;
                float d1 = Vector3.Dot(ab, ap);
                float d2 = Vector3.Dot(ac, ap);
                if (d1 <= 0f && d2 <= 0f)
                {
                    barycentric = new Vector3(1f, 0f, 0f);
                    return a;
                }

                Vector3 bp = point - b;
                float d3 = Vector3.Dot(ab, bp);
                float d4 = Vector3.Dot(ac, bp);
                if (d3 >= 0f && d4 <= d3)
                {
                    barycentric = new Vector3(0f, 1f, 0f);
                    return b;
                }

                float vc = d1 * d4 - d3 * d2;
                if (vc <= 0f && d1 >= 0f && d3 <= 0f)
                {
                    float v = d1 / (d1 - d3);
                    barycentric = new Vector3(1f - v, v, 0f);
                    return a + ab * v;
                }

                Vector3 cp = point - c;
                float d5 = Vector3.Dot(ab, cp);
                float d6 = Vector3.Dot(ac, cp);
                if (d6 >= 0f && d5 <= d6)
                {
                    barycentric = new Vector3(0f, 0f, 1f);
                    return c;
                }

                float vb = d5 * d2 - d1 * d6;
                if (vb <= 0f && d2 >= 0f && d6 <= 0f)
                {
                    float w = d2 / (d2 - d6);
                    barycentric = new Vector3(1f - w, 0f, w);
                    return a + ac * w;
                }

                float va = d3 * d6 - d5 * d4;
                if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
                {
                    float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                    barycentric = new Vector3(0f, 1f - w, w);
                    return b + (c - b) * w;
                }

                float denominator = 1f / (va + vb + vc);
                float insideV = vb * denominator;
                float insideW = vc * denominator;
                barycentric = new Vector3(1f - insideV - insideW, insideV, insideW);
                return a + ab * insideV + ac * insideW;
            }
        }

        private static void AppendCorner(
            int sourceIndex,
            Vector3 vertex,
            Vector3 normal,
            Color32[] sourceColors,
            Vector2[] sourceUv,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color32> colors,
            List<Vector2> uv,
            List<int> triangles)
        {
            int destinationIndex = vertices.Count;
            vertices.Add(vertex);
            normals.Add(normal);
            if (colors != null) colors.Add(sourceColors[sourceIndex]);
            if (uv != null) uv.Add(sourceUv[sourceIndex]);
            triangles.Add(destinationIndex);
        }
    }
}
