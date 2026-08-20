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
            float maximumRiseSeconds,
            float chargeExponent = 1.55f)
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
            ChargeExponent = math.clamp(chargeExponent, 0.25f, 4f);
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
        public float ChargeExponent { get; }

        public static EarthPillarLaunchProfile Default => new EarthPillarLaunchProfile(
            1.45f, 2.2f, 8.8f, 12f, 25f, 0.76f, 1.4f, 0.20f, 0.46f, 1.55f);
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
            // An ease-out power curve gives even a short hold a readable launch while
            // preserving a long, controllable tail for the strongest pillar.
            return 1f - math.pow(1f - normalized, profile.ChargeExponent);
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
