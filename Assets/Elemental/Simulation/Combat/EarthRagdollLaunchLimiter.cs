using Unity.Mathematics;

namespace Elemental.Simulation.Combat
{
    public static class EarthRagdollLaunchLimiter
    {
        public const float DefaultMaximumRiseMeters = 2.0f;
        public const float DefaultGravityMagnitude = 14f;
        public const float DefaultMaximumTangentSpeed = 4.0f;

        public static float3 LimitVelocityChange(
            float3 currentVelocity,
            float3 requestedVelocityChange,
            float3 localUp,
            float gravityMagnitude = DefaultGravityMagnitude,
            float maximumRiseMeters = DefaultMaximumRiseMeters,
            float maximumTangentSpeed = DefaultMaximumTangentSpeed)
        {
            float3 up = math.normalizesafe(localUp, new float3(0f, 1f, 0f));
            float3 desired = currentVelocity + requestedVelocityChange;
            float maximumUpSpeed = math.sqrt(2f * math.max(0.1f, gravityMagnitude) *
                                             math.max(0.1f, maximumRiseMeters));
            float desiredUp = math.min(math.dot(desired, up), maximumUpSpeed);
            float3 tangent = desired - up * math.dot(desired, up);
            tangent = math.lengthsq(tangent) > maximumTangentSpeed * maximumTangentSpeed
                ? math.normalize(tangent) * maximumTangentSpeed
                : tangent;
            return tangent + up * desiredUp - currentVelocity;
        }

        public static float3 LimitInheritedVelocity(
            float3 velocity,
            float3 localUp,
            float gravityMagnitude = DefaultGravityMagnitude,
            float maximumRiseMeters = DefaultMaximumRiseMeters,
            float maximumTangentSpeed = DefaultMaximumTangentSpeed) =>
            velocity + LimitVelocityChange(
                velocity,
                float3.zero,
                localUp,
                gravityMagnitude,
                maximumRiseMeters,
                maximumTangentSpeed);
    }
}
