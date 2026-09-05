using System;

namespace Elemental.Runtime.World
{
    /// <summary>Pure, allocation-free geometry for the authored atmosphere envelope.</summary>
    public static class AtmosphereEnvelopePolicy
    {
        public const float DefaultMinimumHeightMeters = 8f;
        public const float MinimumRevealDistanceMeters = .25f;
        public const float RevealThicknessFraction = .12f;

        public static float EffectiveOuterRadius(
            float planetRadius,
            float authoredOuterRadiusMultiplier,
            float minimumHeightMeters)
        {
            float radius = IsFinite(planetRadius) ? Math.Max(.01f, planetRadius) : .01f;
            float multiplier = IsFinite(authoredOuterRadiusMultiplier)
                ? Math.Max(1.001f, authoredOuterRadiusMultiplier)
                : 1.001f;
            float height = IsFinite(minimumHeightMeters) && minimumHeightMeters >= .5f
                ? minimumHeightMeters
                : DefaultMinimumHeightMeters;
            return Math.Max(radius * multiplier, radius + height);
        }

        public static float SystemBodyVisibility(
            float planetRadius,
            float outerRadius,
            float observerRadius)
        {
            if (!IsFinite(observerRadius)) return 0f;
            float radius = IsFinite(planetRadius) ? Math.Max(.01f, planetRadius) : .01f;
            float outer = IsFinite(outerRadius) ? Math.Max(radius, outerRadius) : radius;
            float revealDistance = Math.Max(
                MinimumRevealDistanceMeters,
                (outer - radius) * RevealThicknessFraction);
            float t = Clamp01((observerRadius - outer) / revealDistance);
            return t * t * (3f - 2f * t);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
    }
}
