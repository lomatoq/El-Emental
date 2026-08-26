using Unity.Mathematics;

namespace Elemental.Simulation.Combat
{
    public enum EarthMvpBotPhase : byte
    {
        Approach = 0,
        Windup = 1,
        Strike = 2,
        Recover = 3,
        Cooldown = 4,
        Disabled = 5
    }

    /// <summary>
    /// Pure simulation view of the body's ability to act. Runtime combat states map
    /// into this enum at the adapter boundary; they do not leak into the planner.
    /// </summary>
    public enum EarthMvpBotBodyState : byte
    {
        Ready = 0,
        Staggered = 1,
        Ragdolled = 2,
        Recovering = 3,
        Disabled = 4
    }

    public enum EarthMvpBotGuardReason : byte
    {
        None = 0,
        PlannerDisabled = 1,
        BodyUnavailable = 2,
        TargetUnavailable = 3,
        SelfOutsideArena = 4,
        TargetOutsideArena = 5,
        TargetOutOfRange = 6,
        TargetOutOfCone = 7,
        InvalidFrame = 8,
        InvalidTuning = 9,
        InvalidState = 10
    }

    public readonly struct EarthMvpBotTuning
    {
        internal const float MaximumWorldMagnitude = 1_000_000f;

        public EarthMvpBotTuning(
            float attackRange,
            float attackConeDegrees,
            float arenaRadius,
            float windupSeconds,
            float recoverSeconds,
            float cooldownSeconds)
        {
            AttackRange = attackRange;
            AttackConeDegrees = attackConeDegrees;
            ArenaRadius = arenaRadius;
            WindupSeconds = windupSeconds;
            RecoverSeconds = recoverSeconds;
            CooldownSeconds = cooldownSeconds;
        }

        public float AttackRange { get; }
        public float AttackConeDegrees { get; }
        public float ArenaRadius { get; }
        public float WindupSeconds { get; }
        public float RecoverSeconds { get; }
        public float CooldownSeconds { get; }

        public bool IsValid =>
            float.IsFinite(AttackRange) && AttackRange > 0f &&
            AttackRange <= MaximumWorldMagnitude &&
            float.IsFinite(AttackConeDegrees) && AttackConeDegrees > 0f && AttackConeDegrees <= 180f &&
            float.IsFinite(ArenaRadius) && ArenaRadius > AttackRange &&
            ArenaRadius <= MaximumWorldMagnitude &&
            float.IsFinite(WindupSeconds) && WindupSeconds > 0f &&
            float.IsFinite(RecoverSeconds) && RecoverSeconds >= 0f &&
            float.IsFinite(CooldownSeconds) && CooldownSeconds >= 0f;

        public static EarthMvpBotTuning Default => new EarthMvpBotTuning(
            2.25f,
            55f,
            6.5f,
            0.42f,
            0.85f,
            1.45f);
    }

    public readonly struct EarthMvpBotFrame
    {
        public EarthMvpBotFrame(
            float deltaTime,
            float3 selfPosition,
            float3 selfForward,
            float3 targetPosition,
            float3 localUp,
            float3 arenaCenter,
            bool plannerEnabled = true,
            bool targetAvailable = true,
            EarthMvpBotBodyState bodyState = EarthMvpBotBodyState.Ready)
        {
            DeltaTime = deltaTime;
            SelfPosition = selfPosition;
            SelfForward = selfForward;
            TargetPosition = targetPosition;
            LocalUp = localUp;
            ArenaCenter = arenaCenter;
            PlannerEnabled = plannerEnabled;
            TargetAvailable = targetAvailable;
            BodyState = bodyState;
        }

        public float DeltaTime { get; }
        public float3 SelfPosition { get; }
        public float3 SelfForward { get; }
        public float3 TargetPosition { get; }
        public float3 LocalUp { get; }
        public float3 ArenaCenter { get; }
        public bool PlannerEnabled { get; }
        public bool TargetAvailable { get; }
        public EarthMvpBotBodyState BodyState { get; }
    }

