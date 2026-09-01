using System;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthPoseMatchedRecoveryTests
    {
        [TestCase(1f, 0f, EarthRecoveryOrientation.Back)]
        [TestCase(-1f, 0f, EarthRecoveryOrientation.Front)]
        [TestCase(0f, 1f, EarthRecoveryOrientation.Right)]
        [TestCase(0f, -1f, EarthRecoveryOrientation.Left)]
        public void ClassificationCoversFrontBackLeftAndRight(
            float outwardY,
            float rightY,
            EarthRecoveryOrientation expected)
        {
            EarthRecoveryOrientation result = EarthRecoveryAlignmentSolver.Classify(
                new float3(0f, outwardY, 0f),
                new float3(0f, rightY, 0f),
                new float3(0f, 1f, 0f));

            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase(0.033333333f, TestName = "AnimatorContinuityAcceptsLeadAt30Hz")]
        [TestCase(0.016666667f, TestName = "AnimatorContinuityAcceptsLeadAt60Hz")]
        [TestCase(0.008333333f, TestName = "AnimatorContinuityAcceptsLeadAt120Hz")]
        public void AnimatorContinuityBudgetAcceptsOnePendingEvaluation(
            float evaluationLeadSeconds)
        {
            const int stateHash = 175079391;
            const float entryPhase = 0.55f;
            const float stateLength = 1.2f;
            const float stateSpeed = 1.9f;
            const float speedMultiplier = 1f;
            const float elapsedSeconds = 0.058f;
            float normalizedRate = stateSpeed * speedMultiplier / stateLength;
            float effectiveElapsedSeconds = elapsedSeconds + evaluationLeadSeconds;
            float observedPhase = entryPhase + effectiveElapsedSeconds * normalizedRate;

            EarthRecoveryAnimatorContinuityResult result =
                EarthRecoveryAnimatorContinuityGate.Evaluate(
                    stateHash,
                    stateHash,
                    entryPhase,
                    observedPhase,
                    elapsedSeconds,
                    stateLength,
                    stateSpeed,
                    speedMultiplier,
                    false,
                    evaluationLeadSeconds: evaluationLeadSeconds);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.EvaluationLeadSeconds,
                Is.EqualTo(evaluationLeadSeconds).Within(0.00001f));
            Assert.That(result.EffectiveElapsedSeconds,
                Is.EqualTo(effectiveElapsedSeconds).Within(0.00001f));
            Assert.That(result.MeasuredAdvance,
                Is.EqualTo(effectiveElapsedSeconds * normalizedRate).Within(0.00001f));
            Assert.That(result.AllowedAdvance,
                Is.EqualTo(
                    effectiveElapsedSeconds * normalizedRate +
                    EarthRecoveryAnimatorContinuityGate.DefaultPhaseSlack)
                    .Within(0.00001f));
        }

        [Test]
        public void AnimatorContinuityBudgetRejectsWrongStateHash()
        {
            EarthRecoveryAnimatorContinuityResult result =
                EarthRecoveryAnimatorContinuityGate.Evaluate(
                    175079391,
                    -1269438207,
                    0.55f,
                    0.60f,
                    0.05f,
                    1.2f,
                    1.9f,
                    1f,
                    false);

            Assert.That(result.HashMatches, Is.False);
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void AnimatorContinuityBudgetAcceptsMeasuredBatchFrameAdvanceWithLead()
        {
            EarthRecoveryAnimatorContinuityResult result =
                EarthRecoveryAnimatorContinuityGate.Evaluate(
                    175079391,
                    175079391,
                    0.55f,
                    0.6673f,
                    0.058f,
                    1.1228f,
                    1.9f,
                    1f,
                    false,
                    evaluationLeadSeconds: 1f / 60f);

            Assert.That(result.MeasuredAdvance, Is.EqualTo(0.1173f).Within(0.00001f));
            Assert.That(result.EvaluationLeadSeconds,
                Is.EqualTo(1f / 60f).Within(0.00001f));
            Assert.That(result.EffectiveElapsedSeconds,
                Is.EqualTo(0.07466667f).Within(0.00001f));
            Assert.That(result.AllowedAdvance, Is.GreaterThan(result.MeasuredAdvance));
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void AnimatorContinuityBudgetCapsEvaluationLead()
        {
            EarthRecoveryAnimatorContinuityResult result =
                EarthRecoveryAnimatorContinuityGate.Evaluate(
                    175079391,
                    175079391,
                    0.55f,
                    0.60f,
                    0.01f,
                    1.2f,
                    1.9f,
                    1f,
                    false,
                    evaluationLeadSeconds: 1f);

            Assert.That(result.EvaluationLeadSeconds,
                Is.EqualTo(EarthRecoveryAnimatorContinuityGate.MaximumEvaluationLeadSeconds)
                    .Within(0.00001f));
            Assert.That(result.EffectiveElapsedSeconds,
                Is.EqualTo(
                    0.01f +
                    EarthRecoveryAnimatorContinuityGate.MaximumEvaluationLeadSeconds)
                    .Within(0.00001f));
            Assert.That(result.IsValid, Is.True);
        }

        [TestCase(0.50f, 0.10f, TestName = "AnimatorContinuityRejectsBackwardJump")]
        [TestCase(0.95f, 0.016666667f,
            TestName = "AnimatorContinuityRejectsForwardTeleportBeyondLead")]
        public void AnimatorContinuityBudgetRejectsDiscontinuity(
            float observedPhase,
            float elapsedSeconds)
        {
            EarthRecoveryAnimatorContinuityResult result =
                EarthRecoveryAnimatorContinuityGate.Evaluate(
                    175079391,
                    175079391,
                    0.55f,
                    observedPhase,
                    elapsedSeconds,
                    1.2f,
                    1.9f,
                    1f,
                    false,
                    evaluationLeadSeconds: 1f / 30f);

            Assert.That(result.TimingIsValid, Is.False);
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void MatcherChoosesClosestValidPoseAndEntryPhase()
        {
            EarthRecoveryPoseFeature current = Feature(0.9f);
            EarthRecoveryPoseFeature far = Feature(-0.6f);
            EarthRecoveryPoseFeature close = Feature(0.88f);
            EarthRecoveryMarkerProfile markers = EarthRecoveryMarkerProfile.Default;
            var database = new EarthRecoveryPoseDatabase(new[]
            {
                Candidate(42u, EarthRecoveryOrientation.Back, 0.10f, in far, in markers),
                Candidate(42u, EarthRecoveryOrientation.Back, 0.65f, in close, in markers),
                new EarthRecoveryPoseCandidate(
                    7u, 77, EarthRecoveryOrientation.Back, 0.5f, in current,
                    new float3(0f, 0.9f, 0f), in markers, false),
                Candidate(9u, EarthRecoveryOrientation.Front, 0.4f, in current, in markers)
            });
            EarthRecoveryPoseMatchWeights weights = EarthRecoveryPoseMatchWeights.Default;

            bool matched = EarthRecoveryPoseMatcher.TryMatch(
                database,
                EarthRecoveryOrientation.Back,
                in current,
                in weights,
                out EarthRecoveryPoseMatch result);

            Assert.That(matched, Is.True);
            Assert.That(result.Candidate.ClipId, Is.EqualTo(42u));
            Assert.That(result.Candidate.EntryPhase, Is.EqualTo(0.65f).Within(0.0001f));
            Assert.That(result.Cost, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void MatcherSteadyStateAllocatesZeroManagedBytes()
        {
            EarthRecoveryPoseFeature feature = Feature(0.25f);
            EarthRecoveryMarkerProfile markers = EarthRecoveryMarkerProfile.Default;
            var database = new EarthRecoveryPoseDatabase(new[]
            {
                Candidate(11u, EarthRecoveryOrientation.Back, 0.3f, in feature, in markers)
            });
            EarthRecoveryPoseMatchWeights weights = EarthRecoveryPoseMatchWeights.Default;
            for (int index = 0; index < 32; index++)
                EarthRecoveryPoseMatcher.TryMatch(
                    database,
                    EarthRecoveryOrientation.Back,
                    in feature,
                    in weights,
                    out _);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 1000; index++)
                EarthRecoveryPoseMatcher.TryMatch(
                    database,
                    EarthRecoveryOrientation.Back,
                    in feature,
                    in weights,
                    out _);
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.That(after - before, Is.EqualTo(0L));
        }

        [Test]
        public void AlignmentUsesLivePelvisAndNeverPreHitRoot()
        {
            float3 livePelvis = new float3(18f, 6f, -11f);
            float3 clipPelvisOffset = new float3(0.04f, 0.91f, -0.06f);
            EarthRecoveryAlignmentInput input = AlignmentInput(livePelvis, clipPelvisOffset);
            EarthRecoveryClearanceResult clearance =
                EarthRecoveryAlignmentSolver.SelectClearance(true, true, true);

            EarthRecoveryAlignmentResult result = EarthRecoveryAlignmentSolver.Solve(
                in input,
                in clearance);
            float3 reconstructedPelvis = result.RootPosition +
                                          math.rotate(result.RootRotation, clipPelvisOffset);

            Assert.That(math.distance(reconstructedPelvis, livePelvis), Is.LessThan(0.0001f));
            Assert.That(math.distance(result.RootPosition, float3.zero), Is.GreaterThan(10f),
                "Recovery must not use an unrelated pre-hit root.");
            Assert.That(math.dot(result.RadialUp, new float3(0f, 1f, 0f)), Is.GreaterThan(0.999f));
        }

        [Test]
        public void DegenerateFacingIsFiniteAndDoesNotFlip()
        {
            var input = new EarthRecoveryAlignmentInput(
                new float3(1f, 2f, 3f),
                new float3(1f, 2f, 3f),
                float3.zero,
                float3.zero,
                new float3(0f, 1f, 0f),
                new float3(1f, 0f, 0f),
                float3.zero,
                new float3(0f, 0f, 1f),
                float3.zero);
            EarthRecoveryClearanceResult clearance =
                EarthRecoveryAlignmentSolver.SelectClearance(true, true, true);

            EarthRecoveryAlignmentResult result = EarthRecoveryAlignmentSolver.Solve(
                in input,
                in clearance);

            Assert.That(math.all(math.isfinite(result.RootPosition)), Is.True);
            Assert.That(math.all(math.isfinite(result.RootRotation.value)), Is.True);
            Assert.That(result.UsedFacingFallback, Is.True);
            Assert.That(math.dot(result.RadialFacing, new float3(0f, 0f, 1f)), Is.GreaterThan(0.99f));
        }

        [Test]
        public void OpposedRagdollFacingDoesNotCreateOneEightyDegreeRootFlip()
        {
            var input = new EarthRecoveryAlignmentInput(
                float3.zero,
                new float3(0f, 0f, -0.5f),
                new float3(0f, 0f, -1f),
                new float3(0f, 0f, -1f),
                new float3(0f, 1f, 0f),
                new float3(1f, 0f, 0f),
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 1f),
                float3.zero);
            EarthRecoveryClearanceResult clearance =
                EarthRecoveryAlignmentSolver.SelectClearance(true, true, true);

            EarthRecoveryAlignmentResult result = EarthRecoveryAlignmentSolver.Solve(
                in input,
                in clearance);

            Assert.That(math.dot(result.RadialFacing, new float3(0f, 0f, 1f)), Is.GreaterThan(0.99f));
        }

        [Test]
        public void ClearanceFallsBackToBoundedMaximumAndReportsBlocked()
        {
            EarthRecoveryClearanceResult result = EarthRecoveryAlignmentSolver.SelectClearance(
                false,
                false,
                false);

            Assert.That(result.Kind, Is.EqualTo(EarthRecoveryClearanceKind.BlockedAtMaximumLift));
            Assert.That(result.LiftMeters,
                Is.EqualTo(EarthRecoveryAlignmentSolver.MaximumClearanceLiftMeters));
            Assert.That(result.Succeeded, Is.False);
        }

        [Test]
        public void OwnershipAdapterRequiresCanonicalModeAndRecoveryIsInterruptible()
        {
            var coordinator = new EarthPhysicalAnimationCoordinator();
            EarthRecoveryResult result = ValidResult();

            Assert.That(coordinator.TryBeginFullRagdoll(
                CharacterPhysicalMode.AnimatedMotor, 1u), Is.False);
            Assert.That(coordinator.TryBeginFullRagdoll(
                CharacterPhysicalMode.FullRagdoll, 1u), Is.True);
            Assert.That(coordinator.TryBeginFullRagdoll(
                CharacterPhysicalMode.FullRagdoll, 1u), Is.False);
            Assert.That(coordinator.TryBeginFullRagdoll(
                CharacterPhysicalMode.FullRagdoll, 2u), Is.False);
            Assert.That(coordinator.RagdollHandoffCount, Is.EqualTo(1));
            Assert.That(coordinator.IsConsistentWith(
                CharacterPhysicalMode.FullRagdoll), Is.True);
            Assert.That(coordinator.TryBeginPoseMatchedRecovery(
                CharacterPhysicalMode.FullRagdoll, 1u, in result), Is.False);
            Assert.That(coordinator.TryBeginPoseMatchedRecovery(
                CharacterPhysicalMode.Recovery, 1u, in result), Is.True);
            Assert.That(coordinator.TryBeginPoseMatchedRecovery(
                CharacterPhysicalMode.Recovery, 1u, in result), Is.False);
            Assert.That(coordinator.RecoveryHandoffCount, Is.EqualTo(1));
            Assert.That(coordinator.IsConsistentWith(
                CharacterPhysicalMode.Recovery), Is.True);

            Assert.That(coordinator.TryAdvancePoseMatchedRecovery(
                CharacterPhysicalMode.Recovery,
                1f,
                false,
                out EarthPhysicalAnimationOwnership beforeSupport), Is.True);
            Assert.That(beforeSupport.FeetEnabled, Is.False);
            Assert.That(beforeSupport.ControlsEnabled, Is.False);
            Assert.That(beforeSupport.RecoveryExitReady, Is.False);

            Assert.That(coordinator.TryAdvancePoseMatchedRecovery(
                CharacterPhysicalMode.Recovery,
                1f,
                true,
                out EarthPhysicalAnimationOwnership afterSupport), Is.True);
            Assert.That(afterSupport.FeetEnabled, Is.True);
            Assert.That(afterSupport.ControlsEnabled, Is.True);
            Assert.That(afterSupport.RecoveryExitReady, Is.True);

            Assert.That(coordinator.TryBeginFullRagdoll(
                CharacterPhysicalMode.FullRagdoll, 2u), Is.True,
                "A distinct accepted hit must atomically interrupt recovery ownership.");
            Assert.That(coordinator.RagdollHandoffCount, Is.EqualTo(2));
            Assert.That(coordinator.Ownership.FeetEnabled, Is.False);
            Assert.That(coordinator.Ownership.ControlsEnabled, Is.False);
            Assert.That(coordinator.IsConsistentWith(
                CharacterPhysicalMode.FullRagdoll), Is.True);
        }

        [Test]
        public void SupportLossRevokesEveryMarkerOwnerUntilSupportReturns()
        {
            var coordinator = new EarthPhysicalAnimationCoordinator();
            EarthRecoveryResult result = ValidResult();
            Assert.That(coordinator.TryBeginFullRagdoll(
                CharacterPhysicalMode.FullRagdoll, 1u), Is.True);
            Assert.That(coordinator.TryBeginPoseMatchedRecovery(
                CharacterPhysicalMode.Recovery, 1u, in result), Is.True);

            Assert.That(coordinator.TryAdvancePoseMatchedRecovery(
                CharacterPhysicalMode.Recovery, 1f, true, out var supported), Is.True);
            Assert.That(supported.RecoveryExitReady, Is.True);
            Assert.That(coordinator.TryAdvancePoseMatchedRecovery(
                CharacterPhysicalMode.Recovery, 1f, false, out var lost), Is.True);
            Assert.That(lost.FeetEnabled, Is.False);
            Assert.That(lost.ControlsEnabled, Is.False);
            Assert.That(lost.ProceduralOwnersEnabled, Is.False);
            Assert.That(lost.RecoveryExitReady, Is.False);
            Assert.That(coordinator.TryCompleteRecovery(
                CharacterPhysicalMode.AnimatedMotor), Is.False);
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void MarkerThresholdsAndSkippedThresholdsAreFrameRateEquivalent(int frameRate)
        {
            EarthRecoveryMarkerProfile markers = new EarthRecoveryMarkerProfile(
                0.38f, 0.72f, 0.94f);
            EarthRecoveryResult result = ValidResult(in markers);
            var stepped = BeginRecovery(in result);
            float phase = 0f;
            float step = 1f / frameRate;
            EarthPhysicalAnimationOwnership steppedOwnership = default;
            while (phase < 1f)
            {
                phase = math.min(1f, phase + step);
                Assert.That(stepped.TryAdvancePoseMatchedRecovery(
                    CharacterPhysicalMode.Recovery,
                    phase,
                    true,
                    out steppedOwnership), Is.True);
                Assert.That(steppedOwnership.FeetEnabled,
                    Is.EqualTo(phase >= markers.FeetEnablePhase));
                Assert.That(steppedOwnership.ControlsEnabled,
                    Is.EqualTo(phase >= markers.ControlsEnablePhase));
                Assert.That(steppedOwnership.RecoveryExitReady,
                    Is.EqualTo(phase >= markers.ExitPhase));
            }

            var skipped = BeginRecovery(in result);
            Assert.That(skipped.TryAdvancePoseMatchedRecovery(
                CharacterPhysicalMode.Recovery,
                1f,
                true,
                out EarthPhysicalAnimationOwnership skippedOwnership), Is.True);
            Assert.That(skippedOwnership.FeetEnabled,
                Is.EqualTo(steppedOwnership.FeetEnabled));
            Assert.That(skippedOwnership.ControlsEnabled,
                Is.EqualTo(steppedOwnership.ControlsEnabled));
            Assert.That(skippedOwnership.RecoveryExitReady,
                Is.EqualTo(steppedOwnership.RecoveryExitReady));
        }

        [Test]
        public void MisorderedMarkersRejectSampleInsteadOfUsingDefaults()
        {
            var profile = ScriptableObject.CreateInstance<EarthPhysicalAnimationProfile>();
            try
            {
                EarthRecoveryMarkerAuthoring invalid =
                    new EarthRecoveryMarkerAuthoring(0.8f, 0.2f, 0.9f);
                profile.ConfigureRecovery(
                    true,
                    new[]
                    {
                        RecoverySample(7u, EarthRecoveryOrientation.Back, in invalid)
                    });

                Assert.That(profile.TryGetRecoveryDatabase(out _), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void HundredRepeatedAlignmentsAreDeterministicWithoutDrift()
        {
            float3 initialPelvis = new float3(-7.25f, 3.5f, 14.75f);
            float3 pelvis = initialPelvis;
            float3 offset = new float3(0.07f, 0.93f, -0.02f);
            EarthRecoveryClearanceResult clearance =
                EarthRecoveryAlignmentSolver.SelectClearance(true, true, true);
            quaternion firstRotation = quaternion.identity;

            for (int cycle = 0; cycle < 100; cycle++)
            {
                EarthRecoveryAlignmentInput input = AlignmentInput(pelvis, offset);
                EarthRecoveryAlignmentResult result = EarthRecoveryAlignmentSolver.Solve(
                    in input,
                    in clearance);
                if (cycle == 0) firstRotation = result.RootRotation;
                pelvis = result.RootPosition + math.rotate(result.RootRotation, offset);
                Assert.That(math.distance(pelvis, initialPelvis), Is.LessThan(0.0001f));
                Assert.That(math.abs(math.dot(firstRotation.value, result.RootRotation.value)),
                    Is.GreaterThan(0.99999f));
            }
        }

        [Test]
        public void PhysicalAnimationProfileDefaultsPoseMatchedRecoveryOff()
        {
            EarthPhysicalAnimationProfile profile =
                ScriptableObject.CreateInstance<EarthPhysicalAnimationProfile>();
            try
            {
                Assert.That(profile.UsePoseMatchedRecovery, Is.False);
                Assert.That(profile.TryGetRecoveryDatabase(out _), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        private static EarthRecoveryPoseCandidate Candidate(
            uint clipId,
            EarthRecoveryOrientation orientation,
            float phase,
            in EarthRecoveryPoseFeature feature,
            in EarthRecoveryMarkerProfile markers) =>
            new EarthRecoveryPoseCandidate(
                clipId,
                (int)clipId + 100,
                orientation,
                phase,
                in feature,
                new float3(0f, 0.9f, 0f),
                in markers);

        private static EarthRecoveryPoseFeature Feature(float x) =>
            new EarthRecoveryPoseFeature(
                new float3(x, 0.4f, 0.1f),
                new float3(-0.5f + x * 0.1f, 0.1f, 0.2f),
                new float3(0.5f + x * 0.1f, 0.1f, 0.2f),
                new float3(-0.2f, -0.7f, x * 0.1f),
                new float3(0.2f, -0.7f, x * 0.1f),
                new float3(0f, 1f, 0f));

        private static EarthRecoveryAlignmentInput AlignmentInput(
            float3 pelvis,
            float3 pelvisOffset) =>
            new EarthRecoveryAlignmentInput(
                pelvis,
                pelvis + new float3(0f, 0f, 0.5f),
                new float3(0f, 0f, 1f),
                new float3(0f, 0f, 1f),
                new float3(0f, 1f, 0f),
                new float3(1f, 0f, 0f),
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 1f),
                pelvisOffset);

        private static EarthPhysicalAnimationCoordinator BeginRecovery(
            in EarthRecoveryResult result)
        {
            var coordinator = new EarthPhysicalAnimationCoordinator();
            Assert.That(coordinator.TryBeginFullRagdoll(
                CharacterPhysicalMode.FullRagdoll, 1u), Is.True);
            Assert.That(coordinator.TryBeginPoseMatchedRecovery(
                CharacterPhysicalMode.Recovery, 1u, in result), Is.True);
            return coordinator;
        }

        private static EarthRecoveryPoseSampleAuthoring RecoverySample(
            uint clipId,
            EarthRecoveryOrientation orientation,
            in EarthRecoveryMarkerAuthoring markers) =>
            new EarthRecoveryPoseSampleAuthoring(
                clipId,
                "Base Layer.Knockdown Recovery",
                orientation,
                0.01f,
                new Vector3(0f, 0.9f, 0f),
                new Vector3(0f, 0.4f, 0.1f),
                new Vector3(-0.45f, 0.1f, 0.15f),
                new Vector3(0.45f, 0.1f, 0.15f),
                new Vector3(-0.2f, -0.7f, 0f),
                new Vector3(0.2f, -0.7f, 0f),
                Vector3.up,
                in markers);

        private static EarthRecoveryResult ValidResult()
        {
            EarthRecoveryMarkerProfile markers = EarthRecoveryMarkerProfile.Default;
            return ValidResult(in markers);
        }

        private static EarthRecoveryResult ValidResult(
            in EarthRecoveryMarkerProfile markers)
        {
            EarthRecoveryClearanceResult clearance =
                EarthRecoveryAlignmentSolver.SelectClearance(true, true, true);
            return new EarthRecoveryResult(
                EarthRecoveryOrientation.Back,
                1u,
                101,
                0.2f,
                0.1f,
                new float3(4f, 2f, 1f),
                new float3(4f, 1.1f, 1f),
                quaternion.identity,
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 1f),
                in clearance,
                in markers,
                false);
        }
    }
}
