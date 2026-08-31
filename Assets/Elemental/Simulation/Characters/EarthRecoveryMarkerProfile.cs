using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public readonly struct EarthRecoveryMarkerProfile
    {
        public EarthRecoveryMarkerProfile(
            float feetEnablePhase,
            float controlsEnablePhase,
            float exitPhase)
        {
            FeetEnablePhase = feetEnablePhase;
            ControlsEnablePhase = controlsEnablePhase;
            ExitPhase = exitPhase;
        }

        public static EarthRecoveryMarkerProfile Default =>
            new EarthRecoveryMarkerProfile(0.38f, 0.72f, 0.94f);

        public float FeetEnablePhase { get; }
        public float ControlsEnablePhase { get; }
        public float ExitPhase { get; }

        public bool IsValid =>
            math.isfinite(FeetEnablePhase) &&
            math.isfinite(ControlsEnablePhase) &&
            math.isfinite(ExitPhase) &&
            FeetEnablePhase >= 0f &&
            FeetEnablePhase <= ControlsEnablePhase &&
            ControlsEnablePhase <= ExitPhase &&
            ExitPhase > 0f &&
            ExitPhase <= 1f;
    }

    public enum EarthRecoveryClearanceKind : byte
    {
        BasePose = 0,
        FirstLift = 1,
        MaximumLift = 2,
        BlockedAtMaximumLift = 3
    }

    public readonly struct EarthRecoveryClearanceResult
    {
        public EarthRecoveryClearanceResult(
            EarthRecoveryClearanceKind kind,
            float liftMeters,
            bool succeeded)
        {
            Kind = kind;
            LiftMeters = liftMeters;
            Succeeded = succeeded;
        }

        public EarthRecoveryClearanceKind Kind { get; }
        public float LiftMeters { get; }
        public bool Succeeded { get; }
    }
}
