using Elemental.Simulation.Combat;
using Elemental.Simulation.Time;
using NUnit.Framework;
namespace Elemental.Tests.EditMode
{
    public sealed class MatchStageTimelineTests
    {
        [Test] public void IndependentRevealWindowsPreserveLastPoseAndButtonDelay()
        {
            Assert.That(MatchStageTimeline.Camera(.15f,false),Is.Zero);
            Assert.That(MatchStageTimeline.Camera(.65f,false),Is.EqualTo(1));
            Assert.That(MatchStageTimeline.Title(.25f,false),Is.Zero);
            Assert.That(MatchStageTimeline.Title(.55f,false),Is.EqualTo(1));
            Assert.That(MatchStageTimeline.Score(.40f,false),Is.Zero);
            Assert.That(MatchStageTimeline.Score(.70f,false),Is.EqualTo(1));
            Assert.That(MatchStageTimeline.Buttons(.65f,false),Is.Zero);
            Assert.That(MatchStageTimeline.Buttons(.90f,false),Is.EqualTo(1));
            for(int i=0;i<=100;i++)
            {float age=i*.01f;Assert.That(MatchStageTimeline.Buttons(age,false),Is.InRange(0f,1f));Assert.That(MatchStageTimeline.Title(age,false),Is.GreaterThanOrEqualTo(MatchStageTimeline.Score(age,false)));}
        }
        [TestCase(EarthDuelFighterId.Player,4,2,MatchStageOutcome.Victory)]
        [TestCase(EarthDuelFighterId.Bot,4,2,MatchStageOutcome.Defeat)]
        [TestCase(EarthDuelFighterId.Player,2,4,MatchStageOutcome.Defeat)]
        [TestCase(EarthDuelFighterId.Bot,2,4,MatchStageOutcome.Victory)]
        [TestCase(EarthDuelFighterId.Bot,4,4,MatchStageOutcome.Draw)]
        public void OutcomeUsesStableLocalId(EarthDuelFighterId local,int player,int bot,MatchStageOutcome expected)
        {Assert.That(MatchStageTimeline.Outcome(local,player,bot),Is.EqualTo(expected));}
        [Test] public void ReducedMotionUsesShortFadeWithoutDelayedCamera()
        {Assert.That(MatchStageTimeline.Camera(0,true),Is.Zero);Assert.That(MatchStageTimeline.Camera(.1f,true),Is.EqualTo(1));Assert.That(MatchStageTimeline.Buttons(.1f,true),Is.EqualTo(1));}
    }
}
