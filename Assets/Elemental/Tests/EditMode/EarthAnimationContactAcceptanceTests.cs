using Elemental.Simulation.Characters;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthAnimationContactAcceptanceTests
    {
        [TestCase(.9f, true, EarthFootContactReason.Stance, -.001f)]
        [TestCase(.9166666f, true, EarthFootContactReason.Stance, -.02f)]
        [TestCase(.9166666f, true, EarthFootContactReason.Capture, -.001f)]
        [TestCase(.9166666f, false, EarthFootContactReason.Stance, -.02f)]
        [TestCase(.92999995f, false, EarthFootContactReason.Stance, -.02f)]
        [TestCase(.9f, false, EarthFootContactReason.Stance, -.001f)]
        [TestCase(.93f, false, EarthFootContactReason.Swing, -.001f)]
        [TestCase(1f, true, EarthFootContactReason.Stance, -.02f)]
        public void PlantedReachDoesNotLagBehindTheContactRamp(
            float weight, bool locked, EarthFootContactReason reason, float expected)
        {
            bool owns = EarthPelvisCompensation.OwnsReach(weight, locked, reason);
            Assert.That(EarthPelvisCompensation.SelectAppliedOffset(0f, -.02f, -.001f, owns),
                Is.EqualTo(expected).Within(.000001f));
            Assert.That(EarthPelvisCompensation.SelectAppliedOffset(0f, -.2f, -.001f, owns),
                Is.GreaterThanOrEqualTo(-EarthPelvisCompensation.MaximumDownwardFrameStep));
        }

        [Test] public void UnlockedStanceRepeatTraceReceivesReachWithinExistingWorldDropBound()
        {
            // Repeat01/frame3636: unlocked stance .93 still submits .98028 IK.
            // Prior offset ~-.04; target -.12039; the old smooth value -.06172
            // left the final ankle26.7mm above its submitted floor target.
            bool owns=EarthPelvisCompensation.OwnsReach(.92999995f,false,EarthFootContactReason.Stance);
            float offset=EarthPelvisCompensation.SelectAppliedOffset(-.04f,-.12038862f,-.061723985f,owns);
            Assert.That(offset,Is.EqualTo(-.09f).Within(.000001f));
            Assert.That(offset,Is.GreaterThanOrEqualTo(-.04f-EarthPelvisCompensation.MaximumDownwardFrameStep));
        }

        [TestCase(0.050f, 1f / 30f, 0.025f)]
        [TestCase(0.025f, 1f / 60f, 0.025f)]
        [TestCase(0.0125f, 1f / 120f, 0.025f)]
        public void MotionStepsNormalizeToTheSame60HzGate(
            float measured,
            float deltaTime,
            float expected)
        {
            Assert.That(
                EarthAnimationContactAcceptance.NormalizeTo60Hz(measured, deltaTime),
                Is.EqualTo(expected).Within(0.00001f));
        }

        [Test]
        public void ContactGapGateIncludesOnlyTheDeclaredSoleBand()
        {
            Assert.That(EarthAnimationContactAcceptance.IsPlantedGapAccepted(-0.010f), Is.True);
            Assert.That(EarthAnimationContactAcceptance.IsPlantedGapAccepted(0.015f), Is.True);
            Assert.That(EarthAnimationContactAcceptance.IsPlantedGapAccepted(-0.011f), Is.False);
            Assert.That(EarthAnimationContactAcceptance.IsPlantedGapAccepted(0.016f), Is.False);
        }

        [Test]
        public void CrossFpsGateRejectsMoreThanTenPercentWithoutDividingByZero()
        {
            Assert.That(EarthAnimationContactAcceptance.IsCrossFpsAccepted(0.010f, 0.011f), Is.True);
            Assert.That(EarthAnimationContactAcceptance.IsCrossFpsAccepted(0.010f, 0.0112f), Is.False);
            Assert.That(EarthAnimationContactAcceptance.IsCrossFpsAccepted(0f, 0f), Is.True);
        }

        [Test]
        public void ExplicitJumpTransitionDoesNotHideOrdinaryLocomotionDiscontinuity()
        {
            Assert.That(EarthAnimationContactAcceptance.IsUnallowedDiscontinuity(
                0.090f, 1f, 1f, 0f, true), Is.False);
            Assert.That(EarthAnimationContactAcceptance.IsUnallowedDiscontinuity(
                0.090f, 1f, 1f, 0f, false), Is.True);
        }

        [Test]
        public void ReleasedSwingFootCannotRetainAPlantedIkWeight()
        {
            Assert.That(EarthFootIkWeightBlend.EnforceSwingMaximum(
                0.58f,
                false,
                EarthFootContactReason.Swing), Is.Zero);
            Assert.That(EarthFootIkWeightBlend.EnforceSwingMaximum(
                0.58f,
                true,
                EarthFootContactReason.Stance), Is.EqualTo(0.58f).Within(0.00001f));
            foreach (float delta in new[] { 1f/30f, 1f/60f, 1f/120f })
            {
                float released = EarthFootIkWeightBlend.StepContact(1f, 0f, delta, .1f, .02f);
                Assert.That(EarthFootIkWeightBlend.EnforceSwingMaximum(released, false,
                    EarthFootContactReason.Swing), Is.Zero,
                    "A released solver goal must not pull the first authored swing frame.");
                float capture = EarthFootIkWeightBlend.StepContact(0f, 1f, delta, .1f, .02f);
                Assert.That(capture, Is.GreaterThan(0f).And.LessThan(1f),
                    "Immediate swing release must not remove gradual stance capture.");
            }
        }

        [Test]
        public void HitchCannotFinishAStanceOrPivotCaptureInOneLargePoseStep()
        {
            float ordinary = EarthFootIkWeightBlend.StepContact(
                0.84f,
                1f,
                0.25f,
                EarthFootIkWeightBlend.StanceCaptureResponseSeconds,
                0.02f,
                EarthFootIkWeightBlend.MaximumStanceCaptureFrameStep);
            Assert.That(ordinary - 0.84f,
                Is.EqualTo(EarthFootIkWeightBlend.MaximumStanceCaptureFrameStep).Within(0.0001f));
            Assert.That(ordinary, Is.LessThan(1f));

            float pivot = EarthFootIkWeightBlend.StepContact(
                0.70f,
                1f,
                0.25f,
                0.06f,
                0.02f,
                EarthFootIkWeightBlend.MaximumPivotCaptureFrameStep);
            Assert.That(pivot - 0.70f,
                Is.EqualTo(EarthFootIkWeightBlend.MaximumPivotCaptureFrameStep).Within(0.0001f));
            Assert.That(pivot, Is.LessThan(1f));

            float normalFrame = EarthFootIkWeightBlend.StepContact(
                0f,
                1f,
                1f / 60f);
            Assert.That(normalFrame, Is.EqualTo(1f / 24f).Within(0.0001f),
                "The hitch cap must not slow the ordinary 60 Hz stance response.");

            float released = EarthFootIkWeightBlend.StepContact(
                1f,
                0f,
                0.25f);
            Assert.That(released, Is.EqualTo(0.10f).Within(0.0001f));
            Assert.That(EarthFootIkWeightBlend.EnforceSwingMaximum(
                released,
                false,
                EarthFootContactReason.Swing), Is.Zero);
        }

        [Test]
        public void FinalHumanoidGoalKeepsLinearAcquisitionAndFinishesContactFirmly()
        {
            Assert.That(EarthFootIkWeightBlend.ResolveSubmittedGoalWeight(0.5f),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(EarthFootIkWeightBlend.ResolveSubmittedGoalWeight(0.8f),
                Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(EarthFootIkWeightBlend.ResolveSubmittedGoalWeight(0.9166667f),
                Is.GreaterThan(0.96f));
            Assert.That(EarthFootIkWeightBlend.ResolveSubmittedGoalWeight(1f),
                Is.EqualTo(1f).Within(0.0001f));
        }
    }
}
