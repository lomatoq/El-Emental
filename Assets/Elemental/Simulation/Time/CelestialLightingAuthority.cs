using System;

namespace Elemental.Simulation.Time
{
    /// <summary>
    /// Chooses who owns the gameplay key light and celestial clock.
    /// GameplayLocked is the production default: the authored key, ambient
    /// and ephemeris remain temporally stable during a duel.
    /// AnimatedEphemeris is an explicit QA/look-development mode.
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
