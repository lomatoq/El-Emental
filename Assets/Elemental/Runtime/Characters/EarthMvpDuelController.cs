using System;
using Unity.Profiling;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Combat;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    [DisallowMultipleComponent]
    public sealed partial class EarthMvpDuelController : MonoBehaviour
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

        [SerializeField, Min(1f)] private float roundDurationSeconds = 300f;
        [SerializeField, Min(1f)] private float maximumHealth = 100f;
        [SerializeField] private EarthDuelDamageSettings damageSettings = EarthDuelDamageSettings.Default;
        private static readonly ProfilerMarker MatchMarker = new ProfilerMarker("Elemental.Duel.MatchTick");
        [SerializeField] private Behaviour[] roundInputBehaviours = Array.Empty<Behaviour>();
        private bool[] _roundControlWasEnabled;
        private bool _roundControlsSuspended;
        private EarthDuelMatchState _match;
        private EarthDuelMatchState Match => _match ??= new EarthDuelMatchState(
            Mathf.Max(1f, roundDurationSeconds), Mathf.Max(1f, maximumHealth));
        public float PlayerHealth => Match.PlayerHealth;
        public float BotHealth => Match.BotHealth;
        public float MaximumHealth => Match.MaximumHealth;
        public int PlayerScore => Match.PlayerScore;
        public int BotScore => Match.BotScore;
        public float RoundRemainingSeconds => Match.RemainingSeconds;
        public bool IsRoundOver => Match.IsOver;
        public bool CombatAllowed => Match.CombatAllowed;
        public Transform PlayerTransform => playerBody != null ? playerBody.transform : null;
        public Transform BotTransform => botBody != null ? botBody.transform : null;
        public event Action StateChanged;
        public event Action RoundRestarted;

        public void ConfigureRoundControls(params Behaviour[] controls)
        {
            SetRoundControlsSuspended(false);
            roundInputBehaviours = controls ?? Array.Empty<Behaviour>();
            _roundControlWasEnabled = new bool[roundInputBehaviours.Length];
            if (Application.isPlaying) SetRoundControlsSuspended(!CombatAllowed);
        }

        private void SetRoundControlsSuspended(bool suspended)
        {
            if (_roundControlsSuspended == suspended)
            {
                // Respawn/ragdoll reset may re-enable a registered control while
                // the enclosing menu/restore gate is still closed. Reassert the
                // gate without recapturing the original enabled-state ledger.
                if (suspended)
                    for (int index = 0; index < roundInputBehaviours.Length; index++)
                    {
                        Behaviour control = roundInputBehaviours[index];
                        if (control != null && control != this) control.enabled = false;
                    }
                return;
            }
            if (_roundControlWasEnabled == null || _roundControlWasEnabled.Length != roundInputBehaviours.Length)
                _roundControlWasEnabled = new bool[roundInputBehaviours.Length];
            _roundControlsSuspended = suspended;
            for (int index = 0; index < roundInputBehaviours.Length; index++)
            {
                Behaviour control = roundInputBehaviours[index];
                if (control == null || control == this) continue;
                if (suspended)
                {
                    _roundControlWasEnabled[index] = control.enabled;
                    control.enabled = false;
                }
                else if (_roundControlWasEnabled[index]) control.enabled = true;
            }
            if (suspended && playerBody != null)
            {
                MagicExecutor executor = playerBody.GetComponent<MagicExecutor>();
                executor?.CancelHeldEarthControl();
                executor?.CancelVectorField();
                executor?.CancelGravityWell();
                playerBody.GetComponent<EarthSurfController>()?.Cancel();
            }
        }

        public void SetRoundReady(bool ready)
        {
            if (ArenaResetInProgress) { _resumeAfterArenaRestore = ready; ready = false; }
            if (ready) CaptureArenaBaselineIfReady();
            Match.IsReady = ready;
            SetRoundControlsSuspended(!CombatAllowed);
            if (botController != null) botController.enabled = CombatAllowed && _botState.Phase == EarthDuelFighterPhase.Active;
            StateChanged?.Invoke();
        }

        public void RestartRound()
        {
            if (!HasSimulationAuthority) return;
            CaptureArenaBaselineIfReady();
            bool resume = Match.IsReady;
            RestoreArenaForMatchBoundary();
            if (ArenaResetInProgress) _resumeAfterArenaRestore = resume;
            MarkArenaMatchStarted();
            Match.Restart();
            PlayerKnockoutCount = BotKnockoutCount = 0;
            _playerState = _botState = EarthDuelFighterState.Active;
            if (ArenaResetInProgress) _respawnAfterArenaRestore = true;
            else { RespawnPlayer(); RespawnBot(); }
            if (botController != null) botController.enabled = CombatAllowed;
            SetRoundControlsSuspended(!CombatAllowed);
            RoundRestarted?.Invoke();
            StateChanged?.Invoke();
        }

        public bool CanReceiveDamage(EarthDuelFighterId fighter) => HasSimulationAuthority && CombatAllowed &&
            (fighter == EarthDuelFighterId.Player ? _playerState.Phase : _botState.Phase) == EarthDuelFighterPhase.Active;

        public float ResolveDamage(EarthCharacterImpactSourceKind source, float reactionVelocity, float closingSpeed) =>
            damageSettings.Resolve(source, reactionVelocity, closingSpeed);

        public bool ApplyDamage(EarthDuelFighterId fighter, float amount, in RagdollHandoff handoff)
        {
            if (!CanReceiveDamage(fighter)) return false;
            bool died = Match.Damage(fighter, amount);
            if (died)
            {
                if (fighter == EarthDuelFighterId.Player) KnockoutPlayer(in handoff);
                else KnockoutBot(in handoff);
            }
            StateChanged?.Invoke();
            return died;
        }

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
            Match.Restart();
            SetRoundReady(true);
            Subscribe();
        }

        public void RequestRecoverableKnockdown(
            EarthDuelFighterId fighter,
            in RagdollHandoff handoff,
            float physicalSeconds = 0.72f,
            float recoverySeconds = 0.72f)
        {
            if (!CanReceiveDamage(fighter)) return;
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
            RequestKnockout(EarthDuelFighterId.Player, RagdollHandoff.Uniform(launchVelocityChange));
        }

        public void KnockoutBot(Vector3 launchVelocityChange)
        {
            RequestKnockout(EarthDuelFighterId.Bot, RagdollHandoff.Uniform(launchVelocityChange));
        }

        public void RequestKnockout(EarthDuelFighterId fighter, in RagdollHandoff handoff)
        {
            ApplyDamage(fighter, Match.MaximumHealth, in handoff);
        }

        private void KnockoutPlayer(in RagdollHandoff handoff)
        {
            if (_playerState.Phase != EarthDuelFighterPhase.Active) return;
            _playerKnockdown = default;
            _playerState = EarthDuelRespawnSolver.KnockOut(respawnSeconds);
            PlayerKnockoutCount++;
            // The visible rig receives the handoff. Giving the same velocity to the
            // motor puppet first would make the rig inherit it and then apply it a
            // second time during the atomic bone handoff.
            playerPuppet?.ForceKnockout(
                playerHumanoidRagdoll != null ? Vector3.zero : handoff.VelocityChange,
                respawnSeconds + 0.2f);
            playerHumanoidRagdoll?.BeginRagdoll(in handoff);
        }

        public void KnockoutBot()
        {
            RequestKnockout(EarthDuelFighterId.Bot, RagdollHandoff.Uniform(Vector3.zero));
        }

        private void KnockoutBot(in RagdollHandoff requestedHandoff)
        {
            if (_botState.Phase != EarthDuelFighterPhase.Active) return;
            _botKnockdown = default;
            _botState = EarthDuelRespawnSolver.KnockOut(respawnSeconds);
            BotKnockoutCount++;
            if (botBody == null) return;
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
            playerCharacterImpactTarget?.BindDuel(this);
            botCharacterImpactTarget?.BindDuel(this);
            // HUD.OnEnable may already have captured these flags before this Awake.
            if (_roundControlWasEnabled == null || _roundControlWasEnabled.Length != roundInputBehaviours.Length)
                _roundControlWasEnabled = new bool[roundInputBehaviours.Length];
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
            if (!HasSimulationAuthority) return;
            if (ArenaResetInProgress) return;
            using (MatchMarker.Auto())
            {
                bool wasOver = Match.IsOver;
                Match.Step(Time.fixedDeltaTime);
                if (!wasOver && Match.IsOver)
                {
                    RestoreArenaForMatchBoundary();
                    if (botController != null) botController.enabled = false;
                    SetRoundControlsSuspended(true);
                    StateChanged?.Invoke();
                }
            }
            if (!CombatAllowed) return;
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
            Match.Respawn(EarthDuelFighterId.Player);
            StateChanged?.Invoke();
            if (playerPuppet == null) return;
            _playerKnockdown = default;
            playerPuppet.ResetPhysicalState(_playerSpawnPosition, _playerSpawnRotation);
            playerHumanoidRagdoll?.ResetToAnimated();
            playerBody?.GetComponent<PlanetMotor>()?.ResetAfterTeleport();
            playerImpactTarget?.SuppressImpacts(0.75f);
            playerCharacterImpactTarget?.SuppressImpacts(0.75f);
        }

        private void RespawnBot()
        {
            Match.Respawn(EarthDuelFighterId.Bot);
            StateChanged?.Invoke();
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
            botMotor?.ResetAfterTeleport();
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
            if (!state.IsActive || rig == null || rig.ImpactReceiver != null && rig.ImpactReceiver.HasBlockingCrushContact) return;
            EarthRecoverableKnockdownStep step = EarthRecoverableKnockdownSolver.Step(
                in state,
                Time.fixedDeltaTime);
            if (step.BeginAuthoredRecovery)
            {
                Vector3 position = body != null ? body.position : rig.transform.position;
                Vector3 up = position.sqrMagnitude > 0.1f ? position.normalized : rig.transform.up;
                Vector3 forward = body != null
                    ? Vector3.ProjectOnPlane(body.rotation * Vector3.forward, up)
                    : Vector3.ProjectOnPlane(rig.transform.forward, up);
                if (!rig.TryRecoverToAnimated(up, forward, false)) return;
            }
            state = step.State;
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
