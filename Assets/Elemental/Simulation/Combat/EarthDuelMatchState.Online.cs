using System;

namespace Elemental.Simulation.Combat
{
    public sealed partial class EarthDuelMatchState
    {
        /// <summary>Canonical replica replacement; never calls Damage/Respawn or increments score.</summary>
        public bool TryApplyReplica(float playerHealth, float botHealth, int playerScore, int botScore,
            float remainingSeconds, bool ready)
        {
            if (!float.IsFinite(playerHealth) || !float.IsFinite(botHealth) || !float.IsFinite(remainingSeconds) ||
                playerHealth < 0 || playerHealth > MaximumHealth || botHealth < 0 || botHealth > MaximumHealth ||
                playerScore < 0 || botScore < 0 || remainingSeconds < 0 || remainingSeconds > DurationSeconds)
                return false;
            PlayerHealth = playerHealth; BotHealth = botHealth;
            PlayerScore = playerScore; BotScore = botScore;
            RemainingSeconds = remainingSeconds; IsReady = ready;
            return true;
        }
    }
}
