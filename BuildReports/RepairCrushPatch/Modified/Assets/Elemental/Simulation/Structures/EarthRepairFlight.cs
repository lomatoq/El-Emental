using Unity.Mathematics;

namespace Elemental.Simulation.Structures
{
    /// <summary>One material cell travels before the next can attach to its support.</summary>
    public static class EarthRepairFlight
    {
        public static float Duration(float distanceMeters) =>
            math.max(.28f, (float.IsFinite(distanceMeters) ? math.max(0f, distanceMeters) : 0f) / 8f);

        public static float Phase(float elapsedSeconds, float durationSeconds)
        {
            float t = math.saturate(elapsedSeconds / math.max(.001f, durationSeconds));
            return t * t * (3f - 2f * t);
        }
    }
}
