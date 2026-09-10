using Elemental.Simulation.Structures;
using NUnit.Framework;
namespace Elemental.Tests.EditMode
{
    public sealed class EarthArenaDominoAdmissionTests
    {
        [Test] public void AlternatingSourcesAndSaturationCannotEvictLiveCooldowns()
        {
            var window=new EarthImpactContactWindow();
            Assert.That(window.TryAdmit(1,1),Is.True);Assert.That(window.TryAdmit(2,1.01f),Is.True);
            Assert.That(window.TryAdmit(1,1.02f),Is.False);
            for(uint i=3;i<=32;i++)Assert.That(window.TryAdmit(i,1.03f),Is.True);
            Assert.That(window.TryAdmit(33,1.04f),Is.False);
            Assert.That(window.TryAdmit(1,1.04f),Is.False);
            Assert.That(window.TryAdmit(1,1.4f),Is.True);
            window.Clear();Assert.That(window.TryAdmit(1,1.4f),Is.True);
        }
        [Test] public void TangentialSpeedAndSolverSpikeCannotManufactureNormalDamage()
        {
            Assert.That(EarthArenaFractureGate.NormalContactImpulse(.1f,100,10000),Is.Zero);
            Assert.That(EarthArenaFractureGate.NormalContactImpulse(1f,2,10000),Is.EqualTo(4));
            Assert.That(EarthArenaFractureGate.NormalContactImpulse(10f,20,0),Is.EqualTo(200));
        }
        [TestCase(9.499f,false)] [TestCase(9.5f,true)] [TestCase(9.501f,true)]
        public void ProposedFloorHasAnExplicitBoundary(float strength,bool admitted)
            => Assert.That(EarthArenaFractureGate.IsMeaningfulDamage(strength,95f,.1f),Is.EqualTo(admitted));
        [Test] public void TinyContactRainDoesNotBecomeAnEventualColumnBreak()
        {
            EarthImpactDamage damage=default;
            for(int i=0;i<10000;i++)
                if(EarthArenaFractureGate.IsMeaningfulDamage(2f,95f,.1f))damage.Add(2f);
            Assert.That(damage.Impulse,Is.Zero);
        }
        [Test] public void RepeatedMeaningfulHitsStillAccumulateWithoutTurningHistoryIntoVelocity()
        {
            EarthImpactDamage damage=default;
            for(int i=0;i<8;i++)
                if(EarthArenaFractureGate.IsMeaningfulDamage(12f,95f,.1f))damage.Add(12f);
            var decision=EarthArenaFractureGate.Resolve(true,EarthArenaFractureTrigger.OrdinaryImpact,damage.Impulse,12);
            Assert.That(decision.Accepted,Is.True);
            Assert.That(EarthArenaFractureGate.ReleaseImpulsePerPiece(12f,decision.ReleaseCount),Is.EqualTo(12f));
            Assert.That(damage.Impulse,Is.EqualTo(96f));
        }
        [TestCase(95f,1)] [TestCase(900f,2)] [TestCase(1800f,3)]
        public void HeavyHitRetainsAdmissionAndSharesItsFiniteMomentumBudget(float impulse,int releases)
        {
            Assert.That(EarthArenaFractureGate.IsMeaningfulDamage(impulse,95f,.1f),Is.True);
            var decision=EarthArenaFractureGate.Resolve(true,EarthArenaFractureTrigger.OrdinaryImpact,impulse,12);
            Assert.That(decision.ReleaseCount,Is.EqualTo(releases));
            float budget=EarthArenaFractureGate.ReleaseImpulsePerPiece(impulse,releases);
            Assert.That(budget*releases,Is.EqualTo(impulse).Within(.001f));
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)]
        public void InvalidStrengthCannotCreateDamageOrLaunchBudget(float impulse)
        {
            Assert.That(EarthArenaFractureGate.IsMeaningfulDamage(impulse,95f,.1f),Is.False);
            Assert.That(EarthArenaFractureGate.ReleaseImpulsePerPiece(impulse,3),Is.Zero);
        }
    }
}
