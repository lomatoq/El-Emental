using System.Collections.Generic;
using UnityEngine;

namespace Elemental.Runtime.Fire
{
    // Fixed storage; convexity is proven from source triangles, never assumed from collider.convex.
    // Each source plane contains every source vertex in its closed negative halfspace.
    public sealed class FireConvexMeshGeometry
    {
        public const int MaximumVertices = 512, MaximumIndices = 1536;
        private readonly Vector3[] _vertices = new Vector3[MaximumVertices];
        private readonly int[] _indices = new int[MaximumIndices];
        private readonly Vector4[] _planes = new Vector4[MaximumIndices / 3];
        private int _vertexCount, _indexCount, _planeCount;
        public Mesh Mesh { get; private set; }
        public uint Version { get; private set; }
        public bool Valid { get; private set; }

        public bool Matches(Mesh mesh, List<Vector3> vertices, List<int> indices)
        {
            if (Mesh != mesh || vertices.Count != _vertexCount || indices.Count != _indexCount) return false;
            for (int i = 0; i < _vertexCount; i++) if (!_vertices[i].Equals(vertices[i])) return false;
            for (int i = 0; i < _indexCount; i++) if (_indices[i] != indices[i]) return false;
            return true;
        }

        public bool Build(Mesh mesh, List<Vector3> vertices, List<int> indices)
        {
            Valid = false;
            if (Version == uint.MaxValue) return false;
            Version++; Mesh = mesh; _vertexCount = vertices.Count; _indexCount = indices.Count; _planeCount = 0;
            if (_vertexCount < 4 || _vertexCount > MaximumVertices || _indexCount < 12 ||
                _indexCount > MaximumIndices || _indexCount % 3 != 0) return false;
            Vector3 center = Vector3.zero;
            for (int i = 0; i < _vertexCount; i++)
            {
                Vector3 vertex = vertices[i];
                if (!Finite(vertex)) return false;
                _vertices[i] = vertex; center += vertex / _vertexCount;
            }
            for (int i = 0; i < _indexCount; i++)
            { if (indices[i] < 0 || indices[i] >= _vertexCount) return false; _indices[i] = indices[i]; }
            // Reject open / nonmanifold sources: missing hull planes could otherwise expand a disc.
            // Geometric endpoints support the duplicated vertices used for hard normals.
            for (int edge = 0; edge < _indexCount; edge++)
            {
                int triangle = edge - edge % 3;
                Vector3 a = _vertices[_indices[edge]];
                Vector3 b = _vertices[_indices[triangle + (edge % 3 + 1) % 3]];
                if (a.Equals(b)) return false;
                int uses = 0;
                for (int other = 0; other < _indexCount; other++)
                {
                    int ot = other - other % 3;
                    Vector3 c = _vertices[_indices[other]];
                    Vector3 d = _vertices[_indices[ot + (other % 3 + 1) % 3]];
                    if (a.Equals(c) && b.Equals(d) || a.Equals(d) && b.Equals(c)) uses++;
                }
                if (uses != 2) return false;
            }
            for (int i = 0; i < _indexCount; i += 3)
            {
                Vector3 a = _vertices[_indices[i]], b = _vertices[_indices[i + 1]], c = _vertices[_indices[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f) continue;
                n.Normalize(); if (Vector3.Dot(n, a - center) < 0) n = -n;
                float d = Vector3.Dot(n, a);
                for (int j = 0; j < _vertexCount; j++)
                    if (Vector3.Dot(n, _vertices[j]) - d > 0.0002f) return false;
                bool duplicate = false;
                for (int j = 0; j < _planeCount; j++)
                    if (Vector3.Dot(n, Normal(_planes[j])) > 0.999999f && Mathf.Abs(d - _planes[j].w) < 0.0001f)
                    { duplicate = true; break; }
                if (!duplicate) _planes[_planeCount++] = new Vector4(n.x, n.y, n.z, d);
            }
            Valid = _planeCount >= 4;
            return Valid;
        }

        public bool TryFace(Transform transform, Vector3 hit, float requestedRadius, out int face,
            out Vector3 point, out Vector3 normal, out float radius)
        {
            face = -1; point = normal = default; radius = 0;
            if (!Valid) return false;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < _planeCount; i++)
            {
                WorldPlane(transform, i, out Vector3 n, out Vector3 p);
                float distance = Mathf.Abs(Vector3.Dot(n, hit - p));
                if (distance < nearest) { nearest = distance; face = i; }
            }
            if (nearest > 0.025f || face < 0) return false;
            WorldPlane(transform, face, out normal, out Vector3 planePoint);
            point = hit - normal * Vector3.Dot(normal, hit - planePoint);
            return FaceRadius(transform, face, point, requestedRadius, out radius);
        }

        public bool RefreshFace(Transform transform, int face, Vector3 localPoint, float requestedRadius,
            out Vector3 point, out Vector3 normal, out float radius)
        {
            point = transform.TransformPoint(localPoint); normal = default; radius = 0;
            if (!Valid || face < 0 || face >= _planeCount) return false;
            WorldPlane(transform, face, out normal, out _);
            return FaceRadius(transform, face, point, requestedRadius, out radius);
        }

        private bool FaceRadius(Transform transform, int face, Vector3 point, float requestedRadius, out float radius)
        {
            WorldPlane(transform, face, out Vector3 normal, out _);
            radius = requestedRadius;
            for (int i = 0; i < _planeCount; i++)
            {
                if (i == face) continue;
                WorldPlane(transform, i, out Vector3 edgeNormal, out Vector3 edgePoint);
                float margin = -Vector3.Dot(edgeNormal, point - edgePoint);
                if (margin < -0.0001f) return false;
                float projected = Vector3.ProjectOnPlane(edgeNormal, normal).magnitude;
                if (projected > 0.0001f) radius = Mathf.Min(radius, margin / projected - 0.002f);
            }
            return Finite(point) && Finite(normal) && !float.IsNaN(radius) && !float.IsInfinity(radius) && radius >= 0.005f;
        }
        private void WorldPlane(Transform transform, int index, out Vector3 normal, out Vector3 point)
        {
            Vector4 plane = _planes[index]; Vector3 localNormal = Normal(plane);
            normal = transform.worldToLocalMatrix.transpose.MultiplyVector(localNormal).normalized;
            point = transform.TransformPoint(localNormal * plane.w);
        }
        private static Vector3 Normal(Vector4 p) => new Vector3(p.x, p.y, p.z);
        private static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x) &&
            !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
    }
}
