using System;
using System.Collections.Generic;
using Elemental.Core.Math;
using Unity.Mathematics;

namespace Elemental.Simulation.Structures
{
    public readonly struct EarthVolumetricFractureFace
    {
        public EarthVolumetricFractureFace(
            float3 normal,
            float area,
            int neighbourCellIndex,
            int[] vertexIndices)
        {
            Normal = normal;
            Area = math.max(0f, area);
            NeighbourCellIndex = neighbourCellIndex;
            VertexIndices = vertexIndices ?? Array.Empty<int>();
        }

        public float3 Normal { get; }
        public float Area { get; }
        public int NeighbourCellIndex { get; }
        public int[] VertexIndices { get; }
        public bool IsExterior => NeighbourCellIndex < 0;
    }

    public readonly struct EarthVolumetricFractureCell
    {
        public EarthVolumetricFractureCell(
            uint id,
            float3 site,
            float3 centroid,
            float volume,
            float aspectRatio,
            bool foundation,
            float3[] vertices,
            int[] triangles,
            EarthVolumetricFractureFace[] faces)
        {
            Id = id;
            Site = site;
            Centroid = centroid;
            Volume = math.max(0f, volume);
            AspectRatio = math.max(1f, aspectRatio);
            Foundation = foundation;
            Vertices = vertices ?? Array.Empty<float3>();
            Triangles = triangles ?? Array.Empty<int>();
            Faces = faces ?? Array.Empty<EarthVolumetricFractureFace>();
        }

        public uint Id { get; }
        public float3 Site { get; }
        public float3 Centroid { get; }
        public float Volume { get; }
        public float AspectRatio { get; }
        public bool Foundation { get; }
        public float3[] Vertices { get; }
        public int[] Triangles { get; }
        public EarthVolumetricFractureFace[] Faces { get; }
        public int TriangleCount => Triangles.Length / 3;
    }

    /// <summary>
    /// A deterministic 3D power-cell plan. Every cell is the intersection of the
    /// source convex prism and all site bisector half-spaces. Shared boundaries
    /// therefore come from the same plane instead of independently extruded 2D cuts.
    /// </summary>
    public readonly struct EarthVolumetricFracturePlan
    {
        public EarthVolumetricFracturePlan(
            uint seed,
            float2[] boundary,
            float bottom,
            float top,
            float sourceVolume,
            EarthVolumetricFractureCell[] cells)
        {
            Seed = seed;
            Boundary = boundary ?? Array.Empty<float2>();
            Bottom = math.min(bottom, top);
            Top = math.max(bottom, top);
            SourceVolume = math.max(0f, sourceVolume);
            Cells = cells ?? Array.Empty<EarthVolumetricFractureCell>();

            float volume = 0f;
            for (int index = 0; index < Cells.Length; index++) volume += Cells[index].Volume;
            CellVolume = volume;
        }

        public uint Seed { get; }
        public float2[] Boundary { get; }
        public float Bottom { get; }
        public float Top { get; }
        public float SourceVolume { get; }
        public float CellVolume { get; }
        public EarthVolumetricFractureCell[] Cells { get; }
        public float RelativeVolumeError => SourceVolume <= 0.000001f
            ? 1f
            : math.abs(CellVolume - SourceVolume) / SourceVolume;
        public bool IsValid => Boundary.Length >= 3 && Cells.Length >= 4 && RelativeVolumeError <= 0.02f;
    }

    public static class EarthVolumetricFractureSolver
    {
        private const float MinimumHeight = 0.02f;
        private const float PlaneEpsilon = 0.00008f;
        // Merge only numerical copies while half-space incidence is being built.
        // A separate topology weld runs after all faces exist, so a tiny legitimate
        // edge cannot erase one side's plane ownership halfway through construction.
        private const float VertexMergeEpsilonSq = 0.0000000001f;
        private const int MinimumCells = 4;
        private const int MaximumCells = 64;

        private readonly struct HalfSpace
        {
            public HalfSpace(float3 normal, float distance, int neighbourCellIndex)
            {
                float length = math.length(normal);
                Normal = length > 0.000001f ? normal / length : new float3(0f, 1f, 0f);
                Distance = length > 0.000001f ? distance / length : distance;
                NeighbourCellIndex = neighbourCellIndex;
            }

            public float3 Normal { get; }
            public float Distance { get; }
            public int NeighbourCellIndex { get; }
            public float SignedDistance(float3 point) => math.dot(Normal, point) - Distance;
        }

        public static EarthVolumetricFracturePlan BuildClosedConvexPrism(
            uint seed,
            float2[] convexBoundary,
            float bottom,
            float top,
            int requestedCellCount,
            int maximumAttempts = 24)
        {
            int attempts = math.clamp(maximumAttempts, 1, 64);
            EarthVolumetricFracturePlan best = default;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                uint candidateSeed = seed + unchecked((uint)attempt * 0x9E3779B9u);
                EarthVolumetricFracturePlan candidate = BuildConvexPrism(
                    candidateSeed,
                    convexBoundary,
                    bottom,
                    top,
                    requestedCellCount);
                if (!candidate.IsValid) continue;
                best = candidate;
                if (HasClosedTopology(in candidate)) return candidate;
            }
            return best;
        }

        public static bool HasClosedTopology(in EarthVolumetricFracturePlan plan)
        {
            if (!plan.IsValid) return false;
            for (int cellIndex = 0; cellIndex < plan.Cells.Length; cellIndex++)
            {
                int[] triangles = plan.Cells[cellIndex].Triangles;
                if (triangles == null || triangles.Length < 12 || triangles.Length % 3 != 0)
                    return false;
                var edges = new Dictionary<ulong, int>(triangles.Length);
                for (int index = 0; index < triangles.Length; index += 3)
                {
                    CountTopologyEdge(edges, triangles[index], triangles[index + 1]);
                    CountTopologyEdge(edges, triangles[index + 1], triangles[index + 2]);
                    CountTopologyEdge(edges, triangles[index + 2], triangles[index]);
                }
                foreach (int count in edges.Values)
                    if (count != 2) return false;
            }
            return true;
        }

