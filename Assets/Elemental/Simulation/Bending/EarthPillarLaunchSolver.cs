using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public readonly struct EarthPillarLaunchProfile
    {
        public EarthPillarLaunchProfile(
            float fullChargeSeconds,
            float minimumHeight,
            float maximumHeight,
            float minimumVelocityChange,
            float maximumVelocityChange,
            float minimumRadius,
            float maximumRadius,
            float minimumRiseSeconds,
            float maximumRiseSeconds)
        {
            FullChargeSeconds = math.max(0.05f, fullChargeSeconds);
            MinimumHeight = math.max(0.1f, minimumHeight);
            MaximumHeight = math.max(MinimumHeight, maximumHeight);
            MinimumVelocityChange = math.max(0.1f, minimumVelocityChange);
            MaximumVelocityChange = math.max(MinimumVelocityChange, maximumVelocityChange);
            MinimumRadius = math.max(0.1f, minimumRadius);
            MaximumRadius = math.max(MinimumRadius, maximumRadius);
            MinimumRiseSeconds = math.max(0.05f, minimumRiseSeconds);
            MaximumRiseSeconds = math.max(MinimumRiseSeconds, maximumRiseSeconds);
        }

        public float FullChargeSeconds { get; }
        public float MinimumHeight { get; }
        public float MaximumHeight { get; }
        public float MinimumVelocityChange { get; }
        public float MaximumVelocityChange { get; }
        public float MinimumRadius { get; }
        public float MaximumRadius { get; }
        public float MinimumRiseSeconds { get; }
        public float MaximumRiseSeconds { get; }

        public static EarthPillarLaunchProfile Default => new EarthPillarLaunchProfile(
            1.35f, 1.5f, 7.2f, 7.5f, 19f, 0.72f, 1.2f, 0.24f, 0.52f);
    }

    public readonly struct EarthPillarLaunchResult
    {
        public EarthPillarLaunchResult(
            float charge01,
            float height,
            float velocityChange,
            float radius,
            float riseSeconds)
        {
            Charge01 = charge01;
            Height = height;
            VelocityChange = velocityChange;
            Radius = radius;
            RiseSeconds = riseSeconds;
        }

        public float Charge01 { get; }
        public float Height { get; }
        public float VelocityChange { get; }
        public float Radius { get; }
        public float RiseSeconds { get; }
    }

    public readonly struct EarthPillarLaunchEvent
    {
        public EarthPillarLaunchEvent(
            uint tick,
            float3 surfaceBase,
            float3 localUp,
            in EarthPillarLaunchResult launch)
        {
            Tick = tick;
            SurfaceBase = surfaceBase;
            LocalUp = localUp;
            Charge01 = launch.Charge01;
            Height = launch.Height;
            VelocityChange = launch.VelocityChange;
            Radius = launch.Radius;
            RiseSeconds = launch.RiseSeconds;
        }

        public uint Tick { get; }
        public float3 SurfaceBase { get; }
        public float3 LocalUp { get; }
        public float Charge01 { get; }
        public float Height { get; }
        public float VelocityChange { get; }
        public float Radius { get; }
        public float RiseSeconds { get; }
    }

    public static class EarthPillarLaunchSolver
    {
        public static float Charge01(float heldSeconds, in EarthPillarLaunchProfile profile)
        {
            float normalized = math.saturate(math.max(0f, heldSeconds) / profile.FullChargeSeconds);
            return normalized * normalized * (3f - (2f * normalized));
        }

        public static EarthPillarLaunchResult Solve(
            float heldSeconds,
            in EarthPillarLaunchProfile profile)
        {
            float charge = Charge01(heldSeconds, in profile);
            return new EarthPillarLaunchResult(
                charge,
                math.lerp(profile.MinimumHeight, profile.MaximumHeight, charge),
                math.lerp(profile.MinimumVelocityChange, profile.MaximumVelocityChange, charge),
                math.lerp(profile.MinimumRadius, profile.MaximumRadius, charge),
                math.lerp(profile.MinimumRiseSeconds, profile.MaximumRiseSeconds, charge));
        }
    }
}
