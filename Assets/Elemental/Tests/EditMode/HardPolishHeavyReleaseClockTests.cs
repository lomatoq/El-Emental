using System.Linq;
using Elemental.Presentation.Animation;
using Elemental.Simulation.Characters;
using NUnit.Framework;
namespace Elemental.Tests.EditMode
{
    public sealed class HardPolishHeavyReleaseClockTests
    {
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void ReleaseKeepsNativeAnticipationAndBoundedContactClock(int hz)
        {
            var entry=EarthMagicMotionProfile.CreateDefaults().Single(e=>(int)e.slot==4);
            var clock=new EarthMagicClipClock();float previous=entry.releaseStartNormalized;
            int reached=-1;
            for(int i=0;i<hz;i++)
            {
                float time=clock.Step(4,71,EarthCastPhase.Strike,true,in entry.timing,1f/hz,true,entry.releaseStartNormalized);
                Assert.That(time,Is.GreaterThanOrEqualTo(previous));Assert.That(time-previous,Is.LessThanOrEqualTo(.24f/hz+.00001f));
                if(time>=entry.timing.Contact-.00001f&&reached<0)reached=i+1;
                previous=time;
            }
            Assert.That(reached,Is.GreaterThan(0));
            Assert.That(reached/(float)hz,Is.InRange(.10f,.22f),"Visual entry must show the measured early forward push without source acceleration.");
            Assert.That(EarthMagicClipClock.MaximumSpeedForSlot(4)*4.3f,Is.LessThanOrEqualTo(1.033f));
        }
    }
}
