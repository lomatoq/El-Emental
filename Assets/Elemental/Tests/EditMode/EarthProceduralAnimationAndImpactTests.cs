using System.Reflection;
using Elemental.Presentation.Animation;
using Elemental.Presentation.VFX;
using Elemental.Runtime.World;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Combat;
using Elemental.Simulation.Magic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthProceduralAnimationAndImpactTests
    {
        [Test]
        public void OneHeavyStoneKnocksDownButThreeDistinctStonesMayKnockOut()
        {
            var single = new CharacterOutcomeInput(
                EarthCharacterImpactSourceKind.LooseStone,
                0f,
                0f,
                7f,
                1);
            var cluster = new CharacterOutcomeInput(
                EarthCharacterImpactSourceKind.LooseStone,
                0f,
                0f,
                7f,
                3);
            Assert.That(
                CharacterOutcomeResolver.Resolve(in single),
                Is.EqualTo(CharacterOutcome.RecoverableKnockdown));
            Assert.That(
                CharacterOutcomeResolver.Resolve(in cluster),
                Is.EqualTo(CharacterOutcome.Knockout));
        }

        [Test]
        public void RecoverableKnockdownHasOnePhysicalAndOneAuthoredRecoveryStage()
        {
            EarthRecoverableKnockdownState state = EarthRecoverableKnockdownState.Begin();
            int authoredPulses = 0;
            int completedPulses = 0;
            for (int frame = 0; frame < 200 && state.IsActive; frame++)
            {
                EarthRecoverableKnockdownStep step = EarthRecoverableKnockdownSolver.Step(
                    in state,
                    1f / 60f);
                state = step.State;
                if (step.BeginAuthoredRecovery) authoredPulses++;
                if (step.Completed) completedPulses++;
            }
            Assert.That(authoredPulses, Is.EqualTo(1));
            Assert.That(completedPulses, Is.EqualTo(1));
            Assert.That(state.IsActive, Is.False);
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void KnockdownTimingIsFrameRateIndependent(int frameRate)
        {
            EarthRecoverableKnockdownState state = EarthRecoverableKnockdownState.Begin();
            float elapsed = 0f;
            float dt = 1f / frameRate;
            while (state.IsActive && elapsed < 3f)
            {
                EarthRecoverableKnockdownStep step = EarthRecoverableKnockdownSolver.Step(
                    in state,
                    dt);
                state = step.State;
                elapsed += dt;
            }
            Assert.That(elapsed, Is.EqualTo(1.44f).Within(dt + 0.0001f));
        }

        [Test]
        public void AuthoredFlightAndContactWindowsAreExplicit()
        {
            EarthAuthoredActionDefinition jump = EarthAuthoredActionCatalog.Resolve(
                EarthAuthoredActionId.Jump);
            EarthAuthoredActionDefinition roll = EarthAuthoredActionCatalog.Resolve(
                EarthAuthoredActionId.MovingLandingRoll);
            EarthAuthoredActionDefinition hard = EarthAuthoredActionCatalog.Resolve(
                EarthAuthoredActionId.HardLandingBrace);
            EarthAuthoredActionDefinition dodge = EarthAuthoredActionCatalog.Resolve(
                EarthAuthoredActionId.DirectionalDodge);

            Assert.That(jump.FootPolicyAt(0.5f), Is.EqualTo(EarthAuthoredFootPolicy.FlightIkOff));
            Assert.That(roll.MinimumClearanceMeters, Is.GreaterThanOrEqualTo(0.30f));
            Assert.That(roll.FootPolicyAt(0.10f), Is.EqualTo(EarthAuthoredFootPolicy.FlightIkOff));
            Assert.That(roll.FootPolicyAt(0.50f), Is.EqualTo(EarthAuthoredFootPolicy.AuthoredContact));
            Assert.That(roll.FootPolicyAt(0.95f), Is.EqualTo(EarthAuthoredFootPolicy.DefaultContact));
            Assert.That(hard.FootPolicyAt(0.10f), Is.EqualTo(EarthAuthoredFootPolicy.FlightIkOff));
            Assert.That(hard.FootPolicyAt(0.55f), Is.EqualTo(EarthAuthoredFootPolicy.BraceBoth));
            Assert.That(dodge.MinimumClearanceMeters, Is.GreaterThanOrEqualTo(0.10f));
            Assert.That(dodge.FootPolicyAt(0.10f), Is.EqualTo(EarthAuthoredFootPolicy.FlightIkOff));
            Assert.That(dodge.FootPolicyAt(0.45f), Is.EqualTo(EarthAuthoredFootPolicy.AuthoredContact));
            Assert.That(dodge.FootPolicyAt(0.95f), Is.EqualTo(EarthAuthoredFootPolicy.DefaultContact));
            Assert.That(
                EarthAuthoredActionResolver.Resolve(
                    EarthAnimationPhase.LocomotionLoop,
                    EarthLandingStyle.None,
                    false,
                    true,
                    true),
                Is.EqualTo(EarthAuthoredActionId.HitRecoil),
                "A resolved hit must interrupt cast presentation for one coherent recoil.");
            Assert.That(
                EarthAuthoredActionResolver.Resolve(
                    EarthAnimationPhase.LandingContact,
                    EarthLandingStyle.Moving,
                    false,
                    true,
                    true),
                Is.EqualTo(EarthAuthoredActionId.MovingLandingRoll),
                "Additive cast/hit lanes may not erase base-layer landing contact ownership.");
        }

        [Test]
        public void DirectionalDodgeGateSelectsFourAuthoredClipsAndHonorsInterruptPolicy()
        {
            var right = new EarthDirectionalDodgeInput(
                new float2(0.9f, 0.2f), true, false, false, false,
                EarthAuthoredActionId.Locomotion, 0f);
            var backward = new EarthDirectionalDodgeInput(
                new float2(0.1f, -0.8f), true, false, false, false,
                EarthAuthoredActionId.Locomotion, 0f);
            EarthDirectionalDodgeDecision rightDecision = EarthDirectionalDodgeGate.Resolve(in right);
            EarthDirectionalDodgeDecision backDecision = EarthDirectionalDodgeGate.Resolve(in backward);
            Assert.That(rightDecision.Accepted, Is.True);
            Assert.That(rightDecision.Direction, Is.EqualTo(EarthDirectionalDodgeDirection.Right));
            Assert.That(rightDecision.BlendDirection, Is.EqualTo(new float2(1f, 0f)));
            Assert.That(backDecision.Accepted, Is.True);
            Assert.That(backDecision.Direction, Is.EqualTo(EarthDirectionalDodgeDirection.Backward));
            Assert.That(backDecision.BlendDirection, Is.EqualTo(new float2(0f, -1f)));

            var airborne = new EarthDirectionalDodgeInput(
                new float2(1f, 0f), false, false, false, false,
                EarthAuthoredActionId.Locomotion, 0f);
            Assert.That(EarthDirectionalDodgeGate.Resolve(in airborne).RejectReason,
                Is.EqualTo(EarthDirectionalDodgeRejectReason.Airborne));
            Assert.That(EarthAuthoredActionCatalog.CanInterrupt(
                EarthAuthoredActionId.DirectionalDodge, 0.20f, EarthAuthoredActionId.HitRecoil),
                Is.False);
            Assert.That(EarthAuthoredActionCatalog.CanInterrupt(
                EarthAuthoredActionId.DirectionalDodge, 0.35f, EarthAuthoredActionId.HitRecoil),
                Is.True);
            Assert.That(EarthAuthoredActionCatalog.CanInterrupt(
                EarthAuthoredActionId.RecoverableKnockdownRecovery, 0.95f,
                EarthAuthoredActionId.DirectionalDodge), Is.False,
                "Authored get-up is one coherent, non-chainable recovery.");
        }

        [Test]
        public void AuthoredContactTimingDiffersByLessThanTenPercentAtThirtySixtyAndOneTwentyFps()
        {
            float2 at30 = SimulateActionWindows(30);
            float2 at60 = SimulateActionWindows(60);
            float2 at120 = SimulateActionWindows(120);
            Assert.That(math.abs(at30.x - at60.x) / at60.x, Is.LessThanOrEqualTo(0.10f));
            Assert.That(math.abs(at120.x - at60.x) / at60.x, Is.LessThanOrEqualTo(0.10f));
            Assert.That(math.abs(at30.y - at60.y) / at60.y, Is.LessThanOrEqualTo(0.10f));
            Assert.That(math.abs(at120.y - at60.y) / at60.y, Is.LessThanOrEqualTo(0.10f));
        }

        [Test]
        public void WorldResponseFansOutOnceWithOneSharedId()
        {
            var events = new MagicWorldEvents();
            var fanout = new EarthWorldResponseFanoutAdapter(events);
            int count = 0;
            uint seenId = 0u;
            events.EarthImpactOccurred += value =>
            {
                count++;
                seenId = value.SourceId;
            };
            uint id = EarthWorldResponseId.Compose(
                0xC0010001u,
                0x77000001u,
                123u,
                EarthCharacterImpactResponse.RecoverableKnockdown);
            var response = new EarthWorldResponseEvent(
                id,
                123u,
                0x77000001u,
                0xC0010001u,
                EarthWorldResponseKind.Knockdown,
                EarthCharacterImpactSourceKind.LooseStone,
                EarthCharacterImpactResponse.RecoverableKnockdown,
                float3.zero,
                new float3(0f, 1f, 0f),
                new float3(1f, 0f, 0f),
                120f,
                240f,
                0.8f);

            Assert.That(fanout.Publish(in response), Is.True);
            Assert.That(fanout.Publish(in response), Is.False);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(seenId, Is.EqualTo(id));
        }

        [Test]
        public void UpperBodyInertiaIsBoundedAndConsistentAtThirtySixtyAndOneTwentyFps()
        {
            float3 at30 = SimulateBody(30);
            float3 at60 = SimulateBody(60);
            float3 at120 = SimulateBody(120);
            Assert.That(math.cmax(math.abs(at30)), Is.LessThanOrEqualTo(10.001f));
            Assert.That(math.distance(at30, at60), Is.LessThan(0.35f));
            Assert.That(math.distance(at60, at120), Is.LessThan(0.20f));
        }

        [Test]
        public void DirectionalImpactRecoveryDecaysOnceWithoutJellyRebound()
        {
            EarthInertialBodyState state = default;
            float previousMagnitude = float.PositiveInfinity;
            float initialPitchSign = 0f;
            float initialRollSign = 0f;
            for (int frame = 0; frame < 90; frame++)
            {
                EarthInertialBodySample sample = EarthInertialBodyMotionSolver.Step(
                    in state,
                    float3.zero,
                    0f,
                    0f,
                    frame == 0 ? new float3(6f, 0f, -4f) : float3.zero,
                    true,
                    false,
                    1f / 60f);
                state = sample.State;
                float magnitude = math.length(sample.AnglesDegrees);
                if (frame == 0)
                {
                    initialPitchSign = math.sign(sample.AnglesDegrees.x);
                    initialRollSign = math.sign(sample.AnglesDegrees.z);
                }
                Assert.That(magnitude, Is.LessThanOrEqualTo(previousMagnitude + 0.0001f));
                if (math.abs(sample.AnglesDegrees.x) > 0.000001f)
                    Assert.That(math.sign(sample.AnglesDegrees.x), Is.EqualTo(initialPitchSign));
                if (math.abs(sample.AnglesDegrees.z) > 0.000001f)
                    Assert.That(math.sign(sample.AnglesDegrees.z), Is.EqualTo(initialRollSign));
                previousMagnitude = magnitude;
            }
        }

        [Test]
        public void VisualContinuityGateRejectsSlowSwingIkReleaseAfterTwoFrames()
        {
            EarthAnimationVisualContinuityState state = default;
            EarthAnimationVisualContinuityAudit.Step(
                ref state,
                VisualSample(true, false, true, false, 1f, 0f));
            for (int frame = 0; frame < 4; frame++)
            {
                float slowWeight = 1f - (frame + 1) * 0.04f;
                EarthAnimationVisualContinuityAudit.Step(
                    ref state,
                    VisualSample(true, false, false, false, slowWeight, 0f));
            }

            EarthAnimationVisualContinuitySummary summary =
                EarthAnimationVisualContinuityAudit.Snapshot(in state);
            Assert.That(summary.SwingResidualViolationFrames, Is.GreaterThan(0));
            Assert.That(summary.MaximumSwingIkAfterTwoFrames, Is.GreaterThan(0.15f));
            Assert.That(summary.HardGatesPassed, Is.False);
        }

        [Test]
        public void VisualContinuityGateAcceptsFastSwingReleaseAndBoundedPivot()
        {
            EarthAnimationVisualContinuityState state = default;
            EarthAnimationVisualContinuityAudit.Step(
                ref state,
                VisualSample(true, false, true, false, 1f, 0f));
            EarthAnimationVisualContinuityAudit.Step(
                ref state,
                VisualSample(true, false, false, false, 0.45f, 0f));
            EarthAnimationVisualContinuityAudit.Step(
                ref state,
                VisualSample(true, false, false, false, 0.10f, 0f));
            EarthAnimationVisualContinuityAudit.Step(
                ref state,
                VisualSample(false, true, true, false, 0.82f, 0f, 0.008f, 3f));

            EarthAnimationVisualContinuitySummary summary =
                EarthAnimationVisualContinuityAudit.Snapshot(in state);
            Assert.That(summary.SwingResidualViolationFrames, Is.Zero);
            Assert.That(summary.MaximumSwingIkAfterTwoFrames, Is.LessThanOrEqualTo(0.15f));
            Assert.That(summary.MaximumAnkleStepDegrees, Is.LessThanOrEqualTo(8f));
            Assert.That(summary.MaximumPivotPlantedFootStepMeters, Is.LessThanOrEqualTo(0.02f));
            Assert.That(summary.PivotWithoutPlantedFootFrames, Is.Zero);
            Assert.That(summary.HardGatesPassed, Is.True);
        }

        [Test]
        public void FootIkHasOneRuntimeWriterAndBodyAdaptationRunsLast()
        {
            const BindingFlags callbackFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo footCallback = typeof(EarthFootContactController).GetMethod(
                "OnAnimatorIK",
                callbackFlags);
            MethodInfo legacyPoseCallback = typeof(EarthCharacterPoseController).GetMethod(
                "OnAnimatorIK",
                callbackFlags);
            MethodInfo legacySurfCallback = typeof(EarthSurfFootContactRescue).GetMethod(
                "OnAnimatorIK",
                callbackFlags);
            MethodInfo legacySurfBaselineCallback = typeof(EarthAnimatorIkBaselineCapture).GetMethod(
                "OnAnimatorIK",
                callbackFlags);
            MethodInfo legacySurfPelvisCallback = typeof(EarthSurfPelvisOwnershipOverride).GetMethod(
                "OnAnimatorIK",
                callbackFlags);
            var organicOrder = typeof(HumanoidOrganicIdle).GetCustomAttribute<DefaultExecutionOrder>();
            var bodyOrder = typeof(HumanoidProceduralBodyResponse)
                .GetCustomAttribute<DefaultExecutionOrder>();

            Assert.That(footCallback, Is.Not.Null,
                "The independent foot-contact owner must receive the base Animator IK pass.");
            Assert.That(legacyPoseCallback, Is.Null,
                "The magic pose component must not register a second foot/knee/pelvis IK writer.");
            Assert.That(legacySurfCallback, Is.Null,
                "The legacy surf rescue must not register a second foot/knee/pelvis IK writer.");
            Assert.That(legacySurfBaselineCallback, Is.Null,
                "Surf must not install a hidden baseline Animator IK callback.");
            Assert.That(legacySurfPelvisCallback, Is.Null,
                "Surf must not install a second pelvis Animator IK callback.");
            Assert.That(organicOrder, Is.Not.Null);
            Assert.That(bodyOrder, Is.Not.Null);
            Assert.That(bodyOrder.order, Is.GreaterThan(organicOrder.order),
                "Bounded inertial response must compose after authored organic upper-body motion.");
        }

        private static float3 SimulateBody(int frameRate)
        {
            EarthInertialBodyState state = default;
            EarthInertialBodySample sample = default;
            float dt = 1f / frameRate;
            int frames = frameRate;
            for (int frame = 0; frame < frames; frame++)
            {
                float3 kick = frame == 0 ? new float3(4f, 1f, -3f) : float3.zero;
                sample = EarthInertialBodyMotionSolver.Step(
                    in state,
                    new float3(3f, 0f, -8f),
                    45f,
                    0.6f,
                    kick,
                    true,
                    false,
                    dt);
                state = sample.State;
            }
            return sample.AnglesDegrees;
        }

        private static float2 SimulateActionWindows(int frameRate)
        {
            EarthAuthoredActionDefinition definition = EarthAuthoredActionCatalog.Resolve(
                EarthAuthoredActionId.MovingLandingRoll);
            float dt = 1f / frameRate;
            float flightSeconds = 0f;
            float authoredSeconds = 0f;
            for (int frame = 0; frame < frameRate; frame++)
            {
                EarthAuthoredFootPolicy policy = definition.FootPolicyAt(frame * dt);
                if (policy == EarthAuthoredFootPolicy.FlightIkOff) flightSeconds += dt;
                if (policy == EarthAuthoredFootPolicy.AuthoredContact) authoredSeconds += dt;
            }
            return new float2(flightSeconds, authoredSeconds);
        }

        private static EarthAnimationVisualContinuitySample VisualSample(
            bool locomoting,
            bool turning,
            bool leftLocked,
            bool rightLocked,
            float leftWeight,
            float rightWeight,
            float footStep = 0f,
            float ankleDegrees = 0f) =>
            new EarthAnimationVisualContinuitySample(
                1f / 60f,
                true,
                locomoting,
                turning,
                new float3(footStep, 0f, 0f),
                new float3(0f, 0f, 0f),
                quaternion.AxisAngle(new float3(0f, 1f, 0f), math.radians(ankleDegrees)),
                quaternion.identity,
                new float3(footStep, 0f, 0f),
                new float3(0f, 0f, 0f),
                leftWeight,
                rightWeight,
                leftLocked,
                rightLocked);
    }
}
