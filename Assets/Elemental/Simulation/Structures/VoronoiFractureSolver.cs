using System;
using System.Collections.Generic;
using Elemental.Core.Math;
using Unity.Mathematics;

namespace Elemental.Simulation.Structures
{
    public readonly struct VoronoiFractureCell
    {
        public VoronoiFractureCell(uint id, float2 site, float2 centroid, float area, float2[] vertices)
        {
            Id = id;
            Site = site;
            Centroid = centroid;
            Area = area;
            Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
        }

        public uint Id { get; }
        public float2 Site { get; }
        public float2 Centroid { get; }
        public float Area { get; }
        public float2[] Vertices { get; }
    }

    public readonly struct EarthStructureFracturePlan
    {
        public EarthStructureFracturePlan(uint seed, float2[] boundary, VoronoiFractureCell[] cells)
        {
            Seed = seed;
            Boundary = boundary ?? Array.Empty<float2>();
            Cells = cells ?? Array.Empty<VoronoiFractureCell>();
        }

        public uint Seed { get; }
        public float2[] Boundary { get; }
        public VoronoiFractureCell[] Cells { get; }
        public bool IsValid => Boundary.Length >= 3 && Cells.Length >= 4;
    }

    public static class VoronoiFractureSolver
    {
        private const float HalfExtent = 0.5f;
        private const float Epsilon = 0.00001f;

        public static VoronoiFractureCell[] Build(uint seed, int requestedCellCount)
        {
            int cellCount = math.clamp(requestedCellCount, 4, 24);
            float2[] sites = GenerateSites(seed, cellCount);
            var result = new VoronoiFractureCell[cellCount];
            for (int siteIndex = 0; siteIndex < sites.Length; siteIndex++)
            {
                var polygon = new List<float2>(16)
                {
                    new float2(-HalfExtent, -HalfExtent),
                    new float2(HalfExtent, -HalfExtent),
                    new float2(HalfExtent, HalfExtent),
                    new float2(-HalfExtent, HalfExtent)
                };

                for (int otherIndex = 0; otherIndex < sites.Length && polygon.Count >= 3; otherIndex++)
                {
                    if (otherIndex == siteIndex) continue;
                    ClipToNearestHalfPlane(polygon, sites[siteIndex], sites[otherIndex]);
                }

                float2[] vertices = polygon.ToArray();
                ComputeAreaAndCentroid(vertices, out float area, out float2 centroid);
                result[siteIndex] = new VoronoiFractureCell(
                    (uint)(siteIndex + 1), sites[siteIndex], centroid, area, vertices);
            }

            return result;
        }

        public static VoronoiFractureCell[] BuildNormalizedForAspect(
            uint seed,
            int requestedCellCount,
            float widthToHeight)
        {
            int cellCount = math.clamp(requestedCellCount, 4, 24);
            float aspect = math.clamp(widthToHeight, 1f, 12f);
            float2 halfExtents = new float2(aspect * 0.5f, HalfExtent);
            float2[] sites = GenerateSites(seed, cellCount, halfExtents);
            // Two bounded Lloyd passes remove the needle-like cells produced by pure
            // random sites while retaining the irregular Voronoi silhouette.
            for (int relaxation = 0; relaxation < 2; relaxation++)
            {
                var relaxedSites = new float2[cellCount];
                for (int siteIndex = 0; siteIndex < sites.Length; siteIndex++)
                {
                    List<float2> polygon = BuildCellPolygon(sites, siteIndex, halfExtents);
                    float2 centroid = sites[siteIndex];
                    if (polygon.Count >= 3)
                        ComputeAreaAndCentroid(polygon.ToArray(), out _, out centroid);
                    relaxedSites[siteIndex] = math.lerp(sites[siteIndex], centroid, 0.72f);
                }
                sites = relaxedSites;
            }

            return BuildNormalizedCells(sites, aspect, halfExtents);
        }

        public static VoronoiFractureCell[] BuildHierarchicalNormalizedForAspect(
            uint seed,
            float widthToHeight)
        {
            float aspect = math.clamp(widthToHeight, 1f, 12f);
            float2 halfExtents = new float2(aspect * 0.5f, HalfExtent);
            float2[] sites = GenerateHierarchicalSites(seed, halfExtents);
            return BuildNormalizedCells(sites, aspect, halfExtents);
        }

