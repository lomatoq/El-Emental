using Elemental.Simulation.Rendering;
using NUnit.Framework;
namespace Elemental.Tests.EditMode
{
    public sealed class DistantStormClockTests
    {
        [Test] public void SeededPulseIntervalsStayBoundedAndDisabledMeansNoPulseOrRumble()
        {
            var a=new DistantStormClock();var b=new DistantStormClock();int events=0,rumble=0;double elapsed=0,last=0;
            for(int i=0;i<72000;i++)
            {
                float power=a.Step(1f/120,true),copy=b.Step(1f/120,true);elapsed+=1.0/120;
                Assert.That(power,Is.EqualTo(copy));Assert.That(power,Is.InRange(0f,1f));
                if(a.Sequence>events){Assert.That(elapsed-last,Is.InRange(17.99f,45.02f));last=elapsed;events++;}
                if(a.RumbleDue)rumble++;
                Assert.That(a.NextInterval,Is.InRange(18f,45f));
            }
            Assert.That(events,Is.GreaterThan(10));Assert.That(rumble,Is.InRange(events-1,events));
            uint before=a.Sequence;
            for(int i=0;i<100;i++){Assert.That(a.Step(1,false),Is.Zero);Assert.That(a.RumbleDue,Is.False);}
            Assert.That(a.Sequence,Is.EqualTo(before));
        }
    }
}
