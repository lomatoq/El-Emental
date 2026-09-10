using Elemental.Simulation.Bending;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Rendering;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class HardPolishResponseTests
    {
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void SchoolEnvelopeReturnsExactlyToAuthoredScale(int fps)
        {
            Assert.That(EarthResponsePreset.SchoolScale(0, false), Is.EqualTo(1));
            for (int i = 0; i < fps; i++)
            {
                float age = i / (float)fps;
                Assert.That(EarthResponsePreset.SchoolScale(age, false), Is.InRange(1f, 1.08f));
                Assert.That(EarthResponsePreset.SchoolScale(age, true), Is.EqualTo(1));
                if (age >= .16f) Assert.That(EarthResponsePreset.SchoolScale(age, false), Is.EqualTo(1));
            }
            Assert.That(EarthResponsePreset.SchoolScale(.08f, false), Is.EqualTo(1.08f).Within(.00001));
        }
        [Test] public void AdmissionDeduplicatesContactsBoundsFloodAndAcceptsReusedGeneration()
        {
            var gate = new EarthResponseAdmission();
            Assert.That(gate.Admit(1, 1, 10, 600), Is.True);
            for (int i = 0; i < 10; i++) Assert.That(gate.Admit(1, 1, 10, 600), Is.False);
            Assert.That(gate.Admit(1, 2, 10, 600), Is.True);
            Assert.That(gate.Admit(2, 1, 10, 600), Is.True);
            Assert.That(gate.Admit(3, 1, 10, 600), Is.True);
            Assert.That(gate.Admit(4, 1, 10, 600), Is.False);
            Assert.That(gate.Admit(1, 1, 10.04, 601), Is.False);
            Assert.That(gate.Admit(1, 1, 10.081, 602), Is.True);
            Assert.That(gate.Admit(1, 1, 0, 1), Is.True, "Round-clock rewind must not retain stale cooldown.");
        }
        [Test] public void SharedSeedAndSchoolSurviveParticleBudgetClamping()
        {
            var cue = new EarthMaterialFeedbackCue(EarthMaterialFeedbackKind.SchoolSwitch,
                new float3(1, 2, 3), math.up(), .5f, .08f, 1, 20, 0, 0, 1, ElementId.Fire);
            var budgeted = cue.WithCounts(0, 0);
            Assert.That(budgeted.Seed, Is.EqualTo(cue.Seed)); Assert.That(budgeted.Element, Is.EqualTo(ElementId.Fire));
            Assert.That(EarthResponsePreset.SparkCount(cue.Kind, cue.Seed), Is.InRange(3, 6));
            Assert.That(EarthResponsePreset.ImpactFlashSeconds, Is.InRange(.04f, .07f));
        }
        [TestCase(EarthMaterialFeedbackKind.SchoolSwitch)]
        [TestCase(EarthMaterialFeedbackKind.FireIgnite)]
        [TestCase(EarthMaterialFeedbackKind.FireEnd)]
        [TestCase(EarthMaterialFeedbackKind.FireContact)]
        public void CosmeticSignalsDoNotAddCameraKick(EarthMaterialFeedbackKind kind)
        { Assert.That(EarthMaterialMicroShake.Weight(kind, 3, 4, 0), Is.Zero); }
    }
}