        /// <summary>
        /// Prefractures an arbitrary convex platform footprint. Sparse parent sites
        /// create a few large plates while clustered child sites form small chips.
        /// Every returned cell is already clipped to the exact authored boundary.
        /// </summary>
        public static EarthStructureFracturePlan BuildHierarchicalClipped(
            uint seed,
            float2[] convexBoundary,
            int requestedCellCount = 24)
        {
            if (convexBoundary == null || convexBoundary.Length < 3)
                return new EarthStructureFracturePlan(seed, convexBoundary, Array.Empty<VoronoiFractureCell>());
            int count = math.clamp(requestedCellCount, 18, 28);
            float2 minimum = new float2(float.PositiveInfinity);
            float2 maximum = new float2(float.NegativeInfinity);
            for (int index = 0; index < convexBoundary.Length; index++)
            {
                minimum = math.min(minimum, convexBoundary[index]);
                maximum = math.max(maximum, convexBoundary[index]);
            }

            float2[] sites = GenerateHierarchicalSitesInBoundary(seed, convexBoundary, minimum, maximum, count);
            var cells = new VoronoiFractureCell[count];
            for (int siteIndex = 0; siteIndex < sites.Length; siteIndex++)
            {
                var polygon = new List<float2>(convexBoundary.Length + 8);
                polygon.AddRange(convexBoundary);
                for (int otherIndex = 0; otherIndex < sites.Length && polygon.Count >= 3; otherIndex++)
                {
                    if (otherIndex == siteIndex) continue;
                    ClipToNearestHalfPlane(polygon, sites[siteIndex], sites[otherIndex]);
                }
                float2[] vertices = polygon.ToArray();
                if (vertices.Length < 3)
                {
                    vertices = new[]
                    {
                        sites[siteIndex] + new float2(-0.02f, -0.02f),
                        sites[siteIndex] + new float2(0.02f, -0.02f),
                        sites[siteIndex] + new float2(0f, 0.02f)
                    };
                }
                ComputeAreaAndCentroid(vertices, out float area, out float2 centroid);
                cells[siteIndex] = new VoronoiFractureCell(
                    (uint)(siteIndex + 1), sites[siteIndex], centroid, area, vertices);
            }
            return new EarthStructureFracturePlan(seed, (float2[])convexBoundary.Clone(), cells);
        }

        /// <summary>
        /// Builds exact shared-boundary cells from caller-authored sites. This is
        /// used by the radial/spiral ground wave: the solver owns topology, while
        /// the generic Voronoi clipper guarantees that adjacent plates reuse the
        /// same bisector instead of approximating neighbours with scaled boxes.
        /// </summary>
        public static EarthStructureFracturePlan BuildClippedFromSites(
            uint seed,
            float2[] convexBoundary,
            float2[] sites)
        {
            if (convexBoundary == null || convexBoundary.Length < 3 ||
                sites == null || sites.Length < 4)
                return new EarthStructureFracturePlan(seed, convexBoundary, Array.Empty<VoronoiFractureCell>());
            var cells = new VoronoiFractureCell[sites.Length];
            for (int siteIndex = 0; siteIndex < sites.Length; siteIndex++)
            {
                var polygon = new List<float2>(convexBoundary.Length + 8);
                polygon.AddRange(convexBoundary);
                for (int otherIndex = 0; otherIndex < sites.Length && polygon.Count >= 3; otherIndex++)
                {
                    if (otherIndex == siteIndex) continue;
                    ClipToNearestHalfPlane(polygon, sites[siteIndex], sites[otherIndex]);
                }
                float2[] vertices = polygon.ToArray();
                if (vertices.Length < 3)
                {
                    vertices = new[]
                    {
                        sites[siteIndex] + new float2(-0.01f, -0.008f),
                        sites[siteIndex] + new float2(0.012f, -0.006f),
                        sites[siteIndex] + new float2(0f, 0.012f)
                    };
                }
                ComputeAreaAndCentroid(vertices, out float area, out float2 centroid);
                cells[siteIndex] = new VoronoiFractureCell(
                    (uint)(siteIndex + 1), sites[siteIndex], centroid, area, vertices);
            }
            return new EarthStructureFracturePlan(seed, (float2[])convexBoundary.Clone(), cells);
        }

