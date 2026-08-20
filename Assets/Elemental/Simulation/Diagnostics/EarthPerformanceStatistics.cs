using System;

namespace Elemental.Simulation.Diagnostics
{
    public readonly struct EarthPercentiles
    {
        public EarthPercentiles(double p50, double p95, double p99, double maximum)
        {
            P50 = p50;
            P95 = p95;
            P99 = p99;
            Maximum = maximum;
        }

        public double P50 { get; }
        public double P95 { get; }
        public double P99 { get; }
        public double Maximum { get; }
    }

    public static class EarthPerformanceStatistics
    {
        public static EarthPercentiles Compute(
            double[] ring,
            int count,
            int writeIndex,
            double[] scratch)
        {
            if (ring == null || scratch == null || ring.Length == 0 || count <= 0)
                return default;
            int sampleCount = Math.Min(Math.Min(count, ring.Length), scratch.Length);
            int start = (writeIndex - sampleCount + ring.Length) % ring.Length;
            for (int index = 0; index < sampleCount; index++)
                scratch[index] = ring[(start + index) % ring.Length];
            Array.Sort(scratch, 0, sampleCount);
            return new EarthPercentiles(
                Quantile(scratch, sampleCount, 0.50),
                Quantile(scratch, sampleCount, 0.95),
                Quantile(scratch, sampleCount, 0.99),
                scratch[sampleCount - 1]);
        }

        private static double Quantile(double[] sorted, int count, double q)
        {
            if (count <= 1) return sorted[0];
            double position = (count - 1) * q;
            int lower = (int)Math.Floor(position);
            int upper = Math.Min(count - 1, lower + 1);
            double t = position - lower;
            return sorted[lower] + (sorted[upper] - sorted[lower]) * t;
        }
    }
}
