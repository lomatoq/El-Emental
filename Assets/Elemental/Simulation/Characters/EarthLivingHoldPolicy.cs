using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    /// <summary>Read-only presentation admission. Never prolongs a gameplay hold.</summary>
    public static class EarthLivingHoldPolicy
    {
        public const string LayerName = "Earth Living Hold";
        public const float MaximumWeight = .35f;

        public static float TargetWeight(bool casting, bool held, bool renderedContact,
            bool physicalOverride, float castWeight) =>
            casting && held && renderedContact && !physicalOverride && math.isfinite(castWeight)
                ? MaximumWeight * math.saturate(castWeight) : 0f;
    }
}