        public static float2[] BuildChippedOutline(VoronoiFractureCell cell, uint seed)
        {
            if (cell.Vertices == null || cell.Vertices.Length < 3)
                return cell.Vertices ?? Array.Empty<float2>();
            var outline = new float2[cell.Vertices.Length * 3];
            int output = 0;
            for (int edgeIndex = 0; edgeIndex < cell.Vertices.Length; edgeIndex++)
            {
                float2 a = cell.Vertices[edgeIndex];
                float2 b = cell.Vertices[(edgeIndex + 1) % cell.Vertices.Length];
                bool canonicalForward = a.x < b.x ||
                                        (math.abs(a.x - b.x) <= Epsilon && a.y <= b.y);
                float2 p0 = canonicalForward ? a : b;
                float2 p1 = canonicalForward ? b : a;
                float2 edge = p1 - p0;
                float length = math.length(edge);
                float2 normal = length > Epsilon
                    ? new float2(-edge.y, edge.x) / length
                    : float2.zero;
                uint hash = HashEdge(seed, p0, p1);
                float amplitude = math.min(0.032f, length * 0.085f);
                float firstOffset = SignedChipping(hash) * amplitude;
                float secondOffset = SignedChipping(Hash(hash ^ 0xA511E9B3u)) * amplitude;
                float2 first = math.lerp(p0, p1, 0.34f) + normal * firstOffset;
                float2 second = math.lerp(p0, p1, 0.67f) + normal * secondOffset;

                outline[output++] = a;
                outline[output++] = canonicalForward ? first : second;
                outline[output++] = canonicalForward ? second : first;
            }
            return outline;
        }

        private static VoronoiFractureCell[] BuildNormalizedCells(
            float2[] sites,
            float aspect,
            float2 halfExtents)
        {
            var result = new VoronoiFractureCell[sites.Length];
            for (int siteIndex = 0; siteIndex < sites.Length; siteIndex++)
            {
                List<float2> polygon = BuildCellPolygon(sites, siteIndex, halfExtents);
                float2[] physicalVertices = polygon.ToArray();
                ComputeAreaAndCentroid(physicalVertices, out float physicalArea, out float2 physicalCentroid);
                var normalizedVertices = new float2[physicalVertices.Length];
                for (int vertexIndex = 0; vertexIndex < physicalVertices.Length; vertexIndex++)
                {
                    normalizedVertices[vertexIndex] = new float2(
                        physicalVertices[vertexIndex].x / aspect,
                        physicalVertices[vertexIndex].y);
                }

                result[siteIndex] = new VoronoiFractureCell(
                    (uint)(siteIndex + 1),
                    new float2(sites[siteIndex].x / aspect, sites[siteIndex].y),
                    new float2(physicalCentroid.x / aspect, physicalCentroid.y),
                    physicalArea / aspect,
                    normalizedVertices);
            }

            return result;
        }

        private static float2[] GenerateHierarchicalSites(uint seed, float2 halfExtents)
        {
            const int largeCount = 5;
            const int mediumCount = 7;
            const int smallCount = 12;
            var sites = new float2[largeCount + mediumCount + smallCount];
            float2[] largePattern =
            {
                new float2(-0.77f, -0.32f),
                new float2(0.46f, -0.73f),
                new float2(-0.38f, 0.69f),
                new float2(0.78f, 0.31f),
                new float2(0.03f, 0.08f)
            };
            for (int index = 0; index < largeCount; index++)
            {
                sites[index] = new float2(
                    largePattern[index].x * halfExtents.x * 0.86f,
                    largePattern[index].y * halfExtents.y * 0.86f);
            }

            var random = new DeterministicRandom(seed ^ 0x51A7C0DEu);
            float angularOffset = random.NextFloat01() * math.PI * 2f;
            for (int index = 0; index < mediumCount; index++)
            {
                float angle = angularOffset + (index * 2.3999632f) +
                              math.lerp(-0.34f, 0.34f, random.NextFloat01());
                float radius = math.lerp(0.32f, 0.88f,
                    random.NextFloat01() * random.NextFloat01());
                sites[largeCount + index] = new float2(
                    math.cos(angle) * halfExtents.x * radius,
                    math.sin(angle) * halfExtents.y * radius);
            }

            float typicalSpacing = math.sqrt((halfExtents.x * halfExtents.y * 4f) / sites.Length);
            for (int index = 0; index < smallCount; index++)
            {
                int parentIndex = largeCount + math.min(
                    mediumCount - 1,
                    (int)math.floor(random.NextFloat01() * mediumCount));
                float angle = (index * 2.3999632f) + (random.NextFloat01() * 0.65f);
                float radius = typicalSpacing * math.lerp(0.12f, 0.42f, random.NextFloat01());
                float2 offset = new float2(math.cos(angle), math.sin(angle)) * radius;
                float2 candidate = sites[parentIndex] + offset;
                sites[largeCount + mediumCount + index] = math.clamp(
                    candidate,
                    -halfExtents + new float2(0.025f),
                    halfExtents - new float2(0.025f));
            }

            return sites;
        }

