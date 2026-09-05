using Elemental.Simulation.Characters;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthSeismicPerceptionTests
    {
        [TestCase(true, true, true, true, false, true)]
        [TestCase(false, true, true, true, false, false)]
        [TestCase(true, false, true, true, false, false)]
        [TestCase(true, true, false, true, false, false)]
        [TestCase(true, true, true, false, false, false)]
        [TestCase(true, true, true, true, true, false)]
        public void VisionRequiresRequestedEarthAndActualUnbrokenFootSupport(
            bool requested, bool earth, bool grounded, bool acceptsSupport, bool mantle, bool expected) =>
            Assert.That(EarthSeismicPerception.CanPerceive(requested, earth, grounded, acceptsSupport, mantle), Is.EqualTo(expected));

        [Test]
        public void WaveExpiresAndTravelsAtPredictableWorldSpeed()
        {
            Assert.That(EarthSeismicPerception.Radius(1.1f, 22f, 2.2f), Is.EqualTo(11f).Within(0.001f));
            Assert.That(EarthSeismicPerception.Strength(-0.1f, 2.2f), Is.Zero);
            Assert.That(EarthSeismicPerception.Strength(2.2f, 2.2f), Is.Zero);
            Assert.That(EarthSeismicPerception.Strength(1f, 2.2f), Is.EqualTo(1f));
        }
    }
}
