using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Combat;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    [DisallowMultipleComponent]
    public sealed class EarthMvpDuelController : MonoBehaviour
    {
        [SerializeField] private ActiveRagdollPuppet playerPuppet;
        [SerializeField] private HumanoidRagdollRig playerHumanoidRagdoll;
        [SerializeField] private Rigidbody playerBody;
        [SerializeField] private PhysicalImpactTarget playerImpactTarget;
        [SerializeField] private EarthCharacterImpactTarget playerCharacterImpactTarget;
        [SerializeField] private EarthMvpBotController botController;
        [SerializeField] private EarthCombatDummy botCombatBody;
        [SerializeField] private PlanetMotor botMotor;
        [SerializeField] private Rigidbody botBody;
        [SerializeField] private Collider botCollider;
        [SerializeField] private Animator botAnimator;
        [SerializeField] private HumanoidRagdollRig botHumanoidRagdoll;
        [SerializeField] private EarthCharacterImpactTarget botCharacterImpactTarget;
        [SerializeField, Range(3f, 4f)] private float respawnSeconds = 3.5f;
        [SerializeField, Range(0f, 4f)] private float initialPlayerProtectionSeconds = 2.5f;

        private EarthDuelFighterState _playerState = EarthDuelFighterState.Active;
        private EarthDuelFighterState _botState = EarthDuelFighterState.Active;
        private Vector3 _playerSpawnPosition;
        private Quaternion _playerSpawnRotation;
        private Vector3 _botSpawnPosition;
        private Quaternion _botSpawnRotation;
        private RigidbodyConstraints _botMotorConstraints;
        private bool _subscribed;
        private EarthRecoverableKnockdownState _playerKnockdown;
        private EarthRecoverableKnockdownState _botKnockdown;

        public EarthDuelFighterPhase PlayerPhase => _playerState.Phase;
        public EarthDuelFighterPhase BotPhase => _botState.Phase;
        public float PlayerRespawnRemaining => _playerState.RemainingSeconds;
        public float BotRespawnRemaining => _botState.RemainingSeconds;
        public int PlayerKnockoutCount { get; private set; }
        public int BotKnockoutCount { get; private set; }

        public bool IsRecoverablyKnockedDown(EarthDuelFighterId fighter) =>
            fighter == EarthDuelFighterId.Player
                ? _playerKnockdown.IsActive
                : _botKnockdown.IsActive;

        public void Configure(
            ActiveRagdollPuppet configuredPlayerPuppet,
            Rigidbody configuredPlayerBody,
            PhysicalImpactTarget configuredPlayerImpactTarget,
            EarthMvpBotController configuredBotController,
            EarthCombatDummy configuredBotCombatBody,
            PlanetMotor configuredBotMotor,
            Rigidbody configuredBotBody,
            Collider configuredBotCollider,
            Animator configuredBotAnimator,
            HumanoidRagdollRig configuredPlayerHumanoidRagdoll,
            HumanoidRagdollRig configuredBotHumanoidRagdoll,
            EarthCharacterImpactTarget configuredPlayerCharacterImpactTarget,
            EarthCharacterImpactTarget configuredBotCharacterImpactTarget,
            float configuredRespawnSeconds = 3.5f)
        {
            Unsubscribe();
            playerPuppet = configuredPlayerPuppet;
            playerBody = configuredPlayerBody;
            playerImpactTarget = configuredPlayerImpactTarget;
            botController = configuredBotController;
            botCombatBody = configuredBotCombatBody;
            botMotor = configuredBotMotor;
            botBody = configuredBotBody;
            botCollider = configuredBotCollider;
            botAnimator = configuredBotAnimator;
            playerHumanoidRagdoll = configuredPlayerHumanoidRagdoll;
            botHumanoidRagdoll = configuredBotHumanoidRagdoll;
            playerCharacterImpactTarget = configuredPlayerCharacterImpactTarget;
            botCharacterImpactTarget = configuredBotCharacterImpactTarget;
            playerCharacterImpactTarget?.BindDuel(this);
            botCharacterImpactTarget?.BindDuel(this);
            MagicExecutor sharedExecutor = playerCharacterImpactTarget != null
                ? playerCharacterImpactTarget.GetComponent<MagicExecutor>()
                : null;
            if (sharedExecutor != null)
            {
                var worldFanout = new EarthWorldResponseFanoutAdapter(sharedExecutor.Events);
                playerCharacterImpactTarget?.BindWorldResponseFanout(worldFanout);
                botCharacterImpactTarget?.BindWorldResponseFanout(worldFanout);
            }
            respawnSeconds = Mathf.Clamp(configuredRespawnSeconds, 3f, 4f);
            CaptureSpawnPoses();
            _playerState = EarthDuelFighterState.Active;
            _botState = EarthDuelFighterState.Active;
            Subscribe();
        }

        public void RequestRecoverableKnockdown(
            EarthDuelFighterId fighter,
            in RagdollHandoff handoff,
            float physicalSeconds = 0.72f,
            float recoverySeconds = 0.72f)
        {
            if (fighter == EarthDuelFighterId.Player)
            {
                if (_playerState.Phase != EarthDuelFighterPhase.Active ||
                    _playerKnockdown.IsActive || playerHumanoidRagdoll == null)
                    return;
                _playerKnockdown = EarthRecoverableKnockdownState.Begin(
                    physicalSeconds,
                    recoverySeconds);
                playerHumanoidRagdoll.BeginRagdoll(in handoff);
                return;
            }

            if (_botState.Phase != EarthDuelFighterPhase.Active ||
                _botKnockdown.IsActive || botHumanoidRagdoll == null)
                return;
            _botKnockdown = EarthRecoverableKnockdownState.Begin(
                physicalSeconds,
                recoverySeconds);
            if (botController != null) botController.enabled = false;
            if (botMotor != null) botMotor.enabled = false;
            botCombatBody?.ForceFullRagdoll(physicalSeconds + recoverySeconds + 0.2f);
            botHumanoidRagdoll.BeginRagdoll(in handoff);
        }

        public void KnockoutPlayer(Vector3 launchVelocityChange)
        {
            KnockoutPlayer(RagdollHandoff.Uniform(launchVelocityChange));
        }

        public void KnockoutBot(Vector3 launchVelocityChange)
        {
            KnockoutBot(RagdollHandoff.Uniform(launchVelocityChange));
        }

        public void RequestKnockout(EarthDuelFighterId fighter, in RagdollHandoff handoff)
        {
            if (fighter == EarthDuelFighterId.Player) KnockoutPlayer(in handoff);
            else KnockoutBot(in handoff);
        }

        private void KnockoutPlayer(in RagdollHandoff handoff)
        {
            if (_playerState.Phase != EarthDuelFighterPhase.Active || playerPuppet == null) return;
            _playerKnockdown = default;
            _playerState = EarthDuelRespawnSolver.KnockOut(respawnSeconds);
            PlayerKnockoutCount++;
            // The visible rig receives the handoff. Giving the same velocity to the
            // motor puppet first would make the rig inherit it and then apply it a
            // second time during the atomic bone handoff.
            playerPuppet.ForceKnockout(
                playerHumanoidRagdoll != null ? Vector3.zero : handoff.VelocityChange,
                respawnSeconds + 0.2f);
            playerHumanoidRagdoll?.BeginRagdoll(in handoff);
        }

        public void KnockoutBot()
        {
            KnockoutBot(RagdollHandoff.Uniform(Vector3.zero));
        }

        private void KnockoutBot(in RagdollHandoff requestedHandoff)
        {
            if (_botState.Phase != EarthDuelFighterPhase.Active || botBody == null) return;
            _botKnockdown = default;
            _botState = EarthDuelRespawnSolver.KnockOut(respawnSeconds);
            BotKnockoutCount++;
            botCombatBody?.ForceFullRagdoll(respawnSeconds + 0.2f);
            if (botController != null) botController.enabled = false;
            if (botMotor != null) botMotor.enabled = false;
            Vector3 launchVelocity = requestedHandoff.VelocityChange;
            if (launchVelocity.sqrMagnitude < 0.5f)
            {
                launchVelocity = botBody.linearVelocity;
                if (launchVelocity.sqrMagnitude < 0.5f)
                    launchVelocity = (botBody.transform.up * 2.2f) + (botBody.transform.right * 1.4f);
            }
            RagdollHandoff handoff = new RagdollHandoff(
                requestedHandoff.WorldPoint,
                launchVelocity,
                requestedHandoff.HasWorldPoint);
            if (botHumanoidRagdoll != null)
                botHumanoidRagdoll.BeginRagdoll(in handoff);
            else
            {
                if (botAnimator != null) botAnimator.enabled = false;
                botBody.constraints = _botMotorConstraints & ~RigidbodyConstraints.FreezeRotation;
                botBody.angularVelocity += transform.right * 4.2f + transform.forward * 1.6f;
                botBody.WakeUp();
            }
        }

        private void Awake()
        {
            CaptureSpawnPoses();
            float protection = Mathf.Clamp(initialPlayerProtectionSeconds, 0f, 4f);
            if (protection <= 0f) return;

            // Give the scene-authored player enough time to acquire locomotion and
            // Earth control before the bot's first projectile can force a ragdoll.
            // Runtime fixtures configure references after Awake and remain unchanged.
            playerPuppet?.SuppressImpacts(protection);
            playerImpactTarget?.SuppressImpacts(protection);
            playerCharacterImpactTarget?.SuppressImpacts(protection);
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void FixedUpdate()
        {
            StepRecoverableKnockdown(
                EarthDuelFighterId.Player,
                ref _playerKnockdown,
                playerBody,
                playerHumanoidRagdoll);
            StepRecoverableKnockdown(
                EarthDuelFighterId.Bot,
                ref _botKnockdown,
                botBody,
                botHumanoidRagdoll);

            EarthDuelFighterStep playerStep = EarthDuelRespawnSolver.Step(
                in _playerState,
                Time.fixedDeltaTime);
            _playerState = playerStep.State;
            playerHumanoidRagdoll?.SetStoneFade(playerStep.StoneFade01);
            if (playerStep.RespawnThisTick) RespawnPlayer();

            EarthDuelFighterStep botStep = EarthDuelRespawnSolver.Step(
                in _botState,
                Time.fixedDeltaTime);
            _botState = botStep.State;
            botHumanoidRagdoll?.SetStoneFade(botStep.StoneFade01);
            if (botStep.RespawnThisTick) RespawnBot();
        }

        private void HandlePlayerState(CharacterPhysicalState state)
        {
            // Physical mode is presentation/recovery state, not a death verdict.
            // Only typed combat impacts and catastrophic falls call RequestKnockout.
        }

        private void HandleBotState(EarthCombatDummyState state)
        {
            // A bot may stumble, ragdoll and recover without losing the round.
            // Combat KO continues through the shared EarthCharacterImpactTarget.
        }

        private void RespawnPlayer()
        {
            if (playerPuppet == null) return;
            _playerKnockdown = default;
            playerPuppet.ResetPhysicalState(_playerSpawnPosition, _playerSpawnRotation);
            playerHumanoidRagdoll?.ResetToAnimated();
            playerImpactTarget?.SuppressImpacts(0.75f);
            playerCharacterImpactTarget?.SuppressImpacts(0.75f);
        }

        private void RespawnBot()
        {
            if (botBody == null) return;
            _botKnockdown = default;
            botBody.position = _botSpawnPosition;
            botBody.rotation = _botSpawnRotation;
            if (!botBody.isKinematic)
            {
                botBody.linearVelocity = Vector3.zero;
                botBody.angularVelocity = Vector3.zero;
            }
            botBody.constraints = _botMotorConstraints;
            botHumanoidRagdoll?.ResetToAnimated();
            if (botCollider != null) botCollider.enabled = true;
            botCombatBody?.ResetCombatState();
            botCharacterImpactTarget?.SuppressImpacts(0.75f);
            if (botHumanoidRagdoll == null && botAnimator != null)
            {
                botAnimator.enabled = true;
                botAnimator.Play("Locomotion", 0, 0f);
                botAnimator.Update(0f);
            }
            if (botMotor != null) botMotor.enabled = true;
            if (botController != null)
            {
                botController.enabled = true;
                botController.ResetPlanner();
            }
            UnityEngine.Physics.SyncTransforms();
        }

        private void StepRecoverableKnockdown(
            EarthDuelFighterId fighter,
            ref EarthRecoverableKnockdownState state,
            Rigidbody body,
            HumanoidRagdollRig rig)
        {
            if (!state.IsActive || rig == null) return;
            EarthRecoverableKnockdownStep step = EarthRecoverableKnockdownSolver.Step(
                in state,
                Time.fixedDeltaTime);
            state = step.State;
            if (step.BeginAuthoredRecovery)
            {
                Vector3 position = body != null ? body.position : rig.transform.position;
                Vector3 up = position.sqrMagnitude > 0.1f ? position.normalized : rig.transform.up;
                Vector3 forward = body != null
                    ? Vector3.ProjectOnPlane(body.rotation * Vector3.forward, up)
                    : Vector3.ProjectOnPlane(rig.transform.forward, up);
                rig.RecoverToAnimated(up, forward, false);
            }
            if (!step.Completed) return;
            rig.CompleteRecovery();
            if (fighter != EarthDuelFighterId.Bot) return;
            botCombatBody?.ResetCombatState();
            if (botMotor != null) botMotor.enabled = true;
            if (botController != null)
            {
                botController.enabled = true;
                botController.ResetPlanner();
            }
        }

        private void CaptureSpawnPoses()
        {
            if (playerBody != null)
            {
                _playerSpawnPosition = playerBody.position;
                _playerSpawnRotation = playerBody.rotation;
            }
            if (botBody != null)
            {
                _botSpawnPosition = botBody.position;
                _botSpawnRotation = botBody.rotation;
                _botMotorConstraints = botBody.constraints;
            }
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            if (playerPuppet != null) playerPuppet.StateChanged += HandlePlayerState;
            if (botCombatBody != null) botCombatBody.StateChanged += HandleBotState;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (playerPuppet != null) playerPuppet.StateChanged -= HandlePlayerState;
            if (botCombatBody != null) botCombatBody.StateChanged -= HandleBotState;
            _subscribed = false;
        }
    }
}
