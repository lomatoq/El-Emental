using Elemental.Simulation.Bending;
using NUnit.Framework;
namespace Elemental.Tests.EditMode
{
    public sealed class EarthWallPushMotionTests
    {
        [Test] public void FullChargeHasLargerBoundedLaunchAndTrailFeedback()
        {
            foreach(bool launch in new[]{false,true})
            {
                Assert.That(EarthWallPushMotion.DustCount(1,launch),Is.GreaterThan(EarthWallPushMotion.DustCount(0,launch)*2));
                Assert.That(EarthWallPushMotion.ChipCount(1,launch),Is.GreaterThan(EarthWallPushMotion.ChipCount(0,launch)*2));
                Assert.That(EarthWallPushMotion.DustCount(10,launch),Is.EqualTo(EarthWallPushMotion.DustCount(1,launch)));
            }
        }
        [Test] public void ChargeCannotProduceMotionBeforeRelease()
        {
            var p=new EarthWallPushMotion();Assert.That(p.Begin(1800),Is.Zero);
            for(int i=0;i<100;i++)Assert.That(p.Step(.02f),Is.Zero);
            Assert.That(p.Charge01,Is.EqualTo(1));Assert.That(p.Release(),Is.EqualTo(60000));
            Assert.That(p.Release(),Is.Zero);Assert.That(p.Active,Is.False);
        }
        [TestCase(120f)] [TestCase(800f)] [TestCase(1800f)]
        public void TapAndFullyChargedReleaseHaveDistinctMassDependentImpulse(float mass)
        {
            var tap=new EarthWallPushMotion();var hold=new EarthWallPushMotion();
            tap.Begin(mass);hold.Begin(mass);tap.Step(.06f);hold.Step(1.5f);
            float ordinary=tap.Release();
            Assert.That(ordinary,Is.EqualTo(System.Math.Min(24000,mass*18)));
            Assert.That(hold.Release(),Is.EqualTo(ordinary*2.5f).Within(.01f));
        }
        [Test] public void CancelDiscardsChargeAndRepeatedBeginDoesNotResetIt()
        {
            var p=new EarthWallPushMotion();p.Begin(1800);p.Step(.6f);
            float charge=p.Charge01;p.Begin(800);Assert.That(p.Charge01,Is.EqualTo(charge));
            p.Cancel();Assert.That(p.Release(),Is.Zero);Assert.That(p.Step(1),Is.Zero);
            p.Begin(1800);Assert.That(p.Charge01,Is.Zero);Assert.That(p.Release(),Is.EqualTo(24000));
        }
        [Test] public void ChargeIsTimePartitionInvariantAndIgnoresInvalidTime()
        {
            var a=new EarthWallPushMotion();var b=new EarthWallPushMotion();a.Begin(1800);b.Begin(1800);
            for(int i=0;i<30;i++)a.Step(.02f);b.Step(.6f);
            a.Step(float.NaN);a.Step(float.PositiveInfinity);a.Step(-1);
            Assert.That(a.Release(),Is.EqualTo(b.Release()).Within(.01f));
        }
    }
}
