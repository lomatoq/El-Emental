using Elemental.Simulation.Combat;
using Elemental.Simulation.Characters;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    public sealed partial class EarthMvpDuelController
    {
        public bool HasSimulationAuthority { get; private set; } = true;
        public void ConfigureOnlineAuthority(bool authority)
        { CancelRespawnPresentations(); HasSimulationAuthority = authority; SetRoundReady(false); }
        public EarthRecoverableKnockdownPhase GetKnockdownPhase(EarthDuelFighterId fighter) =>
            fighter == EarthDuelFighterId.Player ? _playerKnockdown.Phase : _botKnockdown.Phase;

        public bool ApplyReplicaMatch(float playerHealth, float botHealth, int playerScore, int botScore,
            float remainingSeconds, bool ready)
        {
            if (HasSimulationAuthority || !Match.TryApplyReplica(playerHealth, botHealth, playerScore,
                botScore, remainingSeconds, ready)) return false;
            SetRoundControlsSuspended(!CombatAllowed);
            if (botController != null) botController.enabled = false;
            StateChanged?.Invoke(); return true;
        }

        public bool ApplyReplicaFighter(EarthDuelFighterId fighter, EarthDuelFighterPhase phase,
            float respawnRemaining, EarthRecoverableKnockdownPhase knockdown)
        {
            if (HasSimulationAuthority || phase > EarthDuelFighterPhase.KnockedOut ||
                knockdown > EarthRecoverableKnockdownPhase.AuthoredRecovery || !float.IsFinite(respawnRemaining) ||
                respawnRemaining < 0 || respawnRemaining > 4f) return false;
            bool player = fighter == EarthDuelFighterId.Player;
            EarthDuelFighterState prior = player ? _playerState : _botState;
            EarthRecoverableKnockdownState previousKnockdown = player ? _playerKnockdown : _botKnockdown;
            HumanoidRagdollRig rig = player ? playerHumanoidRagdoll : botHumanoidRagdoll;
            Rigidbody body = player ? playerBody : botBody;
            var current = new EarthDuelFighterState(phase, respawnRemaining);
            var recovery = new EarthRecoverableKnockdownState(knockdown, 0, .72f, .72f);
            if (player) { _playerState = current; _playerKnockdown = recovery; }
            else { _botState = current; _botKnockdown = recovery; }

            bool beginPhysical = phase == EarthDuelFighterPhase.KnockedOut && prior.Phase != phase ||
                knockdown == EarthRecoverableKnockdownPhase.Physical && previousKnockdown.Phase != knockdown;
            if (beginPhysical) rig?.BeginRagdoll(RagdollHandoff.Uniform(Vector3.zero));
            if (knockdown == EarthRecoverableKnockdownPhase.AuthoredRecovery && previousKnockdown.Phase != knockdown && rig != null)
            {
                Vector3 up = body != null && body.position.sqrMagnitude > .1f ? body.position.normalized : transform.up;
                Vector3 forward = body != null ? Vector3.ProjectOnPlane(body.rotation * Vector3.forward, up) : transform.forward;
                rig.RecoverToAnimated(up, forward, false);
            }
            if (phase == EarthDuelFighterPhase.Active && prior.Phase != phase) rig?.ResetToAnimated();
            else if (knockdown == EarthRecoverableKnockdownPhase.Inactive && previousKnockdown.IsActive) rig?.CompleteRecovery();
            rig?.SetStoneFade(phase == EarthDuelFighterPhase.KnockedOut ? 1f - Mathf.Clamp01(respawnRemaining / .35f) : 0f);
            return true;
        }
    }
}
