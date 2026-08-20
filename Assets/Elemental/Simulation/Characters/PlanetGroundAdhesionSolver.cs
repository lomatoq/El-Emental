using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    /// <summary>
    /// Computes an inward-only support adhesion. Contact separation and outward
    /// velocity increase the pull toward the support; the solver can never create
    /// the outward suspension force that made the character visibly hover.
    /// </summary>
    public static class PlanetGroundAdhesionSolver
    {
        public static float SolveInwardAcceleration(
            float groundDistance,
            float probeDistance,
            float outwardSpeed,
            float springAcceleration,
            float damping)
        {
            float safeProbe = math.max(0.001f, probeDistance);
            float separation01 = math.saturate(math.max(0f, groundDistance) / safeProbe);
            float separationPull = separation01 * math.max(0f, springAcceleration);
            float departurePull = math.max(0f, outwardSpeed) * math.max(0f, damping);
            return math.clamp(
                separationPull + departurePull,
                0f,
                math.max(0f, springAcceleration));
        }
    }
}
