using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public enum EarthTransitionFamily : byte
    {
        PhaseSynchronized = 0,
        PoseInertialized = 1,
        ContactAligned = 2,
        ProtectedAction = 3,
        AdditiveOverlay = 4,
        PoseMatchedRecovery = 5,
        FixedDurationFallback = 6
    }

    public enum EarthTransitionGaitPhaseRule : byte
    {
        None = 0,
        PreserveSource = 1,
        OppositeContact = 2,
        FixedTarget = 3,
        ContactAligned = 4
    }

    public enum EarthTransitionContactPolicy : byte
    {
        PreserveCurrentPlants = 0,
        MatchDestinationContacts = 1,
        AuthoredLandingContact = 2,
        ReleaseBeforeBlend = 3,
        IgnoreContacts = 4
    }

    public enum EarthTransitionCancelPolicy : byte
    {
        Always = 0,
        InsideCancelWindow = 1,
        OutsideProtectedWindow = 2,
        Never = 3
    }

    [System.Flags]
    public enum EarthTransitionBodyMask : ushort
    {
        None = 0,
        Root = 1 << 0,
        Pelvis = 1 << 1,
        Spine = 1 << 2,
        Head = 1 << 3,
        LeftArm = 1 << 4,
        RightArm = 1 << 5,
        LeftLeg = 1 << 6,
        RightLeg = 1 << 7,
        FullBody = Root | Pelvis | Spine | Head |
                   LeftArm | RightArm | LeftLeg | RightLeg
    }

    public enum EarthTransitionFootReleasePolicy : byte
    {
        PreservePlanted = 0,
        ReleaseAfterDelay = 1,
        ReleaseOnDestinationContact = 2,
        ReleaseImmediately = 3
    }

    /// <summary>
    /// Wrap-aware normalized animation window. A disabled window never contains
    /// a phase; an enabled window with start greater than end crosses the loop seam.
    /// </summary>
    public readonly struct EarthNormalizedAnimationWindow
    {
        public EarthNormalizedAnimationWindow(bool enabled, float start01, float end01)
        {
            Enabled = enabled;
            Start01 = SanitizePhase(start01);
            End01 = SanitizePhase(end01);
        }

        public bool Enabled { get; }
        public float Start01 { get; }
        public float End01 { get; }

        public bool Contains(float normalizedPhase)
        {
            if (!Enabled) return false;
            float phase = SanitizePhase(normalizedPhase);
            return Start01 <= End01
                ? phase >= Start01 && phase <= End01
                : phase >= Start01 || phase <= End01;
        }

        private static float SanitizePhase(float value) =>
            math.saturate(math.isfinite(value) ? value : 0f);
    }

    /// <summary>
    /// Pure, authored transition-pair behavior. Runtime state and Animator writes
    /// remain owned by EarthTransitionDirector.
    /// </summary>
    public readonly struct EarthTransitionRule
    {
        public EarthTransitionRule(
            bool configured,
            EarthTransitionFamily family,
            EarthAnimationTransitionPriority priority,
            float halfLifeSeconds,
            float fallbackDurationSeconds,
            EarthTransitionGaitPhaseRule gaitPhaseRule,
            EarthTransitionContactPolicy contactPolicy,
            EarthTransitionCancelPolicy cancelPolicy,
            in EarthNormalizedAnimationWindow protectedWindow,
            in EarthNormalizedAnimationWindow cancelWindow,
            float targetPhase01,
            EarthTransitionBodyMask bodyMask,
            EarthTransitionFootReleasePolicy footReleasePolicy,
            float footReleaseSeconds,
            bool queueWhenBlocked)
        {
            Configured = configured;
            Family = IsValid(family) ? family : EarthTransitionFamily.FixedDurationFallback;
            Priority = IsValid(priority) ? priority : EarthAnimationTransitionPriority.Locomotion;
            HalfLifeSeconds = SanitizeDuration(halfLifeSeconds, 0.08f);
            FallbackDurationSeconds = SanitizeDuration(fallbackDurationSeconds, 0.10f);
            GaitPhaseRule = IsValid(gaitPhaseRule)
                ? gaitPhaseRule
                : EarthTransitionGaitPhaseRule.None;
            ContactPolicy = IsValid(contactPolicy)
                ? contactPolicy
                : EarthTransitionContactPolicy.PreserveCurrentPlants;
            CancelPolicy = IsValid(cancelPolicy)
                ? cancelPolicy
                : EarthTransitionCancelPolicy.OutsideProtectedWindow;
            ProtectedWindow = protectedWindow;
            CancelWindow = cancelWindow;
            TargetPhase01 = math.saturate(math.isfinite(targetPhase01) ? targetPhase01 : 0f);
            BodyMask = bodyMask & EarthTransitionBodyMask.FullBody;
            FootReleasePolicy = IsValid(footReleasePolicy)
                ? footReleasePolicy
                : EarthTransitionFootReleasePolicy.PreservePlanted;
            FootReleaseSeconds = math.clamp(
                math.isfinite(footReleaseSeconds) ? footReleaseSeconds : 0f,
                0f,
                0.5f);
            QueueWhenBlocked = queueWhenBlocked;
        }

        public bool Configured { get; }
        public EarthTransitionFamily Family { get; }
        public EarthAnimationTransitionPriority Priority { get; }
        public float HalfLifeSeconds { get; }
        public float FallbackDurationSeconds { get; }
        public EarthTransitionGaitPhaseRule GaitPhaseRule { get; }
        public EarthTransitionContactPolicy ContactPolicy { get; }
        public EarthTransitionCancelPolicy CancelPolicy { get; }
        public EarthNormalizedAnimationWindow ProtectedWindow { get; }
        public EarthNormalizedAnimationWindow CancelWindow { get; }
        public float TargetPhase01 { get; }
        public EarthTransitionBodyMask BodyMask { get; }
        public EarthTransitionFootReleasePolicy FootReleasePolicy { get; }
        public float FootReleaseSeconds { get; }
        public bool QueueWhenBlocked { get; }

        public static EarthTransitionRule FixedFallback(
            EarthAnimationTransitionPriority priority,
            float durationSeconds) =>
            new EarthTransitionRule(
                true,
                EarthTransitionFamily.FixedDurationFallback,
                priority,
                0.08f,
                durationSeconds,
                EarthTransitionGaitPhaseRule.None,
                EarthTransitionContactPolicy.PreserveCurrentPlants,
                EarthTransitionCancelPolicy.OutsideProtectedWindow,
                default,
                default,
                0f,
                EarthTransitionBodyMask.FullBody,
                EarthTransitionFootReleasePolicy.PreservePlanted,
                0f,
                false);

        private static float SanitizeDuration(float value, float fallback) =>
            math.clamp(math.isfinite(value) ? value : fallback, 0.01f, 0.5f);

        private static bool IsValid(EarthTransitionFamily value) =>
            value >= EarthTransitionFamily.PhaseSynchronized &&
            value <= EarthTransitionFamily.FixedDurationFallback;

        private static bool IsValid(EarthAnimationTransitionPriority value) =>
            value == EarthAnimationTransitionPriority.Idle ||
            value == EarthAnimationTransitionPriority.Locomotion ||
            value == EarthAnimationTransitionPriority.LandingContact ||
            value == EarthAnimationTransitionPriority.CommittedAction ||
            value == EarthAnimationTransitionPriority.MediumStagger ||
            value == EarthAnimationTransitionPriority.DefensiveCancel ||
            value == EarthAnimationTransitionPriority.HeavyImpact ||
            value == EarthAnimationTransitionPriority.FullRagdoll;

        private static bool IsValid(EarthTransitionGaitPhaseRule value) =>
            value >= EarthTransitionGaitPhaseRule.None &&
            value <= EarthTransitionGaitPhaseRule.ContactAligned;

        private static bool IsValid(EarthTransitionContactPolicy value) =>
            value >= EarthTransitionContactPolicy.PreserveCurrentPlants &&
            value <= EarthTransitionContactPolicy.IgnoreContacts;

        private static bool IsValid(EarthTransitionCancelPolicy value) =>
            value >= EarthTransitionCancelPolicy.Always &&
            value <= EarthTransitionCancelPolicy.Never;

        private static bool IsValid(EarthTransitionFootReleasePolicy value) =>
            value >= EarthTransitionFootReleasePolicy.PreservePlanted &&
            value <= EarthTransitionFootReleasePolicy.ReleaseImmediately;
    }

    public static class EarthTransitionRulePolicy
    {
        public static EarthAnimationTransitionDecision Resolve(
            in EarthAnimationTransitionContext context,
            in EarthTransitionRule rule)
        {
            if (!rule.Configured || context.DestinationState == EarthMotionStateId.None)
                return Reject(EarthAnimationTransitionReason.InvalidDestination);
            if (context.SourceState == context.DestinationState && !context.ForceRestart)
                return Reject(EarthAnimationTransitionReason.SameState);

            EarthAnimationTransitionReason interruptReason = ResolveInterruptReason(
                in rule,
                context.SourceNormalizedTime,
                context.MayInterruptSource,
                context.ActivePriority);
            if (interruptReason != EarthAnimationTransitionReason.Accepted)
                return Reject(interruptReason);

            float destinationPhase = ResolveDestinationPhase(in context, in rule);
            bool normalizedStart = rule.GaitPhaseRule != EarthTransitionGaitPhaseRule.None &&
                                   rule.GaitPhaseRule != EarthTransitionGaitPhaseRule.ContactAligned;
            float destinationStartSeconds = normalizedStart
                ? destinationPhase * context.DestinationCycleSeconds
                : ResolveDestinationStartSeconds(in context, in rule);
            EarthAnimationTransitionKind kind = ResolveKind(rule.Family);
            bool inertialized = rule.Family == EarthTransitionFamily.PoseInertialized;
            float duration = inertialized
                ? math.clamp(rule.HalfLifeSeconds * 4f, 0.04f, 0.5f)
                : rule.FallbackDurationSeconds;

            return new EarthAnimationTransitionDecision(
                true,
                kind,
                kind == EarthAnimationTransitionKind.FixedDurationFallback
                    ? EarthAnimationTransitionReason.ProfileFallback
                    : EarthAnimationTransitionReason.Accepted,
                duration,
                destinationStartSeconds,
                destinationPhase,
                normalizedStart,
                rule.GaitPhaseRule == EarthTransitionGaitPhaseRule.PreserveSource ||
                rule.GaitPhaseRule == EarthTransitionGaitPhaseRule.OppositeContact,
                inertialized,
                true);
        }

        public static EarthAnimationTransitionReason ResolveInterruptReason(
            in EarthTransitionRule rule,
            float sourceNormalizedTime,
            bool contextMayInterrupt,
            EarthAnimationTransitionPriority activePriority)
        {
            if (!contextMayInterrupt)
                return EarthAnimationTransitionReason.ProtectedSource;
            if (activePriority > rule.Priority)
                return EarthAnimationTransitionReason.LowerPriority;

            bool permitted = rule.CancelPolicy switch
            {
                EarthTransitionCancelPolicy.Always => true,
                EarthTransitionCancelPolicy.InsideCancelWindow =>
                    rule.CancelWindow.Contains(sourceNormalizedTime),
                EarthTransitionCancelPolicy.OutsideProtectedWindow =>
                    !rule.ProtectedWindow.Contains(sourceNormalizedTime),
                EarthTransitionCancelPolicy.Never => false,
                _ => false
            };
            return permitted
                ? EarthAnimationTransitionReason.Accepted
                : EarthAnimationTransitionReason.ProtectedSource;
        }

        public static float ResolveDestinationPhase(
            in EarthAnimationTransitionContext context,
            in EarthTransitionRule rule) =>
            rule.GaitPhaseRule switch
            {
                EarthTransitionGaitPhaseRule.PreserveSource => context.GaitPhase01,
                EarthTransitionGaitPhaseRule.OppositeContact => math.frac(context.GaitPhase01 + 0.5f),
                EarthTransitionGaitPhaseRule.FixedTarget => rule.TargetPhase01,
                _ => 0f
            };

        public static bool ShouldReleaseFeet(
            in EarthTransitionRule rule,
            float elapsedSeconds,
            bool destinationContactReached)
        {
            EarthTransitionFootReleasePolicy policy = ResolveFootReleasePolicy(in rule);
            return policy switch
            {
                EarthTransitionFootReleasePolicy.PreservePlanted => false,
                EarthTransitionFootReleasePolicy.ReleaseAfterDelay =>
                    math.max(0f, math.isfinite(elapsedSeconds) ? elapsedSeconds : 0f) >=
                    rule.FootReleaseSeconds,
                EarthTransitionFootReleasePolicy.ReleaseOnDestinationContact =>
                    destinationContactReached,
                EarthTransitionFootReleasePolicy.ReleaseImmediately => true,
                _ => false
            };
        }

        /// <summary>
        /// Maps semantic contact ownership to the command consumed by the sole
        /// foot-lock owner. Pair-authored release timing remains effective unless
        /// the contact contract requires a stronger release boundary.
        /// </summary>
        public static EarthTransitionFootReleasePolicy ResolveFootReleasePolicy(
            in EarthTransitionRule rule) =>
            rule.ContactPolicy switch
            {
                EarthTransitionContactPolicy.PreserveCurrentPlants =>
                    rule.FootReleasePolicy,
                EarthTransitionContactPolicy.MatchDestinationContacts =>
                    rule.FootReleasePolicy == EarthTransitionFootReleasePolicy.PreservePlanted
                        ? EarthTransitionFootReleasePolicy.ReleaseOnDestinationContact
                        : rule.FootReleasePolicy,
                EarthTransitionContactPolicy.AuthoredLandingContact =>
                    rule.FootReleasePolicy == EarthTransitionFootReleasePolicy.PreservePlanted
                        ? EarthTransitionFootReleasePolicy.ReleaseOnDestinationContact
                        : rule.FootReleasePolicy,
                EarthTransitionContactPolicy.ReleaseBeforeBlend =>
                    EarthTransitionFootReleasePolicy.ReleaseImmediately,
                EarthTransitionContactPolicy.IgnoreContacts =>
                    EarthTransitionFootReleasePolicy.ReleaseImmediately,
                _ => EarthTransitionFootReleasePolicy.PreservePlanted
            };

        private static float ResolveDestinationStartSeconds(
            in EarthAnimationTransitionContext context,
            in EarthTransitionRule rule)
        {
            if (rule.GaitPhaseRule == EarthTransitionGaitPhaseRule.ContactAligned ||
                rule.ContactPolicy == EarthTransitionContactPolicy.AuthoredLandingContact)
            {
                return math.max(
                    0f,
                    context.LandingContactSeconds -
                    (context.HasLandingPrediction ? context.PredictedTimeToContact : 0f));
            }
            return rule.TargetPhase01 * context.DestinationCycleSeconds;
        }

        private static EarthAnimationTransitionKind ResolveKind(EarthTransitionFamily family) =>
            family switch
            {
                EarthTransitionFamily.PhaseSynchronized =>
                    EarthAnimationTransitionKind.PhaseMatchedLocomotion,
                EarthTransitionFamily.PoseInertialized =>
                    EarthAnimationTransitionKind.Inertialized,
                EarthTransitionFamily.ContactAligned =>
                    EarthAnimationTransitionKind.ContactAlignedLanding,
                EarthTransitionFamily.ProtectedAction =>
                    EarthAnimationTransitionKind.ProtectedAuthoredAction,
                EarthTransitionFamily.AdditiveOverlay =>
                    EarthAnimationTransitionKind.AdditiveImpactOverlay,
                EarthTransitionFamily.PoseMatchedRecovery =>
                    EarthAnimationTransitionKind.PoseMatchedRagdollRecovery,
                _ => EarthAnimationTransitionKind.FixedDurationFallback
            };

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
    }
}
