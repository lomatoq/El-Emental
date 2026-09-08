using Elemental.Simulation.Characters;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthWallBraceStateTests
    {
        [Test]
        public void BriefTouchCannotAcquireHands()
        {
            var state = new EarthWallBraceState();
            state.Step(true, true, 1f, 0f, .06f);
            Assert.That(state.Weight, Is.Zero);
            state.Step(true, false, 1f, 0f, .06f);
            Assert.That(state.ContactSeconds, Is.Zero);
        }
        [Test]
        public void StableBlockedIntentAcquiresAndReleaseIsBounded()
        {
            var state = new EarthWallBraceState();
            for (int i = 0; i < 30; i++) state.Step(true, true, 1f, 0f, .02f);
            Assert.That(state.Weight, Is.EqualTo(1f));
            state.Step(false, true, 1f, 0f, .02f);
            Assert.That(state.Weight, Is.InRange(.8f, .9f));
            for (int i = 0; i < 8; i++) state.Step(false, true, 1f, 0f, .02f);
            Assert.That(state.Weight, Is.Zero);
        }
        [TestCase(false, true, 1f, 0f)]
        [TestCase(true, false, 1f, 0f)]
        [TestCase(true, true, 0f, 0f)]
        [TestCase(true, true, -1f, 0f)]
        [TestCase(true, true, 1f, 2f)]
        public void NonBlockedOrOwnedMotionCannotBrace(bool permitted, bool contact, float intent, float speed)
        {
            var state = new EarthWallBraceState();
            for (int i = 0; i < 30; i++) state.Step(permitted, contact, intent, speed, .02f);
            Assert.That(state.Weight, Is.Zero);
        }
    }
}
