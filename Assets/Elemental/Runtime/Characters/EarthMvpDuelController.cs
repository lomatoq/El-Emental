using Elemental.Runtime.Physics;
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

        private EarthDuelFighterState _playerState = EarthDuelFighterState.Active;
        private EarthDuelFighterState _botState = EarthDuelFighterState.Active;
        private Vector3 _playerSpawnPosition;
        private Quaternion _playerSpawnRotation;
        private Vector3 _botSpawnPosition;
        private Quaternion _botSpawnRotation;
        private RigidbodyConstraints _botMotorConstraints;
        private bool _subscribed;

        public EarthDuelFighterPhase PlayerPhase => _playerState.Phase;
        public EarthDuelFighterPhase BotPhase => _botState.Phase;
        public float PlayerRespawnRemaining => _playerState.RemainingSeconds;
        public float BotRespawnRemaining => _botState.RemainingSeconds;
        public int PlayerKnockoutCount { get; private set; }
        public int BotKnockoutCount { get; private set; }

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
            respawnSeconds = Mathf.Clamp(configuredRespawnSeconds, 3f, 4f);
            CaptureSpawnPoses();
            _playerState = EarthDuelFighterState.Active;
            _botState = EarthDuelFighterState.Active;
            Subscribe();
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
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void FixedUpdate()
        {
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
            playerPuppet.ResetPhysicalState(_playerSpawnPosition, _playerSpawnRotation);
            playerHumanoidRagdoll?.ResetToAnimated();
            playerImpactTarget?.SuppressImpacts(0.75f);
            playerCharacterImpactTarget?.SuppressImpacts(0.75f);
        }

        private void RespawnBot()
        {
            if (botBody == null) return;
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
