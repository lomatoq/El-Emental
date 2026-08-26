using Unity.Mathematics;

namespace Elemental.Simulation.Combat
{
    public readonly struct EarthArmorImpact
    {
        public EarthArmorImpact(float projectileMass, float relativeSpeed, int primaryPieceIndex)
        {
            ProjectileMass = math.max(0f, projectileMass);
            RelativeSpeed = math.max(0f, relativeSpeed);
            PrimaryPieceIndex = primaryPieceIndex;
        }

        public float ProjectileMass { get; }
        public float RelativeSpeed { get; }
        public int PrimaryPieceIndex { get; }
        public float Momentum => ProjectileMass * RelativeSpeed;
    }

    public readonly struct EarthArmorDamageResult
    {
        public EarthArmorDamageResult(int damageBudget, float absorbedImpulse, float residualVelocityFraction)
        {
            DamageBudget = math.clamp(damageBudget, 1, 12);
            AbsorbedImpulse = math.max(0f, absorbedImpulse);
            ResidualVelocityFraction = math.saturate(residualVelocityFraction);
        }

        public int DamageBudget { get; }
        public float AbsorbedImpulse { get; }
        public float ResidualVelocityFraction { get; }
        public bool FullyBlocked => ResidualVelocityFraction <= 0.0001f;
    }

    public static class EarthArmorDamageResolver
    {
        public const float ImpulseAbsorbedPerPlate = 75f;

        public static EarthArmorDamageResult Resolve(in EarthArmorImpact impact)
        {
            float momentum = impact.Momentum;
            int budget = math.clamp((int)math.round(momentum / 75f), 1, 12);
            float absorbed = math.min(momentum, budget * ImpulseAbsorbedPerPlate);
            float residual = math.max(0f, momentum - absorbed);
            float residualFraction = momentum > 0.001f
                ? residual / momentum
                : 0f;
            if (residualFraction > 0f) residualFraction = math.clamp(residualFraction, 0.20f, 0.55f);
            return new EarthArmorDamageResult(budget, absorbed, residualFraction);
        }
    }
}
