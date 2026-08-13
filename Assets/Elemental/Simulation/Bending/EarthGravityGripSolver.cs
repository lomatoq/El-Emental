using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public readonly struct EarthGravityGripSample
    {
        public EarthGravityGripSample(float3 acceleration, float3 angularAcceleration, bool speedLimited)
        {
            Acceleration = acceleration;
            AngularAcceleration = angularAcceleration;
            SpeedLimited = speedLimited;
        }

        public float3 Acceleration { get; }
        public float3 AngularAcceleration { get; }
        public bool SpeedLimited { get; }
    }

    public static class EarthGravityGripSolver
    {
        public static float3 SlotOffset(uint stableId, float orbitRadius, float3 localUp)
        {
            uint hash = stableId * 0x9E3779B9u + 0x7F4A7C15u;
            float angle = (hash & 0xFFFFu) * (math.PI * 2f / 65535f);
            float ring = math.lerp(0.35f, 1f, ((hash >> 16) & 0xFFu) / 255f);
            float height = math.lerp(-0.35f, 0.35f, ((hash >> 24) & 0xFFu) / 255f);
            float3 up = math.normalizesafe(localUp, new float3(0f, 1f, 0f));
            float3 tangent = math.normalizesafe(math.cross(up, new float3(0.37f, 0.61f, 0.19f)), new float3(1f, 0f, 0f));
            float3 bitangent = math.normalizesafe(math.cross(up, tangent), new float3(0f, 0f, 1f));
            float radius = math.max(0f, orbitRadius) * ring;
            return ((tangent * math.cos(angle)) + (bitangent * math.sin(angle))) * radius +
                   up * height * math.max(0f, orbitRadius);
        }

        public static EarthGravityGripSample Solve(
            float3 position,
            float3 velocity,
            float3 angularVelocity,
            float3 target,
            float3 gravityAcceleration,
            float stiffness,
            float damping,
            float angularDamping,
            float maximumAcceleration,
            float maximumSpeed,
            float deltaTime)
        {
            float dt = math.max(0.0001f, deltaTime);
            float3 acceleration = ((target - position) * math.max(0f, stiffness)) -
                                  (velocity * math.max(0f, damping)) - gravityAcceleration;
            acceleration = ClampMagnitude(acceleration, math.max(0.1f, maximumAcceleration));
            float3 predicted = velocity + acceleration * dt;
            float speedLimit = math.max(0.1f, maximumSpeed);
            bool limited = math.lengthsq(predicted) > speedLimit * speedLimit;
            if (limited)
            {
                float3 capped = math.normalizesafe(predicted) * speedLimit;
                acceleration = (capped - velocity) / dt;
            }

            float3 angularAcceleration = -angularVelocity * math.max(0f, angularDamping);
            return new EarthGravityGripSample(acceleration, angularAcceleration, limited);
        }

        private static float3 ClampMagnitude(float3 value, float maximum)
        {
            float lengthSq = math.lengthsq(value);
            if (lengthSq <= maximum * maximum) return value;
            return math.normalizesafe(value) * maximum;
        }
    }
}
