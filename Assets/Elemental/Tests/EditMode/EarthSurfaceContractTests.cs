using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthSurfaceContractTests
    {
        [Test]
        public void SampleCarriesStableIdentityFrameVelocityAndProvenance()
        {
            var handle = new EarthSurfaceHandle(EarthSurfaceKind.Platform, 17u, 3u);
            var sample = new EarthSurfaceSample(
                handle,
                new float3(1f, 2f, 3f),
                new float3(0f, 1f, 0f),
                new float3(2f, 1f, 0f),
                new float3(4f, 0f, 0f),
                2.5f,
                EarthSurfaceMaterial.RaisedEarth,
                EarthSurfaceProvenance.RaisedPlatform,
                EarthSurfaceCapabilities.Support | EarthSurfaceCapabilities.Pillar |
                EarthSurfaceCapabilities.Moving);

            Assert.That(sample.IsValid, Is.True);
            Assert.That(sample.Handle, Is.EqualTo(handle));
            Assert.That(math.dot(sample.Normal, sample.Tangent), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(math.dot(sample.Normal, sample.Bitangent), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(sample.Velocity.x, Is.EqualTo(4f));
            Assert.That(sample.Provenance, Is.EqualTo(EarthSurfaceProvenance.RaisedPlatform));
            Assert.That(sample.Supports(EarthSurfaceCapabilities.Support | EarthSurfaceCapabilities.Pillar), Is.True);
        }

        [Test]
        public void NearestValidSurfaceWinsAndConstructedSupportBreaksTies()
        {
            EarthSurfaceSample planet = Sample(EarthSurfaceKind.Planet, 6f);
            EarthSurfaceSample platform = Sample(EarthSurfaceKind.Platform, 2f);
            EarthSurfaceSample tiedPlatform = Sample(EarthSurfaceKind.Platform, 6f);

            Assert.That(EarthSurfaceSelection.IsBetter(
                in platform, in planet, EarthSurfaceCapabilities.Support), Is.True);
            Assert.That(EarthSurfaceSelection.IsBetter(
                in tiedPlatform, in planet, EarthSurfaceCapabilities.Support), Is.True);
        }

        [Test]
        public void PlatformBudgetHasEightMeterSoftAndTwentyTwoMeterHardRegion()
        {
            var geometry = new EarthPlatformGeometry(
                new float3(0f, 24f, 0f),
                new float3(0f, 1f, 0f),
                new float3(1f, 0f, 0f),
                new float3(0f, 0f, 1f),
                new[]
                {
                    new float2(-2f, -2f), new float2(2f, -2f),
                    new float2(2f, 2f), new float2(-2f, 2f)
                },
                16f,
                24f);

            EarthPlatformBudgetSample soft = EarthPlatformGeometrySolver.EvaluateHeightBudget(
                in geometry, 8f, 8f, 22f);
            EarthPlatformBudgetSample tall = EarthPlatformGeometrySolver.EvaluateHeightBudget(
                in geometry, 15f, 8f, 22f);
            EarthPlatformBudgetSample capped = EarthPlatformGeometrySolver.EvaluateHeightBudget(
                in geometry, 40f, 8f, 22f);

            Assert.That(soft.AboveSoftLimit, Is.False);
            Assert.That(tall.AboveSoftLimit, Is.True);
            Assert.That(tall.CostMultiplier, Is.GreaterThan(soft.CostMultiplier));
            Assert.That(tall.Stability01, Is.LessThan(soft.Stability01));
            Assert.That(capped.AcceptedHeight, Is.EqualTo(22f));
            Assert.That(capped.HardLimited, Is.True);
        }

        private static EarthSurfaceSample Sample(EarthSurfaceKind kind, float distance) =>
            new EarthSurfaceSample(
                new EarthSurfaceHandle(kind, kind == EarthSurfaceKind.Planet ? 1u : 2u, 1u),
                new float3(0f, 1f, 0f),
                new float3(0f, 1f, 0f),
                new float3(1f, 0f, 0f),
                float3.zero,
                distance,
                EarthSurfaceMaterial.RaisedEarth,
                EarthSurfaceProvenance.RaisedPlatform,
                EarthSurfaceCapabilities.Support);
    }
}
