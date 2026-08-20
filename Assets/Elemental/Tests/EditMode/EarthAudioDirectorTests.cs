using Elemental.Presentation.VFX;
using Elemental.Simulation.Magic;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthAudioDirectorTests
    {
        [Test]
        public void HeavyImpactProducesMoreLowBodyAndLowerPitchThanSmallStone()
        {
            var light = new EarthImpactEvent(
                1u, 1u, 90f, 120f, 8f, 7f, float3.zero, new float3(0f, 1f, 0f),
                EarthImpactMaterialKind.LooseStone);
            var heavy = new EarthImpactEvent(
                2u, 2u, 2200f, 18000f, 620f, 21f, float3.zero, new float3(0f, 1f, 0f),
                EarthImpactMaterialKind.Structure);

            EarthAudioResponse lightSample = EarthAudioResponseSolver.Impact(in light);
            EarthAudioResponse heavySample = EarthAudioResponseSolver.Impact(in heavy);

            Assert.That(heavySample.Body, Is.GreaterThan(lightSample.Body));
            Assert.That(heavySample.Crack, Is.GreaterThan(lightSample.Crack));
            Assert.That(heavySample.Pitch, Is.LessThan(lightSample.Pitch));
        }

        [Test]
        public void CompletedReturnHasStrongerBodyThanCapturePreview()
        {
            var captured = new EarthReturnEvent(
                1u, 4u, 1, EarthReturnEventStage.Captured, float3.zero, 1.2f, 180f);
            var completed = new EarthReturnEvent(
                8u, 4u, 1, EarthReturnEventStage.Completed, float3.zero, 1.2f, 180f);
            Assert.That(EarthAudioResponseSolver.Return(in completed).Body,
                Is.GreaterThan(EarthAudioResponseSolver.Return(in captured).Body));
        }
    }
}
