using Elemental.Simulation.Characters;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthTurnStepSequenceTests
    {
        [TestCase(-1f)] [TestCase(1f)]
        public void ShortTapCompletesVisibleClipAtFullDirectionalWeight(float direction)
        {
            var state = new EarthTurnStepState();
            EarthTurnStepSequence.Step(ref state, direction, 0, true, false, 0);
            EarthTurnStepSequence.Step(ref state, 0, 0, true, true, .04f);
            EarthTurnStepSequence.Step(ref state, 0, 0, true, true, .84f);
            Assert.That(state.Active, Is.True); Assert.That(state.Direction, Is.EqualTo(direction));
            EarthTurnStepSequence.Step(ref state, 0, 0, true, true, 1.01f);
            Assert.That(state.Active, Is.False);
        }
        [Test]
        public void SustainedTurnRepeatsAndReversalChangesAtFootstepBoundary()
        {
            var state = new EarthTurnStepState();
            EarthTurnStepSequence.Step(ref state, 1, 0, true, true, .01f);
            EarthTurnStepSequence.Step(ref state, -1, 0, true, true, .6f);
            Assert.That(state.Direction, Is.EqualTo(1));
            EarthTurnStepSequence.Step(ref state, -1, 0, true, true, 1.02f);
            Assert.That(state.Direction, Is.EqualTo(-1)); Assert.That(state.Active, Is.True);
            EarthTurnStepSequence.Step(ref state, -1, 0, true, true, 2.02f);
            Assert.That(state.CycleEnd, Is.EqualTo(3));
        }
        [Test]
        public void MovementJumpAndImpactEligibilityInterruptWithoutWaiting()
        {
            var state = new EarthTurnStepState();
            EarthTurnStepSequence.Step(ref state, 1, 0, true, true, 0);
            EarthTurnStepSequence.Step(ref state, 1, 0, false, true, .2f);
            Assert.That(state.Active, Is.False);
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void SlowRealBotYawTriggersAtTheSameAngleAcrossRenderRates(int fps)
        {
            var state = new EarthTurnStepState();
            float angle = 0f;
            for (int i = 0; i < fps && !state.Active; i++)
            {
                angle += 20f / fps;
                EarthTurnStepSequence.Step(ref state, 0, 20f / fps, true, false, 0, 1f / fps);
            }
            Assert.That(state.Active, Is.True);
            Assert.That(angle, Is.InRange(5f, 5f + 20f / fps + .001f));
        }

        [Test]
        public void RealUncommandedYawTriggersButAlternatingSupportNoiseDoesNot()
        {
            var state = new EarthTurnStepState();
            for (int i = 0; i < 20; i++) EarthTurnStepSequence.Step(ref state, 0, i % 2 == 0 ? 1 : -1, true, false, 0);
            Assert.That(state.Active, Is.False);
            for (int i = 0; i < 4; i++) EarthTurnStepSequence.Step(ref state, 0, 2, true, false, 0);
            Assert.That(state.Active, Is.True); Assert.That(state.Direction, Is.EqualTo(1));
        }
    }
}
