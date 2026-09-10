using System;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class GoldRespawnTimelineTests
    {
        private static EarthRespawnCue Cue(double begin = 2.8, uint revision = 1) =>
            new(EarthDuelFighterId.Player, 3, revision, float3.zero, quaternion.identity,
                float3.zero, new float3(0, 1, 0), begin, 3.5);
        [TestCase(30), TestCase(60), TestCase(120)]
        public void TerminalRevealNeverExtendsExistingKnockoutOrSurvivesDeadline(int fps)
        {
            var cue = Cue(); var state = EarthDuelRespawnSolver.KnockOut(3.5f);
            double time = 0; int respawns = 0; bool proxySeen = false;
            for (int index = 0; index <= fps * 4; index++)
            {
                time = index / (double)fps;
                var frame = GoldRespawnTimeline.Sample(in cue, time, false);
                Assert.That(frame.Scale, Is.EqualTo(1)); Assert.That(frame.Lift,Is.Zero); Assert.That(frame.Emission, Is.InRange(0, 1));
                if (time < 2.8 || time >= 3.5) Assert.That(frame.Visible, Is.False);
                if (time < 3.2) Assert.That(frame.ShowProxy, Is.False);
                proxySeen |= frame.ShowProxy;
                var step = EarthDuelRespawnSolver.Step(in state, 1f / fps); state = step.State;
                if (step.RespawnThisTick) { respawns++; Assert.That(time + 1d / fps, Is.InRange(3.4999, 3.5 + 1d / fps + .0001)); }
            }
            Assert.That(respawns, Is.EqualTo(1)); Assert.That(proxySeen, Is.True);
        }
        [Test]
        public void LateReplacementShortensRevealAndNeverReplaysAfterActiveDeadline()
        {
            var cue = Cue(3.35, 2);
            Assert.That(GoldRespawnTimeline.Sample(in cue, 3.34, false).Visible, Is.False);
            Assert.That(GoldRespawnTimeline.Sample(in cue, 3.36, false).ShowProxy, Is.False);
            Assert.That(GoldRespawnTimeline.Sample(in cue, 3.48, false).ShowProxy, Is.True);
            Assert.That(GoldRespawnTimeline.Sample(in cue, 3.5, false).Visible, Is.False);
            Assert.That(GoldRespawnTimeline.Sample(in cue, 7, false).Visible, Is.False);
        }
        [Test]
        public void PausedClockIsIdempotentAndReducedMotionBoundsBrightnessAndOvershoot()
        {
            var cue = Cue(); var held = GoldRespawnTimeline.Sample(in cue, 3.3, false);
            for (int index = 0; index < 100; index++)
            {
                var repeated = GoldRespawnTimeline.Sample(in cue, 3.3, false);
                Assert.That(repeated.Scale, Is.EqualTo(held.Scale)); Assert.That(repeated.Lift, Is.EqualTo(held.Lift));
                var reduced = GoldRespawnTimeline.Sample(in cue, 2.8 + .007 * index, true);
                Assert.That(reduced.Scale, Is.LessThanOrEqualTo(1)); Assert.That(reduced.Emission, Is.LessThanOrEqualTo(.6f));
                Assert.That(reduced.RingAlpha, Is.LessThanOrEqualTo(.55f));
            }
        }
        [Test]
        public void InitialSpawnAndInvalidReservationsCannotProduceVisuals()
        {
            EarthRespawnCue cue = default;
            Assert.That(GoldRespawnTimeline.Sample(in cue, 0, false).Visible, Is.False);
            Assert.Throws<ArgumentException>(() => new EarthRespawnCue(EarthDuelFighterId.Player, 0, 1,
                float3.zero, quaternion.identity, float3.zero, new float3(0, 1, 0), 0, .7));
            Assert.Throws<ArgumentException>(() => new EarthRespawnCue(EarthDuelFighterId.Player, 1, 1,
                new float3(float.NaN), quaternion.identity, float3.zero, new float3(0, 1, 0), 0, .7));
        }
    }
}
