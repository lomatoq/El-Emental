using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public enum EarthVectorReleaseIntent : byte
    {
        Controlled = 0,
        QuickPulse = 1,
        ProjectileFlick = 2,
        ChargedPulse = 3
    }

    public readonly struct EarthVectorGestureSample
    {
        public EarthVectorGestureSample(
            EarthVectorReleaseIntent intent,
            float strength01,
            float2 screenDirection)
        {
            Intent = intent;
            Strength01 = math.saturate(strength01);
            ScreenDirection = math.normalizesafe(screenDirection);
        }

        public EarthVectorReleaseIntent Intent { get; }
        public float Strength01 { get; }
        public float2 ScreenDirection { get; }
    }

    public static class EarthVectorGestureSolver
    {
        public static EarthVectorGestureSample Classify(
            float heldSeconds,
            float travelViewport,
            float2 releaseVelocityViewportPerSecond,
            float quickTapSeconds = 0.22f,
            float flickMinimumTravelViewport = 0.018f,
            float flickMinimumSpeedViewportPerSecond = 0.50f,
            float flickFullSpeedViewportPerSecond = 2.4f)
        {
            float speed = math.length(releaseVelocityViewportPerSecond);
            if (travelViewport >= math.max(0.001f, flickMinimumTravelViewport) &&
                speed >= math.max(0.01f, flickMinimumSpeedViewportPerSecond))
            {
                float strength = math.unlerp(
                    flickMinimumSpeedViewportPerSecond,
                    math.max(flickMinimumSpeedViewportPerSecond + 0.01f,
                        flickFullSpeedViewportPerSecond),
                    speed);
                return new EarthVectorGestureSample(
                    EarthVectorReleaseIntent.ProjectileFlick,
                    math.saturate(strength),
                    releaseVelocityViewportPerSecond);
            }

            if (heldSeconds <= math.max(0.02f, quickTapSeconds) &&
                travelViewport < math.max(0.001f, flickMinimumTravelViewport))
            {
                return new EarthVectorGestureSample(
                    EarthVectorReleaseIntent.QuickPulse,
                    0.38f,
                    float2.zero);
            }

            return new EarthVectorGestureSample(
                EarthVectorReleaseIntent.Controlled,
                0f,
                float2.zero);
        }
    }
}
