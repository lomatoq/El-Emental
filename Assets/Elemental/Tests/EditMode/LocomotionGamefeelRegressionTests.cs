using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class LocomotionGamefeelRegressionTests
    {
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void TurnFootReleasesAtLiftOffDespiteLingeringAuthoredContact(int fps)
        {
            EarthFootContactState left = default, right = default;
            var l = Foot(true, -.027f, 0f, .81f, fps);
            var r = Foot(false, -.027f, 0f, .2f, fps);
            EarthFootContactSolver.ResolvePair(ref left, ref right, in l, in r);
            Assert.That(left.Locked, Is.True);
            // Imported turn confidence remains .65 even as the foot lifts.
            // Advance real seconds so the boundary is identical at every render rate.
            float lift = 0f;
            while (left.Locked && lift < .08f)
            {
                lift += .12f / fps;
                l = Foot(true, -.027f + lift, .12f, .65f, fps);
                EarthFootContactSolver.ResolvePair(ref left, ref right, in l, in r);
            }
            Assert.That(left.Locked, Is.False);
            Assert.That(lift, Is.LessThanOrEqualTo(.015f + .12f / fps + .00001f),
                "A planted turn accumulated a large offset before abruptly returning to its authored pose.");
        }

        [Test]
        public void RisingPivotCannotRecaptureAfterCooldownWhileFootIsStillAirborne()
        {
            EarthFootContactState left = default, right = default;
            var l = Foot(true, .06f, .08f, .8f, 60);
            var r = Foot(false, .02f, 0f, .9f, 60);
            var pair = EarthFootContactSolver.ResolvePair(ref left, ref right, in l, in r);
            Assert.That(pair.Left.Locked, Is.False);
            Assert.That(pair.Right.Locked, Is.True);
        }

        [Test]
        public void MillimetreStationaryTurnNoiseKeepsSupport()
        {
            EarthFootContactState left = default, right = default;
            var l = Foot(true, -.027f, 0f, .81f, 60);
            var r = Foot(false, -.027f, 0f, .2f, 60);
            EarthFootContactSolver.ResolvePair(ref left, ref right, in l, in r);
            l = Foot(true, -.025f, .1f, .81f, 60);
            EarthFootContactSolver.ResolvePair(ref left, ref right, in l, in r);
            Assert.That(left.Locked, Is.True);
        }

        [TestCase(3f)] [TestCase(7.2f)]
        public void RunCommandDoesNotInsertSlowWalkingFragment(float desired)
        {
            var state = new EarthShortTransitionState { Initialized = true, IdleSeconds = 1f };
            var input = new EarthShortTransitionInput { Grounded = true, TangentSpeed = .4f,
                ForwardSpeed = .4f, DesiredSpeed = desired, MaximumStartWalkSpeed = 2f };
            Assert.That(EarthShortTransitionPolicy.Step(ref state, in input, 1f / 60f).Kind,
                Is.EqualTo(EarthShortTransition.None));
        }

        [Test]
        public void WalkingAnticipationYieldsImmediatelyWhenPlayerAcceleratesIntoRun()
        {
            var state = new EarthShortTransitionState { Initialized = true, Active = EarthShortTransition.StartWalk };
            var input = new EarthShortTransitionInput { Grounded = true, TangentSpeed = 1f,
                ForwardSpeed = 1f, DesiredSpeed = 7.2f, MaximumStartWalkSpeed = 2f };
            Assert.That(EarthShortTransitionPolicy.Step(ref state, in input, 1f / 60f).Kind,
                Is.EqualTo(EarthShortTransition.None));
        }

        [Test]
        public void GradualAnalogStartKeepsRestLatchThroughAccelerationGap()
        {
            var state = new EarthShortTransitionState();
            var input = new EarthShortTransitionInput { Grounded = true, MaximumStartWalkSpeed = 2f };
            EarthShortTransitionPolicy.Step(ref state, in input, .25f);
            input.DesiredSpeed = 1.4f;
            foreach (float speed in new[] { .1f, .2f, .3f })
            {
                input.TangentSpeed = input.ForwardSpeed = speed;
                Assert.That(EarthShortTransitionPolicy.Step(ref state, in input, 1f / 60f).Kind,
                    Is.EqualTo(EarthShortTransition.None));
            }
            input.TangentSpeed = input.ForwardSpeed = .4f;
            Assert.That(EarthShortTransitionPolicy.Step(ref state, in input, 1f / 60f).Kind,
                Is.EqualTo(EarthShortTransition.StartWalk));
        }

        private static EarthFootContactInput Foot(bool left, float height, float velocity, float contact, int fps) =>
            new EarthFootContactInput(left, true, true, true, false, true, height, velocity,
                left ? 1f : 0f, 0f, new float3(left ? -.1f : .1f, 0f, 0f), math.up(),
                new float3(left ? -.1f : .1f, height, 0f), math.up(), 11u, 1u, 1f / fps, contact);
    }
}
