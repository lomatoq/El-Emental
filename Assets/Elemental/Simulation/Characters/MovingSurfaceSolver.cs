using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public readonly struct MovingSupportSnapshot
    {
        public MovingSupportSnapshot(uint surfaceId, float3 pointVelocity, float3 up, bool emerging)
        {
            SurfaceId = surfaceId;
            PointVelocity = pointVelocity;
            Up = math.normalizesafe(up, new float3(0f, 1f, 0f));
            Emerging = emerging;
        }

        public uint SurfaceId { get; }
        public float3 PointVelocity { get; }
        public float3 Up { get; }
        public bool Emerging { get; }
        public bool IsValid => SurfaceId != 0u;
    }

    public static class MovingSurfaceSolver
    {
        public static float3 CarryAcceleration(
            float3 riderVelocity,
            float3 supportVelocity,
            float3 localUp,
            float verticalError,
            float maximumSpeed,
            float maximumAcceleration,
            float deltaTime)
        {
            float3 up = math.normalizesafe(localUp, new float3(0f, 1f, 0f));
            float desiredUpSpeed = math.clamp(
                math.dot(supportVelocity, up) + verticalError / math.max(0.0001f, deltaTime),
                -math.max(0.1f, maximumSpeed),
                math.max(0.1f, maximumSpeed));
            float currentUpSpeed = math.dot(riderVelocity, up);
            float required = (desiredUpSpeed - currentUpSpeed) / math.max(0.0001f, deltaTime);
            return up * math.clamp(required, -math.max(0.1f, maximumAcceleration), math.max(0.1f, maximumAcceleration));
        }
    }
}
