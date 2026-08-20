using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public readonly struct EarthPlatformGeometry
    {
        public EarthPlatformGeometry(
            float3 center,
            float3 up,
            float3 right,
            float3 forward,
            float2[] polygon,
            float area,
            float surfaceRadius)
        {
            Center = center;
            Up = up;
            Right = right;
            Forward = forward;
            Polygon = polygon ?? Array.Empty<float2>();
            Area = math.max(0f, area);
            SurfaceRadius = math.max(0f, surfaceRadius);
        }

        public float3 Center { get; }
        public float3 Up { get; }
        public float3 Right { get; }
        public float3 Forward { get; }
        public float2[] Polygon { get; }
        public float Area { get; }
        public float SurfaceRadius { get; }
        public bool IsValid => Polygon != null && Polygon.Length >= 3 && Area > 0.001f;
    }

    public readonly struct EarthPlatformBudgetSample
    {
        public EarthPlatformBudgetSample(
            float acceptedHeight,
            float aspectRatio,
            float costMultiplier,
            float stability01,
            bool aboveSoftLimit,
            bool hardLimited)
        {
            AcceptedHeight = acceptedHeight;
            AspectRatio = aspectRatio;
            CostMultiplier = costMultiplier;
            Stability01 = stability01;
            AboveSoftLimit = aboveSoftLimit;
            HardLimited = hardLimited;
        }

        public float AcceptedHeight { get; }
        public float AspectRatio { get; }
        public float CostMultiplier { get; }
        public float Stability01 { get; }
        public bool AboveSoftLimit { get; }
        public bool HardLimited { get; }
    }

    public static class EarthPlatformGeometrySolver
    {
        public static EarthPlatformBudgetSample EvaluateHeightBudget(
            in EarthPlatformGeometry geometry,
            float requestedHeight,
            float softHeight,
            float hardHeight,
            float heightCostExponent = 1.65f,
            float aspectCost = 0.18f)
        {
            float soft = math.max(0.1f, softHeight);
            float hard = math.max(soft, hardHeight);
            float accepted = math.clamp(requestedHeight, 0.1f, hard);
            float2 minimum = new float2(float.PositiveInfinity);
            float2 maximum = new float2(float.NegativeInfinity);
            if (geometry.Polygon != null)
            {
                for (int index = 0; index < geometry.Polygon.Length; index++)
                {
                    minimum = math.min(minimum, geometry.Polygon[index]);
                    maximum = math.max(maximum, geometry.Polygon[index]);
                }
            }
            float2 size = math.max(new float2(0.1f), maximum - minimum);
            float aspect = math.max(size.x, size.y) / math.max(0.1f, math.min(size.x, size.y));
            if (!math.isfinite(aspect)) aspect = 1f;
            float heightRatio = accepted / soft;
            float heightPenalty = math.pow(math.max(1f, heightRatio), math.max(1f, heightCostExponent));
            float aspectPenalty = 1f + math.max(0f, aspect - 1f) * math.max(0f, aspectCost);
            float cost = heightPenalty * aspectPenalty;
            float stability = math.saturate(1f / math.sqrt(math.max(1f, cost)));
            return new EarthPlatformBudgetSample(
                accepted,
                aspect,
                cost,
                stability,
                accepted > soft + 0.001f,
                requestedHeight > hard + 0.001f);
        }

        public static EarthPlatformGeometry Build(IReadOnlyList<float3> worldPath, float3 planetCenter, int maximumVertices = 32)
        {
            if (worldPath == null || worldPath.Count < 3) return default;
            float3 average = float3.zero;
            float surfaceRadius = 0f;
            for (int index = 0; index < worldPath.Count; index++)
            {
                average += worldPath[index];
                surfaceRadius += math.length(worldPath[index] - planetCenter);
            }
            average /= worldPath.Count;
            surfaceRadius /= worldPath.Count;
            float3 up = math.normalizesafe(average - planetCenter, new float3(0f, 1f, 0f));
            float3 reference = math.abs(up.y) < 0.92f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);
            float3 right = math.normalizesafe(math.cross(reference, up), new float3(1f, 0f, 0f));
            // Unity's LookRotation maps local +X to cross(up, forward). Using
            // cross(right, up) here preserves the exact preview X axis at runtime
            // instead of mirroring the committed platform sideways.
            float3 forward = math.normalizesafe(math.cross(right, up), new float3(0f, 0f, 1f));

            var projected = new List<float2>(worldPath.Count);
            for (int index = 0; index < worldPath.Count; index++)
            {
                float3 offset = worldPath[index] - average;
                float2 point = new float2(math.dot(offset, right), math.dot(offset, forward));
                bool duplicate = false;
                for (int existing = 0; existing < projected.Count; existing++)
                {
                    if (math.distancesq(point, projected[existing]) > 0.0004f) continue;
                    duplicate = true;
                    break;
                }
                if (!duplicate) projected.Add(point);
            }
            if (projected.Count < 3) return default;

            projected.Sort(ComparePoints);
            var lower = new List<float2>(projected.Count);
            var upper = new List<float2>(projected.Count);
            for (int index = 0; index < projected.Count; index++) AddHullPoint(lower, projected[index]);
            for (int index = projected.Count - 1; index >= 0; index--) AddHullPoint(upper, projected[index]);
            if (lower.Count > 0) lower.RemoveAt(lower.Count - 1);
            if (upper.Count > 0) upper.RemoveAt(upper.Count - 1);
            var hull = new List<float2>(lower.Count + upper.Count);
            hull.AddRange(lower);
            hull.AddRange(upper);
            if (hull.Count < 3) return default;

            int limit = math.clamp(maximumVertices, 8, 32);
            while (hull.Count > limit)
            {
                int remove = 0;
                float smallest = float.PositiveInfinity;
                for (int index = 0; index < hull.Count; index++)
                {
                    float2 previous = hull[(index + hull.Count - 1) % hull.Count];
                    float2 current = hull[index];
                    float2 next = hull[(index + 1) % hull.Count];
                    float triangleArea = math.abs(Cross(current - previous, next - current));
                    if (triangleArea >= smallest) continue;
                    smallest = triangleArea;
                    remove = index;
                }
                hull.RemoveAt(remove);
            }

            float area = 0f;
            float2 centroid = float2.zero;
            for (int index = 0; index < hull.Count; index++)
            {
                float2 current = hull[index];
                float2 next = hull[(index + 1) % hull.Count];
                float cross = Cross(current, next);
                area += cross;
                centroid += (current + next) * cross;
            }
            area *= 0.5f;
            if (math.abs(area) <= 0.001f) return default;
            if (area < 0f)
            {
                hull.Reverse();
                area = -area;
                centroid = -centroid;
            }
            centroid /= 6f * area;
            for (int index = 0; index < hull.Count; index++) hull[index] -= centroid;
            average += (right * centroid.x) + (forward * centroid.y);
            // Put the hull centroid back onto the sampled planet radius. Averaging
            // points on a sphere otherwise pulls large gestures inward and makes the
            // committed platform visibly shift relative to its preview.
            up = math.normalizesafe(average - planetCenter, up);
            average = planetCenter + (up * surfaceRadius);
            right = math.normalizesafe(right - (up * math.dot(right, up)), right);
            forward = math.normalizesafe(math.cross(right, up), forward);
            return new EarthPlatformGeometry(
                average, up, right, forward, hull.ToArray(), area, surfaceRadius);
        }

        public static float RequiredChordEmbedDepth(
            in EarthPlatformGeometry geometry,
            float minimumEmbed,
            float visibleSurfaceSafety)
        {
            float radius = math.max(1f, geometry.SurfaceRadius);
            float safety = math.clamp(visibleSurfaceSafety, 0f, radius * 0.25f);
            float maximumPlanarRadiusSq = 0f;
            for (int index = 0; index < geometry.Polygon.Length; index++)
                maximumPlanarRadiusSq = math.max(
                    maximumPlanarRadiusSq,
                    math.lengthsq(geometry.Polygon[index]));
            float targetRadius = math.max(0.1f, radius - safety);
            float availableSq = math.max(0.01f,
                (targetRadius * targetRadius) - maximumPlanarRadiusSq);
            float required = radius - math.sqrt(availableSq);
            return math.max(math.max(0f, minimumEmbed), required);
        }

        private static int ComparePoints(float2 a, float2 b)
        {
            int x = a.x.CompareTo(b.x);
            return x != 0 ? x : a.y.CompareTo(b.y);
        }

        private static void AddHullPoint(List<float2> hull, float2 point)
        {
            while (hull.Count >= 2 && Cross(
                       hull[hull.Count - 1] - hull[hull.Count - 2],
                       point - hull[hull.Count - 1]) <= 0f)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(point);
        }

        private static float Cross(float2 a, float2 b) => (a.x * b.y) - (a.y * b.x);
    }
}
