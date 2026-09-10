using Unity.Mathematics;
using Elemental.Simulation.Structures;

namespace Elemental.Simulation.Combat
{
    public enum EarthStoneCounterTier : byte { Small, Medium, Large }
    public readonly struct EarthStoneCounterDecision
    {
        public EarthStoneCounterDecision(EarthStoneCounterTier tier, float closingSpeed, float recoilSpeed,
            float interceptSeconds, EarthRockBreakDecision fracture)
        { Accepted = true; Tier = tier; ClosingSpeed = closingSpeed; RecoilSpeed = recoilSpeed;
          InterceptSeconds = interceptSeconds; Fracture = fracture; }
        public bool Accepted { get; }
        public EarthStoneCounterTier Tier { get; }
        public float ClosingSpeed { get; }
        public float RecoilSpeed { get; }
        public float InterceptSeconds { get; }
        public EarthRockBreakDecision Fracture { get; }
    }

    /// <summary>Finite swept hand-space admission, independent of physics and presentation.</summary>
    public static class EarthStoneCounterPolicy
    {
        public const float Reach = 1.25f;
        public const float HalfWidth = .65f;
        public const float MinimumClosingSpeed = 2f;
        public const float MaximumSourceRadius = 2.4f;
        public const float MaximumRecoilSpeed = 4.5f;
        public static EarthStoneCounterDecision Evaluate(float3 relativePosition, float3 relativeVelocity,
            float3 forward, float radius, float sourceMass, float defenderMass, float fixedDelta,
            float smallRadius = .35f, float largeRadius = 1.2f)
        {
            if (!math.all(math.isfinite(relativePosition)) || !math.all(math.isfinite(relativeVelocity)) ||
                !math.all(math.isfinite(forward)) || math.lengthsq(forward) < .5f ||
                !math.isfinite(radius) || radius <= 0f || radius > MaximumSourceRadius ||
                !math.isfinite(sourceMass) || sourceMass <= 0f ||
                !math.isfinite(defenderMass) || defenderMass <= 0f ||
                !math.isfinite(fixedDelta) || fixedDelta <= 0f || fixedDelta > .1f ||
                !math.isfinite(smallRadius) || smallRadius <= 0f ||
                !math.isfinite(largeRadius) || largeRadius < smallRadius) return default;
            forward = math.normalize(forward);
            float longitudinal = math.dot(relativePosition, forward);
            if (longitudinal <= .15f) return default;
            float closing = -math.dot(relativeVelocity, forward);
            if (closing < MinimumClosingSpeed) return default;
            float intercept = math.max(0f, (longitudinal - Reach - radius) / closing);
            if (intercept > fixedDelta) return default;
            float3 atContact = relativePosition + relativeVelocity * intercept;
            float3 lateral = atContact - forward * math.dot(atContact, forward);
            if (math.lengthsq(lateral) > (HalfWidth + radius) * (HalfWidth + radius) ||
                math.dot(relativePosition, relativeVelocity) >= 0f) return default;
            bool small = radius <= smallRadius, large = radius > largeRadius;
            var tier = small ? EarthStoneCounterTier.Small : large ? EarthStoneCounterTier.Large : EarthStoneCounterTier.Medium;
            float recoil = large ? math.clamp(sourceMass * closing / (defenderMass + sourceMass) * .3f, 1.2f, MaximumRecoilSpeed) : 0f;
            return new EarthStoneCounterDecision(tier, closing, recoil, intercept,
                new EarthRockBreakDecision(true, small ? 0 : large ? 3 : 2,
                    small ? 32 : large ? 140 : 72, small ? 8 : large ? 28 : 16));
        }
    }
}
