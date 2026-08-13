using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public enum BendPhase : byte
    {
        Idle,
        Acquiring,
        Forming,
        Holding,
        Charging,
        Committing,
        Sustaining,
        Recovery,
        Cancelled
    }

    public enum BendOriginMode : byte
    {
        Aim,
        Self
    }

    public enum BendGestureIntent : byte
    {
        None,
        Tap,
        HoldStill,
        Flick,
        DragUp,
        DragDown,
        SweepHorizontal,
        CircleClockwise,
        CircleCounterClockwise,
        ExpandSelf,
        ContractSelf
    }

    public readonly struct BendTuning
    {
        public readonly float UsefulChargeSeconds;
        public readonly float FullChargeSeconds;
        public readonly float OverloadSeconds;
        public readonly float MaximumChargeHoldSeconds;
        public readonly float ChargeMemorySeconds;
        public readonly float ChargeDecayPerSecond;
        public readonly float PositionGain;
        public readonly float VelocityGain;
        public readonly float MaximumControlForce;
        public readonly float ChargedControlMultiplier;
        public readonly float MinimumReleaseSpeed;
        public readonly float MaximumReleaseSpeed;
        public readonly float GestureVelocityTransfer;

        public BendTuning(
            float usefulChargeSeconds = 0.12f,
            float fullChargeSeconds = 0.9f,
            float overloadSeconds = 1.25f,
            float maximumChargeHoldSeconds = 2f,
            float chargeMemorySeconds = 0.35f,
            float chargeDecayPerSecond = 1.5f,
            float positionGain = 28f,
            float velocityGain = 9f,
            float maximumControlForce = 5400f,
            float chargedControlMultiplier = 1.7f,
            float minimumReleaseSpeed = 0f,
            float maximumReleaseSpeed = 24f,
            float gestureVelocityTransfer = 0.72f)
        {
            UsefulChargeSeconds = math.max(0.01f, usefulChargeSeconds);
            FullChargeSeconds = math.max(UsefulChargeSeconds, fullChargeSeconds);
            OverloadSeconds = math.max(FullChargeSeconds, overloadSeconds);
            MaximumChargeHoldSeconds = math.max(OverloadSeconds, maximumChargeHoldSeconds);
            ChargeMemorySeconds = math.max(0f, chargeMemorySeconds);
            ChargeDecayPerSecond = math.max(0f, chargeDecayPerSecond);
            PositionGain = math.max(0f, positionGain);
            VelocityGain = math.max(0f, velocityGain);
            MaximumControlForce = math.max(0f, maximumControlForce);
            ChargedControlMultiplier = math.max(1f, chargedControlMultiplier);
            MinimumReleaseSpeed = math.max(0f, minimumReleaseSpeed);
            MaximumReleaseSpeed = math.max(MinimumReleaseSpeed, maximumReleaseSpeed);
            GestureVelocityTransfer = math.max(0f, gestureVelocityTransfer);
        }

        public static BendTuning Default => new BendTuning(
            usefulChargeSeconds: 0.12f,
            fullChargeSeconds: 0.9f,
            overloadSeconds: 1.25f,
            maximumChargeHoldSeconds: 2f,
            chargeMemorySeconds: 0.35f,
            chargeDecayPerSecond: 1.5f,
            positionGain: 28f,
            velocityGain: 14f,
            maximumControlForce: 16000f,
            chargedControlMultiplier: 1.7f,
            minimumReleaseSpeed: 0f,
            maximumReleaseSpeed: 24f,
            gestureVelocityTransfer: 0.72f);
    }

    public readonly struct BendForceResult
    {
        public readonly float3 PositionError;
        public readonly float3 VelocityError;
        public readonly float3 AppliedForce;
        public readonly bool WasClamped;

        public BendForceResult(float3 positionError, float3 velocityError, float3 appliedForce, bool wasClamped)
        {
            PositionError = positionError;
            VelocityError = velocityError;
            AppliedForce = appliedForce;
            WasClamped = wasClamped;
        }
    }
}