        public static EarthVolumetricFracturePlan BuildConvexPrism(
            uint seed,
            float2[] convexBoundary,
            float bottom,
            float top,
            int requestedCellCount)
        {
            if (convexBoundary == null || convexBoundary.Length < 3)
                return default;

            float safeBottom = math.min(bottom, top);
            float safeTop = math.max(bottom, top);
            if (safeTop - safeBottom < MinimumHeight) safeTop = safeBottom + MinimumHeight;
            float2[] boundary = EnsureCounterClockwise(convexBoundary);
            float area = math.abs(SignedArea(boundary));
            if (area <= 0.00001f) return default;

            int cellCount = math.clamp(requestedCellCount, MinimumCells, MaximumCells);
            float3[] sites = GenerateSites(seed, boundary, safeBottom, safeTop, cellCount);
            HalfSpace[] boundaryPlanes = BuildBoundaryPlanes(boundary, safeBottom, safeTop);
            var cells = new EarthVolumetricFractureCell[cellCount];
            for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
            {
                var planes = new HalfSpace[boundaryPlanes.Length + cellCount - 1];
                Array.Copy(boundaryPlanes, planes, boundaryPlanes.Length);
                int output = boundaryPlanes.Length;
                float3 site = sites[cellIndex];
                for (int otherIndex = 0; otherIndex < cellCount; otherIndex++)
                {
                    if (otherIndex == cellIndex) continue;
                    float3 other = sites[otherIndex];
                    float3 normal = other - site;
                    float distance = (math.lengthsq(other) - math.lengthsq(site)) * 0.5f;
                    planes[output++] = new HalfSpace(normal, distance, otherIndex);
                }

                cells[cellIndex] = BuildCell(
                    (uint)(cellIndex + 1),
                    cellIndex,
                    site,
                    safeBottom,
                    safeTop,
                    planes);
            }

            return new EarthVolumetricFracturePlan(
                seed,
                boundary,
                safeBottom,
                safeTop,
                area * (safeTop - safeBottom),
                cells);
        }

        private static EarthVolumetricFractureCell BuildCell(
            uint id,
            int cellIndex,
            float3 site,
            float bottom,
            float top,
            HalfSpace[] planes)
        {
            var vertices = new List<float3>(32);
            var incidentPlanes = new List<HashSet<int>>(32);
            for (int a = 0; a < planes.Length - 2; a++)
            for (int b = a + 1; b < planes.Length - 1; b++)
            for (int c = b + 1; c < planes.Length; c++)
            {
                if (!TryIntersect(planes[a], planes[b], planes[c], out float3 point)) continue;
                if (!ContainsAll(point, planes)) continue;
                AddUnique(vertices, incidentPlanes, point, a, b, c);
            }

            if (vertices.Count < 4)
                return new EarthVolumetricFractureCell(
                    id, site, site, 0f, 1f, false,
                    Array.Empty<float3>(), Array.Empty<int>(), Array.Empty<EarthVolumetricFractureFace>());

            var faces = new List<EarthVolumetricFractureFace>(16);
            var triangles = new List<int>(96);
            for (int planeIndex = 0; planeIndex < planes.Length; planeIndex++)
            {
                HalfSpace plane = planes[planeIndex];
                List<int> face = BuildFaceHull(vertices, incidentPlanes, planeIndex, plane);
                if (face.Count < 3) continue;
                float area = PolygonArea(face, vertices, plane.Normal);
                if (area <= 0.0000001f) continue;
                int[] indices = face.ToArray();
                faces.Add(new EarthVolumetricFractureFace(
                    plane.Normal, area, plane.NeighbourCellIndex, indices));
                for (int triangle = 1; triangle < indices.Length - 1; triangle++)
                {
                    triangles.Add(indices[0]);
                    triangles.Add(indices[triangle]);
                    triangles.Add(indices[triangle + 1]);
                }
            }

            WeldTopology(vertices, faces, triangles,
                out List<float3> weldedVertices,
                out List<EarthVolumetricFractureFace> weldedFaces,
                out List<int> weldedTriangles);
            vertices = weldedVertices;
            faces = weldedFaces;
            triangles = weldedTriangles;

            float3[] vertexArray = vertices.ToArray();
            int[] triangleArray = triangles.ToArray();
            ComputeMassProperties(vertexArray, triangleArray, out float volume, out float3 centroid);
            float3 minimum = new float3(float.PositiveInfinity);
            float3 maximum = new float3(float.NegativeInfinity);
            for (int index = 0; index < vertexArray.Length; index++)
            {
                minimum = math.min(minimum, vertexArray[index]);
                maximum = math.max(maximum, vertexArray[index]);
            }
            float3 size = math.max(new float3(0.001f), maximum - minimum);
            float aspect = math.cmax(size) / math.max(0.001f, math.cmin(size));
            bool foundation = minimum.y <= bottom + math.max(0.025f, (top - bottom) * 0.035f);
            return new EarthVolumetricFractureCell(
                id,
                site,
                centroid,
                volume,
                aspect,
                foundation,
                vertexArray,
                triangleArray,
                faces.ToArray());
        }

