using Unity.Mathematics;

namespace Elemental.Simulation.Combat
{
    public enum CharacterOutcome : byte
    {
        Ignore = 0,
        Stumble = 1,
        Stagger = 2,
        RecoverableKnockdown = 3,
        // Compatibility name for old reports and callers. Both names represent
        // the same non-lethal physical fall-and-recover outcome.
        RecoverableRagdoll = RecoverableKnockdown,
        Knockout = 4
    }

    public readonly struct CharacterOutcomeInput
    {
        public CharacterOutcomeInput(
            EarthCharacterImpactSourceKind source,
            float fallDistance,
            float downwardImpactSpeed,
            float effectiveVelocityChange,
            int distinctSourceCount = 1,
            bool knockoutAllowed = true)
        {
            Source = source;
            FallDistance = math.max(0f, fallDistance);
            DownwardImpactSpeed = math.max(0f, downwardImpactSpeed);
            EffectiveVelocityChange = math.max(0f, effectiveVelocityChange);
            DistinctSourceCount = math.max(0, distinctSourceCount);
            KnockoutAllowed = knockoutAllowed;
        }

        public EarthCharacterImpactSourceKind Source { get; }
        public float FallDistance { get; }
        public float DownwardImpactSpeed { get; }
        public float EffectiveVelocityChange { get; }
        public int DistinctSourceCount { get; }
        public bool KnockoutAllowed { get; }
    }

    public static class CharacterOutcomeResolver
    {
        public const float RecoverableFallDistance = 2f;
        public const float KnockoutFallDistance = 5.5f;
        public const float KnockoutDownwardSpeed = 11f;

        public static CharacterOutcome Resolve(in CharacterOutcomeInput input)
        {
            if (input.Source == EarthCharacterImpactSourceKind.FallLanding)
            {
                if (input.FallDistance >= KnockoutFallDistance &&
                    input.DownwardImpactSpeed >= KnockoutDownwardSpeed)
                    return input.KnockoutAllowed
                        ? CharacterOutcome.Knockout
                        : CharacterOutcome.RecoverableKnockdown;
                if (input.FallDistance >= RecoverableFallDistance)
                    return CharacterOutcome.RecoverableKnockdown;
                return input.EffectiveVelocityChange >= 2f
                    ? CharacterOutcome.Stagger
                    : input.EffectiveVelocityChange >= 0.65f
                        ? CharacterOutcome.Stumble
                        : CharacterOutcome.Ignore;
            }

            // A single non-lethal stone may take the fighter off their feet, but
            // never removes the round. Only a concentrated cluster of distinct
            // sources crosses the KO boundary. This stays pure and deterministic;
            // the runtime cluster tracker merely supplies DistinctSourceCount.
            if (IsStone(input.Source))
            {
                if (input.KnockoutAllowed &&
                    input.DistinctSourceCount >= EarthLocalizedHitClusterSolver.FullRagdollHitCount &&
                    input.EffectiveVelocityChange >= EarthLocalizedHitClusterSolver.FullRagdollVelocityChange)
                    return CharacterOutcome.Knockout;
                if (input.EffectiveVelocityChange >= 5f)
                    return CharacterOutcome.RecoverableKnockdown;
                if (input.EffectiveVelocityChange >= 2f) return CharacterOutcome.Stagger;
                if (input.EffectiveVelocityChange >= 0.65f) return CharacterOutcome.Stumble;
                return CharacterOutcome.Ignore;
            }

            bool combatKoSource = input.Source is EarthCharacterImpactSourceKind.SurfNose or
                EarthCharacterImpactSourceKind.StonePunch or
                EarthCharacterImpactSourceKind.PillarCrest or
                EarthCharacterImpactSourceKind.PillarWave or
                EarthCharacterImpactSourceKind.ArmorProjectile or
                EarthCharacterImpactSourceKind.BotProjectile;
            if (input.KnockoutAllowed && combatKoSource && input.EffectiveVelocityChange >= 5f)
                return CharacterOutcome.Knockout;
            if (input.EffectiveVelocityChange >= 5f)
                return CharacterOutcome.RecoverableKnockdown;
            if (input.EffectiveVelocityChange >= 2f) return CharacterOutcome.Stagger;
            if (input.EffectiveVelocityChange >= 0.65f) return CharacterOutcome.Stumble;
            return CharacterOutcome.Ignore;
        }

        private static bool IsStone(EarthCharacterImpactSourceKind source) => source is
            EarthCharacterImpactSourceKind.LooseStone or
            EarthCharacterImpactSourceKind.ArmorProjectile or
            EarthCharacterImpactSourceKind.BotProjectile or
            EarthCharacterImpactSourceKind.StonePunch;
    }
}