    public readonly struct EarthMvpBotPlannerState
    {
        public EarthMvpBotPlannerState(
            EarthMvpBotPhase phase,
            float phaseSeconds,
            float3 lockedStrikeDirection)
        {
            Phase = phase;
            PhaseSeconds = phaseSeconds;
            LockedStrikeDirection = lockedStrikeDirection;
        }

        public EarthMvpBotPhase Phase { get; }
        public float PhaseSeconds { get; }
        public float3 LockedStrikeDirection { get; }

        public static EarthMvpBotPlannerState Initial => new EarthMvpBotPlannerState(
            EarthMvpBotPhase.Approach,
            0f,
            float3.zero);
    }

    public readonly struct EarthMvpBotPlan
    {
        public EarthMvpBotPlan(
            in EarthMvpBotPlannerState state,
            float3 desiredMoveDirection,
            float3 desiredFacingDirection,
            bool strikeThisTick,
            EarthMvpBotGuardReason guardReason)
        {
            State = state;
            DesiredMoveDirection = desiredMoveDirection;
            DesiredFacingDirection = desiredFacingDirection;
            StrikeThisTick = strikeThisTick;
            GuardReason = guardReason;
        }

        public EarthMvpBotPlannerState State { get; }
        public float3 DesiredMoveDirection { get; }
        public float3 DesiredFacingDirection { get; }
        public bool StrikeThisTick { get; }
        public EarthMvpBotGuardReason GuardReason { get; }
    }

    /// <summary>
    /// Deterministic fixed-tick planner for the MVP linebreaker. The caller owns and
    /// feeds back <see cref="EarthMvpBotPlannerState"/>; the strike flag is a one-step
    /// command pulse and never remains latched in planner state.
    /// </summary>
    public static class EarthMvpBotPlanner
    {
        private const float DirectionEpsilonSq = 0.000001f;

        public static EarthMvpBotPlan Step(
            in EarthMvpBotPlannerState state,
            in EarthMvpBotFrame frame,
            in EarthMvpBotTuning tuning)
        {
            if (!tuning.IsValid)
                return Disabled(EarthMvpBotGuardReason.InvalidTuning);
            if (!IsFinite(in frame) || frame.DeltaTime <= 0f)
                return Disabled(EarthMvpBotGuardReason.InvalidFrame);
            if (!IsValid(in state))
                return Disabled(EarthMvpBotGuardReason.InvalidState);
            if (!frame.PlannerEnabled)
                return Disabled(EarthMvpBotGuardReason.PlannerDisabled);
            if (frame.BodyState != EarthMvpBotBodyState.Ready)
                return Disabled(EarthMvpBotGuardReason.BodyUnavailable);

            float3 up = math.normalizesafe(frame.LocalUp, new float3(0f, 1f, 0f));
            float3 forward = TangentDirection(frame.SelfForward, up, float3.zero);
            float3 selfArenaOffset = frame.SelfPosition - frame.ArenaCenter;
            if (math.lengthsq(selfArenaOffset) > tuning.ArenaRadius * tuning.ArenaRadius)
            {
                float3 inward = TangentDirection(-selfArenaOffset, up, forward);
                return ApproachPlan(inward, inward, EarthMvpBotGuardReason.SelfOutsideArena);
            }

            if (!frame.TargetAvailable)
                return Disabled(EarthMvpBotGuardReason.TargetUnavailable);

            float3 targetArenaOffset = frame.TargetPosition - frame.ArenaCenter;
            if (math.lengthsq(targetArenaOffset) > tuning.ArenaRadius * tuning.ArenaRadius)
                return ApproachPlan(float3.zero, forward, EarthMvpBotGuardReason.TargetOutsideArena);

            float3 targetWorldOffset = frame.TargetPosition - frame.SelfPosition;
            float targetDistanceSq = math.lengthsq(targetWorldOffset);
            float3 targetTangentOffset = ProjectOnPlane(targetWorldOffset, up);
            if (math.lengthsq(targetTangentOffset) <= DirectionEpsilonSq)
                return ApproachPlan(float3.zero, forward, EarthMvpBotGuardReason.TargetUnavailable);
            float3 targetDirection = math.normalize(targetTangentOffset);
            forward = TangentDirection(frame.SelfForward, up, targetDirection);

            switch (state.Phase)
            {
                case EarthMvpBotPhase.Approach:
                case EarthMvpBotPhase.Disabled:
                    return StepApproach(targetDirection, forward, targetDistanceSq, in tuning);

                case EarthMvpBotPhase.Windup:
                    return StepWindup(in state, in frame, in tuning, up, targetDirection, targetDistanceSq);

                case EarthMvpBotPhase.Strike:
                    return Hold(
                        EarthMvpBotPhase.Recover,
                        0f,
                        TangentDirection(state.LockedStrikeDirection, up, forward));

                case EarthMvpBotPhase.Recover:
                    return StepTimedHold(
                        in state,
                        frame.DeltaTime,
                        tuning.RecoverSeconds,
                        EarthMvpBotPhase.Cooldown,
                        up,
                        forward);

                case EarthMvpBotPhase.Cooldown:
                {
                    EarthMvpBotPlan cooldown = StepTimedHold(
                        in state,
                        frame.DeltaTime,
                        tuning.CooldownSeconds,
                        EarthMvpBotPhase.Approach,
                        up,
                        targetDirection);
                    if (cooldown.State.Phase != EarthMvpBotPhase.Approach)
                        return cooldown;
                    return ApproachPlan(float3.zero, targetDirection, EarthMvpBotGuardReason.None);
                }

                default:
                    return Disabled(EarthMvpBotGuardReason.InvalidState);
            }
        }