        private static HalfSpace[] BuildBoundaryPlanes(float2[] boundary, float bottom, float top)
        {
            var planes = new HalfSpace[boundary.Length + 2];
            for (int index = 0; index < boundary.Length; index++)
            {
                float2 a = boundary[index];
                float2 b = boundary[(index + 1) % boundary.Length];
                float2 edge = b - a;
                float2 outward = math.normalizesafe(new float2(edge.y, -edge.x), new float2(1f, 0f));
                var normal = new float3(outward.x, 0f, outward.y);
                planes[index] = new HalfSpace(normal, math.dot(outward, a), -1);
            }
            planes[boundary.Length] = new HalfSpace(new float3(0f, -1f, 0f), -bottom, -1);
            planes[boundary.Length + 1] = new HalfSpace(new float3(0f, 1f, 0f), top, -1);
            return planes;
        }

        private static float3[] GenerateSites(
            uint seed,
            float2[] boundary,
            float bottom,
            float top,
            int count)
        {
            var random = new DeterministicRandom(seed ^ 0xB35A7D19u);
            var sites = new float3[count];
            Bounds(boundary, out float2 minimum, out float2 maximum);
            float2 center = Centroid(boundary);
            int layerCount = math.clamp((int)math.round(math.sqrt(count) * 0.65f), 3, 5);
            int distributedCount = math.clamp((int)math.round(count * 0.58f), 8, count);
            float goldenAngle = 2.39996323f;

            for (int index = 0; index < distributedCount; index++)
            {
                float radius = math.sqrt((index + 0.65f) / distributedCount) * 0.92f;
                float angle = goldenAngle * index + random.NextFloat01() * 0.34f;
                float2 candidate = center + new float2(
                    math.cos(angle) * (maximum.x - minimum.x) * 0.5f * radius,
                    math.sin(angle) * (maximum.y - minimum.y) * 0.5f * radius);
                float2 point = PullInside(candidate, center, boundary);
                int layer = (index * 2 + index / layerCount) % layerCount;
                float layer01 = (layer + 0.5f) / layerCount;
                float jitter = (random.NextFloat01() - 0.5f) * 0.24f / layerCount;
                sites[index] = new float3(
                    point.x,
                    math.lerp(bottom, top, math.clamp(layer01 + jitter, 0.08f, 0.92f)),
                    point.y);
            }

            float typicalRadius = math.sqrt(math.max(0.001f,
                math.abs(SignedArea(boundary)) / count)) * 0.31f;
            for (int index = distributedCount; index < count; index++)
            {
                int parent = math.clamp(
                    (int)math.floor(random.NextFloat01() * distributedCount),
                    0,
                    distributedCount - 1);
                float angle = goldenAngle * index + random.NextFloat01() * 0.9f;
                float radius = typicalRadius * math.lerp(0.12f, 0.72f,
                    random.NextFloat01() * random.NextFloat01());
                float2 parentPoint = new float2(sites[parent].x, sites[parent].z);
                float2 point = PullInside(
                    parentPoint + new float2(math.cos(angle), math.sin(angle)) * radius,
                    parentPoint,
                    boundary);
                int parentLayer = NearestLayer(sites[parent].y, bottom, top, layerCount);
                int layerOffset = (index & 1) == 0 ? 1 : -1;
                int layer = math.clamp(parentLayer + layerOffset, 0, layerCount - 1);
                float layer01 = (layer + 0.5f) / layerCount;
                sites[index] = new float3(
                    point.x,
                    math.lerp(bottom, top, layer01),
                    point.y);
            }
            return sites;
        }

        private static int NearestLayer(float y, float bottom, float top, int layerCount)
        {
            float normalized = math.saturate((y - bottom) / math.max(MinimumHeight, top - bottom));
            return math.clamp((int)math.floor(normalized * layerCount), 0, layerCount - 1);
        }

        private static bool TryIntersect(HalfSpace a, HalfSpace b, HalfSpace c, out float3 point)
        {
            float3 bc = math.cross(b.Normal, c.Normal);
            float denominator = math.dot(a.Normal, bc);
            if (math.abs(denominator) <= 0.0000005f)
            {
                point = default;
                return false;
            }
            point = (a.Distance * bc +
                     b.Distance * math.cross(c.Normal, a.Normal) +
                     c.Distance * math.cross(a.Normal, b.Normal)) / denominator;
            return math.all(math.isfinite(point));
        }

        private static bool ContainsAll(float3 point, HalfSpace[] planes)
        {
            for (int index = 0; index < planes.Length; index++)
                if (planes[index].SignedDistance(point) > PlaneEpsilon) return false;
            return true;
        }

        private static void AddUnique(
            List<float3> vertices,
            List<HashSet<int>> incidentPlanes,
            float3 point,
            int planeA,
            int planeB,
            int planeC)
        {
            for (int index = 0; index < vertices.Count; index++)
            {
                if (math.distancesq(vertices[index], point) > VertexMergeEpsilonSq) continue;
                incidentPlanes[index].Add(planeA);
                incidentPlanes[index].Add(planeB);
                incidentPlanes[index].Add(planeC);
                return;
            }
            vertices.Add(point);
            incidentPlanes.Add(new HashSet<int> { planeA, planeB, planeC });
        }

        private static void SortFace(List<int> face, List<float3> vertices, float3 normal)
        {
            float3 center = float3.zero;
            for (int index = 0; index < face.Count; index++) center += vertices[face[index]];
            center /= face.Count;
            float3 reference = math.abs(normal.y) < 0.86f
                ? new float3(0f, 1f, 0f)
                : new float3(1f, 0f, 0f);
            float3 u = math.normalizesafe(math.cross(reference, normal), new float3(1f, 0f, 0f));
            float3 v = math.cross(normal, u);
            face.Sort((left, right) =>
            {
                float3 a = vertices[left] - center;
                float3 b = vertices[right] - center;
                float angleA = math.atan2(math.dot(a, v), math.dot(a, u));
                float angleB = math.atan2(math.dot(b, v), math.dot(b, u));
                return angleA.CompareTo(angleB);
            });
        }

