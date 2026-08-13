using System.Collections.Generic;
using Unity.Mathematics;

namespace Elemental.Input.Gestures
{
    public static class MagicGestureKinematics
    {
        public static float PixelsPerSecond(IReadOnlyList<float2> points, float durationSeconds)
        {
            if (points == null || points.Count < 2 || !float.IsFinite(durationSeconds) || durationSeconds <= 0f)
                return 0f;
            float displacement = math.distance(points[0], points[points.Count - 1]);
            return displacement / math.max(durationSeconds, 0.016f);
        }

        public static float FlickIntensity(
            IReadOnlyList<float2> points,
            float durationSeconds,
            float minimumSpeed = 220f,
            float fullSpeed = 1400f)
        {
            float speed = PixelsPerSecond(points, durationSeconds);
            float range = math.max(1f, fullSpeed - minimumSpeed);
            return math.saturate((speed - minimumSpeed) / range);
        }

        public static float WallHoldIntensity(float durationSeconds)
        {
            if (!float.IsFinite(durationSeconds) || durationSeconds <= 0.12f) return 0f;
            // Soft asymptote: a longer brace always adds some height without exposing an
            // abrupt charge plateau, while command intensity remains network-safe and bounded.
            return math.saturate(1f - math.exp(-(durationSeconds - 0.12f) / 1.15f));
        }
    }
}
