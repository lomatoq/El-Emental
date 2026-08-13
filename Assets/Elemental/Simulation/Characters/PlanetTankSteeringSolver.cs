using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public static class PlanetTankSteeringSolver
    {
        public static float3 Turn(
            float3 localUp,
            float3 currentForward,
            float turnInput,
            float turnRateDegreesPerSecond,
            float deltaSeconds)
        {
            float3 up = math.normalizesafe(localUp, new float3(0f, 1f, 0f));
            float3 forward = PlanetFacingSolver.SolveTangentForward(
                up, currentForward, new float3(0f, 0f, 1f));
            float degrees = math.clamp(turnInput, -1f, 1f) *
                            math.max(0f, turnRateDegreesPerSecond) *
                            math.max(0f, deltaSeconds);
            quaternion turn = quaternion.AxisAngle(up, math.radians(degrees));
            return PlanetFacingSolver.SolveTangentForward(up, math.mul(turn, forward), forward);
        }
    }
}
