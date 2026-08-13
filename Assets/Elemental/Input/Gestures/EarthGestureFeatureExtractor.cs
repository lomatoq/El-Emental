using System.Collections.Generic;
using Unity.Mathematics;

namespace Elemental.Input.Gestures
{
    public static class EarthGestureFeatureExtractor
    {
        public static EarthGestureFeatures Extract(
            IReadOnlyList<PointerStrokeSample> input,
            in EarthGestureSettings settings,
            List<float2> filtered,
            List<float2> resampled)
        {
            filtered.Clear();
            resampled.Clear();
            if (input == null || input.Count < 2) return default;

            for (int index = 0; index < input.Count; index++)
            {
                float2 point = input[index].ViewportPosition01;
                if (!math.all(math.isfinite(point))) continue;
                if (filtered.Count > 0 && math.distancesq(filtered[filtered.Count - 1], point) < 0.00000025f)
                    continue;
                filtered.Add(point);
            }
            if (filtered.Count < 2) return default;

            GestureResampler.Resample(filtered, settings.ResampleCount, resampled);
            SmoothInPlace(resampled, settings.Smoothing);

            float pathLength = 0f;
            float totalCurvature = 0f;
            float2 minimum = resampled[0];
            float2 maximum = resampled[0];
            float2 previousDirection = float2.zero;
            bool hasPreviousDirection = false;
            uint digest = 2166136261u;
            for (int index = 0; index < resampled.Count; index++)
            {
                float2 point = resampled[index];
                minimum = math.min(minimum, point);
                maximum = math.max(maximum, point);
                digest = Hash(digest, (uint)math.clamp(math.round(point.x * 4095f), 0f, 4095f));
                digest = Hash(digest, (uint)math.clamp(math.round(point.y * 4095f), 0f, 4095f));
                if (index == 0) continue;
                float2 segment = point - resampled[index - 1];
                float length = math.length(segment);
                pathLength += length;
                if (length <= 0.000001f) continue;
                float2 direction = segment / length;
                if (hasPreviousDirection)
                {
                    float cross = previousDirection.x * direction.y - previousDirection.y * direction.x;
                    float dot = math.clamp(math.dot(previousDirection, direction), -1f, 1f);
                    totalCurvature += math.abs(math.atan2(cross, dot));
                }
                previousDirection = direction;
                hasPreviousDirection = true;
            }

            float2 displacement = resampled[resampled.Count - 1] - resampled[0];
            float directDistance = math.length(displacement);
            float2 direction01 = directDistance > 0.000001f
                ? displacement / directDistance
                : float2.zero;
            float signedArea = SignedArea(resampled);
            float duration = math.max(0.001f, input[input.Count - 1].Time - input[0].Time);
            float2 bounds = maximum - minimum;
            float aspect = math.max(bounds.x, bounds.y) /
                           math.max(0.0001f, math.min(bounds.x, bounds.y));
            int intersections = CountSelfIntersections(resampled);
            return new EarthGestureFeatures(
                resampled.Count,
                pathLength,
                directDistance,
                math.saturate(directDistance / math.max(0.0001f, pathLength)),
                direction01,
                totalCurvature,
                signedArea,
                directDistance / math.max(0.0001f, pathLength),
                pathLength / duration,
                duration,
                aspect,
                intersections,
                digest);
        }

        private static void SmoothInPlace(List<float2> points, float amount)
        {
            if (amount <= 0f || points.Count < 3) return;
            float2 previousOriginal = points[0];
            for (int index = 1; index < points.Count - 1; index++)
            {
                float2 currentOriginal = points[index];
                float2 nextOriginal = points[index + 1];
                float2 neighborhood = (previousOriginal + currentOriginal + nextOriginal) / 3f;
                points[index] = math.lerp(currentOriginal, neighborhood, amount);
                previousOriginal = currentOriginal;
            }
        }

        private static float SignedArea(IReadOnlyList<float2> points)
        {
            float twiceArea = 0f;
            for (int index = 0; index < points.Count; index++)
            {
                float2 current = points[index];
                float2 next = points[(index + 1) % points.Count];
                twiceArea += current.x * next.y - next.x * current.y;
            }
            return twiceArea * 0.5f;
        }

        private static int CountSelfIntersections(IReadOnlyList<float2> points)
        {
            int count = 0;
            for (int a = 1; a < points.Count; a++)
            {
                for (int b = a + 2; b < points.Count; b++)
                {
                    if (a == 1 && b == points.Count - 1) continue;
                    if (SegmentsIntersect(points[a - 1], points[a], points[b - 1], points[b])) count++;
                }
            }
            return count;
        }

        private static bool SegmentsIntersect(float2 a, float2 b, float2 c, float2 d)
        {
            float abC = Cross(b - a, c - a);
            float abD = Cross(b - a, d - a);
            float cdA = Cross(d - c, a - c);
            float cdB = Cross(d - c, b - c);
            return abC * abD < -0.00000001f && cdA * cdB < -0.00000001f;
        }

        private static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;
        private static uint Hash(uint hash, uint value) => (hash ^ value) * 16777619u;
    }
}
