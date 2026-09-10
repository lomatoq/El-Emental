using Elemental.Simulation.Time;
using NUnit.Framework;
using Unity.Mathematics;
namespace Elemental.Tests.EditMode
{
    public sealed class CelestialDuskContinuityTests
    {
        [Test] public void PositiveLateAfternoonSunRetainsDaylightAndHorizonRemainsTwilight()
        {
            Assert.That(CelestialDayNightCycle.SolarStrength(.08f),Is.EqualTo(1f).Within(.0001f));
            Assert.That(CelestialDayNightCycle.SolarStrength(.04f),Is.GreaterThan(.75f));
            Assert.That(CelestialDayNightCycle.SolarStrength(0f),Is.InRange(.3f,.5f));
            Assert.That(Night(.04f),Is.LessThan(.05f));
            Assert.That(Night(0f),Is.InRange(.1f,.25f));
        }
        [Test] public void EntireDuskRampIsContinuousAndKeepsNoonAndDeepNightEndpoints()
        {
            float previousSun=CelestialDayNightCycle.SolarStrength(-.25f),previousNight=Night(-.25f);
            for(int i=1;i<=5000;i++)
            {
                float altitude=-.25f+i*.0001f;
                float sun=CelestialDayNightCycle.SolarStrength(altitude),night=Night(altitude);
                Assert.That(sun,Is.InRange(previousSun-1e-6f,previousSun+.0011f));
                Assert.That(night,Is.InRange(previousNight-.0007f,previousNight+1e-6f));
                previousSun=sun;previousNight=night;
            }
            Assert.That(CelestialDayNightCycle.SolarStrength(-.1f),Is.Zero);
            Assert.That(CelestialDayNightCycle.SolarStrength(.8f),Is.EqualTo(1));
            Assert.That(Night(-1),Is.EqualTo(1));Assert.That(Night(1),Is.Zero);
        }
        private static float Night(float altitude)=>CelestialDayNightCycle.Night(
            new float3(math.sqrt(math.max(0,1-altitude*altitude)),altitude,0),math.up());
    }
}
