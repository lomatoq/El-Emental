using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthRagdollRecoveryPoseSolverTests
    {
        [Test]
        public void CurrentPelvisDeterminesRecoveryRootInsteadOfPreHitPose()
        {
            float3 pelvis = new float3(12f, 4f, -8f);
            float3 pelvisOffset = new float3(0f, 0.9f, 0f);
            EarthRagdollRecoveryPose pose = Resolve(
                pelvis,
                new float3(12f, 4.4f, -7.5f),
                new float3(0f, 0f, 1f),
                new float3(0f, 1f, 0f),
                pelvisOffset);

            float3 reconstructedPelvis = pose.RootPosition +
                                          math.rotate(pose.RootRotation, pelvisOffset);
            Assert.That(math.distance(reconstructedPelvis, pelvis), Is.LessThan(0.0001f));
            Assert.That(math.distance(pose.RootPosition, float3.zero), Is.GreaterThan(10f),
                "Recovery must not return to an unrelated pre-hit/spawn transform.");
        }

        [TestCase(1f, EarthRagdollRecoverySide.Back)]
        [TestCase(-1f, EarthRagdollRecoverySide.Front)]
        public void ChestOutwardClassifiesFrontAndBack(
            float outwardY,
            EarthRagdollRecoverySide expected)
        {
            EarthRagdollRecoveryPose pose = EarthRagdollRecoveryPoseSolver.Resolve(
                float3.zero,
                new float3(0f, 0f, 0.5f),
                new float3(0f, 0f, 1f),
                new float3(0f, 0f, 1f),
                new float3(0f, outwardY, 0f),
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 1f),
                float3.zero,
                0f,
                true);

            Assert.That(pose.Side, Is.EqualTo(expected));
        }

        [Test]
        public void DegenerateFacingUsesFiniteFallbackWithoutFlip()
        {
            EarthRagdollRecoveryPose pose = EarthRagdollRecoveryPoseSolver.Resolve(
                new float3(1f, 2f, 3f),
                new float3(1f, 2f, 3f),
                float3.zero,
                float3.zero,
                float3.zero,
                float3.zero,
                float3.zero,
                float3.zero,
                0f,
                true);

            Assert.That(math.all(math.isfinite(pose.RootPosition)), Is.True);
            Assert.That(math.all(math.isfinite(pose.RootRotation.value)), Is.True);
            Assert.That(pose.UsedFacingFallback, Is.True);
        }

        [Test]
        public void ActualFacingIsHemisphereAlignedToPreventOneEightyFlip()
        {
            EarthRagdollRecoveryPose pose = EarthRagdollRecoveryPoseSolver.Resolve(
                float3.zero,
                new float3(0f, 0f, -0.5f),
                new float3(0f, 0f, -1f),
                new float3(0f, 0f, -1f),
                new float3(0f, 1f, 0f),
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 1f),
                float3.zero,
                0f,
                true);
            float3 resolvedForward = math.rotate(
                pose.RootRotation,
                new float3(0f, 0f, 1f));

            Assert.That(math.dot(resolvedForward, new float3(0f, 0f, 1f)), Is.GreaterThan(0.99f));
        }

        [Test]
        public void ClearanceFailureUsesBoundedMaximumLiftFallback()
        {
            float lift = EarthRagdollRecoveryPoseSolver.SelectClearanceLift(
                false,
                false,
                false,
                out bool succeeded);

            Assert.That(succeeded, Is.False);
            Assert.That(lift, Is.EqualTo(
                EarthRagdollRecoveryPoseSolver.MaximumClearanceLiftMeters));
        }

        [Test]
        public void RepeatedRecoveryRequestIsIdempotent()
        {
            EarthRagdollRecoveryGateState gate = default;
            Assert.That(EarthRagdollRecoveryPoseSolver.TryConsumeRecoveryRequest(
                ref gate,
                true), Is.True);
            Assert.That(EarthRagdollRecoveryPoseSolver.TryConsumeRecoveryRequest(
                ref gate,
                true), Is.False);
        }

        private static EarthRagdollRecoveryPose Resolve(
            float3 pelvis,
            float3 chest,
            float3 forward,
            float3 chestOutward,
            float3 pelvisOffset) =>
            EarthRagdollRecoveryPoseSolver.Resolve(
                pelvis,
                chest,
                forward,
                forward,
                chestOutward,
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 1f),
                pelvisOffset,
                0f,
                true);
    }
}
