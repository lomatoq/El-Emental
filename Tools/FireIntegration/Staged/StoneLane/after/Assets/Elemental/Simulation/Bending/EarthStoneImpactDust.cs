using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    /// <summary>Cosmetic impact response only. Mass is supplied by the canonical body owner.</summary>
    public static class EarthStoneImpactDust
    {
        public const float MinimumClosingSpeed = .25f;
        public const float CooldownSeconds = .12f;
        public static float Strength(float mass, float closingSpeed)
        {
            if (!math.isfinite(mass) || !math.isfinite(closingSpeed) || mass <= 0f ||
                closingSpeed < MinimumClosingSpeed) return 0f;
            // Normal kinetic energy distinguishes a heavy short drop from a light tap.
            // Log compression bounds count/velocity growth without scaling every mote.
            float speed = math.min(closingSpeed, 100f);
            float energy = .5f * math.min(mass, 1000000f) * speed * speed;
            return math.clamp(.25f + .32f * math.log2(1f + energy / 12f), .25f, 2.4f);
        }
        public static float FineBiasedSize(float unitSample)
        {
            float u = math.saturate(unitSample);
            return u * u * u;
        }
        public static uint CueSeed(uint source, uint generation, EarthMaterialFeedbackKind kind, float3 point)
        {
            uint h = math.hash(new uint4(source, generation, (uint)kind,
                math.hash(math.asuint(math.round(point * 64f)))));
            return h == 0u ? 7919u : h;
        }
    }
}
