using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public readonly struct EarthGravityWellSample
    {
        public EarthGravityWellSample(float3 acceleration, float weight, float predictedSpeed, bool speedLimited)
        {
            Acceleration = acceleration;
            Weight = weight;
            PredictedSpeed = predictedSpeed;
            SpeedLimited = speedLimited;
        }

        public float3 Acceleration { get; }
        public float Weight { get; }
        public float PredictedSpeed { get; }
        public bool SpeedLimited { get; }
    }

    public static class EarthGravityWellSolver
    {
        public static EarthGravityWellSample Solve(
            float3 position,
            float3 velocity,
            float3 focus,
            float3 localUp,
            float radius,
            float coreRadius,
            float pullAcceleration,
            float orbitAcceleration,
            float velocityDamping,
            float maximumSpeed,
            float deltaTime,
            float orbitSign)
        {
            float3 toFocus = focus - position;
            float distance = math.length(toFocus);
            float safeRadius = math.max(0.1f, radius);
            if (distance >= safeRadius || distance < 0.0001f)
                return new EarthGravityWellSample(float3.zero, 0f, math.length(velocity), false);

            float3 inward = toFocus / distance;
            float edge01 = math.saturate(1f - (distance / safeRadius));
            float weight = edge01 * edge01 * (3f - (2f * edge01));
            float safeCore = math.clamp(coreRadius, 0.05f, safeRadius * 0.8f);
            float coreFade = math.saturate(distance / safeCore);
            coreFade = coreFade * coreFade * (3f - (2f * coreFade));
            float3 up = math.normalizesafe(localUp, new float3(0f, 1f, 0f));
            float3 orbit = math.normalizesafe(math.cross(up, inward));
            float signedOrbit = orbitSign < 0f ? -1f : 1f;
            float3 acceleration =
                (inward * pullAcceleration * weight * coreFade) +
                (orbit * orbitAcceleration * weight * signedOrbit) -
                (velocity * velocityDamping * weight * (1f - (coreFade * 0.55f)));

            float dt = math.max(0.0001f, deltaTime);
            float3 predicted = velocity + (acceleration * dt);
            float limit = math.max(0.1f, maximumSpeed);
            bool limited = math.lengthsq(predicted) > limit * limit;
            if (limited)
            {
                float3 capped = math.normalizesafe(predicted) * limit;
                acceleration = (capped - velocity) / dt;
                predicted = capped;
            }
            return new EarthGravityWellSample(acceleration, weight, math.length(predicted), limited);
        }
    }
}
