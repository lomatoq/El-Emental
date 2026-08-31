using Elemental.Simulation.Combat;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthProjectileSurfaceContactSolverTests
    {
        [Test]
        public void TangentContactPreservesProjectileGameplayEffect()
        {
            EarthProjectileSurfaceContactState state = default;
            EarthProjectileSurfaceContactTuning tuning =
                EarthProjectileSurfaceContactTuning.Default;
            var sample = Sample(
                surfaceId: 41u,
                time: 1f,
                velocity: new float3(18f, -0.15f, 0f),
                normal: math.up(),
                clearance: 0f);

            EarthProjectileSurfaceContactResult result =
                EarthProjectileSurfaceContactSolver.Resolve(in state, in sample, in tuning);

            Assert.That(result.Decision,
                Is.EqualTo(EarthProjectileSurfaceContactDecision.Grazing));
            Assert.That(result.AcceptImpact, Is.False);
            Assert.That(result.PreserveTangentialTravel, Is.True);
            Assert.That(result.ApproachSpeed, Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(result.State.HasImpact, Is.False);
        }

        [Test]
        public void SpeculativeContactOutsidePhysicalClearanceIsNotAnImpact()
        {
            EarthProjectileSurfaceContactState state = default;
            EarthProjectileSurfaceContactTuning tuning =
                EarthProjectileSurfaceContactTuning.Default;
            var sample = Sample(
                surfaceId: 52u,
                time: 2f,
                velocity: new float3(0f, -14f, 0f),
                normal: math.up(),
                clearance: 0.09f,
                radius: 0.42f);

            EarthProjectileSurfaceContactResult result =
                EarthProjectileSurfaceContactSolver.Resolve(in state, in sample, in tuning);

            Assert.That(result.Decision,
                Is.EqualTo(EarthProjectileSurfaceContactDecision.OutsideClearance));
            Assert.That(result.AcceptImpact, Is.False);
            Assert.That(result.PreserveTangentialTravel, Is.True);
            Assert.That(result.State.HasImpact, Is.False);
        }

        [Test]
        public void DirectWallApproachIsAcceptedFromRelativeNormalSpeed()
        {
            EarthProjectileSurfaceContactState state = default;
            EarthProjectileSurfaceContactTuning tuning =
                EarthProjectileSurfaceContactTuning.Default;
            var sample = new EarthProjectileSurfaceContactSample(
                true,
                63u,
                3f,
                new float3(15f, 0f, 0f),
                new float3(1f, 0f, 0f),
                new float3(-1f, 0f, 0f),
                0f,
                0.42f);

            EarthProjectileSurfaceContactResult result =
                EarthProjectileSurfaceContactSolver.Resolve(in state, in sample, in tuning);

            Assert.That(result.Decision,
                Is.EqualTo(EarthProjectileSurfaceContactDecision.Impact));
            Assert.That(result.AcceptImpact, Is.True);
            Assert.That(result.ApproachSpeed, Is.EqualTo(14f).Within(0.0001f));
            Assert.That(result.State.LastImpactSurfaceId, Is.EqualTo(63u));
        }

        [Test]
        public void SameSurfaceContactEpisodeEmitsOneImpactButLaterImpactRearms()
        {
            EarthProjectileSurfaceContactTuning tuning =
                EarthProjectileSurfaceContactTuning.Default;
            EarthProjectileSurfaceContactState state = default;
            EarthProjectileSurfaceContactSample firstSample = Sample(
                74u, 4f, new float3(0f, -10f, 0f), math.up(), 0f);
            EarthProjectileSurfaceContactResult first =
                EarthProjectileSurfaceContactSolver.Resolve(in state, in firstSample, in tuning);

            EarthProjectileSurfaceContactSample duplicateSample = Sample(
                74u, 4.05f, new float3(0f, -9f, 0f), math.up(), 0f);
            EarthProjectileSurfaceContactState acceptedState = first.State;
            EarthProjectileSurfaceContactResult duplicate =
                EarthProjectileSurfaceContactSolver.Resolve(
                    in acceptedState,
                    in duplicateSample,
                    in tuning);

            EarthProjectileSurfaceContactSample laterSample = Sample(
                74u, 4.2f, new float3(0f, -8f, 0f), math.up(), 0f);
            EarthProjectileSurfaceContactResult later =
                EarthProjectileSurfaceContactSolver.Resolve(
                    in acceptedState,
                    in laterSample,
                    in tuning);

            Assert.That(first.Decision,
                Is.EqualTo(EarthProjectileSurfaceContactDecision.Impact));
            Assert.That(duplicate.Decision,
                Is.EqualTo(EarthProjectileSurfaceContactDecision.Duplicate));
            Assert.That(later.Decision,
                Is.EqualTo(EarthProjectileSurfaceContactDecision.Impact));
        }

        private static EarthProjectileSurfaceContactSample Sample(
            uint surfaceId,
            float time,
            float3 velocity,
            float3 normal,
            float clearance,
            float radius = 0.42f) =>
            new(
                true,
                surfaceId,
                time,
                velocity,
                float3.zero,
                normal,
                clearance,
                radius);
    }
}
