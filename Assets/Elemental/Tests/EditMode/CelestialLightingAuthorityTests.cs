using Elemental.Simulation.Time;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class CelestialLightingAuthorityTests
    {
        [Test]
        public void GameplayLockedPreservesCelestialClock()
        {
            double elapsed = CelestialLightingClockPolicy.Step(
                12.5d,
                1f / 60f,
                40f,
                CelestialLightingAuthorityMode.GameplayLocked);

            Assert.That(elapsed, Is.EqualTo(12.5d));
        }

        [Test]
        public void AnimatedEphemerisAdvancesFiniteClock()
        {
            double elapsed = CelestialLightingClockPolicy.Step(
                2d,
                0.02f,
                5f,
                CelestialLightingAuthorityMode.AnimatedEphemeris);

            Assert.That(elapsed, Is.EqualTo(2.1d).Within(0.00001d));
        }

        [Test]
        public void InvalidInputsCannotPoisonClock()
        {
            double elapsed = CelestialLightingClockPolicy.Step(
                double.NaN,
                float.PositiveInfinity,
                float.NaN,
                CelestialLightingAuthorityMode.AnimatedEphemeris);

            Assert.That(elapsed, Is.EqualTo(0d));
        }
    }
}
