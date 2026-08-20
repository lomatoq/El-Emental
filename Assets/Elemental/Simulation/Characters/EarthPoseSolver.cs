using System;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Matter;
using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public enum EarthCastPhase : byte
    {
        Idle = 0,
        Acquire = 1,
        Root = 2,
        Load = 3,
        Strike = 4,
        Sustain = 5,
        Recover = 6
    }

    public enum EarthPoseFamily : byte
    {
        None = 0,
        Grip = 1,
        Structure = 2,
        Push = 3,
        Stomp = 4,
        Wave = 5,
        Repair = 6
    }

    public enum EarthBendingDialect : byte
    {
        CompactTactile = 0,
        RootedPower = 1
    }

    /// <summary>
    /// Canonical presentation request. Gameplay describes physical intent; the
    /// choreography layer remains free to choose clips, rig offsets and pose holds.
    /// </summary>
    public readonly struct BendingPoseRequest
    {
        public BendingPoseRequest(
            EarthTechniqueId technique,
            EarthCastPhase phase,
            float3 actionAxis,
            float3 localUp,
            float controlledMass,
            float effort01,
            float grounding01,
            float precision01,
            bool leftDominant,
            EarthMatterId focusMatter)
        {
            Technique = technique;
            Phase = phase;
            LocalUp = math.normalizesafe(localUp, new float3(0f, 1f, 0f));
            ActionAxis = math.normalizesafe(
                actionAxis - LocalUp * math.dot(actionAxis, LocalUp),
                new float3(0f, 0f, 1f));
            ControlledMass = math.max(0f, controlledMass);
            Effort01 = math.saturate(effort01);
            Grounding01 = math.saturate(grounding01);
            Precision01 = math.saturate(precision01);
            LeftDominant = leftDominant;
            FocusMatter = focusMatter;
        }

        public EarthTechniqueId Technique { get; }
        public EarthCastPhase Phase { get; }
        public float3 ActionAxis { get; }
        public float3 LocalUp { get; }
        public float ControlledMass { get; }
        public float Effort01 { get; }
        public float Grounding01 { get; }
        public float Precision01 { get; }
        public bool LeftDominant { get; }
        public EarthMatterId FocusMatter { get; }
        public bool IsActive => Technique != EarthTechniqueId.None && Phase != EarthCastPhase.Idle;
    }

    public readonly struct EarthChoreographySample
    {
        public EarthChoreographySample(
            EarthBendingDialect dialect,
            float pelvisCompression01,
            float stanceWidth01,
            float upperBodyWeight01,
            float poseHoldSeconds)
        {
            Dialect = dialect;
            PelvisCompression01 = math.saturate(pelvisCompression01);
            StanceWidth01 = math.saturate(stanceWidth01);
            UpperBodyWeight01 = math.saturate(upperBodyWeight01);
            PoseHoldSeconds = math.clamp(poseHoldSeconds, 0f, 0.08f);
        }

        public EarthBendingDialect Dialect { get; }
        public float PelvisCompression01 { get; }
        public float StanceWidth01 { get; }
        public float UpperBodyWeight01 { get; }
        public float PoseHoldSeconds { get; }
    }

    public static class EarthChoreographySolver
    {
        public static EarthChoreographySample Solve(in BendingPoseRequest request)
        {
            EarthBendingDialect dialect = IsRooted(request.Technique)
                ? EarthBendingDialect.RootedPower
                : EarthBendingDialect.CompactTactile;
            float rooted = dialect == EarthBendingDialect.RootedPower ? 1f : 0.42f;
            float commit = request.Phase == EarthCastPhase.Strike ? 1f : 0f;
            float mass01 = 1f - math.exp(-request.ControlledMass / 220f);
            float load = math.saturate(request.Effort01 * 0.58f + mass01 * 0.42f);
            return new EarthChoreographySample(
                dialect,
                request.Grounding01 * rooted * load,
                request.Grounding01 * math.lerp(0.28f, 1f, rooted) * load,
                math.saturate(math.lerp(0.72f, 1f, request.Precision01) * request.Effort01),
                commit * math.lerp(0.025f, 0.078f, load));
        }

        private static bool IsRooted(EarthTechniqueId technique) => technique is
            EarthTechniqueId.RaiseWall or EarthTechniqueId.RaisePlatform or
            EarthTechniqueId.PillarJump or EarthTechniqueId.WebWave or
            EarthTechniqueId.Armor or EarthTechniqueId.Repair or
            EarthTechniqueId.WallSlide or EarthTechniqueId.FractureFan;
    }

    [Serializable]
    public readonly struct EarthCastTiming
    {
        public EarthCastTiming(ushort startupTicks, ushort activeTicks, ushort recoveryTicks, float contactPhase01)
        {
            StartupTicks = startupTicks;
            ActiveTicks = (ushort)math.max(1, (int)activeTicks);
            RecoveryTicks = recoveryTicks;
            ContactPhase01 = math.saturate(contactPhase01);
        }

        public ushort StartupTicks { get; }
        public ushort ActiveTicks { get; }
        public ushort RecoveryTicks { get; }
        public float ContactPhase01 { get; }
        public uint TotalTicks => (uint)StartupTicks + ActiveTicks + RecoveryTicks;
        public uint ContactTick => StartupTicks + (uint)math.round((ActiveTicks - 1) * ContactPhase01);
    }

    public readonly struct EarthPoseIntent
    {
        public EarthPoseIntent(
            EarthPoseFamily family,
            EarthCastPhase phase,
            float3 localDirection,
            float3 targetPosition,
            float effort01,
            float brace01,
            float stanceWidth01,
            float pelvisCompression01,
            float upperBodyTwist01)
        {
            Family = family;
            Phase = phase;
            LocalDirection = math.normalizesafe(localDirection, new float3(0f, 0f, 1f));
            TargetPosition = math.select(float3.zero, targetPosition, math.isfinite(targetPosition));
            Effort01 = math.saturate(effort01);
            Brace01 = math.saturate(brace01);
            StanceWidth01 = math.saturate(stanceWidth01);
            PelvisCompression01 = math.saturate(pelvisCompression01);
            UpperBodyTwist01 = math.clamp(upperBodyTwist01, -1f, 1f);
        }

        public EarthPoseFamily Family { get; }
        public EarthCastPhase Phase { get; }
        public float3 LocalDirection { get; }
        public float3 TargetPosition { get; }
        public float Effort01 { get; }
        public float Brace01 { get; }
        public float StanceWidth01 { get; }
        public float PelvisCompression01 { get; }
        public float UpperBodyTwist01 { get; }
        public bool LocksFeet => Phase == EarthCastPhase.Root || Phase == EarthCastPhase.Load ||
                                 Phase == EarthCastPhase.Strike || Phase == EarthCastPhase.Sustain;
    }

    public static class EarthCastPhaseSolver
    {
        public static EarthCastPhase Evaluate(uint elapsedTicks, in EarthCastTiming timing, bool sustained)
        {
            if (elapsedTicks < timing.StartupTicks)
            {
                float startup01 = timing.StartupTicks > 0 ? elapsedTicks / (float)timing.StartupTicks : 1f;
                if (startup01 < 0.22f) return EarthCastPhase.Acquire;
                if (startup01 < 0.55f) return EarthCastPhase.Root;
                return EarthCastPhase.Load;
            }

            uint activeElapsed = elapsedTicks - timing.StartupTicks;
            if (activeElapsed < timing.ActiveTicks)
                return activeElapsed <= math.max(1u, timing.ContactTick - timing.StartupTicks)
                    ? EarthCastPhase.Strike
                    : EarthCastPhase.Sustain;
            if (sustained) return EarthCastPhase.Sustain;
            return elapsedTicks < timing.TotalTicks ? EarthCastPhase.Recover : EarthCastPhase.Idle;
        }
    }

    public static class EarthPoseSolver
    {
        public static EarthPoseIntent Solve(
            EarthTechniqueKind technique,
            EarthCastPhase phase,
            float3 localDirection,
            float3 targetPosition,
            float controlledMass,
            float acceleration,
            float charge01,
            bool supported,
            float authoredEffort = 0.7f,
            float authoredBrace = 0.65f)
        {
            float massEffort = 1f - math.exp(-math.max(0f, controlledMass) / 190f);
            float accelerationEffort = 1f - math.exp(-math.max(0f, acceleration) / 18f);
            float effort = math.saturate(
                (massEffort * 0.42f) + (accelerationEffort * 0.18f) +
                (math.saturate(charge01) * 0.25f) + (math.saturate(authoredEffort) * 0.3f));
            if (!supported) effort *= 0.58f;
            float phaseLoad = phase switch
            {
                EarthCastPhase.Acquire => 0.28f,
                EarthCastPhase.Root => 0.62f,
                EarthCastPhase.Load => 0.88f,
                EarthCastPhase.Strike => 1f,
                EarthCastPhase.Sustain => 0.82f,
                EarthCastPhase.Recover => 0.38f,
                _ => 0f
            };
            float brace = supported
                ? math.saturate(authoredBrace * effort * phaseLoad)
                : 0f;
            float directionSide = math.clamp(localDirection.x, -1f, 1f);
            return new EarthPoseIntent(
                Family(technique),
                phase,
                localDirection,
                targetPosition,
                effort * phaseLoad,
                brace,
                math.saturate(brace * 1.15f),
                math.saturate(brace * (technique == EarthTechniqueKind.Pillar ? 1f : 0.72f)),
                directionSide * effort * (technique == EarthTechniqueKind.Repair ? 0.35f : 0.72f));
        }

        public static EarthPoseFamily Family(EarthTechniqueKind technique) => technique switch
        {
            EarthTechniqueKind.Grip => EarthPoseFamily.Grip,
            EarthTechniqueKind.Wall => EarthPoseFamily.Structure,
            EarthTechniqueKind.Platform => EarthPoseFamily.Structure,
            EarthTechniqueKind.Pillar => EarthPoseFamily.Stomp,
            EarthTechniqueKind.GroundWave => EarthPoseFamily.Wave,
            EarthTechniqueKind.Repair => EarthPoseFamily.Repair,
            _ => EarthPoseFamily.None
        };
    }

    public readonly struct PlanetJumpWindowState
    {
        public PlanetJumpWindowState(ushort coyoteTicks, ushort bufferTicks)
        {
            CoyoteTicks = coyoteTicks;
            BufferTicks = bufferTicks;
        }

        public ushort CoyoteTicks { get; }
        public ushort BufferTicks { get; }
        public bool CanConsume => CoyoteTicks > 0 && BufferTicks > 0;

        public PlanetJumpWindowState Step(bool grounded, bool jumpPressed, ushort coyoteWindow, ushort bufferWindow)
        {
            ushort coyote = grounded ? coyoteWindow : (ushort)math.max(0, CoyoteTicks - 1);
            ushort buffer = jumpPressed ? bufferWindow : (ushort)math.max(0, BufferTicks - 1);
            return new PlanetJumpWindowState(coyote, buffer);
        }

        public PlanetJumpWindowState Consume() => new PlanetJumpWindowState(0, 0);
    }

    public readonly struct EarthFootPlantResult
    {
        public EarthFootPlantResult(float3 position, float3 normal, float weight01, bool locked)
        {
            Position = position;
            Normal = math.normalizesafe(normal, new float3(0f, 1f, 0f));
            Weight01 = math.saturate(weight01);
            Locked = locked;
        }

        public float3 Position { get; }
        public float3 Normal { get; }
        public float Weight01 { get; }
        public bool Locked { get; }
    }

    /// <summary>
    /// Keeps authored casting braces from pinning a locomoting character to the
    /// previous frame's foot contacts. Input intent is used in addition to body
    /// velocity so the lock releases before the root has had time to accelerate.
    /// </summary>
    public static class EarthFootPlantMotionGate
    {
        private const float LocomotionIntentThresholdSq = 0.0025f;
        private const float MinimumCastingBrace = 0.12f;

        public static bool HasLocomotionIntent(float2 moveInput) =>
            math.lengthsq(moveInput) > LocomotionIntentThresholdSq;

        public static bool ShouldLock(
            bool supported,
            bool surfActive,
            bool poseRequestsLock,
            float brace01,
            float tangentSpeed,
            float2 moveInput)
        {
            if (!supported) return false;
            if (surfActive) return true;
            if (HasLocomotionIntent(moveInput)) return false;
            return poseRequestsLock && brace01 >= MinimumCastingBrace;
        }

        public static float TargetContactWeight(
            bool supported,
            bool surfActive,
            bool locked,
            float tangentSpeed,
            float2 moveInput)
        {
            if (!supported) return 0f;
            if (surfActive || locked) return 1f;
            return 0f;
        }
    }

    public static class EarthFootPlantSolver
    {
        public static EarthFootPlantResult SolveContact(
            float3 animatedPosition,
            bool hasGround,
            float3 groundPoint,
            float3 groundNormal,
            float3 localUp,
            bool grounded,
            float soleOffset)
        {
            if (!grounded || !hasGround)
                return new EarthFootPlantResult(animatedPosition, localUp, 0f, false);
            float3 normal = math.normalizesafe(groundNormal, localUp);
            return new EarthFootPlantResult(
                groundPoint + normal * math.max(0f, soleOffset),
                normal,
                1f,
                false);
        }

        public static EarthFootPlantResult Solve(
            float3 animatedPosition,
            bool hasGround,
            float3 groundPoint,
            float3 groundNormal,
            float3 localUp,
            bool grounded,
            bool requestLock,
            bool wasLocked,
            float3 previousLockPosition,
            float soleOffset)
        {
            if (!grounded || !hasGround)
                return new EarthFootPlantResult(animatedPosition, localUp, 0f, false);
            if (!requestLock)
                return new EarthFootPlantResult(animatedPosition, groundNormal, 0f, false);
            float3 planted = groundPoint + math.normalizesafe(groundNormal, localUp) * math.max(0f, soleOffset);
            bool locked = requestLock;
            if (requestLock && wasLocked) planted = previousLockPosition;
            return new EarthFootPlantResult(planted, groundNormal, 1f, locked);
        }
    }

    public static class EarthPelvisCompensation
    {
        public static float Solve(float leftFootError, float rightFootError, float compression01, float maximumDrop)
        {
            float supportDrop = math.min(0f, math.min(leftFootError, rightFootError));
            float braceDrop = -math.saturate(compression01) * math.max(0f, maximumDrop);
            return math.clamp(supportDrop + braceDrop, -math.max(0f, maximumDrop), 0f);
        }
    }
}
