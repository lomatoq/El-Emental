using Elemental.Simulation.Characters;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthHeadPitchStabilizerTests
    {
        [TestCase(-25f)]
        [TestCase(-12f)]
        [TestCase(0f)]
        [TestCase(23.16f)]
        [TestCase(28f)]
        public void ExpressivePitchInsideEnvelopeIsUnchanged(float pitch) =>
            Assert.That(EarthHeadPitchStabilizer.CorrectionDegrees(pitch), Is.Zero.Within(.0001f));

        [TestCase(46.177364f, -18.177364f)]
        [TestCase(61.56589f, -33.56589f)]
        [TestCase(32.23f, -4.23f)]
        [TestCase(-31f, 6f)]
        public void ExcessPitchIsCorrectedExactlyToNearestExpressiveLimit(
            float measured, float expectedCorrection) =>
            Assert.That(EarthHeadPitchStabilizer.CorrectionDegrees(measured),
                Is.EqualTo(expectedCorrection).Within(.001f));

        [Test]
        public void NonFiniteMeasurementCannotInjectANewRotation()
        {
            Assert.That(EarthHeadPitchStabilizer.CorrectionDegrees(float.NaN), Is.Zero);
            Assert.That(EarthHeadPitchStabilizer.CorrectionDegrees(float.PositiveInfinity), Is.Zero);
            Assert.That(EarthHeadPitchStabilizer.CorrectionDegrees(float.NegativeInfinity), Is.Zero);
        }
    }
}
