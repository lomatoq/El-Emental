using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Runtime.Geometry
{
    [Flags]
    public enum EarthMeshIntegrityIssue
    {
        None = 0,
        MissingMesh = 1 << 0,
        EmptyMesh = 1 << 1,
        UnsupportedTopology = 1 << 2,
        InvalidIndex = 1 << 3,
        NonFiniteVertex = 1 << 4,
        DegenerateTriangle = 1 << 5,
        DuplicateTriangle = 1 << 6,
        OpenBoundary = 1 << 7,
        NonManifoldEdge = 1 << 8,
        InconsistentWinding = 1 << 9,
        InvertedClosedComponent = 1 << 10,
        MissingOrInvalidNormals = 1 << 11,
        InvertedNormals = 1 << 12,
        MissingOrInvalidTangents = 1 << 13,
        InvalidBounds = 1 << 14,
        TriangleBudgetExceeded = 1 << 15,
        NegativeTransformDeterminant = 1 << 16
    }

    public readonly struct EarthMeshIntegrityPolicy
    {
        public EarthMeshIntegrityPolicy(
            bool requireClosedVolume,
            bool requireNormals,
            bool requireTangents,
            int maximumTriangleCount,
            float weldTolerance = 0.00001f)
        {
            RequireClosedVolume = requireClosedVolume;
            RequireNormals = requireNormals;
            RequireTangents = requireTangents;
            MaximumTriangleCount = Mathf.Max(0, maximumTriangleCount);
            WeldTolerance = Mathf.Max(0.0000001f, weldTolerance);
        }

        public bool RequireClosedVolume { get; }
        public bool RequireNormals { get; }
        public bool RequireTangents { get; }
        public int MaximumTriangleCount { get; }
        public float WeldTolerance { get; }

        public static EarthMeshIntegrityPolicy ClosedHero =>
            new EarthMeshIntegrityPolicy(true, true, false, 4096);

        public static EarthMeshIntegrityPolicy ConvexCollider =>
            new EarthMeshIntegrityPolicy(true, true, false, 255);

        public static EarthMeshIntegrityPolicy OpenVisualSurface =>
            new EarthMeshIntegrityPolicy(false, true, false, 8192);
    }

    public readonly struct EarthMeshIntegrityReport
    {
        public EarthMeshIntegrityReport(
            string meshName,
            EarthMeshIntegrityIssue issues,
            int vertexCount,
            int triangleCount,
            int degenerateTriangleCount,
            int duplicateTriangleCount,
            int openEdgeCount,
            int nonManifoldEdgeCount,
            int inconsistentEdgeCount,
            int componentCount,
            int invertedComponentCount,
            double signedVolume,
            float transformDeterminant)
        {
            MeshName = meshName;
            Issues = issues;
            VertexCount = vertexCount;
            TriangleCount = triangleCount;
            DegenerateTriangleCount = degenerateTriangleCount;
            DuplicateTriangleCount = duplicateTriangleCount;
            OpenEdgeCount = openEdgeCount;
            NonManifoldEdgeCount = nonManifoldEdgeCount;
            InconsistentEdgeCount = inconsistentEdgeCount;
            ComponentCount = componentCount;
            InvertedComponentCount = invertedComponentCount;
            SignedVolume = signedVolume;
            TransformDeterminant = transformDeterminant;
        }

        public string MeshName { get; }
        public EarthMeshIntegrityIssue Issues { get; }
        public int VertexCount { get; }
        public int TriangleCount { get; }
        public int DegenerateTriangleCount { get; }
        public int DuplicateTriangleCount { get; }
        public int OpenEdgeCount { get; }
        public int NonManifoldEdgeCount { get; }
        public int InconsistentEdgeCount { get; }
        public int ComponentCount { get; }
        public int InvertedComponentCount { get; }
        public double SignedVolume { get; }
        public float TransformDeterminant { get; }
        public bool IsValid => Issues == EarthMeshIntegrityIssue.None;

        public override string ToString() =>
            $"{MeshName}: {(IsValid ? "valid" : Issues.ToString())}; " +
            $"v={VertexCount}, t={TriangleCount}, components={ComponentCount}, " +
            $"open={OpenEdgeCount}, nonManifold={NonManifoldEdgeCount}, " +
            $"degenerate={DegenerateTriangleCount}, duplicate={DuplicateTriangleCount}, " +
            $"signedVolume={SignedVolume:F6}, determinant={TransformDeterminant:F4}";
    }

    /// <summary>
    /// Publication-time mesh court shared by procedural stones, structure fracture,
    /// armor and editor-authored geometry. It reads non-readable imported meshes via
    /// MeshData and never mutates the source during validation.
    /// </summary>
    public static class EarthMeshIntegrityValidator
    {
        private const double AreaEpsilon = 1e-14;
        private const double VolumeEpsilon = 1e-10;

        public static EarthMeshIntegrityReport Validate(
            Mesh mesh,
            in EarthMeshIntegrityPolicy policy,
            Matrix4x4 localToWorld)
        {
            if (mesh == null)
            {
                return new EarthMeshIntegrityReport(
                    "<missing>", EarthMeshIntegrityIssue.MissingMesh, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0d, localToWorld.determinant);
            }

            EarthMeshIntegrityIssue issues = EarthMeshIntegrityIssue.None;
            float determinant = localToWorld.determinant;
            if (!float.IsFinite(determinant) || determinant < 0f)
                issues |= EarthMeshIntegrityIssue.NegativeTransformDeterminant;

            using Mesh.MeshDataArray dataArray = Mesh.AcquireReadOnlyMeshData(mesh);
            Mesh.MeshData data = dataArray[0];
            int vertexCount = data.vertexCount;
            if (vertexCount <= 0 || data.subMeshCount <= 0)
            {
                return new EarthMeshIntegrityReport(
                    mesh.name, issues | EarthMeshIntegrityIssue.EmptyMesh, vertexCount,
                    0, 0, 0, 0, 0, 0, 0, 0, 0d, determinant);
            }

            var vertices = new NativeArray<Vector3>(vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            data.GetVertices(vertices);
            var normals = new NativeArray<Vector3>(vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var tangents = new NativeArray<Vector4>(vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            bool hasNormals = data.HasVertexAttribute(VertexAttribute.Normal);
            bool hasTangents = data.HasVertexAttribute(VertexAttribute.Tangent);
            if (hasNormals) data.GetNormals(normals);
            if (hasTangents) data.GetTangents(tangents);

            Bounds bounds = mesh.bounds;
            if (!IsFinite(bounds.center) || !IsFinite(bounds.extents) ||
                bounds.extents.x < 0f || bounds.extents.y < 0f || bounds.extents.z < 0f)
                issues |= EarthMeshIntegrityIssue.InvalidBounds;

            float scale = Mathf.Max(1f, bounds.size.magnitude);
            float weldTolerance = policy.WeldTolerance * scale;
            float boundsTolerance = weldTolerance * 4f;
            var weldMap = new Dictionary<QuantizedVertex, int>(vertexCount);
            var welded = new int[vertexCount];
            int weldedCount = 0;
            int invalidNormalCount = 0;
            int invalidTangentCount = 0;
            for (int index = 0; index < vertexCount; index++)
            {
                Vector3 vertex = vertices[index];
                if (!IsFinite(vertex))
                {
                    issues |= EarthMeshIntegrityIssue.NonFiniteVertex;
                    welded[index] = -1;
                    continue;
                }

                if (!ContainsWithTolerance(bounds, vertex, boundsTolerance))
                    issues |= EarthMeshIntegrityIssue.InvalidBounds;

                var key = new QuantizedVertex(vertex, weldTolerance);
                if (!weldMap.TryGetValue(key, out int weldIndex))
                {
                    weldIndex = weldedCount++;
                    weldMap.Add(key, weldIndex);
                }
                welded[index] = weldIndex;

                if (hasNormals && (!IsFinite(normals[index]) || normals[index].sqrMagnitude < 0.25f))
                    invalidNormalCount++;
                if (hasTangents && (!IsFinite(tangents[index]) ||
                                    new Vector3(tangents[index].x, tangents[index].y, tangents[index].z).sqrMagnitude < 0.25f ||
                                    Mathf.Abs(tangents[index].w) < 0.5f))
                    invalidTangentCount++;
            }

            if (policy.RequireNormals && (!hasNormals || invalidNormalCount > 0))
                issues |= EarthMeshIntegrityIssue.MissingOrInvalidNormals;
            if (policy.RequireTangents && (!hasTangents || invalidTangentCount > 0))
                issues |= EarthMeshIntegrityIssue.MissingOrInvalidTangents;

            var triangles = new List<TriangleRecord>(Mathf.Max(4, (int)data.GetSubMesh(0).indexCount / 3));
            int degenerateCount = 0;
            int invalidIndexCount = 0;
            int invertedNormalVotes = 0;
            int normalVotes = 0;
            var duplicateSet = new HashSet<TriangleKey>();
            int duplicateCount = 0;

            for (int submesh = 0; submesh < data.subMeshCount; submesh++)
            {
                SubMeshDescriptor descriptor = data.GetSubMesh(submesh);
                if (descriptor.topology != MeshTopology.Triangles || descriptor.indexCount % 3 != 0)
                {
                    issues |= EarthMeshIntegrityIssue.UnsupportedTopology;
                    continue;
                }

                var indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                data.GetIndices(indices, submesh, true);
                for (int offset = 0; offset < indices.Length; offset += 3)
                {
                    int ia = indices[offset];
                    int ib = indices[offset + 1];
                    int ic = indices[offset + 2];
                    if ((uint)ia >= vertexCount || (uint)ib >= vertexCount || (uint)ic >= vertexCount)
                    {
                        invalidIndexCount++;
                        continue;
                    }

                    Vector3 a = vertices[ia];
                    Vector3 b = vertices[ib];
                    Vector3 c = vertices[ic];
                    int wa = welded[ia];
                    int wb = welded[ib];
                    int wc = welded[ic];
                    Vector3 cross = Vector3.Cross(b - a, c - a);
                    if (wa < 0 || wb < 0 || wc < 0 || wa == wb || wb == wc || wc == wa ||
                        !IsFinite(cross) || cross.sqrMagnitude <= AreaEpsilon * scale * scale)
                    {
                        degenerateCount++;
                        continue;
                    }

                    var key = new TriangleKey(wa, wb, wc);
                    if (!duplicateSet.Add(key)) duplicateCount++;
                    int triangleIndex = triangles.Count;
                    triangles.Add(new TriangleRecord(ia, ib, ic, wa, wb, wc, triangleIndex));

                    if (hasNormals)
                    {
                        Vector3 averageNormal = normals[ia] + normals[ib] + normals[ic];
                        if (averageNormal.sqrMagnitude > 0.01f)
                        {
                            normalVotes++;
                            if (Vector3.Dot(cross, averageNormal) < 0f) invertedNormalVotes++;
                        }
                    }
                }
                indices.Dispose();
            }

            if (invalidIndexCount > 0) issues |= EarthMeshIntegrityIssue.InvalidIndex;
            if (degenerateCount > 0) issues |= EarthMeshIntegrityIssue.DegenerateTriangle;
            if (duplicateCount > 0) issues |= EarthMeshIntegrityIssue.DuplicateTriangle;
            if (normalVotes > 0 && invertedNormalVotes * 2 > normalVotes)
                issues |= EarthMeshIntegrityIssue.InvertedNormals;
            if (policy.MaximumTriangleCount > 0 && triangles.Count > policy.MaximumTriangleCount)
                issues |= EarthMeshIntegrityIssue.TriangleBudgetExceeded;

            var union = new UnionFind(triangles.Count);
            var edges = new Dictionary<EdgeKey, EdgeAccumulator>(triangles.Count * 2);
            for (int index = 0; index < triangles.Count; index++)
            {
                TriangleRecord triangle = triangles[index];
                AddEdge(edges, union, triangle.Wa, triangle.Wb, index);
                AddEdge(edges, union, triangle.Wb, triangle.Wc, index);
                AddEdge(edges, union, triangle.Wc, triangle.Wa, index);
            }

            int openEdgeCount = 0;
            int nonManifoldEdgeCount = 0;
            int inconsistentEdgeCount = 0;
            foreach (KeyValuePair<EdgeKey, EdgeAccumulator> pair in edges)
            {
                EdgeAccumulator edge = pair.Value;
                if (edge.Count == 1) openEdgeCount++;
                else if (edge.Count > 2) nonManifoldEdgeCount++;
                else if (edge.DirectionBalance != 0) inconsistentEdgeCount++;
            }
            if (policy.RequireClosedVolume && openEdgeCount > 0)
                issues |= EarthMeshIntegrityIssue.OpenBoundary;
            if (nonManifoldEdgeCount > 0)
                issues |= EarthMeshIntegrityIssue.NonManifoldEdge;
            if (inconsistentEdgeCount > 0)
                issues |= EarthMeshIntegrityIssue.InconsistentWinding;

            var componentVolumes = new Dictionary<int, double>();
            Vector3 origin = bounds.center;
            for (int index = 0; index < triangles.Count; index++)
            {
                TriangleRecord triangle = triangles[index];
                int root = union.Find(index);
                Vector3 a = vertices[triangle.Ia] - origin;
                Vector3 b = vertices[triangle.Ib] - origin;
                Vector3 c = vertices[triangle.Ic] - origin;
                double volume = Vector3.Dot(a, Vector3.Cross(b, c)) / 6.0;
                componentVolumes.TryGetValue(root, out double current);
                componentVolumes[root] = current + volume;
            }

            int invertedComponentCount = 0;
            double signedVolume = 0d;
            bool closedAndConsistent = openEdgeCount == 0 && nonManifoldEdgeCount == 0 && inconsistentEdgeCount == 0;
            foreach (double volume in componentVolumes.Values)
            {
                signedVolume += volume;
                if (closedAndConsistent && volume < -VolumeEpsilon * scale * scale * scale)
                    invertedComponentCount++;
            }
            if (invertedComponentCount > 0)
                issues |= EarthMeshIntegrityIssue.InvertedClosedComponent;

            vertices.Dispose();
            normals.Dispose();
            tangents.Dispose();

            return new EarthMeshIntegrityReport(
                mesh.name, issues, vertexCount, triangles.Count, degenerateCount, duplicateCount,
                openEdgeCount, nonManifoldEdgeCount, inconsistentEdgeCount, componentVolumes.Count,
                invertedComponentCount, signedVolume, determinant);
        }

        public static EarthMeshIntegrityReport Validate(Mesh mesh, in EarthMeshIntegrityPolicy policy) =>
            Validate(mesh, policy, Matrix4x4.identity);

        /// <summary>
        /// The only automatic repair allowed by V4.1: flip a fully closed and
        /// consistently inverted mesh as one unit. Mixed winding is rejected.
        /// </summary>
        public static bool TryRepairFullyInvertedClosedMesh(Mesh mesh, out EarthMeshIntegrityReport repaired)
        {
            repaired = Validate(mesh, EarthMeshIntegrityPolicy.ClosedHero);
            EarthMeshIntegrityIssue repairable = EarthMeshIntegrityIssue.InvertedClosedComponent |
                                                 EarthMeshIntegrityIssue.InvertedNormals;
            if (mesh == null || repaired.InvertedComponentCount == 0 ||
                (repaired.Issues & ~repairable) != EarthMeshIntegrityIssue.None)
                return false;

            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                if (mesh.GetTopology(submesh) != MeshTopology.Triangles) return false;
                int[] indices = mesh.GetIndices(submesh, true);
                for (int index = 0; index + 2 < indices.Length; index += 3)
                {
                    (indices[index + 1], indices[index + 2]) = (indices[index + 2], indices[index + 1]);
                }
                mesh.SetIndices(indices, MeshTopology.Triangles, submesh, false, 0);
            }

            Vector3[] normals = mesh.normals;
            if (normals != null && normals.Length == mesh.vertexCount)
            {
                for (int index = 0; index < normals.Length; index++) normals[index] = -normals[index];
                mesh.normals = normals;
            }
            Vector4[] tangents = mesh.tangents;
            if (tangents != null && tangents.Length == mesh.vertexCount)
            {
                for (int index = 0; index < tangents.Length; index++) tangents[index].w = -tangents[index].w;
                mesh.tangents = tangents;
            }
            mesh.RecalculateBounds();
            repaired = Validate(mesh, EarthMeshIntegrityPolicy.ClosedHero);
            return repaired.IsValid;
        }

        private static void AddEdge(
            Dictionary<EdgeKey, EdgeAccumulator> edges,
            UnionFind union,
            int from,
            int to,
            int triangleIndex)
        {
            var key = new EdgeKey(from, to);
            int direction = from < to ? 1 : -1;
            if (edges.TryGetValue(key, out EdgeAccumulator existing))
            {
                union.Join(existing.FirstTriangle, triangleIndex);
                existing.Count++;
                existing.DirectionBalance += direction;
                edges[key] = existing;
            }
            else
            {
                edges.Add(key, new EdgeAccumulator(triangleIndex, 1, direction));
            }
        }

        private static bool ContainsWithTolerance(Bounds bounds, Vector3 point, float tolerance)
        {
            Vector3 min = bounds.min - Vector3.one * tolerance;
            Vector3 max = bounds.max + Vector3.one * tolerance;
            return point.x >= min.x && point.y >= min.y && point.z >= min.z &&
                   point.x <= max.x && point.y <= max.y && point.z <= max.z;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private static bool IsFinite(Vector4 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) &&
            float.IsFinite(value.z) && float.IsFinite(value.w);

        private readonly struct QuantizedVertex : IEquatable<QuantizedVertex>
        {
            private readonly long _x;
            private readonly long _y;
            private readonly long _z;

            public QuantizedVertex(Vector3 point, float tolerance)
            {
                double inverse = 1.0 / tolerance;
                _x = (long)Math.Round(point.x * inverse);
                _y = (long)Math.Round(point.y * inverse);
                _z = (long)Math.Round(point.z * inverse);
            }

            public bool Equals(QuantizedVertex other) => _x == other._x && _y == other._y && _z == other._z;
            public override bool Equals(object obj) => obj is QuantizedVertex other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _x.GetHashCode();
                    hash = (hash * 397) ^ _y.GetHashCode();
                    return (hash * 397) ^ _z.GetHashCode();
                }
            }
        }

        private readonly struct TriangleKey : IEquatable<TriangleKey>
        {
            private readonly int _a;
            private readonly int _b;
            private readonly int _c;

            public TriangleKey(int a, int b, int c)
            {
                if (a > b) (a, b) = (b, a);
                if (b > c) (b, c) = (c, b);
                if (a > b) (a, b) = (b, a);
                _a = a;
                _b = b;
                _c = c;
            }

            public bool Equals(TriangleKey other) => _a == other._a && _b == other._b && _c == other._c;
            public override bool Equals(object obj) => obj is TriangleKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked { return ((_a * 397) ^ _b) * 397 ^ _c; }
            }
        }

        private readonly struct TriangleRecord
        {
            public TriangleRecord(int ia, int ib, int ic, int wa, int wb, int wc, int index)
            {
                Ia = ia;
                Ib = ib;
                Ic = ic;
                Wa = wa;
                Wb = wb;
                Wc = wc;
                Index = index;
            }
            public int Ia { get; }
            public int Ib { get; }
            public int Ic { get; }
            public int Wa { get; }
            public int Wb { get; }
            public int Wc { get; }
            public int Index { get; }
        }

        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            public EdgeKey(int a, int b)
            {
                if (a <= b) { A = a; B = b; }
                else { A = b; B = a; }
            }
            private int A { get; }
            private int B { get; }
            public bool Equals(EdgeKey other) => A == other.A && B == other.B;
            public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
            public override int GetHashCode() => unchecked((A * 397) ^ B);
        }

        private struct EdgeAccumulator
        {
            public EdgeAccumulator(int firstTriangle, int count, int directionBalance)
            {
                FirstTriangle = firstTriangle;
                Count = count;
                DirectionBalance = directionBalance;
            }
            public int FirstTriangle;
            public int Count;
            public int DirectionBalance;
        }

        private sealed class UnionFind
        {
            private readonly int[] _parent;
            private readonly byte[] _rank;

            public UnionFind(int count)
            {
                _parent = new int[count];
                _rank = new byte[count];
                for (int index = 0; index < count; index++) _parent[index] = index;
            }

            public int Find(int value)
            {
                int root = value;
                while (_parent[root] != root) root = _parent[root];
                while (_parent[value] != value)
                {
                    int next = _parent[value];
                    _parent[value] = root;
                    value = next;
                }
                return root;
            }

            public void Join(int left, int right)
            {
                int a = Find(left);
                int b = Find(right);
                if (a == b) return;
                if (_rank[a] < _rank[b]) _parent[a] = b;
                else if (_rank[a] > _rank[b]) _parent[b] = a;
                else { _parent[b] = a; _rank[a]++; }
            }
        }
    }
}
