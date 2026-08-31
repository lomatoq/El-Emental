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
        public void OwnershipHandoffsAndRepeatedRecoveryRequestsAreIdempotent()
        {
            var coordinator = new EarthPhysicalAnimationCoordinator();
            EarthRecoveryResult result = ValidResult();

            Assert.That(coordinator.TryBeginFullRagdoll(1u), Is.True);
            Assert.That(coordinator.TryBeginFullRagdoll(1u), Is.False);
            Assert.That(coordinator.TryBeginFullRagdoll(2u), Is.False);
            Assert.That(coordinator.RagdollHandoffCount, Is.EqualTo(1));
            Assert.That(coordinator.TryBeginGetUp(1u, in result), Is.True);
            Assert.That(coordinator.TryBeginGetUp(1u, in result), Is.False);
            Assert.That(coordinator.RecoveryHandoffCount, Is.EqualTo(1));

            EarthPhysicalAnimationOwnership beforeSupport = coordinator.AdvanceGetUp(1f, false);
            Assert.That(beforeSupport.FeetEnabled, Is.False);
            Assert.That(beforeSupport.ControlsEnabled, Is.False);
            Assert.That(beforeSupport.RecoveryExitReady, Is.False);

            EarthPhysicalAnimationOwnership afterSupport = coordinator.AdvanceGetUp(1f, true);
            Assert.That(afterSupport.FeetEnabled, Is.True);
            Assert.That(afterSupport.ControlsEnabled, Is.True);
            Assert.That(afterSupport.RecoveryExitReady, Is.True);
            coordinator.CompleteGetUp();
            coordinator.CompleteGetUp();
            Assert.That(coordinator.Mode, Is.EqualTo(EarthPhysicalAnimationMode.Animated));
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

        private static EarthRecoveryResult ValidResult()
        {
            EarthRecoveryClearanceResult clearance =
                EarthRecoveryAlignmentSolver.SelectClearance(true, true, true);
            EarthRecoveryMarkerProfile markers = EarthRecoveryMarkerProfile.Default;
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
