using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthAnimationRescueTests
    {
        [Test]
        public void LandingClipStartAlignsAuthoredContactWithPredictedContact()
        {
            Assert.That(EarthLandingClipPhaseAlignment.ResolveStartSeconds(0.625f, 0.125f, true),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(EarthLandingClipPhaseAlignment.ResolveStartSeconds(0.625f, 4f, true),
                Is.EqualTo(0f).Within(0.0001f));
            Assert.That(EarthLandingClipPhaseAlignment.ResolveStartSeconds(0.625f, 0.125f, false),
                Is.EqualTo(0.625f).Within(0.0001f));
        }

        [Test]
        public void LandingStyleCannotDowngradeBetweenContactAndRecovery()
        {
            EarthAnimationRescueTuning tuning = EarthAnimationRescueTuning.Default;
            var state = new EarthAnimationRescueState
            {
                Phase = EarthAnimationPhase.PreLanding,
                LandingStyle = EarthLandingStyle.Moving,
                LastPredictedImpactSpeed = 5f,
                LastPredictedPlanarSpeed = 4f,
                MinimumAirVerticalSpeed = -5f
            };
            EarthAnimationRescueSample contact = EarthAnimationStateResolver.Step(
                ref state, in tuning, default, true, false, false, 0f, 4f, 0.016f);
            Assert.That(contact.Phase, Is.EqualTo(EarthAnimationPhase.LandingContact));
            Assert.That(contact.LandingStyle, Is.EqualTo(EarthLandingStyle.Moving));

            EarthAnimationRescueSample recovery = EarthAnimationStateResolver.Step(
                ref state, in tuning, default, true, false, false, 0f, 0f, 0.03f);
            Assert.That(recovery.Phase, Is.EqualTo(EarthAnimationPhase.LandingRecovery));
            Assert.That(recovery.LandingStyle, Is.EqualTo(EarthLandingStyle.Moving));
        }

        [Test]
        public void LandingAnticipationScalesFromSixtyToOneHundredEightyMilliseconds()
        {
            EarthAnimationRescueTuning tuning = EarthAnimationRescueTuning.Default;
            Assert.That(tuning.AnticipationFor(4.5f), Is.EqualTo(0.06f).Within(0.0001f));
            Assert.That(tuning.AnticipationFor(7.5f), Is.EqualTo(0.18f).Within(0.0001f));
        }

        [Test]
        public void FastPlanarMotionWithoutImpactCannotSelectMovingLandingRoll()
        {
            EarthAnimationRescueTuning tuning = EarthAnimationRescueTuning.Default;
            var state = new EarthAnimationRescueState
            {
                Phase = EarthAnimationPhase.Falling,
                MinimumAirVerticalSpeed = -0.2f,
                LastPredictedPlanarSpeed = 4f
            };

            EarthAnimationRescueSample contact = EarthAnimationStateResolver.Step(
                ref state, in tuning, default, true, false, false, 0f, 4f, 0.016f);

            Assert.That(contact.Phase, Is.EqualTo(EarthAnimationPhase.LandingContact));
            Assert.That(contact.LandingStyle, Is.EqualTo(EarthLandingStyle.Soft),
                "Startup support acquisition and a compact hop must not become a falling-to-roll clip.");
        }

        [Test]
        public void LandingPoseAmplitudeScalesFromStartupToHopToHighDrop()
        {
            Assert.That(EarthLandingPoseStrength.Resolve(0f, 3f, 0f), Is.Zero);
            Assert.That(EarthLandingPoseStrength.Resolve(0.36f, 3.2f, 0.45f),
                Is.InRange(0.08f, 0.22f));
            Assert.That(EarthLandingPoseStrength.Resolve(3f, 9f, 0.9f), Is.EqualTo(1f));
        }

        [Test]
        public void FallingCandidateEntersPreLandingBeforePhysicalGrounding()
        {
            var state = new EarthAnimationRescueState { Phase = EarthAnimationPhase.Falling };
            EarthAnimationRescueTuning tuning = EarthAnimationRescueTuning.Default;
            var candidate = Candidate(0.11f, 6f, 0.4f);
            EarthAnimationRescueSample sample = EarthAnimationStateResolver.Step(
                ref state, in tuning, in candidate, false, false, false, -6f, 0.4f, 1f / 60f);
            Assert.That(sample.Phase, Is.EqualTo(EarthAnimationPhase.PreLanding));
            Assert.That(sample.LandingStyle, Is.EqualTo(EarthLandingStyle.Soft));
        }

        [Test]
        public void MissingLandingCandidateReturnsToFallWithinGraceBudget()
        {
            var state = new EarthAnimationRescueState
            {
                Phase = EarthAnimationPhase.PreLanding,
                LandingStyle = EarthLandingStyle.Soft,
                CandidateSurfaceId = 19u,
                CandidateGeneration = 2u
            };
            EarthAnimationRescueTuning tuning = EarthAnimationRescueTuning.Default;
            EarthLandingCandidateSnapshot none = default;
            for (int frame = 0; frame < 8; frame++)
                EarthAnimationStateResolver.Step(
                    ref state, in tuning, in none, false, false, false, -3f, 0f, 0.02f);
            Assert.That(state.Phase, Is.EqualTo(EarthAnimationPhase.Falling));
        }

        [Test]
        public void MovingLandingRecoversToLocomotionInOneHundredMilliseconds()
        {
            var state = new EarthAnimationRescueState
            {
                Phase = EarthAnimationPhase.PreLanding,
                LandingStyle = EarthLandingStyle.Moving,
                MinimumAirVerticalSpeed = -5f,
                LastPredictedImpactSpeed = 5f
            };
            EarthAnimationRescueTuning tuning = EarthAnimationRescueTuning.Default;
            EarthLandingCandidateSnapshot none = default;
            EarthAnimationStateResolver.Step(
                ref state, in tuning, in none, true, false, false, 0f, 3f, 0.02f);
            EarthAnimationStateResolver.Step(
                ref state, in tuning, in none, true, false, false, 0f, 3f, 0.02f);
            for (int frame = 0; frame < 5; frame++)
                EarthAnimationStateResolver.Step(
                    ref state, in tuning, in none, true, false, false, 0f, 3f, 0.02f);
            Assert.That(state.Phase, Is.EqualTo(EarthAnimationPhase.LocomotionLoop));
        }

        [Test]
        public void PreLandingSeverityCannotDowngradeBeforeContact()
        {
            var state = new EarthAnimationRescueState { Phase = EarthAnimationPhase.Falling };
            EarthAnimationRescueTuning tuning = EarthAnimationRescueTuning.Default;
            EarthLandingCandidateSnapshot moving = Candidate(0.05f, 5f, 2.2f);
            EarthAnimationStateResolver.Step(
                ref state, in tuning, in moving, false, false, false, -5f, 2.2f, 0.02f);
            EarthLandingCandidateSnapshot slower = Candidate(0.02f, 2f, 0.2f);
            EarthAnimationStateResolver.Step(
                ref state, in tuning, in slower, false, false, false, -2f, 0.2f, 0.02f);
            EarthLandingCandidateSnapshot none = default;
            EarthAnimationRescueSample contact = EarthAnimationStateResolver.Step(
                ref state, in tuning, in none, true, false, false, 0f, 0.2f, 0.02f);
            Assert.That(contact.LandingStyle, Is.EqualTo(EarthLandingStyle.Moving));
            Assert.That(state.LastPredictedPlanarSpeed, Is.EqualTo(2.2f).Within(0.0001f));
        }

        [Test]
        public void TurnFilterCannotFlipSignInOneFrameAndReleasesOverAuthoredWindow()
        {
            var state = new EarthScalarPresentationState { Value = 0.8f };
            EarthTurnPresentationSample reversed = EarthAnimationParameterFilter.StepTurn(
                ref state, -145f, -1f, 145f, 7f, 0.055f, 0.065f, 0.16f, 1f / 60f);
            Assert.That(reversed.Value, Is.GreaterThanOrEqualTo(0f));
            state = new EarthScalarPresentationState { Value = 0.8f };
            for (int frame = 0; frame < 7; frame++)
                EarthAnimationParameterFilter.StepTurn(
                    ref state, 0f, 0f, 145f, 7f, 0.055f, 0.065f, 0.16f, 1f / 60f);
            Assert.That(math.abs(state.Value), Is.GreaterThan(0.01f));
        }

        [Test]
        public void FixedClockYawNoiseCannotEnterTurnWithoutPlayerIntent()
        {
            EarthScalarPresentationState state = default;
            float[] aliasedYaw = { 0f, 38f, 0f, -31f, 0f, 24f, -19f, 0f };
            for (int frame = 0; frame < 240; frame++)
            {
                EarthTurnPresentationSample sample = EarthAnimationParameterFilter.StepTurn(
                    ref state,
                    aliasedYaw[frame % aliasedYaw.Length],
                    0f,
                    145f,
                    7f,
                    0.055f,
                    0.065f,
                    0.16f,
                    1f / 60f);
                Assert.That(sample.Value, Is.EqualTo(0f).Within(0.000001f));
                Assert.That(sample.PivotActive, Is.False);
            }
        }

        [Test]
        public void PassiveSupportDriftDoesNotBlendWalkingIntoIdle()
        {
            Assert.That(EarthAnimationParameterFilter.ResolveLocomotionTargetSpeed(
                0.09f, 0f, 0.14f), Is.Zero);
            Assert.That(EarthAnimationParameterFilter.ResolveLocomotionTargetSpeed(
                -0.12f, 0f, 0.14f), Is.Zero);
            Assert.That(EarthAnimationParameterFilter.ResolveLocomotionTargetSpeed(
                0.09f, 1f, 0.14f), Is.EqualTo(0.09f).Within(0.000001f));
            Assert.That(EarthAnimationParameterFilter.ResolveLocomotionTargetSpeed(
                0.5f, 0f, 0.14f), Is.EqualTo(0.5f).Within(0.000001f));
        }

        [Test]
        public void SupportPresentationExtrapolationPreservesIdentityAndAdvancesPose()
        {
            var support = new SupportFrameSnapshot(
                44u, 7u, float3.zero, quaternion.identity,
                new float3(2f, 0f, 0f), new float3(0f, math.PI, 0f),
                new float3(2f, 0f, 0f), new float3(0f, 1f, 0f), false);
            SupportFrameSnapshot render = EarthPresentationSupportSolver.Extrapolate(in support, 0.02f);
            Assert.That(render.SurfaceId, Is.EqualTo(44u));
            Assert.That(render.Generation, Is.EqualTo(7u));
            Assert.That(render.Position.x, Is.EqualTo(0.04f).Within(0.0001f));
            float3 forward = math.rotate(render.Rotation, new float3(0f, 0f, 1f));
            Assert.That(forward.x, Is.GreaterThan(0.05f));
        }

        [Test]
        public void SpeedFilterUsesDifferentAccelerationAndDecelerationResponses()
        {
            EarthScalarPresentationState accelerating = default;
            EarthScalarPresentationState decelerating = new EarthScalarPresentationState { Value = 6f };
            float up = EarthAnimationParameterFilter.StepSpeed(ref accelerating, 6f, 0.075f, 0.11f, 0.02f);
            float down = EarthAnimationParameterFilter.StepSpeed(ref decelerating, 0f, 0.075f, 0.11f, 0.02f);
            Assert.That(up, Is.GreaterThan(0f));
            Assert.That(down, Is.LessThan(6f));
            Assert.That(float.IsFinite(up) && float.IsFinite(down), Is.True);
        }

        private static EarthLandingCandidateSnapshot Candidate(
            float timeToContact,
            float impactSpeed,
            float planarSpeed) => new EarthLandingCandidateSnapshot(
            true,
            timeToContact,
            impactSpeed,
            planarSpeed,
            new float3(0f, 0f, 0f),
            new float3(0f, 1f, 0f),
            float3.zero,
            19u,
            2u,
            false);
    }
}
