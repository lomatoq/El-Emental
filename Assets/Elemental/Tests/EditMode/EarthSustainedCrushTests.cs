using Elemental.Simulation.Combat;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthSustainedCrushTests
    {
        [Test]
        public void BriefMeasuredUnloadingDoesNotRestartAnOtherwiseStablePin()
        {
            float elapsed=0f,gap=0f,legacy=0f,damage=0f;
            // Native012 repeatedly sustained ~700N then dipped below350N for
            // six fixed ticks while the same stone still blocked recovery.
            for(int frame=0;frame<205;frame++)
            {
                float load=frame%41<35?700f:24f;
                legacy=EarthSustainedCrush.StepPinned(legacy,load,42f,true,true,.02f);
                elapsed=EarthSustainedCrush.StepPinnedWithBriefUnloading(elapsed,ref gap,load,42f,true,true,.02f);
                float sample=EarthSustainedCrush.PinnedDamage(elapsed,load,42f,.02f);
                if(load<350f)Assert.That(sample,Is.Zero,"No damage during absent/subthreshold measured load.");
                damage+=sample;
            }
            Assert.That(legacy,Is.Zero,"Old hard-reset policy never qualified this measured oscillation.");
            Assert.That(damage,Is.GreaterThan(20f));
            Assert.That(elapsed,Is.EqualTo(EarthSustainedCrush.PinnedDwellSeconds));
        }

        [Test]
        public void RemovalStopsDamageImmediatelyAndExpiresRememberedPin()
        {
            float elapsed=EarthSustainedCrush.PinnedDwellSeconds,gap=0f;
            for(int frame=0;frame<20;frame++)
            {
                elapsed=EarthSustainedCrush.StepPinnedWithBriefUnloading(elapsed,ref gap,0f,42f,true,true,.02f);
                Assert.That(EarthSustainedCrush.PinnedDamage(elapsed,0f,42f,.02f),Is.Zero);
            }
            Assert.That(elapsed,Is.Zero);
            elapsed=EarthSustainedCrush.StepPinnedWithBriefUnloading(1.25f,ref gap,700f,42f,true,false,.02f);
            Assert.That(elapsed,Is.Zero);Assert.That(gap,Is.Zero);
            Assert.That(EarthSustainedCrush.StepPinnedWithBriefUnloading(1.25f,ref gap,float.NaN,42f,true,true,.02f),Is.Zero);
            float underThreshold=0f;
            for(int frame=0;frame<200;frame++)underThreshold=EarthSustainedCrush.StepPinnedWithBriefUnloading(underThreshold,ref gap,200f,42f,true,true,.02f);
            Assert.That(underThreshold,Is.Zero,"Grace never qualifies a continuously subthreshold stone.");
        }

        [Test]
        public void SettledMeasuredStackForceSurvivesSleepWithoutRetainingLandingSpike()
        {
            EarthSettledLoad load = default;
            load = EarthSustainedCrush.SampleSettledLoad(load, 16000f, 4f, false);
            Assert.That(load.RetainedForce, Is.Zero);
            for (int i = 0; i < 4; i++) load = EarthSustainedCrush.SampleSettledLoad(load, 1680f, .28f, true);
            Assert.That(load.RetainedForce, Is.Zero);
            load = EarthSustainedCrush.SampleSettledLoad(load, 1680f, .28f, true);
            Assert.That(load.RetainedForce, Is.EqualTo(1680f).Within(.001f), "Includes upper stone load, not merely bottom stone weight.");
            Assert.That(EarthSustainedCrush.SampleSettledLoad(load, 1680f, .28f, false).RetainedForce, Is.Zero);
            Assert.That(EarthSustainedCrush.SampleSettledLoad(load, 20000f, .28f, true).RetainedForce, Is.Zero);
        }

        [Test]
        public void SleepingImpactSpikeIsCappedOnceAcrossAllBonePairs()
        {
            float first = EarthSustainedCrush.SleepingPairForce(8000f, 10000f, 120f);
            float second = EarthSustainedCrush.SleepingPairForce(2000f, 10000f, 120f);
            Assert.That(first + second, Is.EqualTo(1680f).Within(.001f));
            Assert.That(EarthSustainedCrush.SleepingPairForce(400f, 1000f, 120f), Is.EqualTo(400f));
        }

        [Test]
        public void DamageRejectsInvalidOrRemovedLoadEvenWithOldDwell()
        {
            Assert.That(EarthSustainedCrush.Damage(.2f, 0f, 42f, .02f), Is.Zero);
            Assert.That(EarthSustainedCrush.Damage(.2f, 1300f, 42f, .02f), Is.Zero);
            Assert.That(EarthSustainedCrush.Damage(.2f, 2000f, float.NaN, .02f), Is.Zero);
            Assert.That(EarthSustainedCrush.Damage(float.NaN, 2000f, 42f, .02f), Is.Zero);
            Assert.That(EarthSustainedCrush.Step(float.NaN, 2000f, 42f, .02f), Is.Zero);
        }

        [Test]
        public void SeparateSubThresholdStonesCombineOnlyAfterSustainedLoad()
        {
            float elapsed = 0f;
            for (int i = 0; i < 9; i++) elapsed = EarthSustainedCrush.Step(elapsed, 1680f, 42f, .02f);
            Assert.That(EarthSustainedCrush.Damage(elapsed, 1680f, 42f, .02f), Is.Zero);
            for (int i = 0; i < 2; i++) elapsed = EarthSustainedCrush.Step(elapsed, 1680f, 42f, .02f);
            Assert.That(EarthSustainedCrush.Damage(elapsed, 1680f, 42f, .02f), Is.GreaterThan(0f));
            Assert.That(EarthSustainedCrush.Step(elapsed, 840f, 42f, .02f), Is.Zero);
        }

        [Test]
        public void BriefImpactAndRemovedLoadDoNotBecomeSustainedDamage()
        {
            float elapsed = EarthSustainedCrush.Step(0f, 100000f, 42f, .02f);
            Assert.That(EarthSustainedCrush.Damage(elapsed, 100000f, 42f, .02f), Is.Zero);
            Assert.That(EarthSustainedCrush.Step(elapsed, 0f, 42f, .02f), Is.Zero);
            Assert.That(EarthSustainedCrush.Step(.2f, float.NaN, 42f, .02f), Is.Zero);
        }

        [Test]
        public void DamageIsBoundedAndIndependentOfPhysicsStep()
        {
            Assert.That(EarthSustainedCrush.Damage(.2f, 1000000f, 42f, 1f), Is.EqualTo(45f));
            Assert.That(EarthSustainedCrush.Damage(.2f, 1680f, 42f, .02f) * 50f,
                Is.EqualTo(EarthSustainedCrush.Damage(.2f, 1680f, 42f, 1f)).Within(.0001f));
        }
    }
}