        private static EarthMvpBotPlan StepApproach(
            float3 targetDirection,
            float3 forward,
            float targetDistanceSq,
            in EarthMvpBotTuning tuning)
        {
            if (targetDistanceSq > tuning.AttackRange * tuning.AttackRange)
                return ApproachPlan(targetDirection, targetDirection, EarthMvpBotGuardReason.TargetOutOfRange);

            float minimumDot = math.cos(math.radians(tuning.AttackConeDegrees * 0.5f));
            if (math.dot(forward, targetDirection) < minimumDot)
                return ApproachPlan(float3.zero, targetDirection, EarthMvpBotGuardReason.TargetOutOfCone);

            var windup = new EarthMvpBotPlannerState(
                EarthMvpBotPhase.Windup,
                0f,
                targetDirection);
            return new EarthMvpBotPlan(
                in windup,
                float3.zero,
                targetDirection,
                false,
                EarthMvpBotGuardReason.None);
        }

        private static EarthMvpBotPlan StepWindup(
            in EarthMvpBotPlannerState state,
            in EarthMvpBotFrame frame,
            in EarthMvpBotTuning tuning,
            float3 up,
            float3 targetDirection,
            float targetDistanceSq)
        {
            float3 locked = TangentDirection(state.LockedStrikeDirection, up, targetDirection);
            if (!AdvanceTimer(state.PhaseSeconds, frame.DeltaTime, tuning.WindupSeconds, out float elapsed))
                return Hold(EarthMvpBotPhase.Windup, elapsed, locked);

            if (targetDistanceSq > tuning.AttackRange * tuning.AttackRange)
                return ApproachPlan(targetDirection, targetDirection, EarthMvpBotGuardReason.TargetOutOfRange);

            float minimumDot = math.cos(math.radians(tuning.AttackConeDegrees * 0.5f));
            if (math.dot(locked, targetDirection) < minimumDot)
                return ApproachPlan(float3.zero, targetDirection, EarthMvpBotGuardReason.TargetOutOfCone);

            var strike = new EarthMvpBotPlannerState(EarthMvpBotPhase.Strike, 0f, locked);
            return new EarthMvpBotPlan(
                in strike,
                float3.zero,
                locked,
                true,
                EarthMvpBotGuardReason.None);
        }

