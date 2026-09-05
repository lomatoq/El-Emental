using Elemental.Simulation.Characters;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthLandingRollMotionTests
    {
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void TravelDecaysMonotonicallyAndHasTickIndependentDistance(int hz)
        {
            float distance = 0f, previous = float.MaxValue;
            float dt = 1f / hz;
            for (int i = 0; i < hz; i++)
            {
                float speed = EarthLandingRollMotion.AverageSpeed(4.5f, 0.72f, i * dt, dt);
                Assert.That(speed, Is.InRange(0f, previous + 0.0001f));
                distance += speed * dt;
                previous = speed;
            }
            Assert.That(distance, Is.EqualTo(1.08f).Within(0.0001f));
            Assert.That(previous, Is.Zero);
        }

        [TestCase(false, false, 3f, 0f, true)]
        [TestCase(false, false, 0.3f, 0f, false)]
        [TestCase(false, false, 3f, -3f, false)]
        [TestCase(true, false, 3f, 0f, false)]
        [TestCase(false, true, 3f, 0f, false)]
        public void OnlyEligibleConfirmedLandingStartsTravel(bool interrupted, bool jump,
            float drop, float forward, bool expected)
        {
            var state = new EarthLandingRollMotion();
            Step(ref state, true, 0f, 0f);
            Assert.That(state.Active, Is.False, "Startup is not a landing.");
            for (int i = 0; i < 20; i++) Step(ref state, false, -drop / 20f, forward);
            state.Step(true, interrupted, jump, 0f, 0f, forward, 0f, 0.72f, 4.5f, 7.2f, 0.02f);
            Assert.That(state.Active, Is.EqualTo(expected));
            Assert.That(state.Sequence, Is.EqualTo(expected ? 1u : 0u));
            for (int i = 0; i < 60; i++) Step(ref state, true, 0f, 0f);
            Assert.That(state.Active, Is.False);
            Assert.That(state.Sequence, Is.EqualTo(expected ? 1u : 0u), "Must not restart on each grounded tick.");
        }

        [TestCase(true, false, false)] [TestCase(false, true, false)] [TestCase(false, false, true)]
        public void SupportLossJumpAndActionInterruptCancelTravel(bool leaveSupport, bool jump, bool interrupted)
        {
            var state = new EarthLandingRollMotion();
            Step(ref state, true, 0f, 0f);
            for (int i = 0; i < 20; i++) Step(ref state, false, -0.15f, 0f);
            Step(ref state, true, 0f, 0f);
            Assert.That(state.Active, Is.True);
            state.Step(!leaveSupport, interrupted, jump, 0f, 0f, 0f, 0f, 0.72f, 4.5f, 7.2f, 0.02f);
            Assert.That(state.Active, Is.False);
            Assert.That(state.Speed, Is.Zero);
        }

        private static void Step(ref EarthLandingRollMotion state, bool grounded, float dy, float forward)
            => state.Step(grounded, false, false, dy, grounded ? 0f : -5f, forward, 0f,
                0.72f, 4.5f, 7.2f, 0.02f);
    }
}
