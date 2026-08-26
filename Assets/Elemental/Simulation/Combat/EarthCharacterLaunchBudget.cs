namespace Elemental.Simulation.Combat
{
    public readonly struct EarthCharacterLaunchBudget
    {
        public EarthCharacterLaunchBudget(float maximumRiseMeters, float maximumTangentSpeed)
        {
            MaximumRiseMeters = maximumRiseMeters < 0.1f ? 0.1f : maximumRiseMeters;
            MaximumTangentSpeed = maximumTangentSpeed < 0.1f ? 0.1f : maximumTangentSpeed;
        }

        public float MaximumRiseMeters { get; }
        public float MaximumTangentSpeed { get; }
    }

    public static class EarthCharacterLaunchBudgetSolver
    {
        public const float CastScopedDedupeSeconds = 0.85f;

        public static EarthCharacterLaunchBudget Resolve(
            EarthCharacterImpactSourceKind source,
            float profileMaximumRise,
            float profileMaximumTangentSpeed)
        {
            float rise = profileMaximumRise < 0.1f ? 0.1f : profileMaximumRise;
            float tangent = profileMaximumTangentSpeed < 0.1f ? 0.1f : profileMaximumTangentSpeed;
            return source switch
            {
                EarthCharacterImpactSourceKind.PillarCrest =>
                    new EarthCharacterLaunchBudget(Min(rise, 0.75f), Min(tangent, 2.2f)),
                EarthCharacterImpactSourceKind.PillarWave =>
                    new EarthCharacterLaunchBudget(Min(rise, 1.0f), Min(tangent, 2.8f)),
                EarthCharacterImpactSourceKind.SurfNose =>
                    new EarthCharacterLaunchBudget(Min(rise, 1.0f), Min(tangent, 3.0f)),
                _ => new EarthCharacterLaunchBudget(rise, tangent)
            };
        }

        public static bool UsesCastScopedDedupe(EarthCharacterImpactSourceKind source) =>
            source is EarthCharacterImpactSourceKind.PillarCrest or
                EarthCharacterImpactSourceKind.PillarWave or
                EarthCharacterImpactSourceKind.SurfNose;

        public static bool IsCastScopedDuplicate(
            EarthCharacterImpactSourceKind source,
            uint sourceStableId,
            float time,
            uint previousSourceStableId,
            float previousTime)
        {
            if (!UsesCastScopedDedupe(source) || sourceStableId == 0u ||
                sourceStableId != previousSourceStableId) return false;
            float elapsed = time - previousTime;
            return elapsed >= 0f && elapsed <= CastScopedDedupeSeconds;
        }

        private static float Min(float a, float b) => a < b ? a : b;
    }
}
