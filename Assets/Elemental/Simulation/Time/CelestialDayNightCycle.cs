using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Time
{
    /// <summary>Dawn is phase 0, sunset .5. Daylight and darkness have independent durations.</summary>
    public static class CelestialDayNightCycle
    {
        public static double SecondsAtPhase(float phase, float daylightSeconds, float nightSeconds)
        {
            double p = Finite(phase) ? phase - Math.Floor(phase) : 0d;
            double day = Duration(daylightSeconds), night = Duration(nightSeconds);
            return p < .5d ? p * 2d * day : day + (p - .5d) * 2d * night;
        }

        public static float Phase(double elapsed, float startPhase, float daylightSeconds, float nightSeconds)
        {
            double day = Duration(daylightSeconds), night = Duration(nightSeconds), cycle = day + night;
            double seconds = SecondsAtPhase(startPhase, daylightSeconds, nightSeconds) + (Finite(elapsed) ? elapsed : 0d);
            seconds -= Math.Floor(seconds / cycle) * cycle;
            return (float)(seconds < day ? .5d * seconds / day : .5d + .5d * (seconds - day) / night);
        }

        public static float Night(float3 sun, float3 observerUp) =>
            1f - math.smoothstep(-.10f, .12f, math.dot(math.normalizesafe(sun), math.normalizesafe(observerUp, new float3(0, 1, 0))));

        public static float SolarStrength(float altitude) => math.smoothstep(-.025f, .16f, altitude);
        public static bool PlanetOccludesRay(float3 observerOffset, float3 ray, float radius)
        {
            float b = math.dot(observerOffset, math.normalizesafe(ray));
            float c = math.lengthsq(observerOffset) - radius * radius;
            float discriminant = b * b - c;
            return radius > 0f && b < 0f && discriminant > 0f && -b + math.sqrt(discriminant) > 0f;
        }
        private static double Duration(float value) => Finite(value) ? Math.Max(10d, value) : 300d;
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>Independent equal-area samples, with no latitude rows, spiral or Cartesian lattice.</summary>
    public static class CelestialStarDistribution
    {
        public static uint Hash(uint value)
        {
            value ^= value >> 16; value *= 0x7feb352dU;
            value ^= value >> 15; value *= 0x846ca68bU;
            return value ^ (value >> 16);
        }

        public static float Random01(uint index, uint seed, uint stream) =>
            (Hash(index * 0x9e3779b9U ^ seed ^ stream * 0x85ebca6bU) >> 8) * (1f / 16777216f);

        public static float3 Direction(uint index, uint seed)
        {
            float z = 1f - 2f * Random01(index, seed, 1);
            float phi = 2f * math.PI * Random01(index, seed, 2);
            float radius = math.sqrt(math.max(0f, 1f - z * z));
            return new float3(radius * math.cos(phi), z, radius * math.sin(phi));
        }
    }
}
