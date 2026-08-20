using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public enum PlanetMotionState : byte
    {
        GroundedStable = 0,
        GroundedEdge = 1,
        SupportedMoving = 2,
        CastingMobile = 3,
        CastingBraced = 4,
        JumpStarting = 5,
        AirborneRising = 6,
        AirborneFalling = 7,
        Landing = 8,
        PillarRiding = 9,
        SurfRiding = 10,
        Staggered = 11,
        FullRagdoll = 12,
        Recovering = 13,
        BlockedOrDepenetrating = 14
    }

    [Flags]
    public enum MotionFaultKind : ushort
    {
        None = 0,
        NonFinitePose = 1 << 0,
        NonFiniteVelocity = 1 << 1,
        ExcessiveLinearVelocity = 1 << 2,
        ExcessiveAngularVelocity = 1 << 3,
        GroundedWithoutContact = 1 << 4,
        InvalidSupportFrame = 1 << 5,
        SupportDiscontinuity = 1 << 6,
        SupportGenerationMismatch = 1 << 7,
        PenetrationDebt = 1 << 8,
        StuckLocomotion = 1 << 9,
        RecoveryTimeout = 1 << 10
    }

    public readonly struct PlanetMotionFrame
    {
        public PlanetMotionFrame(
            uint tick,
            PlanetMotionState state,
            float3 position,
            quaternion rotation,
            float3 linearVelocity,
            float3 angularVelocity,
            float2 moveInput,
            bool jumpPressed,
            bool grounded,
            byte contactCount,
            in SupportFrameSnapshot support)
        {
            Tick = tick;
            State = state;
            Position = position;
            Rotation = rotation;
            LinearVelocity = linearVelocity;
            AngularVelocity = angularVelocity;
            MoveInput = moveInput;
            JumpPressed = jumpPressed;
            Grounded = grounded;
            ContactCount = contactCount;
            Support = support;
        }

        public uint Tick { get; }
        public PlanetMotionState State { get; }
        public float3 Position { get; }
        public quaternion Rotation { get; }
        public float3 LinearVelocity { get; }
        public float3 AngularVelocity { get; }
        public float2 MoveInput { get; }
        public bool JumpPressed { get; }
        public bool Grounded { get; }
        public byte ContactCount { get; }
        public SupportFrameSnapshot Support { get; }
    }

    public readonly struct MotionFaultEvent
    {
        public MotionFaultEvent(in PlanetMotionFrame frame, MotionFaultKind faults, uint profileHash, uint seed)
        {
            Frame = frame;
            Faults = faults;
            ProfileHash = profileHash;
            Seed = seed;
        }

        public PlanetMotionFrame Frame { get; }
        public MotionFaultKind Faults { get; }
        public uint ProfileHash { get; }
        public uint Seed { get; }
    }

    public static class PlanetMotionIntegritySolver
    {
        public static PlanetMotionState ResolveState(
            bool grounded,
            bool nearEdge,
            bool hasMovingSupport,
            bool jumpStarting,
            float verticalSpeed,
            float castBrace01,
            CharacterPhysicalMode physicalMode,
            bool surfRiding,
            bool pillarRiding)
        {
            if (physicalMode == CharacterPhysicalMode.FullRagdoll) return PlanetMotionState.FullRagdoll;
            if (physicalMode == CharacterPhysicalMode.Recovery) return PlanetMotionState.Recovering;
            if (physicalMode == CharacterPhysicalMode.Stagger) return PlanetMotionState.Staggered;
            if (surfRiding) return PlanetMotionState.SurfRiding;
            if (pillarRiding) return PlanetMotionState.PillarRiding;
            if (jumpStarting) return PlanetMotionState.JumpStarting;
            if (hasMovingSupport) return PlanetMotionState.SupportedMoving;
            if (grounded)
            {
                if (castBrace01 >= 0.7f) return PlanetMotionState.CastingBraced;
                if (castBrace01 > 0.001f) return PlanetMotionState.CastingMobile;
                return nearEdge ? PlanetMotionState.GroundedEdge : PlanetMotionState.GroundedStable;
            }
            return verticalSpeed > 0.05f
                ? PlanetMotionState.AirborneRising
                : PlanetMotionState.AirborneFalling;
        }

        public static MotionFaultKind Evaluate(
            in PlanetMotionFrame frame,
            float maximumLinearSpeed,
            float maximumAngularSpeed)
        {
            MotionFaultKind faults = MotionFaultKind.None;
            if (!math.all(math.isfinite(frame.Position)) || !math.all(math.isfinite(frame.Rotation.value)))
                faults |= MotionFaultKind.NonFinitePose;
            if (!math.all(math.isfinite(frame.LinearVelocity)) || !math.all(math.isfinite(frame.AngularVelocity)))
                faults |= MotionFaultKind.NonFiniteVelocity;
            if (math.lengthsq(frame.LinearVelocity) > maximumLinearSpeed * maximumLinearSpeed)
                faults |= MotionFaultKind.ExcessiveLinearVelocity;
            if (math.lengthsq(frame.AngularVelocity) > maximumAngularSpeed * maximumAngularSpeed)
                faults |= MotionFaultKind.ExcessiveAngularVelocity;
            if (frame.Grounded && frame.ContactCount == 0 && !frame.Support.IsValid)
                faults |= MotionFaultKind.GroundedWithoutContact;
            if (frame.Support.SurfaceId != 0u && !frame.Support.IsValid)
                faults |= MotionFaultKind.InvalidSupportFrame;
            return faults;
        }
    }
}
