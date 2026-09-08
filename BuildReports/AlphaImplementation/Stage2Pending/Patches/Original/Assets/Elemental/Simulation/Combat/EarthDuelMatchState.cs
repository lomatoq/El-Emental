using System;

namespace Elemental.Simulation.Combat
{
    /// <summary>Explicit match owner; physical knockdown is deliberately independent of death.</summary>
    public sealed class EarthDuelMatchState
    {
        public EarthDuelMatchState(float durationSeconds = 300f, float maximumHealth = 100f)
        {
            if (!float.IsFinite(durationSeconds) || durationSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            if (!float.IsFinite(maximumHealth) || maximumHealth <= 0f)
                throw new ArgumentOutOfRangeException(nameof(maximumHealth));
            DurationSeconds = durationSeconds;
            MaximumHealth = maximumHealth;
            Restart();
        }

        public float DurationSeconds { get; }
        public float MaximumHealth { get; }
        public float PlayerHealth { get; private set; }
        public float BotHealth { get; private set; }
        public int PlayerScore { get; private set; }
        public int BotScore { get; private set; }
        public float RemainingSeconds { get; private set; }
        public bool IsOver => RemainingSeconds <= 0f;
        public bool IsReady { get; set; }
        public bool CombatAllowed => IsReady && !IsOver;

        public void Restart()
        {
            PlayerHealth = BotHealth = MaximumHealth;
            PlayerScore = BotScore = 0;
            RemainingSeconds = DurationSeconds;
        }

        public void Step(float deltaSeconds)
        {
            if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (CombatAllowed) RemainingSeconds = Math.Max(0f, RemainingSeconds - deltaSeconds);
        }

        /// <returns>True only on the transition from living to dead.</returns>
        public bool Damage(EarthDuelFighterId fighter, float amount)
        {
            if (!float.IsFinite(amount) || amount < 0f)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (!CombatAllowed || amount <= 0f) return false;
            if (fighter == EarthDuelFighterId.Player)
            {
                if (PlayerHealth <= 0f) return false;
                PlayerHealth = Math.Max(0f, PlayerHealth - amount);
                if (PlayerHealth > 0f) return false;
                BotScore++;
            }
            else
            {
                if (BotHealth <= 0f) return false;
                BotHealth = Math.Max(0f, BotHealth - amount);
                if (BotHealth > 0f) return false;
                PlayerScore++;
            }
            return true;
        }

        public void Respawn(EarthDuelFighterId fighter)
        {
            if (IsOver) return;
            if (fighter == EarthDuelFighterId.Player) PlayerHealth = MaximumHealth;
            else BotHealth = MaximumHealth;
        }
    }

    [Serializable]
    public struct EarthDuelDamageSettings
    {
        public float QuickStone;
        public float ArmorProjectile;
        public float SurfNose;
        public float PillarWave;
        public float PillarCrest;
        public float PhysicsPerVelocity;
        public float PhysicsMinimumVelocity;
        public float PhysicsMaximumDamage;

        public static EarthDuelDamageSettings Default => new EarthDuelDamageSettings
        {
            QuickStone = 8f, ArmorProjectile = 10f,
            SurfNose = 20f, PillarWave = 16f, PillarCrest = 20f,
            PhysicsPerVelocity = 4f, PhysicsMinimumVelocity = 2f, PhysicsMaximumDamage = 25f
        };

        public float Resolve(EarthCharacterImpactSourceKind kind, float reactionVelocity, float closingSpeed)
        {
            float value;
            switch (kind)
            {
                case EarthCharacterImpactSourceKind.StonePunch:
                case EarthCharacterImpactSourceKind.BotProjectile:
                case EarthCharacterImpactSourceKind.LooseStone:
                    value = EarthCharacterImpactSolver.StoneDamage(QuickStone, reactionVelocity); break;
                case EarthCharacterImpactSourceKind.ArmorProjectile:
                    value = EarthCharacterImpactSolver.StoneDamage(ArmorProjectile, reactionVelocity); break;
                case EarthCharacterImpactSourceKind.SurfNose: value = SurfNose; break;
                case EarthCharacterImpactSourceKind.PillarWave: value = PillarWave; break;
                case EarthCharacterImpactSourceKind.PillarCrest: value = PillarCrest; break;
                default:
                    float velocity = Math.Min(reactionVelocity, closingSpeed);
                    value = Math.Min(PhysicsMaximumDamage, Math.Max(0f, velocity - PhysicsMinimumVelocity) * PhysicsPerVelocity);
                    break;
            }
            return float.IsFinite(value) ? Math.Max(0f, value) : 0f;
        }
    }
}
