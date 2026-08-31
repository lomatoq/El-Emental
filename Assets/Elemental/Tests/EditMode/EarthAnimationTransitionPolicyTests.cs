using Elemental.Simulation.Characters;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthAnimationTransitionPolicyTests
    {
        [Test]
        public void SameStateRequestIsNoOpUnlessForced()
        {
            EarthAnimationTransitionContext context = Context(
                EarthMotionStateId.Locomotion,
                EarthMotionStateId.Locomotion,
                EarthMotionCategory.Locomotion,
                EarthMotionCategory.Locomotion);
            EarthAnimationTransitionDecision decision = Resolve(in context);
            Assert.That(decision.ShouldTransition, Is.False);
            Assert.That(decision.Reason, Is.EqualTo(EarthAnimationTransitionReason.SameState));
        }

        [Test]
        public void LocomotionEntryPreservesGaitPhaseInDestinationSeconds()
        {
            EarthAnimationTransitionContext context = Context(
                EarthMotionStateId.TurnInPlace,
                EarthMotionStateId.Locomotion,
                EarthMotionCategory.Turn,
                EarthMotionCategory.Locomotion,
                gaitPhase: 0.625f,
                cycleSeconds: 1.2f);
            EarthAnimationTransitionDecision decision = Resolve(in context);
            Assert.That(decision.Kind, Is.EqualTo(EarthAnimationTransitionKind.PhaseMatchedLocomotion));
            Assert.That(decision.PreserveGaitPhase, Is.True);
            Assert.That(decision.DestinationStartSeconds, Is.EqualTo(0.75f).Within(0.0001f));
        }

        [Test]
        public void LandingStartsSoContactFrameMatchesPrediction()
        {
            EarthAnimationTransitionContext context = Context(
                EarthMotionStateId.Fall,
                EarthMotionStateId.HardLanding,
                EarthMotionCategory.Airborne,
                EarthMotionCategory.Landing,
                landingContact: 0.625f,
                predictedContact: 0.145f,
                hasPrediction: true);
            EarthAnimationTransitionDecision decision = Resolve(in context);
            Assert.That(decision.Kind, Is.EqualTo(EarthAnimationTransitionKind.ContactAlignedLanding));
            Assert.That(decision.DestinationStartSeconds, Is.EqualTo(0.48f).Within(0.0001f));
        }

        [TestCase(EarthMotionStateId.Jump, EarthMotionCategory.Airborne,
            EarthAnimationTransitionKind.ResponsiveTakeoff)]
        [TestCase(EarthMotionStateId.SoftLanding, EarthMotionCategory.Landing,
            EarthAnimationTransitionKind.ContactAlignedLanding)]
        [TestCase(EarthMotionStateId.KnockdownRecovery, EarthMotionCategory.RagdollRecovery,
            EarthAnimationTransitionKind.PoseMatchedRagdollRecovery)]
        [TestCase(EarthMotionStateId.DirectionalDodge, EarthMotionCategory.AuthoredAction,
            EarthAnimationTransitionKind.ProtectedAuthoredAction)]
        public void SelectsTransitionFamily(
            EarthMotionStateId destination,
            EarthMotionCategory destinationCategory,
            EarthAnimationTransitionKind expected)
        {
            EarthAnimationTransitionContext context = Context(
                EarthMotionStateId.Fall,
                destination,
                EarthMotionCategory.Airborne,
                destinationCategory);
            Assert.That(Resolve(in context).Kind, Is.EqualTo(expected));
        }

        [Test]
        public void ProtectedSourceAndLowerPriorityRejectInterruption()
        {
            EarthAnimationTransitionContext protectedContext = Context(
                EarthMotionStateId.DirectionalDodge,
                EarthMotionStateId.Locomotion,
                EarthMotionCategory.AuthoredAction,
                EarthMotionCategory.Locomotion,
                mayInterrupt: false);
            Assert.That(Resolve(in protectedContext).Reason,
                Is.EqualTo(EarthAnimationTransitionReason.ProtectedSource));

            EarthAnimationTransitionContext priorityContext = Context(
                EarthMotionStateId.DirectionalDodge,
                EarthMotionStateId.Locomotion,
                EarthMotionCategory.AuthoredAction,
                EarthMotionCategory.Locomotion,
                activePriority: EarthAnimationTransitionPriority.DefensiveCancel,
                requestPriority: EarthAnimationTransitionPriority.Locomotion);
            Assert.That(Resolve(in priorityContext).Reason,
                Is.EqualTo(EarthAnimationTransitionReason.LowerPriority));
        }

        [Test]
        public void MalformedInputProducesFiniteDeterministicDecision()
        {
            EarthAnimationTransitionContext first = Context(
                EarthMotionStateId.Fall,
                EarthMotionStateId.SoftLanding,
                EarthMotionCategory.Airborne,
                EarthMotionCategory.Landing,
                gaitPhase: float.NaN,
                cycleSeconds: float.PositiveInfinity,
                landingContact: float.NaN,
                predictedContact: float.NegativeInfinity,
                hasPrediction: true);
            EarthAnimationTransitionContext second = first;
            EarthAnimationTransitionDecision a = Resolve(in first);
            EarthAnimationTransitionDecision b = Resolve(in second);
            Assert.That(float.IsFinite(a.DurationSeconds), Is.True);
            Assert.That(float.IsFinite(a.DestinationStartSeconds), Is.True);
            Assert.That(a.Kind, Is.EqualTo(b.Kind));
            Assert.That(a.DestinationStartSeconds, Is.EqualTo(b.DestinationStartSeconds));
        }

        private static EarthAnimationTransitionDecision Resolve(
            in EarthAnimationTransitionContext context)
        {
            EarthAnimationTransitionTuning tuning = EarthAnimationTransitionTuning.Default;
            return EarthAnimationTransitionPolicy.Resolve(in context, in tuning);
        }

        private static EarthAnimationTransitionContext Context(
            EarthMotionStateId source,
            EarthMotionStateId destination,
            EarthMotionCategory sourceCategory,
            EarthMotionCategory destinationCategory,
            float gaitPhase = 0.25f,
            float cycleSeconds = 1f,
            float landingContact = 0.6f,
            float predictedContact = 0.1f,
            bool hasPrediction = false,
            bool mayInterrupt = true,
            EarthAnimationTransitionPriority activePriority = EarthAnimationTransitionPriority.Idle,
            EarthAnimationTransitionPriority requestPriority = EarthAnimationTransitionPriority.LandingContact) =>
            new EarthAnimationTransitionContext(
                source,
                destination,
                sourceCategory,
                destinationCategory,
                requestPriority,
                activePriority,
                0.5f,
                gaitPhase,
                cycleSeconds,
                landingContact,
                predictedContact,
                hasPrediction,
                mayInterrupt,
                false,
                false);
    }
}
