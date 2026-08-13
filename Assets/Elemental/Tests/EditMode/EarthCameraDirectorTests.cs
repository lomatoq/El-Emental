using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthCameraDirectorTests
    {
        [Test]
        public void StatePriorityKeepsImpactAndAirborneReadable()
        {
            var all = new EarthCameraContext(true, true, true, true, true, true, true, 1f);
            Assert.That(EarthCameraStateResolver.Resolve(in all), Is.EqualTo(EarthCameraState.Impact));

            var airborne = new EarthCameraContext(true, true, true, true, true, false, false, 1f);
            Assert.That(EarthCameraStateResolver.Resolve(in airborne), Is.EqualTo(EarthCameraState.Airborne));

            var heavy = new EarthCameraContext(true, true, false, false, false, false, false, 0.9f);
            Assert.That(EarthCameraStateResolver.Resolve(in heavy), Is.EqualTo(EarthCameraState.BendHeavy));
        }

        [Test]
        public void WeightedFocusCannotAbandonPlayerComposition()
        {
            var input = new EarthCameraFocusInput(
                float3.zero,
                new float3(0f, 0f, 100f),
                new float3(30f, 0f, 0f),
                new float3(-50f, 0f, 0f),
                1f, 1f, 1f, 1f);

            float3 focus = EarthCameraFocusSolver.Solve(in input, 7.5f);

            Assert.That(math.length(focus), Is.LessThanOrEqualTo(7.501f));
            Assert.That(math.all(math.isfinite(focus)), Is.True);
        }

        [Test]
        public void OcclusionPullsInQuicklyAndReleasesOnlyAfterHysteresis()
        {
            var state = new EarthCameraOcclusionState(7f, 0f);
            state = EarthCameraOcclusionSolver.Step(in state, 7f, 2f, true, 0.1f, 24f, 4f, 0.15f);
            Assert.That(state.Distance, Is.EqualTo(4.6f).Within(0.001f));

            EarthCameraOcclusionState waiting = EarthCameraOcclusionSolver.Step(
                in state, 7f, 7f, false, 0.1f, 24f, 4f, 0.15f);
            Assert.That(waiting.Distance, Is.EqualTo(state.Distance).Within(0.001f));

            EarthCameraOcclusionState releasing = EarthCameraOcclusionSolver.Step(
                in waiting, 7f, 7f, false, 0.1f, 24f, 4f, 0.15f);
            Assert.That(releasing.Distance, Is.GreaterThan(waiting.Distance));
            Assert.That(releasing.Distance - waiting.Distance, Is.LessThanOrEqualTo(0.401f));
        }

        [Test]
        public void ReducedMotionSuppressesFovAndStrongShake()
        {
            var full = new EarthCameraAccessibilitySettings(1f, 0.8f, 1f, false);
            var reduced = new EarthCameraAccessibilitySettings(1f, 0.8f, 1f, true);

            Assert.That(reduced.EffectiveShake, Is.LessThan(full.EffectiveShake));
            Assert.That(reduced.EffectiveLag, Is.LessThan(full.EffectiveLag));
            Assert.That(reduced.EffectiveFieldOfViewMotion, Is.Zero);
        }

        [Test]
        public void ShoulderSwapIsDeterministicAndDoesNotDrift()
        {
            float sign = EarthCameraShoulderSolver.Resolve(0f, false);
            Assert.That(sign, Is.EqualTo(1f));
            sign = EarthCameraShoulderSolver.Resolve(sign, true);
            Assert.That(sign, Is.EqualTo(-1f));
            Assert.That(EarthCameraShoulderSolver.Resolve(sign, false), Is.EqualTo(-1f));
            Assert.That(EarthCameraShoulderSolver.Resolve(sign, true), Is.EqualTo(1f));
        }
    }
}
