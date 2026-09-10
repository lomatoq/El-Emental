using Elemental.Simulation.Fire;
using NUnit.Framework;
using Unity.Mathematics;
namespace Elemental.Tests.EditMode
{
    public sealed class FireWeaveControlsTests
    {
        [Test] public void FullSphereNeedsAnotherUpwardDetentToReleaseWave()
        {
            var c=new FireWeaveControls();
            for(int i=0;i<8;i++)Assert.That(c.Step(false,false,true,120,false,true).SphereBurst,Is.False);
            Assert.That(c.Level,Is.EqualTo(12));
            Assert.That(c.Step(false,false,true,0,false,true).SphereBurst,Is.False);
            Assert.That(c.Step(false,false,true,120,false,true).SphereBurst,Is.True);
            Assert.That(c.Step(false,false,true,-120,false,true).SphereBurst,Is.False);
            Assert.That(c.Step(false,false,true,120,false,false).SphereBurst,Is.False);
            c.Interrupt();Assert.That(c.Step(false,false,true,120,false,true).SphereBurst,Is.False);
        }
        [Test] public void SphericalWaveExpandsAndDeceleratesWithinFiniteLifetime()
        {
            Assert.That(FireSphereWave.Radius(0),Is.EqualTo(2.3f).Within(.001));
            Assert.That(FireSphereWave.Radius(10),Is.EqualTo(9).Within(.001));
            float early=FireSphereWave.Radius(.2f)-FireSphereWave.Radius(.1f);
            float late=FireSphereWave.Radius(.8f)-FireSphereWave.Radius(.7f);
            Assert.That(early,Is.GreaterThan(late));Assert.That(late,Is.GreaterThan(0));
        }
        [Test] public void MiddleOwnsStreamAndWheelDoesNotChangeIdleForm()
        {var c=new FireWeaveControls();Assert.That(c.Step(true,false,false,120,false,false).Draw,Is.True);Assert.That(c.Level,Is.EqualTo(4));Assert.That(c.Step(false,false,true,120,false,true).Stream,Is.True);Assert.That(c.Level,Is.EqualTo(5));}
        [Test] public void PlainMiddleIsReservedAndDoesNotEmitOrChangePower()
        {var c=new FireWeaveControls();var a=c.Step(false,false,true,120,false,false);Assert.That(a.Stream||a.RapidShot||a.Drill,Is.False);Assert.That(c.Level,Is.EqualTo(4));}
        [Test] public void WheelTraversesAllFormsAndClamps()
        {var c=new FireWeaveControls();for(int i=0;i<8;i++)c.Step(false,false,true,-120,false,true);Assert.That(c.Form,Is.EqualTo(FireWeaveForm.Jet));for(int i=0;i<7;i++)c.Step(false,false,true,120,false,true);Assert.That(c.Form,Is.EqualTo(FireWeaveForm.Orbit));for(int i=0;i<20;i++)c.Step(false,false,true,120,false,true);Assert.That(c.Form,Is.EqualTo(FireWeaveForm.Sphere));Assert.That(c.Power01,Is.EqualTo(1));}
        [Test] public void ModifierShotsAreEdgesAndReleasingMiddleCannotStartDraw()
        {var c=new FireWeaveControls();var a=c.Step(true,true,true,0,false,true);Assert.That(a.RapidShot&&a.Drill,Is.True);Assert.That(a.Draw||a.ChargeBegin,Is.False);a=c.Step(true,true,true,0,false,true);Assert.That(a.RapidShot||a.Drill,Is.False);a=c.Step(true,false,false,0,false,false);Assert.That(a.Draw||a.ChargeRelease,Is.False);}
        [Test] public void ChargedBoltReleasesOnceAndMiddleCancelsWithoutDrill()
        {var c=new FireWeaveControls();Assert.That(c.Step(false,true,false,0,false,false).ChargeBegin,Is.True);Assert.That(c.Step(false,false,false,0,false,false).ChargeRelease,Is.True);Assert.That(c.Step(false,false,false,0,false,false).ChargeRelease,Is.False);c.Step(false,true,false,0,false,false);var a=c.Step(false,true,true,0,false,true);Assert.That(a.ChargeCancel,Is.True);Assert.That(a.Drill||a.ChargeRelease,Is.False);}
        [Test] public void InterruptRequiresAllButtonsReleasedAndRetainsPower()
        {var c=new FireWeaveControls();c.Step(false,false,true,120,false,true);c.Interrupt();for(int i=0;i<4;i++)Assert.That(c.Step(true,true,true,120,true,false).Flight,Is.False);Assert.That(c.Level,Is.EqualTo(5));c.Step(false,false,false,0,false,false);Assert.That(c.Step(false,false,true,0,false,true).Stream,Is.True);}
        [Test] public void ShiftSpaceStillHoldsRingUntilRelease()
        {var c=new FireWeaveControls();Assert.That(c.Step(false,false,false,0,true,true).Ring,Is.True);for(int i=0;i<50;i++)Assert.That(c.Step(false,false,false,0,true,true).RingRelease,Is.False);Assert.That(c.Step(false,false,false,0,false,true).RingRelease,Is.True);}
        [Test] public void DefaultPowerRetainsAcceptedStreamAndExtremesAreMonotonic()
        {Assert.That(FireWeaveTuning.StreamPower(new FireWeaveControls().Power01),Is.EqualTo(1).Within(.001));Assert.That(FireWeaveTuning.StreamPower(0),Is.LessThan(1));Assert.That(FireWeaveTuning.StreamPower(.5f),Is.GreaterThan(2));}
        [Test] public void CatalystUsesSweptPathAndCooldownRatherThanEndpointOnly()
        {float3 a=new float3(-3,0,0),b=new float3(3,0,0);Assert.That(FireWeaveTuning.CanReact(2,0,float3.zero,a,b,.5f),Is.True);Assert.That(FireWeaveTuning.CanReact(2,1.9f,float3.zero,a,b,.5f),Is.False);Assert.That(FireWeaveTuning.CanReact(2,0,new float3(0,1,0),a,b,.5f),Is.False);}
        [Test] public void CatalystRejectsNonfiniteAndHandlesStationaryStream()
        {Assert.That(FireWeaveTuning.CanReact(2,0,float3.zero,float3.zero,float3.zero,.5f),Is.True);Assert.That(FireWeaveTuning.CanReact(2,0,new float3(float.NaN),float3.zero,float3.zero,.5f),Is.False);}
    }
}
