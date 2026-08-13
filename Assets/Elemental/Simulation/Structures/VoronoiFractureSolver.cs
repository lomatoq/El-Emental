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
                new float2(-0.62f, -0.56f),
                new float2(0.58f, -0.57f),
                new float2(-0.66f, 0.52f),
                new float2(0.64f, 0.50f),
                new float2(0.00f, 0.02f)
            };
            for (int index = 0; index < largeCount; index++)
            {
                sites[index] = new float2(
                    largePattern[index].x * halfExtents.x * 0.86f,
                    largePattern[index].y * halfExtents.y * 0.86f);
            }

            var random = new DeterministicRandom(seed ^ 0x51A7C0DEu);
            for (int index = 0; index < mediumCount; index++)
            {
                float angle = (index * 2.3999632f) + (random.NextFloat01() * 0.42f);
                float radius = math.lerp(0.42f, 0.78f, random.NextFloat01());
                sites[largeCount + index] = new float2(
                    math.cos(angle) * halfExtents.x * radius,
                    math.sin(angle) * halfExtents.y * radius);
            }

            float typicalSpacing = math.sqrt((halfExtents.x * halfExtents.y * 4f) / sites.Length);
            for (int index = 0; index < smallCount; index++)
            {
                int parentIndex = largeCount + (index % mediumCount);
                float angle = (index * 2.3999632f) + (random.NextFloat01() * 0.65f);
                float radius = typicalSpacing * math.lerp(0.13f, 0.27f, random.NextFloat01());
                float2 offset = new float2(math.cos(angle), math.sin(angle)) * radius;
                float2 candidate = sites[parentIndex] + offset;
                sites[largeCount + mediumCount + index] = math.clamp(
                    candidate,
                    -halfExtents + new float2(0.025f),
                    halfExtents - new float2(0.025f));
            }

            return sites;
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
