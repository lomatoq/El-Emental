using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public static class PlanetFacingSolver
    {
        public static float3 SolveTangentForward(float3 localUp, float3 requestedDirection, float3 fallback)
        {
            float3 up = math.normalizesafe(localUp, new float3(0f, 1f, 0f));
            float3 tangent = requestedDirection - (up * math.dot(requestedDirection, up));
            float3 safeFallback = fallback - (up * math.dot(fallback, up));
            safeFallback = math.normalizesafe(safeFallback, math.abs(up.y) < 0.95f
                ? math.normalize(math.cross(up, new float3(0f, 1f, 0f)))
                : new float3(0f, 0f, 1f));
            return math.normalizesafe(tangent, safeFallback);
        }
    }
}
