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
            EarthTechniqueId.PillarJump or EarthTechniqueId.WebWave or EarthTechniqueId.FaultLine or
            EarthTechniqueId.Armor or EarthTechniqueId.Repair or
            EarthTechniqueId.WallSlide or EarthTechniqueId.FractureFan;
    }

    /// <summary>
    /// Small upper-body correction layered over the authored Humanoid clip. It is
    /// deliberately expressed as local Euler deltas so it cannot own root motion,
    /// pelvis, legs or foot contact.
    /// </summary>
    public readonly struct EarthChoreographyPoseOffset
    {
        public EarthChoreographyPoseOffset(
            float3 chestEuler,
            float3 headEuler,
            float3 leftShoulderEuler,
            float3 rightShoulderEuler)
        {
            ChestEuler = chestEuler;
            HeadEuler = headEuler;
            LeftShoulderEuler = leftShoulderEuler;
            RightShoulderEuler = rightShoulderEuler;
        }

        public float3 ChestEuler { get; }
        public float3 HeadEuler { get; }
        public float3 LeftShoulderEuler { get; }
        public float3 RightShoulderEuler { get; }
        public bool IsFinite => math.all(math.isfinite(ChestEuler)) && math.all(math.isfinite(HeadEuler)) &&
                                math.all(math.isfinite(LeftShoulderEuler)) &&
                                math.all(math.isfinite(RightShoulderEuler));
        public float MaximumAbsDegrees => math.cmax(math.abs(new float4(
            math.cmax(math.abs(ChestEuler)),
            math.cmax(math.abs(HeadEuler)),
            math.cmax(math.abs(LeftShoulderEuler)),
            math.cmax(math.abs(RightShoulderEuler)))));

        public static EarthChoreographyPoseOffset Lerp(
            in EarthChoreographyPoseOffset from,
            in EarthChoreographyPoseOffset to,
            float amount) => new EarthChoreographyPoseOffset(
                math.lerp(from.ChestEuler, to.ChestEuler, math.saturate(amount)),
                math.lerp(from.HeadEuler, to.HeadEuler, math.saturate(amount)),
                math.lerp(from.LeftShoulderEuler, to.LeftShoulderEuler, math.saturate(amount)),
                math.lerp(from.RightShoulderEuler, to.RightShoulderEuler, math.saturate(amount)));
    }

    /// <summary>
    /// Consumes the six semantic choreography channels written by the presentation
    /// bridge. The authored clip remains the primary silhouette; these bounded
    /// offsets distinguish techniques which currently share or reuse source clips.
    /// </summary>
    public static class EarthChoreographyVisualSolver
    {
        public const float MaximumChestDegrees = 7f;
        public const float MaximumHeadDegrees = 3f;
        public const float MaximumShoulderDegrees = 6f;

        public static EarthChoreographyPoseOffset Solve(
            EarthTechniqueId technique,
            EarthCastPhase phase,
            EarthBendingDialect dialect,
            float effort01,
            float brace01,
            float grounding01,
            float precision01,
            bool leftDominant)
        {
            if (technique == EarthTechniqueId.None || phase == EarthCastPhase.Idle) return default;

            float phaseWeight = PhaseWeight(phase);
            float effort = math.saturate(effort01);
            float brace = math.saturate(brace01);
            float grounding = math.saturate(grounding01);
            float precision = math.saturate(precision01);
            float rooted = dialect == EarthBendingDialect.RootedPower ? 1f : 0f;
            float dominant = leftDominant ? -1f : 1f;
            float4 flavor = Flavor(EarthHumanoidMotionResolver.Resolve(technique));
            float power = math.lerp(0.52f, 1f, effort) * phaseWeight;

            float chestPitch = (flavor.x * power + brace * 1.35f + rooted * grounding * 0.9f) *
                               math.lerp(0.86f, 1.08f, grounding);
            float chestYaw = (flavor.y * dominant * math.lerp(1.08f, 0.76f, grounding) +
                              (1f - rooted) * dominant * 0.4f) * power;
            float chestRoll = (flavor.z * dominant + brace * dominant * 0.42f) * power;
            float3 chest = Clamp(new float3(chestPitch, chestYaw, chestRoll), MaximumChestDegrees);

            // Precision keeps the gaze on the manipulated matter while the torso
            // supplies the technique silhouette. This remains a small counter-pose.
            float headCounter = math.lerp(0.18f, 0.48f, precision);
            float3 head = Clamp(new float3(
                -chest.x * math.lerp(0.14f, 0.28f, precision),
                -chest.y * headCounter,
                -chest.z * 0.22f), MaximumHeadDegrees);

            float shoulderDrive = flavor.w * power;
            float shoulderSpread = (brace * math.lerp(0.65f, 1.25f, rooted) - precision * 0.34f) *
                                   phaseWeight;
            float shoulderLead = dominant * math.lerp(0.8f, 1.8f, effort) * phaseWeight;
            float3 leftShoulder = Clamp(new float3(
                shoulderDrive - shoulderLead * 0.34f,
                -shoulderLead,
                shoulderSpread), MaximumShoulderDegrees);
            float3 rightShoulder = Clamp(new float3(
                shoulderDrive + shoulderLead * 0.34f,
                shoulderLead,
                -shoulderSpread), MaximumShoulderDegrees);
            return new EarthChoreographyPoseOffset(chest, head, leftShoulder, rightShoulder);
        }

        /// <summary>
        /// Removes the presentation-only side lean when the physical action axis is
        /// centered on the character. The authored clip keeps its arm lead, while
        /// the late choreography pass may only yaw/roll the torso toward a real
        /// lateral target. Previously x == 0 selected the right-dominant branch and
        /// added a visible sideways torso drift even for a straight-ahead pointer.
        /// </summary>
        public static EarthChoreographyPoseOffset AlignLateralBodyToAim(
            in EarthChoreographyPoseOffset pose,
            float3 localDirection)
        {
            float3 direction = math.normalizesafe(
                math.select(new float3(0f, 0f, 1f), localDirection, math.isfinite(localDirection)),
                new float3(0f, 0f, 1f));
            float lateral = math.smoothstep(0.08f, 0.45f, math.abs(direction.x));
            float3 chest = pose.ChestEuler;
            float3 head = pose.HeadEuler;
            chest.y *= lateral;
            chest.z *= lateral;
            head.y *= lateral;
            head.z *= lateral;
            return new EarthChoreographyPoseOffset(
                chest,
                head,
                pose.LeftShoulderEuler,
                pose.RightShoulderEuler);
        }

        private static float PhaseWeight(EarthCastPhase phase) => phase switch
        {
            EarthCastPhase.Acquire => 0.28f,
            EarthCastPhase.Root => 0.54f,
            EarthCastPhase.Load => 0.82f,
            EarthCastPhase.Strike => 1f,
            EarthCastPhase.Sustain => 0.74f,
            EarthCastPhase.Recover => 0.32f,
            _ => 0f
        };

        // x/y/z = chest pitch/yaw/roll, w = symmetric shoulder drive.
        private static float4 Flavor(EarthHumanoidPoseSlot slot) => slot switch
        {
            EarthHumanoidPoseSlot.RaiseWall => new float4(3.8f, 0.7f, 0.35f, 2.4f),
            EarthHumanoidPoseSlot.RaisePlatform => new float4(4.9f, 1.15f, -0.3f, 1.8f),
            EarthHumanoidPoseSlot.PullStone => new float4(-1.5f, 4.2f, 1.25f, -0.8f),
            EarthHumanoidPoseSlot.HeavyThrow => new float4(3.2f, 5.8f, 1.6f, 3.4f),
            EarthHumanoidPoseSlot.VectorPush => new float4(2.6f, 4.8f, 0.65f, 2.8f),
            EarthHumanoidPoseSlot.GravityRepair => new float4(1.4f, 2.2f, -0.7f, -1.4f),
            EarthHumanoidPoseSlot.WaveResonance => new float4(4.3f, 0.25f, 1.7f, 3.7f),
            EarthHumanoidPoseSlot.Pillar => new float4(5.6f, 0.5f, -1.5f, 2.1f),
            EarthHumanoidPoseSlot.ArmorAssemble => new float4(0.8f, 0.9f, 2.1f, -2.3f),
            EarthHumanoidPoseSlot.ArmorBarrage => new float4(2.9f, 6.1f, -1.2f, 3.1f),
            EarthHumanoidPoseSlot.GenericCast => new float4(2.1f, 3.1f, 0.85f, 1.1f),
            _ => float4.zero
        };

        private static float3 Clamp(float3 value, float maximum) =>
            math.clamp(math.select(float3.zero, value, math.isfinite(value)), -maximum, maximum);
    }

    /// <summary>
    /// Small deterministic ownership rules shared by presentation and input. An
    /// equipped armor shell is a locomotion modifier, not a permanent cast clip;
    /// a short Space tap is a jump until the pillar charge really begins.
    /// </summary>
    public static class EarthPersistentAnimationPolicy
    {
        public const float MinimumArmorEncumbrance = 0.58f;
        public const float MaximumArmorEncumbrance = 0.82f;
        public const float FullArmorSpeedScale = 0.70f;

        public static bool AllowsSustainedUpperBody(
            EarthActionOwner owner,
            bool pillarCharging)
        {
            return owner switch
            {
                EarthActionOwner.Armor => false,
                EarthActionOwner.Pillar => pillarCharging,
                EarthActionOwner.LandingCushion => true,
                EarthActionOwner.Wave or EarthActionOwner.Resonance or
                    EarthActionOwner.Surf => true,
                _ => false
            };
        }

        public static float ResolveArmorEncumbrance(
            bool armorActive,
            float armorPhase01)
        {
            return armorActive
                ? math.lerp(
                    MinimumArmorEncumbrance,
                    MaximumArmorEncumbrance,
                    math.saturate(armorPhase01))
                : 0f;
        }

        public static float ResolveArmorSpeedScale(float armorEncumbrance01) =>
            math.lerp(1f, FullArmorSpeedScale, math.saturate(armorEncumbrance01));

        public static bool ShouldClearMagicForOrdinaryJump(
            bool supported,
            bool deliberateJump,
            bool alreadyCleared,
            EarthAnimationPhase phase)
        {
            if (supported || !deliberateJump || alreadyCleared) return false;
            return phase is EarthAnimationPhase.Rising or EarthAnimationPhase.Apex or
                EarthAnimationPhase.Falling;
        }
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
    /// Ordinary locomotion still receives contact IK: it follows the current surface
    /// without locking the foot to an old world-space point.
    /// </summary>
    public static class EarthFootPlantMotionGate
    {
        private const float LocomotionIntentThresholdSq = 0.0025f;
        private const float MinimumCastingBrace = 0.12f;
        private const float FullContactSpeed = 0.75f;
        private const float ReducedContactSpeed = 7.5f;

        public static bool HasLocomotionIntent(float2 moveInput) =>
            math.lengthsq(moveInput) > LocomotionIntentThresholdSq;

        public static bool IsLocomoting(float2 moveInput, float tangentSpeed) =>
            HasLocomotionIntent(moveInput) ||
            math.abs(math.isfinite(tangentSpeed) ? tangentSpeed : 0f) > 0.22f;

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

            // This is contact following, not a foot lock. The previous implementation
            // returned zero for all ordinary walking, so imported animation height was
            // never reconciled with the actual planet/platform surface. Keep enough IK
            // at speed to prevent visible hovering while letting the authored gait own
            // most of the swing arc.
            float speed01 = math.saturate(
                (math.max(0f, tangentSpeed) - FullContactSpeed) /
                math.max(0.01f, ReducedContactSpeed - FullContactSpeed));
            float contact = math.lerp(0.82f, 0.42f, speed01);
            if (HasLocomotionIntent(moveInput)) contact *= 0.90f;
            return math.saturate(contact);
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
        // A 30 Hz rendered locomotion step can combine roughly five centimetres
        // of authored leg extension and rising terrain. Keep a fully owned
        // stance planted through that legitimate change while still splitting
        // an exceptional 18-22 cm pose transition across several frames.
        public const float MaximumDownwardFrameStep = 0.05f;

        public static float Solve(float leftFootError, float rightFootError, float compression01, float maximumDrop)
        {
            float supportDrop = math.min(0f, math.min(leftFootError, rightFootError));
            float braceDrop = -math.saturate(compression01) * math.max(0f, maximumDrop);
            return math.clamp(supportDrop + braceDrop, -math.max(0f, maximumDrop), 0f);
        }

        public static float SelectAppliedOffset(
            float current,
            float target,
            float smoothed,
            bool finalContactOwned,
            float baseRiseAlongUp = 0f)
        {
            float safeCurrent = math.isfinite(current) ? current : 0f;
            float safeTarget = math.isfinite(target) ? target : 0f;
            float safeSmoothed = math.isfinite(smoothed) ? smoothed : safeTarget;
            if (safeTarget >= safeCurrent) return safeSmoothed;

            // Reaching full IK weight must not turn a deeper support request
            // into a one-frame body teleport. Prefer the exact target once the
            // contact owns the chain, but bound the rendered downward change;
            // recovery still follows the supplied SmoothDamp result.
            // The offset is relative to Animator.bodyPosition. When the motor and
            // authored pelvis rise over a hump, an equal offset decrease keeps the
            // final world pelvis still; treating that compensation as a downward
            // snap leaves a locked foot outside leg reach for one rendered frame.
            // Only the remaining world-space drop is bounded.
            float safeBaseRise = math.max(0f,
                math.isfinite(baseRiseAlongUp) ? baseRiseAlongUp : 0f);
            float maximumOffsetDrop = MaximumDownwardFrameStep + safeBaseRise;
            float desired = finalContactOwned ? safeTarget : safeSmoothed;
            return math.max(safeTarget, math.max(
                safeCurrent - maximumOffsetDrop,
                desired));
        }
    }
}