        private static EarthMvpBotPlan StepTimedHold(
            in EarthMvpBotPlannerState state,
            float deltaTime,
            float duration,
            EarthMvpBotPhase nextPhase,
            float3 up,
            float3 facingFallback)
        {
            float3 locked = TangentDirection(state.LockedStrikeDirection, up, facingFallback);
            bool complete = AdvanceTimer(state.PhaseSeconds, deltaTime, duration, out float elapsed);
            return Hold(complete ? nextPhase : state.Phase, complete ? 0f : elapsed, locked);
        }

        private static EarthMvpBotPlan Hold(EarthMvpBotPhase phase, float elapsed, float3 locked)
        {
            var next = new EarthMvpBotPlannerState(phase, elapsed, locked);
            return new EarthMvpBotPlan(
                in next,
                float3.zero,
                locked,
                false,
                EarthMvpBotGuardReason.None);
        }

        private static EarthMvpBotPlan ApproachPlan(
            float3 move,
            float3 facing,
            EarthMvpBotGuardReason reason)
        {
            var next = EarthMvpBotPlannerState.Initial;
            return new EarthMvpBotPlan(in next, move, facing, false, reason);
        }

        private static EarthMvpBotPlan Disabled(EarthMvpBotGuardReason reason)
        {
            var disabled = new EarthMvpBotPlannerState(
                EarthMvpBotPhase.Disabled,
                0f,
                float3.zero);
            return new EarthMvpBotPlan(
                in disabled,
                float3.zero,
                float3.zero,
                false,
                reason);
        }

        private static bool AdvanceTimer(float elapsed, float deltaTime, float duration, out float next)
        {
            if (elapsed >= duration || deltaTime >= duration - elapsed)
            {
                next = duration;
                return true;
            }

            next = elapsed + deltaTime;
            return false;
        }

        private static float3 TangentDirection(float3 direction, float3 up, float3 fallback)
        {
            float3 tangent = ProjectOnPlane(direction, up);
            if (math.lengthsq(tangent) > DirectionEpsilonSq)
                return math.normalize(tangent);

            tangent = ProjectOnPlane(fallback, up);
            if (math.lengthsq(tangent) > DirectionEpsilonSq)
                return math.normalize(tangent);

            float3 axis = math.abs(up.y) < 0.9f
                ? new float3(0f, 1f, 0f)
                : new float3(1f, 0f, 0f);
            return math.normalizesafe(math.cross(up, axis), new float3(0f, 0f, 1f));
        }

        private static float3 ProjectOnPlane(float3 value, float3 normal) =>
            value - normal * math.dot(value, normal);

        private static bool IsFinite(in EarthMvpBotFrame frame) =>
            float.IsFinite(frame.DeltaTime) &&
            IsFiniteAndBounded(frame.SelfPosition) &&
            IsFiniteAndBounded(frame.SelfForward) &&
            IsFiniteAndBounded(frame.TargetPosition) &&
            IsFiniteAndBounded(frame.LocalUp) &&
            IsFiniteAndBounded(frame.ArenaCenter);

        private static bool IsValid(in EarthMvpBotPlannerState state)
        {
            if ((byte)state.Phase > (byte)EarthMvpBotPhase.Disabled ||
                !float.IsFinite(state.PhaseSeconds) || state.PhaseSeconds < 0f ||
                !IsFiniteAndBounded(state.LockedStrikeDirection))
                return false;

            if (state.Phase is EarthMvpBotPhase.Windup or EarthMvpBotPhase.Strike or
                EarthMvpBotPhase.Recover or EarthMvpBotPhase.Cooldown)
                return math.lengthsq(state.LockedStrikeDirection) > DirectionEpsilonSq;

            return true;
        }

        private static bool IsFiniteAndBounded(float3 value) =>
            math.all(math.isfinite(value)) &&
            math.cmax(math.abs(value)) <= EarthMvpBotTuning.MaximumWorldMagnitude;
    }
}
