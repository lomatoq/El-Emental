using Elemental.Simulation.Bending;
using Elemental.Simulation.Rendering;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthMaterialMicroShakeTests
    {
        [TestCase(EarthMaterialFeedbackKind.Footstep)]
        [TestCase(EarthMaterialFeedbackKind.Roll)]
        [TestCase(EarthMaterialFeedbackKind.RepairSeat)]
        public void IncidentalContactsNeverShake(EarthMaterialFeedbackKind kind)
        { Assert.That(EarthMaterialMicroShake.Weight(kind,3,1,0), Is.Zero); }

        [Test] public void HighFallIsStrongerThanLowFallAndStopsAtDistance()
        {
            Assert.That(EarthMaterialMicroShake.Weight(EarthMaterialFeedbackKind.Land,6f/7f,.5f,0), Is.Zero);
            float heavy = EarthMaterialMicroShake.Weight(EarthMaterialFeedbackKind.Land,14f/7f,.5f,0);
            Assert.That(heavy, Is.GreaterThan(.5f));
            Assert.That(EarthMaterialMicroShake.Weight(EarthMaterialFeedbackKind.Land,2,.5f,12), Is.InRange(0.001f,heavy-.001f));
            Assert.That(EarthMaterialMicroShake.Weight(EarthMaterialFeedbackKind.Land,2,.5f,24), Is.Zero);
        }
        [Test] public void EightWallContactStationsProduceOneEnvelope()
        {
            var one = new EarthMaterialMicroShake(); var eight = new EarthMaterialMicroShake();
            for (int frame=0;frame<120;frame++)
            {
                one.Observe(EarthMaterialFeedbackKind.Friction,1.5f,.5f,4);
                for (int station=0;station<8;station++) eight.Observe(EarthMaterialFeedbackKind.Friction,1.5f,.5f,4);
                Assert.That(eight.Step(.02f,true,1,false), Is.EqualTo(one.Step(.02f,true,1,false)).Within(.000001f));
            }
        }
        [Test] public void StrongestConcurrentAbilityWinsRatherThanAdding()
        {
            var one = new EarthMaterialMicroShake(); var many = new EarthMaterialMicroShake();
            one.Observe(EarthMaterialFeedbackKind.Impact,3,1,0);
            many.Observe(EarthMaterialFeedbackKind.Impact,3,1,0);
            many.Observe(EarthMaterialFeedbackKind.Emerge,3,1,0);
            many.Observe(EarthMaterialFeedbackKind.WaveSurfaceBurst,3,1,0);
            Assert.That(many.Step(.02f,true,1,false), Is.EqualTo(one.Step(.02f,true,1,false)));
        }
        [Test] public void AccessibilityAndPauseClearPendingAndActiveMotion()
        {
            var state = new EarthMaterialMicroShake(); state.Observe(EarthMaterialFeedbackKind.Fracture,2,1,0);
            Assert.That(state.Step(.02f,true,1,true), Is.Zero);
            Assert.That(state.Step(.02f,true,1,false), Is.Zero);
            state.Observe(EarthMaterialFeedbackKind.Emerge,2,1,0);
            Assert.That(state.Step(.02f,true,1,false), Is.GreaterThan(0));
            Assert.That(state.Step(.02f,false,1,false), Is.Zero);
            state.Observe(EarthMaterialFeedbackKind.Emerge,2,1,0);
            Assert.That(state.Step(.02f,true,0,false), Is.Zero);
        }
        [Test] public void EnvelopeSettlesExactlyToZeroAndCannotExceedMicroBudget()
        {
            var state = new EarthMaterialMicroShake();
            state.Observe(EarthMaterialFeedbackKind.Impact,1000,1,0);
            float maximum = 0;
            for (int i=0;i<200;i++) maximum = System.Math.Max(maximum,state.Step(.02f,true,1,false));
            Assert.That(maximum, Is.InRange(.1f,1)); Assert.That(state.Envelope, Is.Zero);
            Assert.That(maximum*EarthMaterialMicroShake.MaximumPositionMeters, Is.LessThanOrEqualTo(.0025f));
            Assert.That(maximum*EarthMaterialMicroShake.MaximumRotationDegrees, Is.LessThanOrEqualTo(.055f));
        }
        [Test] public void InvalidAndTinyImpactInputsAreSilent()
        {
            Assert.That(EarthMaterialMicroShake.Weight(EarthMaterialFeedbackKind.Impact,float.NaN,1,0), Is.Zero);
            Assert.That(EarthMaterialMicroShake.Weight(EarthMaterialFeedbackKind.Impact,2,1,float.PositiveInfinity), Is.Zero);
            Assert.That(EarthMaterialMicroShake.Weight(EarthMaterialFeedbackKind.Impact,.4f,.5f,0), Is.Zero);
            Assert.That(EarthMaterialMicroShake.Weight(EarthMaterialFeedbackKind.Impact,2,.05f,0), Is.Zero);
        }
    }
}