        private static float2[] GenerateHierarchicalSitesInBoundary(
            uint seed,
            float2[] boundary,
            float2 minimum,
            float2 maximum,
            int count)
        {
            var random = new DeterministicRandom(seed ^ 0xA17E5EEDu);
            var sites = new float2[count];
            float2 center = (minimum + maximum) * 0.5f;
            float2 half = math.max(new float2(0.1f), (maximum - minimum) * 0.5f);
            int largeCount = math.clamp((int)math.round(count * 0.18f), 3, 5);
            int mediumCount = math.clamp((int)math.round(count * 0.32f), 6, 9);
            float angularOffset = random.NextFloat01() * math.PI * 2f;
            for (int index = 0; index < largeCount + mediumCount; index++)
            {
                float angle = angularOffset + index * 2.3999632f + math.lerp(-0.18f, 0.18f, random.NextFloat01());
                float radius01 = index < largeCount
                    ? math.lerp(0.18f, 0.72f, (index + 0.5f) / largeCount)
                    : math.lerp(0.22f, 0.88f, random.NextFloat01());
                float2 candidate = center + new float2(
                    math.cos(angle) * half.x * radius01,
                    math.sin(angle) * half.y * radius01);
                sites[index] = PullInside(candidate, center, boundary);
            }

            float typicalSpacing = math.sqrt(math.max(0.01f, half.x * half.y * 4f / count));
            for (int index = largeCount + mediumCount; index < count; index++)
            {
                int parent = largeCount + ((index - largeCount - mediumCount) % mediumCount);
                float angle = angularOffset + index * 2.3999632f + random.NextFloat01() * 0.7f;
                float radius = typicalSpacing * math.lerp(0.10f, 0.34f, random.NextFloat01());
                float2 candidate = sites[parent] + new float2(math.cos(angle), math.sin(angle)) * radius;
                sites[index] = PullInside(candidate, sites[parent], boundary);
            }
            return sites;
        }

        private static float2 PullInside(float2 candidate, float2 fallback, float2[] boundary)
        {
            if (IsInsideConvex(candidate, boundary)) return candidate;
            float2 current = candidate;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                current = math.lerp(current, fallback, 0.5f);
                if (IsInsideConvex(current, boundary)) return current;
            }
            return fallback;
        }

        private static bool IsInsideConvex(float2 point, float2[] polygon)
        {
            float sign = 0f;
            for (int index = 0; index < polygon.Length; index++)
            {
                float2 a = polygon[index];
                float2 b = polygon[(index + 1) % polygon.Length];
                float cross = ((b.x - a.x) * (point.y - a.y)) -
                              ((b.y - a.y) * (point.x - a.x));
                if (math.abs(cross) <= Epsilon) continue;
                if (sign == 0f) sign = math.sign(cross);
                else if (math.sign(cross) != sign) return false;
            }
            return true;
        }

        private static uint HashEdge(uint seed, float2 p0, float2 p1)
        {
            uint hash = seed ^ 0x9E3779B9u;
            hash = Hash(hash ^ unchecked((uint)math.round(p0.x * 100000f)));
            hash = Hash(hash ^ unchecked((uint)math.round(p0.y * 100000f)));
            hash = Hash(hash ^ unchecked((uint)math.round(p1.x * 100000f)));
            return Hash(hash ^ unchecked((uint)math.round(p1.y * 100000f)));
        }

        private static float SignedChipping(uint hash)
        {
            float magnitude = math.lerp(0.38f, 1f, (hash & 0xFFFFu) / 65535f);
            return (hash & 0x10000u) == 0u ? -magnitude : magnitude;
        }

        private static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        private static List<float2> BuildCellPolygon(
            float2[] sites,
            int siteIndex,
            float2 halfExtents)
        {
            var polygon = new List<float2>(16)
            {
                new float2(-halfExtents.x, -halfExtents.y),
                new float2(halfExtents.x, -halfExtents.y),
                new float2(halfExtents.x, halfExtents.y),
                new float2(-halfExtents.x, halfExtents.y)
            };
            for (int otherIndex = 0; otherIndex < sites.Length && polygon.Count >= 3; otherIndex++)
            {
                if (otherIndex == siteIndex) continue;
                ClipToNearestHalfPlane(polygon, sites[siteIndex], sites[otherIndex]);
            }
            return polygon;
        }

