using Unity.Mathematics;

namespace Elemental.Simulation.Gravity
{
    public static class GravityFrame
    {
        private const float DirectionEpsilonSquared = 0.000001f;

        public static void BuildTangentBasis(
            float3 localUp,
            float3 referenceForward,
            out float3 tangentForward,
            out float3 tangentRight)
        {
            float3 up = math.normalizesafe(localUp, new float3(0f, 1f, 0f));
            tangentForward = referenceForward - (up * math.dot(referenceForward, up));

            if (math.lengthsq(tangentForward) <= DirectionEpsilonSquared)
            {
                float3 fallbackAxis = math.abs(up.y) < 0.95f
                    ? new float3(0f, 1f, 0f)
                    : new float3(0f, 0f, 1f);
                tangentForward = math.cross(fallbackAxis, up);
            }

            tangentForward = math.normalizesafe(tangentForward, new float3(0f, 0f, 1f));
            tangentRight = math.normalizesafe(math.cross(up, tangentForward), new float3(1f, 0f, 0f));
            tangentForward = math.normalizesafe(math.cross(tangentRight, up), tangentForward);
        }
    }
}
