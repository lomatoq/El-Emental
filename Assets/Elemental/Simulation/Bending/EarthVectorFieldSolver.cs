using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public readonly struct EarthVectorFieldSample
    {
        public EarthVectorFieldSample(float3 velocityChange, float resultingForwardSpeed, bool speedLimited)
        {
            VelocityChange = velocityChange;
            ResultingForwardSpeed = resultingForwardSpeed;
            SpeedLimited = speedLimited;
        }

        public float3 VelocityChange { get; }
        public float ResultingForwardSpeed { get; }
        public bool SpeedLimited { get; }
    }

    public static class EarthVectorFieldSolver
    {
        public static EarthVectorFieldSample Solve(
            float3 velocity,
            float mass,
            float3 direction,
            float charge01,
            float forceNewtons,
            float speedLimit,
            float deltaSeconds)
        {
            float3 forward = math.normalizesafe(direction, new float3(0f, 0f, 1f));
            float safeMass = math.max(0.01f, mass);
            float safeDelta = math.max(0f, deltaSeconds);
            float response = math.lerp(0.22f, 1f, math.saturate(charge01));
            float requestedDelta = math.max(0f, forceNewtons) * response * safeDelta / safeMass;
            float currentForward = math.dot(velocity, forward);
            float available = math.max(0f, math.max(0.1f, speedLimit) - currentForward);
            float applied = math.min(requestedDelta, available);
            return new EarthVectorFieldSample(
                forward * applied,
                currentForward + applied,
                applied + 0.00001f < requestedDelta);
        }

        public static float FinalImpulse(
            float charge01,
            float minimumImpulse,
            float maximumImpulse)
        {
            float charge = math.saturate(charge01);
            float shaped = charge * charge * (3f - (2f * charge));
            return math.lerp(math.max(0f, minimumImpulse), math.max(minimumImpulse, maximumImpulse), shaped);
        }
    }
}