        private readonly struct ProjectedVertex
        {
            public ProjectedVertex(int index, float x, float y)
            {
                Index = index;
                X = x;
                Y = y;
            }

            public int Index { get; }
            public float X { get; }
            public float Y { get; }
        }

        /// <summary>
        /// Rebuilds each convex face from the actual half-space plane instead of the
        /// union of incident triples. Near-coincident triple intersections can merge
        /// into one vertex; carrying all of their incident-plane labels into a face
        /// inserted interior/collinear points and produced duplicate or open triangles.
        /// A deterministic planar convex hull keeps only the true boundary cycle.
        /// </summary>
        private static List<int> BuildFaceHull(
            List<float3> vertices,
            List<HashSet<int>> incidentPlanes,
            int planeIndex,
            HalfSpace plane)
        {
            float3 normal = plane.Normal;
            float3 reference = math.abs(normal.y) < 0.86f
                ? new float3(0f, 1f, 0f)
                : new float3(1f, 0f, 0f);
            float3 u = math.normalizesafe(math.cross(reference, normal), new float3(1f, 0f, 0f));
            float3 v = math.cross(normal, u);
            var projected = new List<ProjectedVertex>(12);
            for (int index = 0; index < vertices.Count; index++)
            {
                if (index >= incidentPlanes.Count || !incidentPlanes[index].Contains(planeIndex)) continue;
                float3 point = vertices[index];
                projected.Add(new ProjectedVertex(index, math.dot(point, u), math.dot(point, v)));
            }
            projected.Sort((left, right) =>
            {
                int x = left.X.CompareTo(right.X);
                if (x != 0) return x;
                int y = left.Y.CompareTo(right.Y);
                return y != 0 ? y : left.Index.CompareTo(right.Index);
            });
            if (projected.Count < 3) return new List<int>(0);

            var unique = new List<ProjectedVertex>(projected.Count);
            const float duplicateEpsilonSq = VertexMergeEpsilonSq;
            for (int index = 0; index < projected.Count; index++)
            {
                ProjectedVertex candidate = projected[index];
                if (unique.Count > 0)
                {
                    ProjectedVertex previous = unique[unique.Count - 1];
                    float dx = candidate.X - previous.X;
                    float dy = candidate.Y - previous.Y;
                    if (dx * dx + dy * dy <= duplicateEpsilonSq) continue;
                }
                unique.Add(candidate);
            }
            if (unique.Count < 3) return new List<int>(0);

            var hull = new List<ProjectedVertex>(unique.Count * 2);
            for (int index = 0; index < unique.Count; index++)
            {
                ProjectedVertex point = unique[index];
                while (hull.Count >= 2 && Cross2D(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0.0000001f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(point);
            }
            int lowerCount = hull.Count;
            for (int index = unique.Count - 2; index >= 0; index--)
            {
                ProjectedVertex point = unique[index];
                while (hull.Count > lowerCount &&
                       Cross2D(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0.0000001f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(point);
            }
            if (hull.Count > 1) hull.RemoveAt(hull.Count - 1);

            var result = new List<int>(hull.Count);
            for (int index = 0; index < hull.Count; index++) result.Add(hull[index].Index);
            return result;
        }

        private static float Cross2D(ProjectedVertex a, ProjectedVertex b, ProjectedVertex c) =>
            (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

        private readonly struct TriangleIndexKey : IEquatable<TriangleIndexKey>
        {
            public TriangleIndexKey(int a, int b, int c)
            {
                if (a > b) Swap(ref a, ref b);
                if (b > c) Swap(ref b, ref c);
                if (a > b) Swap(ref a, ref b);
                A = a;
                B = b;
                C = c;
            }
            public int A { get; }
            public int B { get; }
            public int C { get; }
            public bool Equals(TriangleIndexKey other) => A == other.A && B == other.B && C == other.C;
            public override bool Equals(object obj) => obj is TriangleIndexKey other && Equals(other);
            public override int GetHashCode() => unchecked(((A * 397) ^ B) * 397 ^ C);
            private static void Swap(ref int a, ref int b)
            {
                int value = a;
                a = b;
                b = value;
            }
        }

        /// <summary>
        /// Applies the same physical weld that the mesh integrity court will see,
        /// but does it while face ownership is still known. Degenerate slivers and
        /// duplicate fan triangles created by collapsing a sub-pixel edge are removed
        /// deterministically from every face, preserving a closed shared boundary.
        /// </summary>
        private static void WeldTopology(
            List<float3> vertices,
            List<EarthVolumetricFractureFace> faces,
            List<int> triangles,
            out List<float3> weldedVertices,
            out List<EarthVolumetricFractureFace> weldedFaces,
            out List<int> weldedTriangles)
        {
            const float weldDistanceSq = 0.0000000025f;
            int[] remap = new int[vertices.Count];
            weldedVertices = new List<float3>(vertices.Count);
            for (int index = 0; index < vertices.Count; index++)
            {
                int match = -1;
                for (int candidate = 0; candidate < weldedVertices.Count; candidate++)
                {
                    if (math.distancesq(vertices[index], weldedVertices[candidate]) > weldDistanceSq) continue;
                    match = candidate;
                    break;
                }
                if (match < 0)
                {
                    match = weldedVertices.Count;
                    weldedVertices.Add(vertices[index]);
                }
                remap[index] = match;
            }

            weldedTriangles = new List<int>(triangles.Count);
            var emittedTriangles = new HashSet<TriangleIndexKey>();
            for (int index = 0; index + 2 < triangles.Count; index += 3)
            {
                int a = remap[triangles[index]];
                int b = remap[triangles[index + 1]];
                int c = remap[triangles[index + 2]];
                if (a == b || b == c || c == a) continue;
                float3 cross = math.cross(weldedVertices[b] - weldedVertices[a], weldedVertices[c] - weldedVertices[a]);
                if (math.lengthsq(cross) <= 0.000000000001f) continue;
                var key = new TriangleIndexKey(a, b, c);
                if (!emittedTriangles.Add(key)) continue;
                weldedTriangles.Add(a);
                weldedTriangles.Add(b);
                weldedTriangles.Add(c);
            }

            weldedFaces = new List<EarthVolumetricFractureFace>(faces.Count);
            for (int faceIndex = 0; faceIndex < faces.Count; faceIndex++)
            {
                EarthVolumetricFractureFace face = faces[faceIndex];
                var mapped = new List<int>(face.VertexIndices.Length);
                for (int index = 0; index < face.VertexIndices.Length; index++)
                {
                    int vertex = remap[face.VertexIndices[index]];
                    if (mapped.Count == 0 || mapped[mapped.Count - 1] != vertex) mapped.Add(vertex);
                }
                if (mapped.Count > 1 && mapped[0] == mapped[mapped.Count - 1])
                    mapped.RemoveAt(mapped.Count - 1);
                for (int index = mapped.Count - 1; index >= 0; index--)
                {
                    int previous = mapped[(index - 1 + mapped.Count) % mapped.Count];
                    int next = mapped[(index + 1) % mapped.Count];
                    if (previous == mapped[index] || next == mapped[index]) mapped.RemoveAt(index);
                }
                if (mapped.Count < 3) continue;
                float area = PolygonArea(mapped, weldedVertices, face.Normal);
                if (area <= 0.0000001f) continue;
                weldedFaces.Add(new EarthVolumetricFractureFace(
                    face.Normal, area, face.NeighbourCellIndex, mapped.ToArray()));
            }

            CullNonManifoldFaces(weldedFaces);

            // The old triangle fans refer to the pre-weld face cycles. Retaining
            // them after a short edge collapses is exactly how a 1→2 edge becomes a
            // T-junction. Re-triangulate every final mapped face once, then remove
            // only truly identical triangles.
            weldedTriangles.Clear();
            emittedTriangles.Clear();
            for (int faceIndex = 0; faceIndex < weldedFaces.Count; faceIndex++)
            {
                int[] indices = weldedFaces[faceIndex].VertexIndices;
                for (int triangle = 1; triangle < indices.Length - 1; triangle++)
                {
                    int a = indices[0];
                    int b = indices[triangle];
                    int c = indices[triangle + 1];
                    if (a == b || b == c || c == a) continue;
                    var key = new TriangleIndexKey(a, b, c);
                    if (!emittedTriangles.Add(key)) continue;
                    weldedTriangles.Add(a);
                    weldedTriangles.Add(b);
                    weldedTriangles.Add(c);
                }
            }
        }

        private static void CullNonManifoldFaces(List<EarthVolumetricFractureFace> faces)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                var owners = new Dictionary<ulong, List<int>>();
                for (int faceIndex = 0; faceIndex < faces.Count; faceIndex++)
                {
                    int[] vertices = faces[faceIndex].VertexIndices;
                    for (int index = 0; index < vertices.Length; index++)
                    {
                        ulong key = TopologyEdgeKey(vertices[index], vertices[(index + 1) % vertices.Length]);
                        if (!owners.TryGetValue(key, out List<int> list))
                        {
                            list = new List<int>(3);
                            owners.Add(key, list);
                        }
                        if (!list.Contains(faceIndex)) list.Add(faceIndex);
                    }
                }
                var remove = new HashSet<int>();
                foreach (KeyValuePair<ulong, List<int>> pair in owners)
                {
                    List<int> list = pair.Value;
                    if (list.Count <= 2) continue;
                    int keepA = list[0];
                    int keepB = list[1];
                    float minimumDot = math.dot(faces[keepA].Normal, faces[keepB].Normal);
                    for (int a = 0; a < list.Count - 1; a++)
                    for (int b = a + 1; b < list.Count; b++)
                    {
                        float dot = math.dot(faces[list[a]].Normal, faces[list[b]].Normal);
                        if (dot >= minimumDot) continue;
                        minimumDot = dot;
                        keepA = list[a];
                        keepB = list[b];
                    }
                    for (int index = 0; index < list.Count; index++)
                        if (list[index] != keepA && list[index] != keepB) remove.Add(list[index]);
                }
                if (remove.Count == 0) return;
                for (int index = faces.Count - 1; index >= 0; index--)
                    if (remove.Contains(index)) faces.RemoveAt(index);
            }
        }

        /// <summary>
        /// Reconstructs the final boundary from the merged point cloud. This second
        /// hull pass is intentionally independent of the triples that discovered the
        /// points: after a sub-weld edge collapses, its old incident-plane graph is no
        /// longer topologically valid. Grouping all coplanar support triples into one
        /// polygon gives every boundary edge exactly two owners.
        /// </summary>
        private static void BuildConvexHullFaces(
            List<float3> vertices,
            HalfSpace[] sourcePlanes,
            List<EarthVolumetricFractureFace> faces,
            List<int> triangles)
        {
            // Welded vertices can move by roughly 5e-5 m. The hull court must use
            // a tolerance above that displacement or one face sees a split edge
            // while its neighbour sees the welded vertex. Values remain far below
            // the smallest authored fracture feature (centimetres).
            const float supportEpsilon = 0.00012f;
            const float coplanarEpsilon = 0.00020f;
            var emitted = new HashSet<string>();
            var coplanar = new List<int>(16);
            int count = vertices.Count;
            for (int a = 0; a < count - 2; a++)
            for (int b = a + 1; b < count - 1; b++)
            for (int c = b + 1; c < count; c++)
            {
                float3 normal = math.cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                float length = math.length(normal);
                if (length <= 0.000001f) continue;
                normal /= length;
                float distance = math.dot(normal, vertices[a]);
                float minimum = float.PositiveInfinity;
                float maximum = float.NegativeInfinity;
                for (int index = 0; index < count; index++)
                {
                    float signed = math.dot(normal, vertices[index]) - distance;
                    minimum = math.min(minimum, signed);
                    maximum = math.max(maximum, signed);
                }
                if (minimum < -supportEpsilon && maximum > supportEpsilon) continue;
                if (minimum >= -supportEpsilon)
                {
                    normal = -normal;
                    distance = -distance;
                }

                coplanar.Clear();
                for (int index = 0; index < count; index++)
                    if (math.abs(math.dot(normal, vertices[index]) - distance) <= coplanarEpsilon)
                        coplanar.Add(index);
                if (coplanar.Count < 3) continue;
                coplanar.Sort();
                string key = string.Join(",", coplanar);
                if (!emitted.Add(key)) continue;

                List<int> hull = BuildPlanarHull(vertices, coplanar, normal);
                if (hull.Count < 3) continue;
                float area = PolygonArea(hull, vertices, normal);
                if (area <= 0.0000001f) continue;
                int neighbour = FindNeighbourForHullFace(vertices[hull[0]], normal, sourcePlanes);
                int[] indices = hull.ToArray();
                faces.Add(new EarthVolumetricFractureFace(normal, area, neighbour, indices));
                for (int triangle = 1; triangle < indices.Length - 1; triangle++)
                {
                    triangles.Add(indices[0]);
                    triangles.Add(indices[triangle]);
                    triangles.Add(indices[triangle + 1]);
                }
            }
        }

        private static void RebuildFacesFromSourcePlanes(
            List<float3> vertices,
            HalfSpace[] sourcePlanes,
            List<EarthVolumetricFractureFace> faces,
            List<int> triangles)
        {
            const float planeMembershipEpsilon = 0.00007f;
            var candidates = new List<int>(16);
            var clusters = new List<FacePlaneCluster>(16);
            for (int planeIndex = 0; planeIndex < sourcePlanes.Length; planeIndex++)
            {
                HalfSpace plane = sourcePlanes[planeIndex];
                candidates.Clear();
                for (int vertexIndex = 0; vertexIndex < vertices.Count; vertexIndex++)
                {
                    if (math.abs(plane.SignedDistance(vertices[vertexIndex])) <= planeMembershipEpsilon)
                        candidates.Add(vertexIndex);
                }
                if (candidates.Count < 3) continue;
                List<int> hull = BuildPlanarHull(vertices, candidates, plane.Normal);
                if (hull.Count < 3) continue;
                float area = PolygonArea(hull, vertices, plane.Normal);
                if (area <= 0.0000001f) continue;
                float distance = math.dot(plane.Normal, vertices[hull[0]]);
                FacePlaneCluster cluster = null;
                for (int clusterIndex = 0; clusterIndex < clusters.Count; clusterIndex++)
                {
                    FacePlaneCluster existing = clusters[clusterIndex];
                    if (math.dot(existing.Normal, plane.Normal) < 0.9995f ||
                        math.abs(existing.Distance - distance) > 0.00045f) continue;
                    cluster = existing;
                    break;
                }
                if (cluster == null)
                {
                    cluster = new FacePlaneCluster(
                        plane.Normal,
                        distance,
                        plane.NeighbourCellIndex);
                    clusters.Add(cluster);
                }
                for (int index = 0; index < hull.Count; index++)
                    if (!cluster.Vertices.Contains(hull[index])) cluster.Vertices.Add(hull[index]);
            }

            for (int clusterIndex = 0; clusterIndex < clusters.Count; clusterIndex++)
            {
                FacePlaneCluster cluster = clusters[clusterIndex];
                List<int> hull = BuildPlanarHull(vertices, cluster.Vertices, cluster.Normal);
                if (hull.Count < 3) continue;
                float area = PolygonArea(hull, vertices, cluster.Normal);
                if (area <= 0.0000001f) continue;
                int[] indices = hull.ToArray();
                faces.Add(new EarthVolumetricFractureFace(
                    cluster.Normal,
                    area,
                    cluster.NeighbourCellIndex,
                    indices));
                for (int triangle = 1; triangle < indices.Length - 1; triangle++)
                {
                    triangles.Add(indices[0]);
                    triangles.Add(indices[triangle]);
                    triangles.Add(indices[triangle + 1]);
                }
            }
        }

        private sealed class FacePlaneCluster
        {
            public FacePlaneCluster(float3 normal, float distance, int neighbourCellIndex)
            {
                Normal = normal;
                Distance = distance;
                NeighbourCellIndex = neighbourCellIndex;
                Vertices = new List<int>(8);
            }

            public float3 Normal { get; }
            public float Distance { get; }
            public int NeighbourCellIndex { get; }
            public List<int> Vertices { get; }
        }

        private readonly struct BoundaryEdge
        {
            public BoundaryEdge(int a, int b)
            {
                A = a;
                B = b;
            }

            public int A { get; }
            public int B { get; }
        }

        private static void SealBoundaryLoops(
            List<float3> vertices,
            HalfSpace[] sourcePlanes,
            List<EarthVolumetricFractureFace> faces,
            List<int> triangles)
        {
            var counts = new Dictionary<ulong, int>(triangles.Count);
            for (int index = 0; index + 2 < triangles.Count; index += 3)
            {
                CountTopologyEdge(counts, triangles[index], triangles[index + 1]);
                CountTopologyEdge(counts, triangles[index + 1], triangles[index + 2]);
                CountTopologyEdge(counts, triangles[index + 2], triangles[index]);
            }
            var openEdges = new List<BoundaryEdge>(12);
            var adjacency = new Dictionary<int, List<int>>();
            foreach (KeyValuePair<ulong, int> pair in counts)
            {
                if (pair.Value != 1) continue;
                int a = (int)(pair.Key >> 32);
                int b = (int)(pair.Key & 0xFFFFFFFFu);
                openEdges.Add(new BoundaryEdge(a, b));
                AddNeighbour(adjacency, a, b);
                AddNeighbour(adjacency, b, a);
            }
            if (openEdges.Count == 0) return;

            var consumed = new HashSet<ulong>();
            float3 cellCenter = float3.zero;
            for (int index = 0; index < vertices.Count; index++) cellCenter += vertices[index];
            cellCenter /= math.max(1, vertices.Count);
            for (int edgeIndex = 0; edgeIndex < openEdges.Count; edgeIndex++)
            {
                BoundaryEdge seed = openEdges[edgeIndex];
                ulong seedKey = TopologyEdgeKey(seed.A, seed.B);
                if (consumed.Contains(seedKey)) continue;
                var loop = new List<int>(8) { seed.A };
                int previous = seed.A;
                int current = seed.B;
                consumed.Add(seedKey);
                int guard = openEdges.Count + 2;
                while (guard-- > 0 && current != loop[0])
                {
                    loop.Add(current);
                    if (!adjacency.TryGetValue(current, out List<int> neighbours)) break;
                    int next = -1;
                    for (int neighbourIndex = 0; neighbourIndex < neighbours.Count; neighbourIndex++)
                    {
                        int candidate = neighbours[neighbourIndex];
                        if (candidate == previous && neighbours.Count > 1) continue;
                        ulong key = TopologyEdgeKey(current, candidate);
                        if (consumed.Contains(key) && candidate != loop[0]) continue;
                        next = candidate;
                        consumed.Add(key);
                        break;
                    }
                    if (next < 0) break;
                    previous = current;
                    current = next;
                }
                if (current != loop[0] || loop.Count < 3) continue;

                float3 normal = float3.zero;
                float3 faceCenter = float3.zero;
                for (int index = 0; index < loop.Count; index++)
                {
                    float3 a = vertices[loop[index]];
                    float3 b = vertices[loop[(index + 1) % loop.Count]];
                    normal += math.cross(a, b);
                    faceCenter += a;
                }
                faceCenter /= loop.Count;
                normal = math.normalizesafe(normal, faceCenter - cellCenter);
                if (math.dot(normal, faceCenter - cellCenter) < 0f)
                {
                    loop.Reverse();
                    normal = -normal;
                }
                float area = PolygonArea(loop, vertices, normal);
                if (area <= 0.0000001f) continue;
                int neighbour = FindNeighbourForHullFace(vertices[loop[0]], normal, sourcePlanes);
                faces.Add(new EarthVolumetricFractureFace(normal, area, neighbour, loop.ToArray()));
                for (int triangle = 1; triangle < loop.Count - 1; triangle++)
                {
                    triangles.Add(loop[0]);
                    triangles.Add(loop[triangle]);
                    triangles.Add(loop[triangle + 1]);
                }
            }
        }

        private static void AddNeighbour(Dictionary<int, List<int>> adjacency, int from, int to)
        {
            if (!adjacency.TryGetValue(from, out List<int> neighbours))
            {
                neighbours = new List<int>(2);
                adjacency.Add(from, neighbours);
            }
            if (!neighbours.Contains(to)) neighbours.Add(to);
        }

        private static void RemoveSpuriousTopologyFlaps(List<int> triangles)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                var counts = new Dictionary<ulong, int>(triangles.Count);
                for (int index = 0; index + 2 < triangles.Count; index += 3)
                {
                    CountTopologyEdge(counts, triangles[index], triangles[index + 1]);
                    CountTopologyEdge(counts, triangles[index + 1], triangles[index + 2]);
                    CountTopologyEdge(counts, triangles[index + 2], triangles[index]);
                }
                bool removed = false;
                for (int index = triangles.Count - 3; index >= 0; index -= 3)
                {
                    int a = counts[TopologyEdgeKey(triangles[index], triangles[index + 1])];
                    int b = counts[TopologyEdgeKey(triangles[index + 1], triangles[index + 2])];
                    int c = counts[TopologyEdgeKey(triangles[index + 2], triangles[index])];
                    int singles = (a == 1 ? 1 : 0) + (b == 1 ? 1 : 0) + (c == 1 ? 1 : 0);
                    bool touchesOverfull = a > 2 || b > 2 || c > 2;
                    if (singles != 2 || !touchesOverfull) continue;
                    triangles.RemoveRange(index, 3);
                    removed = true;
                }
                if (!removed) return;
            }
        }

        private static void CountTopologyEdge(Dictionary<ulong, int> counts, int a, int b)
        {
            ulong key = TopologyEdgeKey(a, b);
            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
        }

        private static ulong TopologyEdgeKey(int a, int b)
        {
            uint low = (uint)math.min(a, b);
            uint high = (uint)math.max(a, b);
            return ((ulong)low << 32) | high;
        }

        private static List<int> BuildPlanarHull(
            List<float3> vertices,
            List<int> candidates,
            float3 normal)
        {
            float3 reference = math.abs(normal.y) < 0.86f
                ? new float3(0f, 1f, 0f)
                : new float3(1f, 0f, 0f);
            float3 u = math.normalizesafe(math.cross(reference, normal), new float3(1f, 0f, 0f));
            float3 v = math.cross(normal, u);
            var projected = new List<ProjectedVertex>(candidates.Count);
            for (int index = 0; index < candidates.Count; index++)
            {
                int vertexIndex = candidates[index];
                float3 point = vertices[vertexIndex];
                projected.Add(new ProjectedVertex(
                    vertexIndex,
                    math.dot(point, u),
                    math.dot(point, v)));
            }
            projected.Sort((left, right) =>
            {
                int x = left.X.CompareTo(right.X);
                if (x != 0) return x;
                int y = left.Y.CompareTo(right.Y);
                return y != 0 ? y : left.Index.CompareTo(right.Index);
            });
            var unique = new List<ProjectedVertex>(projected.Count);
            for (int index = 0; index < projected.Count; index++)
            {
                ProjectedVertex point = projected[index];
                if (unique.Count > 0)
                {
                    ProjectedVertex previous = unique[unique.Count - 1];
                    float dx = point.X - previous.X;
                    float dy = point.Y - previous.Y;
                    if (dx * dx + dy * dy <= VertexMergeEpsilonSq) continue;
                }
                unique.Add(point);
            }
            if (unique.Count < 3) return new List<int>(0);

            var hull = new List<ProjectedVertex>(unique.Count * 2);
            for (int index = 0; index < unique.Count; index++)
            {
                ProjectedVertex point = unique[index];
                while (hull.Count >= 2 &&
                       Cross2D(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0.0000001f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(point);
            }
            int lowerCount = hull.Count;
            for (int index = unique.Count - 2; index >= 0; index--)
            {
                ProjectedVertex point = unique[index];
                while (hull.Count > lowerCount &&
                       Cross2D(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0.0000001f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(point);
            }
            if (hull.Count > 1) hull.RemoveAt(hull.Count - 1);
            var result = new List<int>(hull.Count);
            for (int index = 0; index < hull.Count; index++) result.Add(hull[index].Index);
            return result;
        }

        private static int FindNeighbourForHullFace(
            float3 point,
            float3 normal,
            HalfSpace[] planes)
        {
            int neighbour = -1;
            float best = float.PositiveInfinity;
            float distance = math.dot(normal, point);
            for (int index = 0; index < planes.Length; index++)
            {
                float alignment = math.dot(normal, planes[index].Normal);
                if (alignment < 0.9995f) continue;
                float error = math.abs(distance - planes[index].Distance);
                if (error >= best || error > 0.0008f) continue;
                best = error;
                neighbour = planes[index].NeighbourCellIndex;
            }
            return neighbour;
        }

        private static float PolygonArea(List<int> face, List<float3> vertices, float3 normal)
        {
            float3 sum = float3.zero;
            for (int index = 0; index < face.Count; index++)
                sum += math.cross(vertices[face[index]], vertices[face[(index + 1) % face.Count]]);
            return math.abs(math.dot(sum, normal)) * 0.5f;
        }

        private static void ComputeMassProperties(
            float3[] vertices,
            int[] triangles,
            out float volume,
            out float3 centroid)
        {
            double signedVolume = 0.0;
            double3 weighted = double3.zero;
            for (int index = 0; index < triangles.Length; index += 3)
            {
                float3 a = vertices[triangles[index]];
                float3 b = vertices[triangles[index + 1]];
                float3 c = vertices[triangles[index + 2]];
                double tetra = math.dot((double3)a, math.cross((double3)b, (double3)c)) / 6.0;
                signedVolume += tetra;
                weighted += ((double3)a + b + c) * (tetra * 0.25);
            }

            if (math.abs(signedVolume) <= 0.000000001)
            {
                centroid = float3.zero;
                for (int index = 0; index < vertices.Length; index++) centroid += vertices[index];
                centroid /= math.max(1, vertices.Length);
                volume = 0f;
                return;
            }
            centroid = (float3)(weighted / signedVolume);
            volume = (float)math.abs(signedVolume);
        }

        private static float2[] EnsureCounterClockwise(float2[] source)
        {
            var boundary = (float2[])source.Clone();
            if (SignedArea(boundary) >= 0f) return boundary;
            Array.Reverse(boundary);
            return boundary;
        }

        private static float SignedArea(float2[] polygon)
        {
            float twiceArea = 0f;
            for (int index = 0; index < polygon.Length; index++)
            {
                float2 a = polygon[index];
                float2 b = polygon[(index + 1) % polygon.Length];
                twiceArea += a.x * b.y - b.x * a.y;
            }
            return twiceArea * 0.5f;
        }

        private static float2 Centroid(float2[] polygon)
        {
            float area6 = 0f;
            float2 sum = float2.zero;
            for (int index = 0; index < polygon.Length; index++)
            {
                float2 a = polygon[index];
                float2 b = polygon[(index + 1) % polygon.Length];
                float cross = a.x * b.y - b.x * a.y;
                area6 += cross;
                sum += (a + b) * cross;
            }
            return math.abs(area6) > 0.000001f ? sum / (area6 * 3f) : polygon[0];
        }

        private static void Bounds(float2[] polygon, out float2 minimum, out float2 maximum)
        {
            minimum = new float2(float.PositiveInfinity);
            maximum = new float2(float.NegativeInfinity);
            for (int index = 0; index < polygon.Length; index++)
            {
                minimum = math.min(minimum, polygon[index]);
                maximum = math.max(maximum, polygon[index]);
            }
        }

        private static float2 PullInside(float2 candidate, float2 fallback, float2[] boundary)
        {
            if (Contains(candidate, boundary)) return candidate;
            float2 point = candidate;
            for (int iteration = 0; iteration < 12; iteration++)
            {
                point = math.lerp(point, fallback, 0.45f);
                if (Contains(point, boundary)) return point;
            }
            return fallback;
        }

        private static bool Contains(float2 point, float2[] polygon)
        {
            for (int index = 0; index < polygon.Length; index++)
            {
                float2 a = polygon[index];
                float2 b = polygon[(index + 1) % polygon.Length];
                float2 edge = b - a;
                float cross = edge.x * (point.y - a.y) - edge.y * (point.x - a.x);
                if (cross < -PlaneEpsilon) return false;
            }
            return true;
        }
    }
}
