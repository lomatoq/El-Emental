using Elemental.Simulation.Bending;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthStoneCounterPolicyTests
    {
        [TestCase(.2f, 0, EarthStoneCounterTier.Small)]
        [TestCase(.35f, 0, EarthStoneCounterTier.Small)]
        [TestCase(.7f, 2, EarthStoneCounterTier.Medium)]
        [TestCase(1.2f, 2, EarthStoneCounterTier.Medium)]
        [TestCase(1.6f, 3, EarthStoneCounterTier.Large)]
        public void ExistingSizeBandsChooseDustTwoOrThree(float radius, int pieces, EarthStoneCounterTier tier)
        {
            var d = EarthStoneCounterPolicy.Evaluate(new float3(0, 0, 1.5f), new float3(0, 0, -12),
                new float3(0, 0, 1), radius, 80, 75, .02f);
            Assert.That(d.Accepted, Is.True); Assert.That(d.Tier, Is.EqualTo(tier));
            Assert.That(d.Fracture.PhysicalPieces, Is.EqualTo(pieces));
            Assert.That(d.Fracture.DustCount, Is.GreaterThan(0));
            Assert.That(d.RecoilSpeed > 0, Is.EqualTo(tier == EarthStoneCounterTier.Large));
        }
        [TestCase(0, 0, -1, 0, 0, -15)]
        [TestCase(0, 0, 1, 0, 0, 15)]
        [TestCase(0, 0, 1, 0, 0, 0)]
        [TestCase(3, 0, 1, 0, 0, -15)]
        [TestCase(0, 3, 1, 0, 0, -15)]
        [TestCase(0, 0, 6, 0, 0, -15)]
        public void RearOutgoingIdleSideOverheadAndRemoteReject(float x,float y,float z,float vx,float vy,float vz)
        {
            Assert.That(EarthStoneCounterPolicy.Evaluate(new float3(x,y,z),new float3(vx,vy,vz),
                new float3(0,0,1),.2f,20,75,.02f).Accepted,Is.False);
        }
        [Test]
        public void SweepCatchesNextTickAndRecoilIsBounded()
        {
            var d = EarthStoneCounterPolicy.Evaluate(new float3(0,0,3.8f),new float3(0,0,-120),
                new float3(0,0,1),1.6f,2000,75,.02f);
            Assert.That(d.Accepted,Is.True); Assert.That(d.InterceptSeconds,Is.InRange(0f,.02f));
            Assert.That(d.RecoilSpeed,Is.EqualTo(EarthStoneCounterPolicy.MaximumRecoilSpeed));
        }
        [Test]
        public void InvalidNumbersCannotAdmit()
        {
            Assert.That(EarthStoneCounterPolicy.Evaluate(new float3(float.NaN,0,1),new float3(0,0,-12),
                new float3(0,0,1),.2f,20,75,.02f).Accepted,Is.False);
        }
        [Test]
        public void HeldChordOwnsHandsAndJumpWhileMovementSurvivesThenReleases()
        {
            var router = new EarthActionRouter();
            var begin = router.Step(new EarthActionRouterFrame(0, jumpPressed:true,jumpHeld:true,
                moveForward:1,wallPushModifierHeld:true,primaryHeld:true));
            Assert.That(begin.Owner,Is.EqualTo(EarthActionOwner.StoneCounter));
            Assert.That(begin.Consumes(EarthInputConsumption.Jump),Is.True);
            Assert.That(begin.Consumes(EarthInputConsumption.Primary),Is.True);
            Assert.That(begin.Consumes(EarthInputConsumption.Move),Is.False);
            Assert.That(router.Step(new EarthActionRouterFrame(.1f,jumpHeld:true,wallPushModifierHeld:true)).Phase,
                Is.EqualTo(EarthActionRoutePhase.Continue));
            Assert.That(router.Step(new EarthActionRouterFrame(.2f,jumpHeld:true)).Phase,
                Is.EqualTo(EarthActionRoutePhase.Cancel));
            Assert.That(router.Owner,Is.EqualTo(EarthActionOwner.None));
        }
    }
}
