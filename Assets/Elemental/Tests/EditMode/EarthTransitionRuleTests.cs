using Elemental.Simulation.Characters;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthTransitionRuleTests
    {
        [Test]
        public void PoseInertializedRuleUsesAuthoredHalfLifeAndOppositeContact()
        {
            EarthTransitionRule rule = Rule(
                EarthTransitionFamily.PoseInertialized,
                gaitRule: EarthTransitionGaitPhaseRule.OppositeContact,
                halfLife: 0.06f);
            EarthAnimationTransitionContext context = Context(
                gaitPhase: 0.8f,
                requestInertialization: true);

            EarthAnimationTransitionDecision decision =
                EarthTransitionRulePolicy.Resolve(in context, in rule);

            Assert.That(decision.ShouldTransition, Is.True);
            Assert.That(decision.Kind, Is.EqualTo(EarthAnimationTransitionKind.Inertialized));
            Assert.That(decision.RequestsInertialization, Is.True);
            Assert.That(decision.DurationSeconds, Is.EqualTo(0.24f).Within(0.0001f));
            Assert.That(decision.DestinationNormalizedTime, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(decision.DestinationStartSeconds, Is.EqualTo(0.36f).Within(0.0001f));
        }

        [Test]
        public void ContactAlignedRuleUsesLandingMarkerAndPrediction()
        {
            EarthTransitionRule rule = Rule(
                EarthTransitionFamily.ContactAligned,
                gaitRule: EarthTransitionGaitPhaseRule.ContactAligned,
                contactPolicy: EarthTransitionContactPolicy.AuthoredLandingContact);
            EarthAnimationTransitionContext context = Context(
                destinationState: EarthMotionStateId.HardLanding,
                destinationCategory: EarthMotionCategory.Landing,
                landingContact: 0.62f,
                predictedContact: 0.14f,
                hasPrediction: true);

            EarthAnimationTransitionDecision decision =
                EarthTransitionRulePolicy.Resolve(in context, in rule);

            Assert.That(decision.Kind, Is.EqualTo(EarthAnimationTransitionKind.ContactAlignedLanding));
            Assert.That(decision.DestinationStartSeconds, Is.EqualTo(0.48f).Within(0.0001f));
            Assert.That(decision.UseNormalizedStart, Is.False);
        }

        [Test]
        public void ProtectedAndCancelWindowsAreWrapAwareAndDeterministic()
        {
            EarthNormalizedAnimationWindow protectedWindow =
                new EarthNormalizedAnimationWindow(true, 0.8f, 0.2f);
            EarthNormalizedAnimationWindow cancelWindow =
                new EarthNormalizedAnimationWindow(true, 0.35f, 0.55f);
            EarthTransitionRule protectedRule = Rule(
                EarthTransitionFamily.ProtectedAction,
                cancelPolicy: EarthTransitionCancelPolicy.OutsideProtectedWindow,
                protectedWindow: protectedWindow);
            EarthTransitionRule cancelRule = Rule(
                EarthTransitionFamily.ProtectedAction,
                cancelPolicy: EarthTransitionCancelPolicy.InsideCancelWindow,
                cancelWindow: cancelWindow);

            Assert.That(protectedWindow.Contains(0.9f), Is.True);
            Assert.That(protectedWindow.Contains(0.1f), Is.True);
            Assert.That(protectedWindow.Contains(0.5f), Is.False);
            Assert.That(Reason(in protectedRule, 0.9f),
                Is.EqualTo(EarthAnimationTransitionReason.ProtectedSource));
            Assert.That(Reason(in protectedRule, 0.5f),
                Is.EqualTo(EarthAnimationTransitionReason.Accepted));
            Assert.That(Reason(in cancelRule, 0.34f),
                Is.EqualTo(EarthAnimationTransitionReason.ProtectedSource));
            Assert.That(Reason(in cancelRule, 0.35f),
                Is.EqualTo(EarthAnimationTransitionReason.Accepted));
            Assert.That(Reason(in cancelRule, 0.55f),
                Is.EqualTo(EarthAnimationTransitionReason.Accepted));
        }

        [Test]
        public void PriorityAndContextProtectionCannotBeBypassedByPairRule()
        {
            EarthTransitionRule rule = Rule(
                EarthTransitionFamily.PoseInertialized,
                priority: EarthAnimationTransitionPriority.Locomotion,
                cancelPolicy: EarthTransitionCancelPolicy.Always);

            Assert.That(
                EarthTransitionRulePolicy.ResolveInterruptReason(
                    in rule,
                    0.5f,
                    false,
                    EarthAnimationTransitionPriority.Idle),
                Is.EqualTo(EarthAnimationTransitionReason.ProtectedSource));
            Assert.That(
                EarthTransitionRulePolicy.ResolveInterruptReason(
                    in rule,
                    0.5f,
                    true,
                    EarthAnimationTransitionPriority.HeavyImpact),
                Is.EqualTo(EarthAnimationTransitionReason.LowerPriority));
        }

        [Test]
        public void FootReleasePolicyPreservesPlantsUntilAuthoredCondition()
        {
            EarthTransitionRule preserve = Rule(
                EarthTransitionFamily.PhaseSynchronized,
                footRelease: EarthTransitionFootReleasePolicy.PreservePlanted);
            EarthTransitionRule delayed = Rule(
                EarthTransitionFamily.PhaseSynchronized,
                footRelease: EarthTransitionFootReleasePolicy.ReleaseAfterDelay,
                footReleaseSeconds: 0.08f);
            EarthTransitionRule contact = Rule(
                EarthTransitionFamily.ContactAligned,
                footRelease: EarthTransitionFootReleasePolicy.ReleaseOnDestinationContact);

            Assert.That(EarthTransitionRulePolicy.ShouldReleaseFeet(in preserve, 1f, true), Is.False);
            Assert.That(EarthTransitionRulePolicy.ShouldReleaseFeet(in delayed, 0.079f, false), Is.False);
            Assert.That(EarthTransitionRulePolicy.ShouldReleaseFeet(in delayed, 0.08f, false), Is.True);
            Assert.That(EarthTransitionRulePolicy.ShouldReleaseFeet(in contact, 1f, false), Is.False);
            Assert.That(EarthTransitionRulePolicy.ShouldReleaseFeet(in contact, 0f, true), Is.True);
        }

        [Test]
        public void ContactPolicyMapsExplicitlyToSoleFootLockOwner()
        {
            EarthTransitionRule preserve = Rule(
                EarthTransitionFamily.PhaseSynchronized,
                contactPolicy: EarthTransitionContactPolicy.PreserveCurrentPlants,
                footRelease: EarthTransitionFootReleasePolicy.ReleaseAfterDelay);
            EarthTransitionRule match = Rule(
                EarthTransitionFamily.ContactAligned,
                contactPolicy: EarthTransitionContactPolicy.MatchDestinationContacts);
            EarthTransitionRule authored = Rule(
                EarthTransitionFamily.ContactAligned,
                contactPolicy: EarthTransitionContactPolicy.AuthoredLandingContact);
            EarthTransitionRule authoredOverride = Rule(
                EarthTransitionFamily.ContactAligned,
                contactPolicy: EarthTransitionContactPolicy.AuthoredLandingContact,
                footRelease: EarthTransitionFootReleasePolicy.ReleaseAfterDelay);
            EarthTransitionRule preRelease = Rule(
                EarthTransitionFamily.PoseInertialized,
                contactPolicy: EarthTransitionContactPolicy.ReleaseBeforeBlend);
            EarthTransitionRule ignore = Rule(
                EarthTransitionFamily.AdditiveOverlay,
                contactPolicy: EarthTransitionContactPolicy.IgnoreContacts);

            Assert.That(
                EarthTransitionRulePolicy.ResolveFootReleasePolicy(in preserve),
                Is.EqualTo(EarthTransitionFootReleasePolicy.ReleaseAfterDelay));
            Assert.That(
                EarthTransitionRulePolicy.ResolveFootReleasePolicy(in match),
                Is.EqualTo(EarthTransitionFootReleasePolicy.ReleaseOnDestinationContact));
            Assert.That(
                EarthTransitionRulePolicy.ResolveFootReleasePolicy(in authored),
                Is.EqualTo(EarthTransitionFootReleasePolicy.ReleaseOnDestinationContact));
            Assert.That(
                EarthTransitionRulePolicy.ResolveFootReleasePolicy(in authoredOverride),
                Is.EqualTo(EarthTransitionFootReleasePolicy.ReleaseAfterDelay));
            Assert.That(
                EarthTransitionRulePolicy.ResolveFootReleasePolicy(in preRelease),
                Is.EqualTo(EarthTransitionFootReleasePolicy.ReleaseImmediately));
            Assert.That(
                EarthTransitionRulePolicy.ResolveFootReleasePolicy(in ignore),
                Is.EqualTo(EarthTransitionFootReleasePolicy.ReleaseImmediately));
            Assert.That(
                EarthTransitionRulePolicy.ShouldReleaseFeet(in match, 0f, false),
                Is.False);
            Assert.That(
                EarthTransitionRulePolicy.ShouldReleaseFeet(in match, 0f, true),
                Is.True);
            Assert.That(
                EarthTransitionRulePolicy.ShouldReleaseFeet(in ignore, 0f, false),
                Is.True);
        }

        [Test]
        public void MalformedRuleProducesFiniteBoundedFallback()
        {
            EarthNormalizedAnimationWindow window =
                new EarthNormalizedAnimationWindow(true, float.NaN, float.PositiveInfinity);
            EarthTransitionRule rule = new EarthTransitionRule(
                true,
                (EarthTransitionFamily)255,
                (EarthAnimationTransitionPriority)255,
                float.NaN,
                float.PositiveInfinity,
                (EarthTransitionGaitPhaseRule)255,
                (EarthTransitionContactPolicy)255,
                (EarthTransitionCancelPolicy)255,
                in window,
                in window,
                float.NaN,
                (EarthTransitionBodyMask)ushort.MaxValue,
                (EarthTransitionFootReleasePolicy)255,
                float.NaN,
                true);

            Assert.That(rule.Family, Is.EqualTo(EarthTransitionFamily.FixedDurationFallback));
            Assert.That(rule.Priority, Is.EqualTo(EarthAnimationTransitionPriority.Locomotion));
            Assert.That(float.IsFinite(rule.HalfLifeSeconds), Is.True);
            Assert.That(float.IsFinite(rule.FallbackDurationSeconds), Is.True);
            Assert.That(
                rule.BodyMask & ~EarthTransitionBodyMask.FullBody,
                Is.EqualTo(EarthTransitionBodyMask.None));
        }

        private static EarthAnimationTransitionReason Reason(
            in EarthTransitionRule rule,
            float phase) =>
            EarthTransitionRulePolicy.ResolveInterruptReason(
                in rule,
                phase,
                true,
                EarthAnimationTransitionPriority.Idle);

        internal static EarthTransitionRule Rule(
            EarthTransitionFamily family,
            EarthAnimationTransitionPriority priority = EarthAnimationTransitionPriority.LandingContact,
            EarthTransitionGaitPhaseRule gaitRule = EarthTransitionGaitPhaseRule.None,
            EarthTransitionContactPolicy contactPolicy = EarthTransitionContactPolicy.PreserveCurrentPlants,
            EarthTransitionCancelPolicy cancelPolicy = EarthTransitionCancelPolicy.Always,
            EarthNormalizedAnimationWindow protectedWindow = default,
            EarthNormalizedAnimationWindow cancelWindow = default,
            EarthTransitionFootReleasePolicy footRelease = EarthTransitionFootReleasePolicy.PreservePlanted,
            float footReleaseSeconds = 0f,
            float halfLife = 0.08f,
            float fallbackDuration = 0.1f,
            bool queueWhenBlocked = true) =>
            new EarthTransitionRule(
                true,
                family,
                priority,
                halfLife,
                fallbackDuration,
                gaitRule,
                contactPolicy,
                cancelPolicy,
                in protectedWindow,
                in cancelWindow,
                0f,
                EarthTransitionBodyMask.FullBody,
                footRelease,
                footReleaseSeconds,
                queueWhenBlocked);

        internal static EarthAnimationTransitionContext Context(
            EarthMotionStateId sourceState = EarthMotionStateId.TurnInPlace,
            EarthMotionStateId destinationState = EarthMotionStateId.Locomotion,
            EarthMotionCategory sourceCategory = EarthMotionCategory.Turn,
            EarthMotionCategory destinationCategory = EarthMotionCategory.Locomotion,
            float sourcePhase = 0.5f,
            float gaitPhase = 0.25f,
            float cycleSeconds = 1.2f,
            float landingContact = 0.6f,
            float predictedContact = 0.1f,
            bool hasPrediction = false,
            bool requestInertialization = false) =>
            new EarthAnimationTransitionContext(
                sourceState,
                destinationState,
                sourceCategory,
                destinationCategory,
                EarthAnimationTransitionPriority.LandingContact,
                EarthAnimationTransitionPriority.Idle,
                sourcePhase,
                gaitPhase,
                cycleSeconds,
                landingContact,
                predictedContact,
                hasPrediction,
                true,
                false,
                requestInertialization);
    }
}
