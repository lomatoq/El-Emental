using Elemental.Simulation.Diagnostics;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthPerformanceStatisticsTests
    {
        [Test]
        public void RingPercentilesUseChronologicalBoundedSamples()
        {
            double[] ring = { 9, 10, 3, 4, 5, 6, 7, 8 };
            double[] scratch = new double[8];
            EarthPercentiles result = EarthPerformanceStatistics.Compute(ring, 8, 2, scratch);
            Assert.That(result.P50, Is.EqualTo(6.5).Within(0.0001));
            Assert.That(result.P95, Is.EqualTo(9.65).Within(0.0001));
            Assert.That(result.P99, Is.EqualTo(9.93).Within(0.0001));
            Assert.That(result.Maximum, Is.EqualTo(10));
        }

        [Test]
        public void EmptyCaptureReturnsZeroes()
        {
            EarthPercentiles result = EarthPerformanceStatistics.Compute(
                new double[4], 0, 0, new double[4]);
            Assert.That(result.P99, Is.Zero);
            Assert.That(result.Maximum, Is.Zero);
        }
    }
}
