using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public enum EarthMotionStateId : byte
    {
        None = 0,
        Locomotion = 1,
        TurnInPlace = 2,
        Jump = 3,
        Fall = 4,
        SoftLanding = 5,
        MovingLanding = 6,
        HardLanding = 7,
        Surf = 8,
        DirectionalDodge = 9,
        KnockdownRecovery = 10,
        ImpactOverlay = 11
    }

    public enum EarthMotionCategory : byte
    {
        None = 0,
        Idle = 1,
        Locomotion = 2,
        Turn = 3,
        Airborne = 4,
        Landing = 5,
        AuthoredAction = 6,
        Impact = 7,
        RagdollRecovery = 8,
        Surf = 9
    }

    public enum EarthAnimationTransitionKind : byte
    {
        None = 0,
        PhaseMatchedLocomotion = 1,
        ContactAlignedLanding = 2,
        ProtectedAuthoredAction = 3,
        ResponsiveTakeoff = 4,
        AdditiveImpactOverlay = 5,
        PoseMatchedRagdollRecovery = 6,
        Inertialized = 7,
        FixedDurationFallback = 8
    }

    public enum EarthAnimationTransitionReason : byte
    {
        Accepted = 0,
        SameState = 1,
        ProtectedSource = 2,
        LowerPriority = 3,
        InvalidDestination = 4,
        LegacyFallback = 5
    }

    public enum EarthAnimationTransitionPriority : byte
    {
        Idle = 0,
        Locomotion = 10,
        LandingContact = 30,
        CommittedAction = 40,
        MediumStagger = 50,
        DefensiveCancel = 60,
        HeavyImpact = 70,
        FullRagdoll = 100
    }

    public readonly struct EarthAnimationTransitionTuning
    {
        public EarthAnimationTransitionTuning(
            float locomotionSeconds,
            float turnSeconds,
            float takeoffSeconds,
            float airborneSeconds,
            float landingSeconds,
            float actionSeconds,
            float recoverySeconds,
            float surfSeconds,
            float fallbackSeconds,
            bool legacyMode,
            bool inertializationEnabled)
        {
            LocomotionSeconds = Sanitize(locomotionSeconds, 0.14f);
            TurnSeconds = Sanitize(turnSeconds, 0.12f);
            TakeoffSeconds = Sanitize(takeoffSeconds, 0.06f);
            AirborneSeconds = Sanitize(airborneSeconds, 0.10f);
            LandingSeconds = Sanitize(landingSeconds, 0.07f);
            ActionSeconds = Sanitize(actionSeconds, 0.12f);
            RecoverySeconds = Sanitize(recoverySeconds, 0.16f);
            SurfSeconds = Sanitize(surfSeconds, 0.12f);
            FallbackSeconds = Sanitize(fallbackSeconds, 0.08f);
            LegacyMode = legacyMode;
            InertializationEnabled = inertializationEnabled;
        }

        public float LocomotionSeconds { get; }
        public float TurnSeconds { get; }
        public float TakeoffSeconds { get; }
        public float AirborneSeconds { get; }
        public float LandingSeconds { get; }
        public float ActionSeconds { get; }
        public float RecoverySeconds { get; }
        public float SurfSeconds { get; }
        public float FallbackSeconds { get; }
        public bool LegacyMode { get; }
        public bool InertializationEnabled { get; }

        public static EarthAnimationTransitionTuning Default =>
            new EarthAnimationTransitionTuning(
                0.14f, 0.12f, 0.06f, 0.10f, 0.07f,
                0.12f, 0.16f, 0.12f, 0.08f, false, false);

        private static float Sanitize(float value, float fallback) =>
            math.clamp(math.isfinite(value) ? value : fallback, 0.01f, 0.5f);
    }

    public readonly struct EarthAnimationTransitionContext
    {
        public EarthAnimationTransitionContext(
            EarthMotionStateId sourceState,
            EarthMotionStateId destinationState,
            EarthMotionCategory sourceCategory,
            EarthMotionCategory destinationCategory,
            EarthAnimationTransitionPriority requestPriority,
            EarthAnimationTransitionPriority activePriority,
            float sourceNormalizedTime,
            float gaitPhase01,
            float destinationCycleSeconds,
            float landingContactSeconds,
            float predictedTimeToContact,
            bool hasLandingPrediction,
            bool mayInterruptSource,
            bool forceRestart,
            bool requestInertialization)
        {
            SourceState = sourceState;
            DestinationState = destinationState;
            SourceCategory = sourceCategory;
            DestinationCategory = destinationCategory;
            RequestPriority = requestPriority;
            ActivePriority = activePriority;
            SourceNormalizedTime = Sanitize01(sourceNormalizedTime);
            GaitPhase01 = math.frac(math.max(0f, math.isfinite(gaitPhase01) ? gaitPhase01 : 0f));
            DestinationCycleSeconds = math.max(
                0.01f,
                math.isfinite(destinationCycleSeconds) ? destinationCycleSeconds : 1f);
            LandingContactSeconds = math.max(
                0f,
                math.isfinite(landingContactSeconds) ? landingContactSeconds : 0f);
            PredictedTimeToContact = math.max(
                0f,
                math.isfinite(predictedTimeToContact) ? predictedTimeToContact : 0f);
            HasLandingPrediction = hasLandingPrediction && math.isfinite(predictedTimeToContact);
            MayInterruptSource = mayInterruptSource;
            ForceRestart = forceRestart;
            RequestInertialization = requestInertialization;
        }

        public EarthMotionStateId SourceState { get; }
        public EarthMotionStateId DestinationState { get; }
        public EarthMotionCategory SourceCategory { get; }
        public EarthMotionCategory DestinationCategory { get; }
        public EarthAnimationTransitionPriority RequestPriority { get; }
        public EarthAnimationTransitionPriority ActivePriority { get; }
        public float SourceNormalizedTime { get; }
        public float GaitPhase01 { get; }
        public float DestinationCycleSeconds { get; }
        public float LandingContactSeconds { get; }
        public float PredictedTimeToContact { get; }
        public bool HasLandingPrediction { get; }
        public bool MayInterruptSource { get; }
        public bool ForceRestart { get; }
        public bool RequestInertialization { get; }

        private static float Sanitize01(float value) =>
            math.saturate(math.isfinite(value) ? value : 0f);
    }

    public readonly struct EarthAnimationTransitionDecision
    {
        public EarthAnimationTransitionDecision(
            bool shouldTransition,
            EarthAnimationTransitionKind kind,
            EarthAnimationTransitionReason reason,
            float durationSeconds,
            float destinationStartSeconds,
            float destinationNormalizedTime,
            bool useNormalizedStart,
            bool preserveGaitPhase,
            bool requestsInertialization,
            bool mayInterruptSource)
        {
            ShouldTransition = shouldTransition;
            Kind = kind;
            Reason = reason;
            DurationSeconds = math.clamp(
                math.isfinite(durationSeconds) ? durationSeconds : 0.08f,
                0f,
                0.5f);
            DestinationStartSeconds = math.max(
                0f,
                math.isfinite(destinationStartSeconds) ? destinationStartSeconds : 0f);
            DestinationNormalizedTime = math.saturate(
                math.isfinite(destinationNormalizedTime) ? destinationNormalizedTime : 0f);
            UseNormalizedStart = useNormalizedStart;
            PreserveGaitPhase = preserveGaitPhase;
            RequestsInertialization = requestsInertialization;
            MayInterruptSource = mayInterruptSource;
        }

        public bool ShouldTransition { get; }
        public EarthAnimationTransitionKind Kind { get; }
        public EarthAnimationTransitionReason Reason { get; }
        public float DurationSeconds { get; }
        public float DestinationStartSeconds { get; }
        public float DestinationNormalizedTime { get; }
        public bool UseNormalizedStart { get; }
        public bool PreserveGaitPhase { get; }
        public bool RequestsInertialization { get; }
        public bool MayInterruptSource { get; }
    }

    public static class EarthAnimationTransitionPolicy
    {
        public static EarthAnimationTransitionDecision Resolve(
            in EarthAnimationTransitionContext context,
            in EarthAnimationTransitionTuning tuning)
        {
            if (context.DestinationState == EarthMotionStateId.None)
                return Reject(EarthAnimationTransitionReason.InvalidDestination);
            if (context.SourceState == context.DestinationState && !context.ForceRestart)
                return Reject(EarthAnimationTransitionReason.SameState);
            if (!context.MayInterruptSource)
                return Reject(EarthAnimationTransitionReason.ProtectedSource);
            if (context.ActivePriority > context.RequestPriority)
                return Reject(EarthAnimationTransitionReason.LowerPriority);

            if (tuning.LegacyMode)
            {
                return Accept(
                    EarthAnimationTransitionKind.FixedDurationFallback,
                    EarthAnimationTransitionReason.LegacyFallback,
                    tuning.FallbackSeconds,
                    0f,
                    0f,
                    false,
                    false,
                    false);
            }

            bool inertia = tuning.InertializationEnabled &&
                           context.RequestInertialization &&
                           IsSafeInertializedPair(in context);
            if (inertia)
            {
                float duration = context.DestinationCategory == EarthMotionCategory.Locomotion
                    ? tuning.LocomotionSeconds
                    : tuning.ActionSeconds;
                return Accept(
                    EarthAnimationTransitionKind.Inertialized,
                    EarthAnimationTransitionReason.Accepted,
                    duration,
                    0f,
                    context.GaitPhase01,
                    context.DestinationCategory == EarthMotionCategory.Locomotion,
                    context.DestinationCategory == EarthMotionCategory.Locomotion,
                    true);
            }

            if (context.DestinationCategory == EarthMotionCategory.Landing)
            {
                float start = math.max(
                    0f,
                    context.LandingContactSeconds -
                    (context.HasLandingPrediction ? context.PredictedTimeToContact : 0f));
                return Accept(
                    EarthAnimationTransitionKind.ContactAlignedLanding,
                    EarthAnimationTransitionReason.Accepted,
                    tuning.LandingSeconds,
                    start,
                    0f,
                    false,
                    false,
                    false);
            }

            if (context.DestinationCategory == EarthMotionCategory.Locomotion)
            {
                return Accept(
                    EarthAnimationTransitionKind.PhaseMatchedLocomotion,
                    EarthAnimationTransitionReason.Accepted,
                    tuning.LocomotionSeconds,
                    context.GaitPhase01 * context.DestinationCycleSeconds,
                    context.GaitPhase01,
                    false,
                    true,
                    false);
            }

            if (context.DestinationCategory == EarthMotionCategory.RagdollRecovery)
            {
                return Accept(
                    EarthAnimationTransitionKind.PoseMatchedRagdollRecovery,
                    EarthAnimationTransitionReason.Accepted,
                    tuning.RecoverySeconds,
                    0f,
                    0f,
                    false,
                    false,
                    false);
            }

            if (context.DestinationCategory == EarthMotionCategory.Impact)
            {
                return Accept(
                    EarthAnimationTransitionKind.AdditiveImpactOverlay,
                    EarthAnimationTransitionReason.Accepted,
                    tuning.ActionSeconds,
                    0f,
                    0f,
                    false,
                    false,
                    false);
            }

            if (context.DestinationCategory == EarthMotionCategory.AuthoredAction)
            {
                return Accept(
                    EarthAnimationTransitionKind.ProtectedAuthoredAction,
                    EarthAnimationTransitionReason.Accepted,
                    tuning.ActionSeconds,
                    0f,
                    0f,
                    false,
                    false,
                    false);
            }

            if (context.DestinationState == EarthMotionStateId.Jump)
            {
                return Accept(
                    EarthAnimationTransitionKind.ResponsiveTakeoff,
                    EarthAnimationTransitionReason.Accepted,
                    tuning.TakeoffSeconds,
                    0f,
                    0f,
                    false,
                    false,
                    false);
            }

            float fallbackDuration = context.DestinationCategory switch
            {
                EarthMotionCategory.Turn => tuning.TurnSeconds,
                EarthMotionCategory.Airborne => tuning.AirborneSeconds,
                EarthMotionCategory.Surf => tuning.SurfSeconds,
                _ => tuning.FallbackSeconds
            };
            return Accept(
                EarthAnimationTransitionKind.FixedDurationFallback,
                EarthAnimationTransitionReason.Accepted,
                fallbackDuration,
                0f,
                0f,
                false,
                false,
                false);
        }

        private static bool IsSafeInertializedPair(
            in EarthAnimationTransitionContext context) =>
            context.DestinationCategory == EarthMotionCategory.Locomotion ||
            context.DestinationCategory == EarthMotionCategory.Impact ||
            context.SourceCategory == EarthMotionCategory.RagdollRecovery;

        private static EarthAnimationTransitionDecision Reject(
            EarthAnimationTransitionReason reason) =>
            new EarthAnimationTransitionDecision(
                false,
                EarthAnimationTransitionKind.None,
                reason,
                0f,
                0f,
                0f,
                false,
                false,
                false,
                false);

        private static EarthAnimationTransitionDecision Accept(
            EarthAnimationTransitionKind kind,
            EarthAnimationTransitionReason reason,
            float duration,
            float startSeconds,
            float normalizedTime,
            bool useNormalizedStart,
            bool preserveGait,
            bool inertia) =>
            new EarthAnimationTransitionDecision(
                true,
                kind,
                reason,
                duration,
                startSeconds,
                normalizedTime,
                useNormalizedStart,
                preserveGait,
                inertia,
                true);
    }
}
