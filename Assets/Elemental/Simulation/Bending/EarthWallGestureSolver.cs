using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public static class EarthWallGestureSolver
    {
        public static bool IsWallStroke(
            float2 normalizedDrag,
            float minimumDistance = 0.012f)
        {
            if (!math.all(math.isfinite(normalizedDrag))) return false;
            float threshold = math.max(0f, minimumDistance);
            return math.lengthsq(normalizedDrag) >= threshold * threshold;
        }
    }
}
