using System;
using Elemental.Simulation.Combat;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthDuelRespawnSolverTests
    {
        [Test]
        public void KnockOutStartsRequestedRespawnWindow()
        {
            EarthDuelFighterState state = EarthDuelRespawnSolver.KnockOut(3.5f);

            Assert.That(state.Phase, Is.EqualTo(EarthDuelFighterPhase.KnockedOut));
            Assert.That(state.RemainingSeconds, Is.EqualTo(3.5f));
        }

        [Test]
        public void StepKeepsFighterDownUntilDeadlineThenPulsesOnce()
        {
            EarthDuelFighterState state = EarthDuelRespawnSolver.KnockOut(3.5f);

            EarthDuelFighterStep beforeDeadline = EarthDuelRespawnSolver.Step(in state, 3.49f);
            Assert.That(beforeDeadline.State.Phase, Is.EqualTo(EarthDuelFighterPhase.KnockedOut));
            Assert.That(beforeDeadline.State.RemainingSeconds, Is.EqualTo(0.01f).Within(0.00001f));
            Assert.That(beforeDeadline.RespawnThisTick, Is.False);

            state = beforeDeadline.State;
            EarthDuelFighterStep atDeadline = EarthDuelRespawnSolver.Step(in state, 0.01f);
            Assert.That(atDeadline.State.Phase, Is.EqualTo(EarthDuelFighterPhase.Active));
            Assert.That(atDeadline.RespawnThisTick, Is.True);

            state = atDeadline.State;
            EarthDuelFighterStep afterRespawn = EarthDuelRespawnSolver.Step(in state, 1f / 60f);
            Assert.That(afterRespawn.State.Phase, Is.EqualTo(EarthDuelFighterPhase.Active));
            Assert.That(afterRespawn.RespawnThisTick, Is.False);
        }

        [Test]
        public void OvershootRespawnsWithoutNegativeTime()
        {
            EarthDuelFighterState state = EarthDuelRespawnSolver.KnockOut(3.5f);

            EarthDuelFighterStep step = EarthDuelRespawnSolver.Step(in state, 4f);

            Assert.That(step.State.Phase, Is.EqualTo(EarthDuelFighterPhase.Active));
            Assert.That(step.State.RemainingSeconds, Is.EqualTo(0f));
            Assert.That(step.RespawnThisTick, Is.True);
        }

        [Test]
        public void StoneFadeOccupiesOnlyTheFinalRecoveryWindow()
        {
            EarthDuelFighterState state = EarthDuelRespawnSolver.KnockOut(3.5f);

            EarthDuelFighterStep early = EarthDuelRespawnSolver.Step(in state, 3.0f, 0.35f);
            Assert.That(early.StoneFade01, Is.EqualTo(0f).Within(0.0001f));

            EarthDuelFighterState earlyState = early.State;
            EarthDuelFighterStep fading = EarthDuelRespawnSolver.Step(in earlyState, 0.325f, 0.35f);
            Assert.That(fading.StoneFade01, Is.EqualTo(0.5f).Within(0.001f));

            EarthDuelFighterState fadingState = fading.State;
            EarthDuelFighterStep respawn = EarthDuelRespawnSolver.Step(in fadingState, 0.2f, 0.35f);
            Assert.That(respawn.RespawnThisTick, Is.True);
            Assert.That(respawn.StoneFade01, Is.EqualTo(1f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidDurationsAreRejected(float value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EarthDuelRespawnSolver.KnockOut(value));
            EarthDuelFighterState state = EarthDuelFighterState.Active;
            Assert.Throws<ArgumentOutOfRangeException>(() => EarthDuelRespawnSolver.Step(in state, value));
        }
    }
}
