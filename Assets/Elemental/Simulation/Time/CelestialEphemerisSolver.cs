using Unity.Mathematics;

namespace Elemental.Simulation.Time
{
    public readonly struct CelestialSnapshot
    {
        public CelestialSnapshot(float timeOfDay01, float orbit01, float moonOrbit01, float3 sunDirection, float3 moonDirection, float moonPhase01, float night01)
        {
            TimeOfDay01 = timeOfDay01;
            Orbit01 = orbit01;
            MoonOrbit01 = moonOrbit01;
            SunDirection = sunDirection;
            MoonDirection = moonDirection;
            MoonPhase01 = moonPhase01;
            Night01 = night01;
        }

        public float TimeOfDay01 { get; }
        public float Orbit01 { get; }
        public float MoonOrbit01 { get; }
        public float3 SunDirection { get; }
        public float3 MoonDirection { get; }
        public float MoonPhase01 { get; }
        public float Night01 { get; }
    }

    public static class CelestialEphemerisSolver
    {
        public static CelestialSnapshot Evaluate(
            double elapsedSeconds,
            float daySeconds,
            float yearSeconds,
            float moonSeconds,
            float axialTiltDegrees,
            float startTime01)
        {
            float day01 = Repeat01(startTime01 + elapsedSeconds / math.max(1f, daySeconds));
            return EvaluateAtPhase(elapsedSeconds, day01, yearSeconds, moonSeconds, axialTiltDegrees);
        }

        public static CelestialSnapshot EvaluateAtPhase(
            double elapsedSeconds, float day01, float yearSeconds, float moonSeconds, float axialTiltDegrees)
        {
            float orbit01 = Repeat01(elapsedSeconds / math.max(1f, yearSeconds));
            float moon01 = Repeat01(elapsedSeconds / math.max(1f, moonSeconds));
            float dayAngle = day01 * math.PI * 2f;
            float orbitAngle = orbit01 * math.PI * 2f;
            float tilt = math.radians(math.clamp(axialTiltDegrees, -45f, 45f));
            float3 sun = math.normalizesafe(new float3(
                math.cos(dayAngle),
                math.sin(dayAngle) * math.cos(tilt),
                math.sin(dayAngle) * math.sin(tilt) + math.sin(orbitAngle) * 0.08f));
            float moonAngle = moon01 * math.PI * 2f;
            float3 moon = math.normalizesafe(new float3(
                math.cos(moonAngle),
                math.sin(moonAngle) * 0.38f,
                math.sin(moonAngle)));
            float phase = math.saturate((1f - math.dot(sun, moon)) * 0.5f);
            float daylight = math.smoothstep(-0.12f, 0.12f, sun.y);
            return new CelestialSnapshot(day01, orbit01, moon01, sun, moon, phase, 1f - daylight);
        }

        private static float Repeat01(double value)
        {
            double result = value - math.floor(value);
            return (float)(result < 0d ? result + 1d : result);
        }
    }
}
