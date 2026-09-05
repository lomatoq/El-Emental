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
        // Nearest loose matter wins the bounded MMB selection; stable identity
        // breaks ties independently of the physics overlap result order.
        public static bool PreferCaptureCandidate(float distanceSquared, uint stableId,
            float otherDistanceSquared, uint otherStableId) =>
            distanceSquared < otherDistanceSquared ||
            (distanceSquared == otherDistanceSquared && stableId < otherStableId);

        // An intact structure can start a circle gesture before it releases any
        // cells. Empty terrain/unsupported targets cannot start a physical grip.
        public static bool CanBeginSession(int capturedTargets, bool hasManipulableStructure) =>
            capturedTargets > 0 || hasManipulableStructure;

        public static float CompactOrbitRadius(float maximumRadius, float summedRadiusCubed) =>
            math.min(math.max(.12f, maximumRadius), math.max(.12f, math.pow(math.max(0f, summedRadiusCubed), 1f / 3f) * 1.2f));

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

        /// <summary>
        /// Places controlled matter in two broken side arcs instead of a uniform cloud
        /// through the camera-to-action corridor. The body remains fully physical;
        /// only its spring target is presentation-aware.
        /// </summary>
        public static float3 CameraAwareSlotOffset(
            uint stableId,
            float orbitRadius,
            float3 localUp,
            float3 viewForward,
            float objectClearance)
        {
            uint hash = stableId * 0x9E3779B9u + 0x7F4A7C15u;
            float3 up = math.normalizesafe(localUp, new float3(0f, 1f, 0f));
            float3 forward = math.normalizesafe(
                viewForward - up * math.dot(viewForward, up),
                new float3(0f, 0f, 1f));
            float3 right = math.normalizesafe(math.cross(up, forward), new float3(1f, 0f, 0f));
            float side = (hash & 1u) == 0u ? -1f : 1f;
            float arc01 = ((hash >> 1) & 0xFFu) / 255f;
            float height01 = ((hash >> 9) & 0xFFu) / 255f;
            float depth01 = ((hash >> 17) & 0xFFu) / 255f;
            float radius = math.max(0.1f, orbitRadius);
            float lateral = radius * math.lerp(0.58f, 1.08f, arc01) + math.max(0f, objectClearance);
            float height = radius * math.lerp(-0.20f, 0.62f, height01);
            float depth = radius * math.lerp(-0.30f, 0.16f, depth01);
            return right * side * lateral + up * height + forward * depth;
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
