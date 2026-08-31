using Elemental.Simulation.Rendering;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthCinematicDepthOfFieldSolverTests
    {
        [Test]
        public void SignedCocSeparatesNearFocusAndFar()
        {
            float near = EarthCinematicDepthOfFieldSolver.SignedCircleOfConfusion(
                4f, 8f, 8f, 2f, 6f);
            float focus = EarthCinematicDepthOfFieldSolver.SignedCircleOfConfusion(
                8f, 8f, 8f, 2f, 6f);
            float far = EarthCinematicDepthOfFieldSolver.SignedCircleOfConfusion(
                14f, 8f, 8f, 2f, 6f);

            Assert.That(near, Is.EqualTo(-1f));
            Assert.That(focus, Is.EqualTo(0f));
            Assert.That(far, Is.EqualTo(1f));
        }

        [Test]
        public void SignedCocIsFiniteAndClampedForBadInputs()
        {
            float result = EarthCinematicDepthOfFieldSolver.SignedCircleOfConfusion(
                0f, 0f, 0f, 0f, 0f);

            Assert.That(float.IsFinite(result), Is.True);
            Assert.That(result, Is.InRange(-1f, 1f));
        }

        [Test]
        public void DualSubjectEnvelopeKeepsBothActorsAndSpaceBetweenSharp()
        {
            EarthCinematicDepthOfFieldEnvelope envelope =
                EarthCinematicDepthOfFieldSolver.ResolveSharpEnvelope(
                    7f, 13f, 0.75f);

            Assert.That(envelope.Near, Is.EqualTo(6.25f).Within(0.0001f));
            Assert.That(envelope.Far, Is.EqualTo(13.75f).Within(0.0001f));
            Assert.That(EarthCinematicDepthOfFieldSolver.SignedCircleOfConfusion(
                7f, envelope.Near, envelope.Far, 2f, 6f), Is.Zero);
            Assert.That(EarthCinematicDepthOfFieldSolver.SignedCircleOfConfusion(
                10f, envelope.Near, envelope.Far, 2f, 6f), Is.Zero);
            Assert.That(EarthCinematicDepthOfFieldSolver.SignedCircleOfConfusion(
                13f, envelope.Near, envelope.Far, 2f, 6f), Is.Zero);
        }

        [Test]
        public void RendererDepthRangesAndPaddingAreFullyContained()
        {
            EarthCinematicDepthOfFieldEnvelope envelope =
                EarthCinematicDepthOfFieldSolver.ResolveSharpEnvelopeFromRanges(
                    6.4f, 7.6f,
                    12.1f, 13.8f,
                    0.35f);

            Assert.That(envelope.Near, Is.EqualTo(6.05f).Within(0.0001f));
            Assert.That(envelope.Far, Is.EqualTo(14.15f).Within(0.0001f));
            Assert.That(EarthCinematicDepthOfFieldSolver.SignedCircleOfConfusion(
                6.4f, envelope.Near, envelope.Far, 2f, 6f), Is.Zero);
            Assert.That(EarthCinematicDepthOfFieldSolver.SignedCircleOfConfusion(
                13.8f, envelope.Near, envelope.Far, 2f, 6f), Is.Zero);
        }

        [Test]
        public void DualSubjectEnvelopeIsOrderInvariantAndBlursOutside()
        {
            EarthCinematicDepthOfFieldEnvelope forward =
                EarthCinematicDepthOfFieldSolver.ResolveSharpEnvelope(
                    6f, 12f, 0.5f);
            EarthCinematicDepthOfFieldEnvelope reversed =
                EarthCinematicDepthOfFieldSolver.ResolveSharpEnvelope(
                    12f, 6f, 0.5f);

            Assert.That(reversed.Near, Is.EqualTo(forward.Near));
            Assert.That(reversed.Far, Is.EqualTo(forward.Far));
            Assert.That(EarthCinematicDepthOfFieldSolver.SignedCircleOfConfusion(
                3.5f, forward.Near, forward.Far, 2f, 5f), Is.LessThan(0f));
            Assert.That(EarthCinematicDepthOfFieldSolver.SignedCircleOfConfusion(
                18f, forward.Near, forward.Far, 2f, 5f), Is.GreaterThan(0f));
        }

        [Test]
        public void EnvelopeExpandsImmediatelyButContractsWithHysteresis()
        {
            var current = new EarthCinematicDepthOfFieldEnvelope(7f, 11f);
            var expandedTarget = new EarthCinematicDepthOfFieldEnvelope(4f, 15f);
            EarthCinematicDepthOfFieldEnvelope expanded =
                EarthCinematicDepthOfFieldSolver.StepSharpEnvelope(
                    in current, in expandedTarget, 4f, 1f / 60f);

            Assert.That(expanded.Near, Is.EqualTo(4f));
            Assert.That(expanded.Far, Is.EqualTo(15f));

            var contractedTarget = new EarthCinematicDepthOfFieldEnvelope(8f, 10f);
            EarthCinematicDepthOfFieldEnvelope contracted =
                EarthCinematicDepthOfFieldSolver.StepSharpEnvelope(
                    in expanded, in contractedTarget, 4f, 0.25f);

            Assert.That(contracted.Near, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(contracted.Far, Is.EqualTo(14f).Within(0.0001f));
        }

        [Test]
        public void ForegroundCoverageSuppressesFarBleed()
        {
            EarthCinematicDepthOfFieldCompositeWeights weights =
                EarthCinematicDepthOfFieldSolver.ResolveCompositeWeights(
                    0.9f, 1f, 1f);

            Assert.That(weights.Near, Is.EqualTo(1f));
            Assert.That(weights.Far, Is.Zero);
            Assert.That(weights.Sharp, Is.Zero);
        }

        [Test]
        public void InFocusAndForegroundPixelsRejectFarGather()
        {
            EarthCinematicDepthOfFieldCompositeWeights focus =
                EarthCinematicDepthOfFieldSolver.ResolveCompositeWeights(
                    0f, 0f, 1f);
            EarthCinematicDepthOfFieldCompositeWeights foreground =
                EarthCinematicDepthOfFieldSolver.ResolveCompositeWeights(
                    -0.8f, 0.25f, 1f);

            Assert.That(focus.Far, Is.Zero);
            Assert.That(focus.Sharp, Is.EqualTo(1f));
            Assert.That(foreground.Far, Is.Zero);
            Assert.That(foreground.Near, Is.EqualTo(0.25f));
        }

        [Test]
        public void FocusedPixelRejectsDilatedNearAndFarCoverage()
        {
            EarthCinematicDepthOfFieldCompositeWeights focus =
                EarthCinematicDepthOfFieldSolver.ResolveCompositeWeights(
                    0f, 1f, 1f);

            Assert.That(focus.Sharp, Is.EqualTo(1f));
            Assert.That(focus.Near, Is.Zero);
            Assert.That(focus.Far, Is.Zero);
        }
    }
}
