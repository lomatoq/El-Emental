using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthCharacterFeelTests
    {
        [Test]
        public void CastTimingTraversesRootLoadStrikeSustainAndRecover()
        {
            var timing = new EarthCastTiming(12, 6, 10, 0.4f);
            Assert.That(EarthCastPhaseSolver.Evaluate(0, in timing, false), Is.EqualTo(EarthCastPhase.Acquire));
            Assert.That(EarthCastPhaseSolver.Evaluate(4, in timing, false), Is.EqualTo(EarthCastPhase.Root));
            Assert.That(EarthCastPhaseSolver.Evaluate(9, in timing, false), Is.EqualTo(EarthCastPhase.Load));
            Assert.That(EarthCastPhaseSolver.Evaluate(timing.ContactTick, in timing, false), Is.EqualTo(EarthCastPhase.Strike));
            Assert.That(EarthCastPhaseSolver.Evaluate(17, in timing, true), Is.EqualTo(EarthCastPhase.Sustain));
            Assert.That(EarthCastPhaseSolver.Evaluate(20, in timing, false), Is.EqualTo(EarthCastPhase.Recover));
            Assert.That(EarthCastPhaseSolver.Evaluate(29, in timing, false), Is.EqualTo(EarthCastPhase.Idle));
        }

        [Test]
        public void HeavyMassProducesMoreEffortBraceAndCompression()
        {
            EarthPoseIntent light = EarthPoseSolver.Solve(
                EarthTechniqueKind.Grip, EarthCastPhase.Sustain,
                new float3(0.2f, 0f, 1f), new float3(1f, 0f, 3f),
                8f, 2f, 0.3f, true);
            EarthPoseIntent heavy = EarthPoseSolver.Solve(
                EarthTechniqueKind.Grip, EarthCastPhase.Sustain,
                new float3(0.2f, 0f, 1f), new float3(1f, 0f, 3f),
                900f, 20f, 1f, true);

            Assert.That(heavy.Effort01, Is.GreaterThan(light.Effort01 + 0.25f));
            Assert.That(heavy.Brace01, Is.GreaterThan(light.Brace01));
            Assert.That(heavy.PelvisCompression01, Is.GreaterThan(light.PelvisCompression01));
            Assert.That(heavy.LocksFeet, Is.True);
        }

        [Test]
        public void AirbornePoseCannotClaimAPlantedBrace()
        {
            EarthPoseIntent airborne = EarthPoseSolver.Solve(
                EarthTechniqueKind.GroundWave, EarthCastPhase.Strike,
                new float3(0f, 0f, 1f), float3.zero,
                500f, 20f, 1f, false);
            Assert.That(airborne.Brace01, Is.Zero);
            Assert.That(airborne.Effort01, Is.LessThan(0.6f));
        }

        [Test]
        public void LockedFootIgnoresAnimationSlideUntilLockReleases()
        {
            EarthFootPlantResult first = EarthFootPlantSolver.Solve(
                new float3(0f, 1f, 0f), true, float3.zero, new float3(0f, 1f, 0f),
                new float3(0f, 1f, 0f), true, true, false, float3.zero, 0.04f);
            EarthFootPlantResult moved = EarthFootPlantSolver.Solve(
                new float3(0.5f, 1f, 0.4f), true, new float3(0.5f, 0f, 0.4f), new float3(0f, 1f, 0f),
                new float3(0f, 1f, 0f), true, true, first.Locked, first.Position, 0.04f);
            EarthFootPlantResult released = EarthFootPlantSolver.Solve(
                new float3(0.5f, 1f, 0.4f), true, new float3(0.5f, 0f, 0.4f), new float3(0f, 1f, 0f),
                new float3(0f, 1f, 0f), true, false, moved.Locked, moved.Position, 0.04f);

            Assert.That(moved.Position, Is.EqualTo(first.Position));
            Assert.That(released.Locked, Is.False);
            Assert.That(math.distance(released.Position, first.Position), Is.GreaterThan(0.5f));
        }

        [Test]
        public void JumpBufferAndCoyoteWindowCanMeetOnLaterTick()
        {
            PlanetJumpWindowState state = default;
            state = state.Step(false, true, 6, 7);
            Assert.That(state.CanConsume, Is.False);
            state = state.Step(true, false, 6, 7);
            Assert.That(state.CanConsume, Is.True);
            Assert.That(state.Consume().CanConsume, Is.False);

            state = new PlanetJumpWindowState(3, 0);
            state = state.Step(false, true, 6, 7);
            Assert.That(state.CanConsume, Is.True);
        }

        [Test]
        public void PelvisCompensationIsBoundedAndUsesLowestSupport()
        {
            float offset = EarthPelvisCompensation.Solve(-0.08f, 0.03f, 0.5f, 0.22f);
            Assert.That(offset, Is.LessThan(-0.08f));
            Assert.That(offset, Is.GreaterThanOrEqualTo(-0.22f));
            Assert.That(EarthPelvisCompensation.Solve(-2f, -1f, 1f, 0.22f), Is.EqualTo(-0.22f));
        }
    }
}
