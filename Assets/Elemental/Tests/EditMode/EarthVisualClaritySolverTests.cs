using Elemental.Simulation.Capabilities;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Rendering;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthVisualClaritySolverTests
    {
        [Test]
        public void ExploreRestoresBoundedBokehOnNativeHigh()
        {
            var input = new EarthVisualClarityInput(
                EarthCameraState.Explore,
                CapabilityProfileKind.NativeHigh,
                0f,
                9f,
                1f,
                false);

            EarthVisualClarityOutput output = EarthVisualClaritySolver.Solve(in input);

            Assert.That(output.DepthOfFieldTier, Is.EqualTo(EarthDepthOfFieldTier.Bokeh));
            Assert.That(output.FocusDistance, Is.EqualTo(9f));
            Assert.That(output.BloomIntensity, Is.EqualTo(0f));
            Assert.That(output.VignetteIntensity, Is.LessThanOrEqualTo(0.11f));
        }

        [Test]
        public void HeavyBendUsesQualitySpecificLensPolicy()
        {
            var highInput = new EarthVisualClarityInput(
                EarthCameraState.BendHeavy,
                CapabilityProfileKind.NativeHigh,
                0.8f,
                7f,
                1f,
                false);
            var lowInput = new EarthVisualClarityInput(
                EarthCameraState.BendHeavy,
                CapabilityProfileKind.NativeLow,
                0.8f,
                7f,
                1f,
                false);

            EarthVisualClarityOutput high = EarthVisualClaritySolver.Solve(in highInput);
            EarthVisualClarityOutput low = EarthVisualClaritySolver.Solve(in lowInput);

            Assert.That(high.DepthOfFieldTier, Is.EqualTo(EarthDepthOfFieldTier.Bokeh));
            Assert.That(low.DepthOfFieldTier, Is.EqualTo(EarthDepthOfFieldTier.Gaussian));
            Assert.That(high.Aperture, Is.EqualTo(5.6f));
            Assert.That(high.FocalLength, Is.EqualTo(50f));
            Assert.That(high.GaussianMaxRadius, Is.EqualTo(1.5f));
            Assert.That(low.GaussianMaxRadius, Is.EqualTo(1.25f));
            Assert.That(low.GaussianStart - low.FocusDistance, Is.InRange(0.09f, 0.18f));
            Assert.That(low.GaussianEnd - low.GaussianStart, Is.InRange(0.95f, 1.4f));
        }

        [Test]
        public void NativeMotionKeepsDepthOfFieldWhileWebStaysOff()
        {
            var webInput = new EarthVisualClarityInput(
                EarthCameraState.HoldMass,
                CapabilityProfileKind.WebLab,
                1f,
                5f,
                1f,
                true);
            var highImpactInput = new EarthVisualClarityInput(
                EarthCameraState.Impact,
                CapabilityProfileKind.NativeHigh,
                1f,
                5f,
                1f,
                true);
            var lowImpactInput = new EarthVisualClarityInput(
                EarthCameraState.Impact,
                CapabilityProfileKind.NativeLow,
                1f,
                5f,
                1f,
                true);

            EarthVisualClarityOutput web = EarthVisualClaritySolver.Solve(in webInput);
            EarthVisualClarityOutput highImpact = EarthVisualClaritySolver.Solve(in highImpactInput);
            EarthVisualClarityOutput lowImpact = EarthVisualClaritySolver.Solve(in lowImpactInput);

            Assert.That(web.DepthOfFieldTier, Is.EqualTo(EarthDepthOfFieldTier.Off));
            Assert.That(web.DustCapacity, Is.Zero);
            Assert.That(highImpact.DepthOfFieldTier, Is.EqualTo(EarthDepthOfFieldTier.Bokeh));
            Assert.That(lowImpact.DepthOfFieldTier, Is.EqualTo(EarthDepthOfFieldTier.Gaussian));
        }

        [Test]
        public void NativeHighSafeStatesKeepBokehWithoutChargeWarmup()
        {
            var coldInput = new EarthVisualClarityInput(
                EarthCameraState.BendLight,
                CapabilityProfileKind.NativeHigh,
                0.42f,
                7f,
                1f,
                false);
            var warmInput = new EarthVisualClarityInput(
                EarthCameraState.BendLight,
                CapabilityProfileKind.NativeHigh,
                0.42f,
                7f,
                1f,
                true);

            EarthVisualClarityOutput cold = EarthVisualClaritySolver.Solve(in coldInput);
            EarthVisualClarityOutput warm = EarthVisualClaritySolver.Solve(in warmInput);

            Assert.That(cold.DepthOfFieldTier, Is.EqualTo(EarthDepthOfFieldTier.Bokeh));
            Assert.That(warm.DepthOfFieldTier, Is.EqualTo(EarthDepthOfFieldTier.Bokeh));
            Assert.That(warm.Aperture, Is.EqualTo(5.6f));
            Assert.That(warm.FocalLength, Is.EqualTo(50f));
        }

        [Test]
        public void ChargeCanRequestBokehDuringExplore()
        {
            var input = new EarthVisualClarityInput(
                EarthCameraState.Explore,
                CapabilityProfileKind.NativeHigh,
                0.72f,
                8f,
                1f,
                false);

            EarthVisualClarityOutput output = EarthVisualClaritySolver.Solve(in input);

            Assert.That(output.DepthOfFieldTier, Is.EqualTo(EarthDepthOfFieldTier.Bokeh));
        }

        [Test]
        public void DustIsBoundedAndFollowsDaylight()
        {
            var dayInput = new EarthVisualClarityInput(
                EarthCameraState.Explore,
                CapabilityProfileKind.NativeHigh,
                0f,
                8f,
                1f,
                false);
            var nightInput = new EarthVisualClarityInput(
                EarthCameraState.Explore,
                CapabilityProfileKind.NativeHigh,
                0f,
                8f,
                0f,
                false);

            EarthVisualClarityOutput day = EarthVisualClaritySolver.Solve(in dayInput);
            EarthVisualClarityOutput night = EarthVisualClaritySolver.Solve(in nightInput);

            Assert.That(day.DustCapacity, Is.EqualTo(64));
            Assert.That(day.DustRate, Is.GreaterThan(0f));
            Assert.That(night.DustRate, Is.Zero);
        }
    }
}
