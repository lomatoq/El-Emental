using System;
using Elemental.Simulation.Fire;
using NUnit.Framework;
namespace Elemental.Tests.EditMode
{
    public sealed class FireChargedBoltProfileTests
    {
        [Test] public void FullHoldBuildsMuchLargerMassWithoutUnboundedHitRadius()
        {
            var tap=FireChargedBoltProfile.Evaluate(0);var full=FireChargedBoltProfile.Evaluate(1);
            Assert.That(full.VisualRadius,Is.GreaterThan(tap.VisualRadius*5));
            Assert.That(full.CollisionRadius,Is.EqualTo(.72f).Within(.0001));
            Assert.That(full.CollisionRadius,Is.LessThan(full.VisualRadius));
            Assert.That(full.FlameTongues,Is.GreaterThanOrEqualTo(tap.FlameTongues*2));
        }
        [Test] public void QuickTapPreservesSmallFastResponse()
        {
            var tap=FireChargedBoltProfile.Evaluate(.08f/FireWeaveTuning.ChargeSeconds);
            Assert.That(tap.Power,Is.InRange(.9f,1f));Assert.That(tap.VisualRadius,Is.LessThan(.20f));
            Assert.That(tap.Speed,Is.InRange(24f,25f));
        }
        [Test] public void GrowthIsMonotonicAndSaturatesWhileHeld()
        {
            float radius=0,power=0;
            for(int i=0;i<=160;i++){var p=FireChargedBoltProfile.Evaluate(i/100f);Assert.That(p.VisualRadius,Is.GreaterThanOrEqualTo(radius));Assert.That(p.Power,Is.GreaterThanOrEqualTo(power));radius=p.VisualRadius;power=p.Power;}
            Assert.That(FireChargedBoltProfile.Evaluate(100).Power,Is.EqualTo(FireChargedBoltProfile.Evaluate(1).Power));
        }
        [Test] public void ReleasedPowerRetainsHeldVisualEnvelopeAtEveryCharge()
        {
            for(int i=0;i<=60;i++){var held=FireChargedBoltProfile.Evaluate(i/60f);var flying=FireChargedBoltProfile.FromPower(held.Power);Assert.That(flying.VisualRadius,Is.EqualTo(held.VisualRadius).Within(.00001));Assert.That(flying.CollisionRadius,Is.EqualTo(held.CollisionRadius).Within(.00001));}
        }
        [Test] public void FineDetailAndCoolingFitExistingFortyEightParticleSeat()
        {
            for(int i=0;i<=60;i++){var p=FireChargedBoltProfile.Evaluate(i/60f);float resident=p.DetailRateForCapacity(48)*(p.TailHotSeconds+p.CoolingSeconds/4);Assert.That(resident,Is.LessThan(40));Assert.That(p.CoolingSeconds,Is.GreaterThanOrEqualTo(.32f));}
        }
        [Test] public void InvalidInputsCannotPoisonHotSimulation()
        {
            Assert.Throws<ArgumentException>(()=>FireChargedBoltProfile.Evaluate(float.NaN));
            Assert.Throws<ArgumentException>(()=>FireChargedBoltProfile.FromPower(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(()=>FireChargedBoltProfile.Evaluate(1).DetailRateForCapacity(0));
        }
    }
}
