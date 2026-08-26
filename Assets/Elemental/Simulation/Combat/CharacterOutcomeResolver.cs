using Unity.Mathematics;

namespace Elemental.Simulation.Combat
{
    public enum CharacterOutcome : byte
    {
        Ignore = 0,
        Stumble = 1,
        Stagger = 2,
        RecoverableRagdoll = 3,
        Knockout = 4
    }

    public readonly struct CharacterOutcomeInput
    {
        public CharacterOutcomeInput(
            EarthCharacterImpactSourceKind source,
            float fallDistance,
            float downwardImpactSpeed,
            float effectiveVelocityChange)
        {
            Source = source;
            FallDistance = math.max(0f, fallDistance);
            DownwardImpactSpeed = math.max(0f, downwardImpactSpeed);
            EffectiveVelocityChange = math.max(0f, effectiveVelocityChange);
        }

        public EarthCharacterImpactSourceKind Source { get; }
        public float FallDistance { get; }
        public float DownwardImpactSpeed { get; }
        public float EffectiveVelocityChange { get; }
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
                    return CharacterOutcome.Knockout;
                if (input.FallDistance >= RecoverableFallDistance)
                    return CharacterOutcome.RecoverableRagdoll;
                return input.EffectiveVelocityChange >= 2f
                    ? CharacterOutcome.Stagger
                    : input.EffectiveVelocityChange >= 0.65f
                        ? CharacterOutcome.Stumble
                        : CharacterOutcome.Ignore;
            }

            bool combatKoSource = input.Source is EarthCharacterImpactSourceKind.SurfNose or
                EarthCharacterImpactSourceKind.StonePunch or
                EarthCharacterImpactSourceKind.PillarCrest or
                EarthCharacterImpactSourceKind.PillarWave or
                EarthCharacterImpactSourceKind.ArmorProjectile or
                EarthCharacterImpactSourceKind.BotProjectile;
            if (combatKoSource && input.EffectiveVelocityChange >= 5f)
                return CharacterOutcome.Knockout;
            if (input.EffectiveVelocityChange >= 5f)
                return CharacterOutcome.RecoverableRagdoll;
            if (input.EffectiveVelocityChange >= 2f) return CharacterOutcome.Stagger;
            if (input.EffectiveVelocityChange >= 0.65f) return CharacterOutcome.Stumble;
            return CharacterOutcome.Ignore;
        }
    }
}
