using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    /// <summary>Ground contact is authority; toggling perception never grants air vision.</summary>
    public static class EarthSeismicPerception
    {
        public static bool CanPerceive(bool requested, bool earthMage, bool stableSupport,
            bool acceptsSupport, bool mantleActive) =>
            requested && earthMage && stableSupport && acceptsSupport && !mantleActive;

        public static float Radius(float age, float maximumRadius, float duration) =>
            math.max(0f, maximumRadius) * math.saturate(age / math.max(0.15f, duration));

        public static float Strength(float age, float duration) =>
            age < 0f || age >= math.max(0.15f, duration) ? 0f :
            1f - math.smoothstep(0.65f, 1f, age / math.max(0.15f, duration));

        // Presentation envelope only; contact authority remains CanPerceive.
        public static float AdvanceFade(float progress, bool requested, float deltaTime) =>
            math.saturate(progress + (requested ? 1f : -1f) *
                math.max(0f, deltaTime)); // One second across the full envelope.
    }
}
