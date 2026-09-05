using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public enum EarthGravityClusterReleaseMode : byte
    {
        Direct = 0,
        CompressionBlast = 1
    }

    public enum EarthGravityThrowStatus : byte
    {
        Idle, Charging, Released, NoActiveGrip, NoTargets, ActiveRepair, InvalidBodies
    }

    public readonly struct EarthGravityClusterThrowTuning
    {
        public EarthGravityClusterThrowTuning(
            float directSpeed,
            float minimumBlastSpeed,
            float maximumBlastSpeed,
            float maximumSpread,
            float spin,
            float referenceMass)
        {
            DirectSpeed = math.max(1f, directSpeed);
            MinimumBlastSpeed = math.max(DirectSpeed, minimumBlastSpeed);
            MaximumBlastSpeed = math.max(MinimumBlastSpeed, maximumBlastSpeed);
            MaximumSpread = math.clamp(maximumSpread, 0f, 0.85f);
            Spin = math.max(0f, spin);
            ReferenceMass = math.max(0.1f, referenceMass);
        }

        public float DirectSpeed { get; }
        public float MinimumBlastSpeed { get; }
        public float MaximumBlastSpeed { get; }
        public float MaximumSpread { get; }
        public float Spin { get; }
        public float ReferenceMass { get; }

        public static EarthGravityClusterThrowTuning Default =>
            new EarthGravityClusterThrowTuning(15f, 19f, 31f, 0.34f, 7.5f, 65f);
    }

    public readonly struct EarthGravityClusterLaunchSample
    {
        public EarthGravityClusterLaunchSample(float3 velocity, float3 angularVelocity, float speed)
        {
            Velocity = velocity;
            AngularVelocity = angularVelocity;
            Speed = speed;
        }

        public float3 Velocity { get; }
        public float3 AngularVelocity { get; }
        public float Speed { get; }
    }

    /// <summary>
    /// Pure launch law for a complete MMB cluster. Large pieces remain near the
    /// coherent centre ray while lighter pieces form the readable outer fan.
    /// </summary>
    public static class EarthGravityClusterThrowSolver
    {
        public static float Charge01(float heldSeconds, float fullChargeSeconds)
        {
            float t = math.saturate(heldSeconds / math.max(0.05f, fullChargeSeconds));
            return 1f - math.pow(1f - t, 2.35f);
        }

        public static float CompressedRadius(float baseRadius, float charge01) =>
            math.max(0.16f, baseRadius * math.lerp(1f, 0.36f, math.smoothstep(0f, 1f, charge01)));

        public static EarthGravityClusterLaunchSample Solve(
            uint stableId,
            int index,
            int count,
            float mass,
            float3 aimDirection,
            float3 localUp,
            EarthGravityClusterReleaseMode mode,
            float charge01,
            in EarthGravityClusterThrowTuning tuning)
        {
            float3 forward = math.normalizesafe(aimDirection, new float3(0f, 0f, 1f));
            float3 up = math.normalizesafe(localUp, new float3(0f, 1f, 0f));
            float3 right = math.normalizesafe(math.cross(up, forward), new float3(1f, 0f, 0f));
            up = math.normalizesafe(math.cross(forward, right), up);

            float mass01 = math.saturate(mass / tuning.ReferenceMass);
            float lightness = 1f - math.sqrt(mass01);
            float phase = Hash01(stableId ^ ((uint)(index + 1) * 0x9E3779B9u)) * math.PI * 2f;
            float ring01 = count <= 1 ? 0f : math.sqrt((index + 0.5f) / count);
            float blast01 = mode == EarthGravityClusterReleaseMode.CompressionBlast
                ? math.saturate(charge01)
                : 0f;
            float spread = tuning.MaximumSpread * ring01 * lightness *
                           math.lerp(0.18f, 1f, blast01);
            float3 radial = right * math.cos(phase) + up * math.sin(phase);
            float3 direction = math.normalizesafe(forward + radial * spread, forward);

            float massCompensation = math.lerp(1.12f, 0.82f, mass01);
            float speed = mode == EarthGravityClusterReleaseMode.Direct
                ? tuning.DirectSpeed * massCompensation
                : math.lerp(tuning.MinimumBlastSpeed, tuning.MaximumBlastSpeed, blast01) *
                  massCompensation;
            float spinSign = (stableId & 1u) == 0u ? -1f : 1f;
            float3 angular = direction * (tuning.Spin * spinSign * math.lerp(0.35f, 1f, blast01));
            return new EarthGravityClusterLaunchSample(direction * speed, angular, speed);
        }

        public static EarthGravityClusterLaunchSample SolveRadial(
            uint stableId, int index, int count, float mass, float3 outward,
            float3 aimDirection, float3 localUp, float charge01,
            in EarthGravityClusterThrowTuning tuning)
        {
            // Actual cluster geometry controls the burst, not a forward cone.
            float3 up = math.normalizesafe(localUp, new float3(0f, 1f, 0f));
            float3 forward = math.normalizesafe(aimDirection, new float3(0f, 0f, 1f));
            float3 right = math.normalizesafe(math.cross(up, forward), new float3(1f, 0f, 0f));
            float phase = Hash01(stableId ^ ((uint)(index + 1) * 0x9E3779B9u)) * math.PI * 2f;
            float3 fallback = math.normalizesafe(right * math.cos(phase) + forward * math.sin(phase), right);
            float3 direction = math.normalizesafe(outward, fallback);
            // Keep downward pieces from being fired immediately into the support.
            float vertical = math.dot(direction, up);
            if (vertical < 0.08f) direction = math.normalizesafe(direction + up * (0.08f - vertical), fallback);
            float speed = math.lerp(tuning.MinimumBlastSpeed, tuning.MaximumBlastSpeed, math.saturate(charge01));
            float sign = (stableId & 1u) == 0u ? -1f : 1f;
            return new EarthGravityClusterLaunchSample(direction * speed,
                right * (tuning.Spin * sign), speed);
        }

        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }
    }
}
