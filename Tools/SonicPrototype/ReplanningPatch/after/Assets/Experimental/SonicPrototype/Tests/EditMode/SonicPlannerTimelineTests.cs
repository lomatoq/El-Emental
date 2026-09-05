using NUnit.Framework;

namespace Elemental.Experimental.SonicPrototype.Tests
{
    public sealed class SonicPlannerTimelineTests
    {
        [Test]
        public void ContextStartsAtOfficialLookAheadAndSamplesFourFutureFrames()
        {
            float start = SonicPlannerTimeline.ContextStartFrame(9f);
            Assert.That(start, Is.EqualTo(10.2f).Within(.0001f));
            Assert.That(SonicPlannerTimeline.ContextFrame(start, 0),
                Is.EqualTo(10.2f).Within(.0001f));
            Assert.That(SonicPlannerTimeline.ContextFrame(start, 3),
                Is.EqualTo(13.2f).Within(.0001f));
        }

        [Test]
        public void AcceptedPlanConsumesElapsedPrefixInsteadOfRestartingAtZero()
        {
            float contextStart = SonicPlannerTimeline.ContextStartFrame(9f);
            Assert.That(SonicPlannerTimeline.IncomingFrameAtAcceptance(12.6f, contextStart),
                Is.EqualTo(2.4f).Within(.0001f));
            Assert.That(SonicPlannerTimeline.IncomingFrameAtAcceptance(9.5f, contextStart),
                Is.Zero);
        }

        [Test]
        public void ReplanCadenceUsesOfficialModeIntervalsAndProtectsShortBuffers()
        {
            Assert.That(SonicPlannerTimeline.PeriodicReplanSeconds(
                SonicPreviewMode.Run, .1f), Is.EqualTo(.1f).Within(.0001f));
            Assert.That(SonicPlannerTimeline.PeriodicReplanSeconds(
                SonicPreviewMode.Walk, .1f), Is.EqualTo(1f).Within(.0001f));
            Assert.That(SonicPlannerTimeline.PeriodicReplanSeconds(
                SonicPreviewMode.RandomPunches, .1f), Is.EqualTo(1f).Within(.0001f));
            Assert.That(SonicPlannerTimeline.ShouldReplan(.5f, 1f, 24, 14f), Is.False);
            Assert.That(SonicPlannerTimeline.ShouldReplan(.5f, 1f, 24, 15f), Is.True);
            Assert.That(SonicPlannerTimeline.ShouldReplan(1f, 1f, 64, 12f), Is.True);
        }

        [Test]
        public void PlannerMayChooseAnyOfficialSixToSixteenTokenHorizon()
        {
            var allowed = new int[11];
            SonicPlannerTimeline.AllowAllPredictionHorizons(allowed);
            Assert.That(allowed, Is.All.EqualTo(1));
        }
    }
}