        private static float2[] GenerateSites(uint seed, int count)
        {
            var random = new DeterministicRandom(seed);
            var sites = new float2[count];
            const float margin = 0.055f;
            const float minimumSeparationSq = 0.105f * 0.105f;
            for (int index = 0; index < count; index++)
            {
                float2 candidate = default;
                bool accepted = false;
                for (int attempt = 0; attempt < 32; attempt++)
                {
                    candidate = new float2(
                        math.lerp(-HalfExtent + margin, HalfExtent - margin, random.NextFloat01()),
                        math.lerp(-HalfExtent + margin, HalfExtent - margin, random.NextFloat01()));
                    accepted = true;
                    for (int existing = 0; existing < index; existing++)
                    {
                        if (math.distancesq(candidate, sites[existing]) >= minimumSeparationSq) continue;
                        accepted = false;
                        break;
                    }
                    if (accepted) break;
                }

                if (!accepted)
                {
                    float angle = index * 2.3999632f;
                    float radius = 0.10f + (0.34f * math.sqrt((index + 0.5f) / count));
                    candidate = new float2(math.cos(angle), math.sin(angle)) * radius;
                }
                sites[index] = candidate;
            }
            return sites;
        }

        private static float2[] GenerateSites(uint seed, int count, float2 halfExtents)
        {
            var random = new DeterministicRandom(seed);
            var sites = new float2[count];
            float width = halfExtents.x * 2f;
            float height = halfExtents.y * 2f;
            float marginX = width * 0.055f;
            float marginY = height * 0.055f;
            float minimumSeparation = math.sqrt((width * height) / count) * 0.45f;
            float minimumSeparationSq = minimumSeparation * minimumSeparation;
            for (int index = 0; index < count; index++)
            {
                float2 candidate = default;
                bool accepted = false;
                for (int attempt = 0; attempt < 48; attempt++)
                {
                    candidate = new float2(
                        math.lerp(-halfExtents.x + marginX, halfExtents.x - marginX, random.NextFloat01()),
                        math.lerp(-halfExtents.y + marginY, halfExtents.y - marginY, random.NextFloat01()));
                    accepted = true;
                    for (int existing = 0; existing < index; existing++)
                    {
                        if (math.distancesq(candidate, sites[existing]) >= minimumSeparationSq) continue;
                        accepted = false;
                        break;
                    }
                    if (accepted) break;
                }

                if (!accepted)
                {
                    float angle = index * 2.3999632f;
                    float radius = 0.10f + (0.34f * math.sqrt((index + 0.5f) / count));
                    candidate = new float2(
                        math.cos(angle) * radius * width,
                        math.sin(angle) * radius * height);
                }
                sites[index] = candidate;
            }
            return sites;
        }

        private static void ClipToNearestHalfPlane(List<float2> polygon, float2 site, float2 other)
        {
            float2 normal = other - site;
            float boundary = (math.lengthsq(other) - math.lengthsq(site)) * 0.5f;
            if (math.lengthsq(normal) <= Epsilon) return;

            var output = new List<float2>(polygon.Count + 2);
            float2 previous = polygon[polygon.Count - 1];
            float previousDistance = math.dot(normal, previous) - boundary;
            bool previousInside = previousDistance <= Epsilon;
            for (int index = 0; index < polygon.Count; index++)
            {
                float2 current = polygon[index];
                float currentDistance = math.dot(normal, current) - boundary;
                bool currentInside = currentDistance <= Epsilon;
                if (currentInside != previousInside)
                {
                    float denominator = previousDistance - currentDistance;
                    float t = math.abs(denominator) > Epsilon ? previousDistance / denominator : 0f;
                    output.Add(math.lerp(previous, current, math.saturate(t)));
                }
                if (currentInside) output.Add(current);
                previous = current;
                previousDistance = currentDistance;
                previousInside = currentInside;
            }

            polygon.Clear();
            polygon.AddRange(output);
        }

        private static void ComputeAreaAndCentroid(float2[] vertices, out float area, out float2 centroid)
        {
            float signedDoubleArea = 0f;
            float2 weighted = float2.zero;
            for (int index = 0; index < vertices.Length; index++)
            {
                float2 a = vertices[index];
                float2 b = vertices[(index + 1) % vertices.Length];
                float cross = (a.x * b.y) - (b.x * a.y);
                signedDoubleArea += cross;
                weighted += (a + b) * cross;
            }

            area = math.abs(signedDoubleArea) * 0.5f;
            centroid = math.abs(signedDoubleArea) > Epsilon
                ? weighted / (3f * signedDoubleArea)
                : vertices[0];
        }
    }
}
