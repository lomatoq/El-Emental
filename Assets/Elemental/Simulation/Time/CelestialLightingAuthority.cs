using System;

namespace Elemental.Simulation.Time
{
    /// <summary>
    /// Chooses who owns the gameplay key light and celestial clock.
    /// AnimatedEphemeris is the production day/night cycle. GameplayLocked remains
    /// an explicit option for fixed-light look development and comparisons.
    /// </summary>
    public enum CelestialLightingAuthorityMode : byte
    {
        GameplayLocked = 0,
        AnimatedEphemeris = 1
    }

    public static class CelestialLightingClockPolicy
    {
        public static double Step(
            double elapsedSeconds,
            float deltaTime,
            float timeScale,
            CelestialLightingAuthorityMode mode)
        {
            double elapsed = IsFinite(elapsedSeconds) ? elapsedSeconds : 0d;
            if (mode != CelestialLightingAuthorityMode.AnimatedEphemeris)
                return elapsed;

            float dt = IsFinite(deltaTime) ? Math.Max(0f, Math.Min(0.25f, deltaTime)) : 0f;
            float scale = IsFinite(timeScale) ? timeScale : 0f;
            double next = elapsed + dt * scale;
            return IsFinite(next) ? next : elapsed;
        }

        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
