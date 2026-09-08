using System;
using System.IO;
using Elemental.Presentation.UI;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Elemental.Simulation.Networking;
using Elemental.Simulation.Combat;
using System.Collections.Generic;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace Elemental.Online
{
    /// <summary>Opt-in real-service development smoke test. Never bypasses readiness or fabricates a room.</summary>
    [DisallowMultipleComponent]
    public sealed class OnlineDevelopmentProbe : MonoBehaviour
    {
        [SerializeField] private EarthOnlineFrontend frontend;
        [SerializeField] private MpsRelaySession session;
        [SerializeField] private NgoGameplayTransport transport;
        [SerializeField] private EarthOnlineGameplayBinding binding;
        public void Configure(EarthOnlineFrontend view, MpsRelaySession relay,
            NgoGameplayTransport gameplay, EarthOnlineGameplayBinding world)
        { frontend = view; session = relay; transport = gameplay; binding = world; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Serializable]
        private sealed class ImpactEvidence
        {
            public uint responseId, sourceId, targetId, tick;
            public string sourceKind, response, region;
            public float impulse, energy, healthAfter;
            public bool authority, selectedForShot;
        }
        [Serializable]
        private sealed class ShotEvidence
        {
            public string stage;
            public uint sequence, sourceId;
            public Vector3 sourcePosition, targetPosition, targetExtents, rayOrigin, rayDirection;
            public Vector2 screenPoint;
            public float sourceMass, parallelMissMeters, targetDistanceAlongRay;
            public bool visible, hasHeldBody, quickPrimed;
        }
        [Serializable]
        private sealed class Report
        {
            public string utc, role, outcome, code, sessionPhase, sessionStatus, transportStatus, frontendState;
            public string buildHash, worldHash;
            public string requestedScenario;
            public int sendDelayMs, sendJitterMs, sendLossPercent, attemptedShots;
            public bool simulatorApplied, attributedStoneHit, attributedStoneKill, replicatedStoneKill;
            public uint stoneKillResponseId, stoneKillSourceId;
            public int arenaResetCount, stoneKillArenaResetCount;
            public uint worldRevision, stoneKillWorldRevision;
            public bool arenaResetInProgress, lifeRespawnPreservedArena;
            public string arenaResetError;
            public int offlineArenaResetCount;
            public bool menuArenaRestored;
            public ulong currentRttMs, maximumRttMs;
            public NgoGameplayTransport.InputDiagnostics inputs;
            public List<ImpactEvidence> impacts = new List<ImpactEvidence>();
            public List<ShotEvidence> shots = new List<ShotEvidence>();
            public int unavailableAimCount;
            public ulong localClientId;
            public int connectedPeerCount;
            public bool listening, connected, canHandshake, worldReady, running;
            public float remainingCountdown, elapsedSeconds, playerHealth, botHealth;
            public uint authorityTick, acceptedPrimarySequence;
            public string scenario, disconnectMessage;
            public int acceptedPrimaryPresses, acceptedPrimaryReleases, playerScore, botScore;
            public float actorOneDisplacement, actorTwoDisplacement, lowestPlayerHealth = 100, lowestBotHealth = 100;
            public bool combatStable10s, movementObserved, inputAcceptedByHost, damageObserved, disconnectVerified, attackAimVisible;
        }
        private string _role, _resultPath, _joinFile, _peerFile, _pendingJson;
        private float _nextPublish, _nextIoWarning, _scenarioAt, _leaveAt;
        private int _scenarioStep, _shotStep;
        private float _shotAt, _resetWaitDeadline;
        private int _offlineResetCountAtCombat;
        // Read-only development diagnostics; no world state is injected or changed.
        private static readonly System.Reflection.FieldInfo WorldRevisionField = typeof(OnlineWorldReplicaRegistry)
            .GetField("_syncRevision", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        private static readonly System.Reflection.FieldInfo OfflineDuelField = typeof(EarthOnlineGameplayBinding)
            .GetField("offlineDuel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        private Elemental.Runtime.Characters.EarthMvpDuelController OfflineDuel =>
            (Elemental.Runtime.Characters.EarthMvpDuelController)OfflineDuelField.GetValue(binding);
        private uint CurrentWorldRevision => (uint)WorldRevisionField.GetValue(binding.GetComponent<OnlineWorldReplicaRegistry>());
        private bool _lagApplied, _stoneScenario;
        private ProbeDriverConstructor _driverConstructor;
        private sealed class ProbeDriverConstructor : INetworkStreamDriverConstructor
        {
            public void CreateDriver(UnityTransport transport, out NetworkDriver driver,
                out NetworkPipeline unreliable, out NetworkPipeline sequenced, out NetworkPipeline reliable)
            {
                // Preserve official Relay/DTLS and default pipelines. Add the
                // opt-in simulator layer with zero impairment until Combat.
                var settings = transport.GetDefaultNetworkSettings();
                settings.WithNetworkSimulatorParameters();
                driver = transport.UseWebSockets
                    ? NetworkDriver.Create(new WebSocketNetworkInterface(), settings)
                    : NetworkDriver.Create(new UDPNetworkInterface(), settings);
                transport.GetDefaultPipelineConfigurations(ref driver, out var a, out var b, out var c);
                unreliable = driver.CreatePipeline(a); sequenced = driver.CreatePipeline(b); reliable = driver.CreatePipeline(c);
            }
        }
        private readonly HashSet<uint> _selectedShotSources = new HashSet<uint>();
        private bool _observing, _clientScenarioDone, _leaveRequested;
        private Vector3 _actorOneStart, _actorTwoStart, _actorOneUp, _actorTwoUp;
        private Keyboard _keyboard;
        private Mouse _mouse;
        private PlayerInput _playerInput;
        private InputDevice[] _originalDevices;
        private float _startedAt, _nextPoll, _combatAt = -1f;
        private bool _requested, _ready, _finished;
        private readonly Report _report = new Report();

        private void Start()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            _role = Argument(arguments, "--online-smoke");
            if (_role == null) { enabled = false; return; }
            if (_role != "host" && _role != "join")
                throw new ArgumentException("--online-smoke must be host or join.");
            // Bound the two opt-in probe processes; ordinary launches retain
            // the user's graphics and frame pacing settings.
            Application.targetFrameRate = 30;
            _resultPath = RequiredAbsoluteFile(Argument(arguments, "--online-result"), "--online-result");
            if (_role == "join") _joinFile = RequiredAbsoluteFile(Argument(arguments, "--online-join-file"), "--online-join-file");
            _peerFile = _role == "join" ? _joinFile : RequiredAbsoluteFile(
                Argument(arguments, "--online-peer-file") ?? Path.Combine(Path.GetDirectoryName(_resultPath), "client.json"), "--online-peer-file");
            if (frontend == null || session == null || transport == null || binding == null)
                throw new InvalidOperationException("Online development probe needs explicit installed scene bindings.");
            // Graphics-capable batch players may have no hardware devices.
            // Register the probe devices before the normal online actor enable
            // creates its InputUser; do not reactivate or bypass gameplay controls.
            if (_role == "join")
            { _keyboard = InputSystem.AddDevice<Keyboard>(); _mouse = InputSystem.AddDevice<Mouse>(); }
            _report.requestedScenario = Argument(arguments, "--online-scenario") ?? "basic";
            if (_report.requestedScenario != "basic" && _report.requestedScenario != "stone-combat")
                throw new ArgumentException("--online-scenario must be basic or stone-combat.");
            _stoneScenario = _report.requestedScenario == "stone-combat";
            _report.sendDelayMs = BoundedArgument(arguments, "--online-delay-ms", 500);
            _report.sendJitterMs = BoundedArgument(arguments, "--online-jitter-ms", 200);
            _report.sendLossPercent = BoundedArgument(arguments, "--online-loss-percent", 20);
            if (UnityTransport.s_DriverConstructor != null)
                throw new InvalidOperationException("The probe will not replace an existing custom UTP driver constructor.");
            _driverConstructor = new ProbeDriverConstructor(); UnityTransport.s_DriverConstructor = _driverConstructor;
            _startedAt = Time.realtimeSinceStartup;
            _report.role = _role;
            Snapshot("waiting-for-menu");
        }

        private void Update()
        {
            if (_pendingJson != null && Time.realtimeSinceStartup >= _nextPublish) PublishPending();
            if (_finished || _role == null) return;
            if (_report.combatStable10s && !_leaveRequested)
            {
                try { AdvanceScenario(); }
                catch (Exception error)
                { Debug.LogException(error, this); CleanupInput(); Finish("scenario-failed: " + error.Message); return; }
            }
            if (Time.realtimeSinceStartup < _nextPoll) return;
            _nextPoll = Time.realtimeSinceStartup + .5f;
            if (Time.realtimeSinceStartup - _startedAt > 240f) { Finish("timeout"); return; }
            var flow = frontend.Flow;
            if (session.Phase == OnlineSessionPhase.Failed) { Finish("service-or-transport-failed"); return; }
            if (!_requested && flow.State == FrontendState.Main && flow.IsWorldReady)
            {
                if (!flow.OnlineAvailable) { Finish("online-unavailable"); return; }
                if (_role == "host") { _requested = true; flow.OpenHost(); }
                else
                {
                    if (!File.Exists(_joinFile)) { Snapshot("waiting-for-real-host-code"); return; }
                    Report host;
                    try { host = ReadReport(_joinFile); }
                    catch (IOException) { Snapshot("waiting-for-real-host-code"); return; }
                    if (host == null || !host.connected || string.IsNullOrEmpty(host.code))
                    { Snapshot("waiting-for-real-host-code"); return; }
                    _requested = true; flow.OpenJoin(); flow.JoinRoom(host.code);
                }
            }
            if (!_ready && session.Connected)
            { _ready = true; flow.SetNetworkReady(); }
            // WorldReady gates entry, not every live terrain edit. Accepted
            // extraction can briefly invalidate GeometryReady while its new
            // collider/mesh is built and acknowledged. Gameplay remains valid
            // while the prepared, connected transport and Combat state persist.
            bool activeCombat = session.Connected && transport.Running &&
                flow.State == FrontendState.Combat && binding.IsPrepared;
            if (activeCombat && (_combatAt >= 0f || binding.WorldReady))
            {
                if (_combatAt < 0f)
                {
                    _combatAt = Time.realtimeSinceStartup; _offlineResetCountAtCombat = OfflineDuel.ArenaResetCount;
                    try { ApplyNetworkSimulation(); }
                    catch (Exception error) { Debug.LogException(error, this); Finish("simulator-setup-failed: " + error.Message); return; }
                    binding.ActorOne.Impact.WorldResponseRequested += ObserveImpact;
                    binding.ActorTwo.Impact.WorldResponseRequested += ObserveImpact;
                    _actorOneStart = binding.ActorOne.Body.position; _actorTwoStart = binding.ActorTwo.Body.position;
                    binding.ActorTwo.SemanticInput.RemoteFrameApplied += ObserveRemoteInput; _observing = true;
                }
                if (!_report.combatStable10s && Time.realtimeSinceStartup - _combatAt >= 10f)
                {
                    _report.combatStable10s = true; _scenarioAt = Time.realtimeSinceStartup;
                    _actorOneStart = binding.ActorOne.Body.position; _actorTwoStart = binding.ActorTwo.Body.position;
                    _actorOneUp = binding.ActorOne.Body.transform.up; _actorTwoUp = binding.ActorTwo.Body.transform.up;
                    _report.actorOneDisplacement = _report.actorTwoDisplacement = 0; _report.movementObserved = false;
                    _report.scenario = "connected-world-and-combat-10s";
                }
            }
            else if (_combatAt >= 0f && !_clientScenarioDone && !_leaveRequested)
            { Finish("combat-interrupted"); return; }
            if (_clientScenarioDone && _role == "join" && !session.Connected && flow.State == FrontendState.Main &&
                flow.IsWorldReady && (!_stoneScenario || OfflineDuel.ArenaResetCount > _offlineResetCountAtCombat))
            {
                _report.disconnectMessage = frontend.LastDisconnectReason;
                _report.disconnectVerified = !string.IsNullOrEmpty(_report.disconnectMessage);
                Finish(_report.disconnectVerified && _report.movementObserved && _report.inputAcceptedByHost
                    && StoneScenarioPassed() ? (_stoneScenario ? "stone-combat-and-host-disconnect-passed" : "movement-input-and-host-disconnect-passed") : "scenario-partial-failure"); return;
            }
            if (_leaveRequested && _role == "host")
            {
                Report peer = TryReadPeer();
                if (peer != null && peer.disconnectVerified && !session.Connected && flow.State == FrontendState.Main &&
                    flow.IsWorldReady && (!_stoneScenario || OfflineDuel.ArenaResetCount > _offlineResetCountAtCombat))
                { _report.disconnectVerified = true; Finish(_report.movementObserved && _report.inputAcceptedByHost
                    && StoneScenarioPassed() ? (_stoneScenario ? "host-stone-combat-and-disconnect-passed" : "host-authority-input-and-disconnect-passed") : "scenario-partial-failure"); return; }
                if (Time.realtimeSinceStartup - _leaveAt > 25f) { Finish("host-leave-or-client-return-timeout"); return; }
            }
            Snapshot(_clientScenarioDone ? "awaiting-host-leave" : "in-progress");
        }

        private void Snapshot(string outcome)
        {
            _report.inputs = transport.Inputs;
            if (session.Connected && session.Network.NetworkConfig.NetworkTransport is UnityTransport utp)
            {
                ulong remote = Unity.Netcode.NetworkManager.ServerClientId;
                if (session.Network.IsHost)
                    foreach (ulong id in session.Network.ConnectedClientsIds) if (id != session.Network.LocalClientId) remote = id;
                _report.currentRttMs = utp.GetCurrentRtt(remote);
                _report.maximumRttMs = Math.Max(_report.maximumRttMs, _report.currentRttMs);
            }
            _report.utc = DateTime.UtcNow.ToString("O"); _report.outcome = outcome;
            _report.elapsedSeconds = Time.realtimeSinceStartup - _startedAt;
            _report.code = session.Code; _report.sessionPhase = session.Phase.ToString();
            _report.sessionStatus = session.Status; _report.transportStatus = transport.Status;
            _report.frontendState = frontend.Flow.State.ToString();
            _report.connected = session.Connected; _report.running = transport.Running;
            _report.canHandshake = binding.CanHandshake; _report.worldReady = binding.WorldReady;
            _report.remainingCountdown = transport.SecondsUntilStart; _report.authorityTick = binding.AuthorityTick;
            _report.buildHash = binding.BuildHash.ToString("X16"); _report.worldHash = binding.InitialWorldHash.ToString("X16");
            var network = session.Network;
            _report.listening = network != null && network.IsListening;
            _report.localClientId = network != null ? network.LocalClientId : 0;
            _report.connectedPeerCount = network != null && network.IsHost ? network.ConnectedClientsIds.Count : 0;
            var duel = binding.OnlineDuel;
            _report.offlineArenaResetCount = OfflineDuel.ArenaResetCount;
            _report.menuArenaRestored = frontend.Flow.State == FrontendState.Main && frontend.Flow.IsWorldReady &&
                _report.offlineArenaResetCount > _offlineResetCountAtCombat;
            if (session.Connected && !_leaveRequested && duel != null)
            {
                _report.arenaResetCount = duel.ArenaResetCount; _report.worldRevision = CurrentWorldRevision;
                _report.arenaResetInProgress = duel.ArenaResetInProgress; _report.arenaResetError = duel.ArenaResetError;
            }
            _report.playerHealth = duel != null ? duel.PlayerHealth : 0f;
            _report.botHealth = duel != null ? duel.BotHealth : 0f;
            if (_report.combatStable10s && session.Connected && !_leaveRequested)
            {
                _report.actorOneDisplacement = Mathf.Max(_report.actorOneDisplacement, Vector3.ProjectOnPlane(binding.ActorOne.Body.position - _actorOneStart, _actorOneUp).magnitude);
                _report.actorTwoDisplacement = Mathf.Max(_report.actorTwoDisplacement, Vector3.ProjectOnPlane(binding.ActorTwo.Body.position - _actorTwoStart, _actorTwoUp).magnitude);
                _report.movementObserved = _report.actorTwoDisplacement > .15f;
                _report.lowestPlayerHealth = Mathf.Min(_report.lowestPlayerHealth, _report.playerHealth);
                _report.lowestBotHealth = Mathf.Min(_report.lowestBotHealth, _report.botHealth);
                _report.damageObserved = _report.lowestPlayerHealth < 100 || _report.lowestBotHealth < 100;
                _report.playerScore = duel.PlayerScore; _report.botScore = duel.BotScore;
            }
            _pendingJson = JsonUtility.ToJson(_report, true); PublishPending();
        }
        private void Finish(string outcome)
        {
            CleanupInput(); Snapshot(outcome); _finished = true;
            Debug.Log("[OnlineDevelopmentProbe] " + outcome + ": " + _resultPath);
            // Leave both real peers running for manual play and disconnect checks.
        }
        private void ObserveRemoteInput(EarthSemanticInputFrame frame)
        {
            if ((frame.Pressed & EarthInputBits.Primary) != 0)
            {
                _report.acceptedPrimaryPresses++; _report.acceptedPrimarySequence = frame.Sequence;
                var selected = binding.ActorTwo.Executor.ReservedOrHeldFragment;
                if (binding.ActorTwo.MagicInput.IsQuickStonePrimed && selected != null)
                {
                    _selectedShotSources.Add(selected.FragmentId);
                    Vector2 screen = new Vector2(frame.PointerViewport.x * Screen.width, frame.PointerViewport.y * Screen.height);
                    RecordShot("host-selected-fire", binding.ActorOne.Collider.bounds.center, screen,
                        true, selected.Body, selected.FragmentId, frame.Sequence);
                }
            }
            if ((frame.Released & EarthInputBits.Primary) != 0) _report.acceptedPrimaryReleases++;
            _report.inputAcceptedByHost = _report.acceptedPrimaryPresses >= 2 && _report.acceptedPrimaryReleases >= 2;
        }
        private void AdvanceScenario()
        {
            if (_role == "host")
            {
                Report peer = TryReadPeer();
                if (peer == null || peer.scenario != "client-input-complete") return;
                if (_stoneScenario)
                    _report.lifeRespawnPreservedArena = peer.lifeRespawnPreservedArena && binding.WorldReady &&
                        binding.OnlineDuel.ArenaResetCount == _report.stoneKillArenaResetCount &&
                        CurrentWorldRevision == peer.worldRevision && !binding.OnlineDuel.ArenaResetInProgress;
                // Capture canonical health, movement and actual routed input before
                // the normal frontend leave restores the authored offline actor.
                Snapshot("host-leaving-after-client-input");
                _leaveRequested = true; _leaveAt = Time.realtimeSinceStartup;
                frontend.Flow.EndMatch(); return;
            }
            float elapsed = Time.realtimeSinceStartup - _scenarioAt;
            if (_scenarioStep == 0)
            {
                _playerInput = binding.ActorTwo.SemanticInput.GetComponent<PlayerInput>();
                if (_playerInput == null || !_playerInput.isActiveAndEnabled || !_playerInput.inputIsActive || !_playerInput.user.valid)
                    throw new InvalidOperationException("The active local PlayerInput has no valid user after normal online activation.");
                _originalDevices = Array.FindAll(_playerInput.devices.ToArray(), device => device != _keyboard && device != _mouse);
                _playerInput.SwitchCurrentControlScheme(_keyboard, _mouse);
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W));
                _report.scenario = "client-move-W"; _scenarioStep = 1;
            }
            else if (_scenarioStep == 1 && elapsed >= 1f)
            { InputSystem.QueueStateEvent(_keyboard, new KeyboardState()); _scenarioStep = 2; }
            else if (_scenarioStep == 2 && elapsed >= 1.8f) { QueueMouse(true); _scenarioStep = 3; }
            else if (_scenarioStep == 3 && elapsed >= 1.9f) { QueueMouse(false); _scenarioStep = 4; }
            else if (_scenarioStep == 4 && elapsed >= 2.8f) { QueueMouse(true); _scenarioStep = 5; }
            else if (_scenarioStep == 5 && elapsed >= 2.9f) { QueueMouse(false); _scenarioStep = 6; }
            else if (_scenarioStep == 6 && elapsed >= 7f)
            {
                if (_stoneScenario) { AdvanceStoneScenario(); }
                else CompleteClientScenario();
            }
            if (_clientScenarioDone)
            {
                Report peer = TryReadPeer();
                if (peer != null)
                {
                    _report.inputAcceptedByHost = peer.inputAcceptedByHost;
                    _report.acceptedPrimaryPresses = peer.acceptedPrimaryPresses;
                    _report.acceptedPrimaryReleases = peer.acceptedPrimaryReleases;
                    _report.acceptedPrimarySequence = peer.acceptedPrimarySequence;
                    _report.replicatedStoneKill = peer.attributedStoneKill && _report.impacts.Exists(hit =>
                        hit.responseId == peer.stoneKillResponseId && hit.sourceId == peer.stoneKillSourceId && hit.response == "Knockout");
                }
            }
        }
        private bool StoneScenarioPassed() => !_stoneScenario || (_report.lifeRespawnPreservedArena && (_role == "host"
            ? _report.attributedStoneKill && TryReadPeer()?.replicatedStoneKill == true
            : _report.replicatedStoneKill));
        private void CompleteClientScenario()
        { CleanupInput(); _clientScenarioDone = true; _scenarioStep = 7; _report.scenario = "client-input-complete"; }
        private void ObserveImpact(EarthWorldResponseEvent hit)
        {
            bool selected = _selectedShotSources.Contains(hit.SourceStableId);
            bool stone = hit.SourceKind == EarthCharacterImpactSourceKind.LooseStone;
            if (_report.impacts.Count < 64)
                _report.impacts.Add(new ImpactEvidence { responseId = hit.ResponseId, sourceId = hit.SourceStableId,
                    targetId = hit.TargetStableId, tick = hit.Tick, sourceKind = hit.SourceKind.ToString(),
                    response = hit.Response.ToString(), region = hit.HitRegion.ToString(), impulse = hit.Impulse,
                    energy = hit.KineticEnergy, authority = _role == "host", selectedForShot = selected,
                    healthAfter = hit.TargetStableId == 1 ? binding.OnlineDuel.PlayerHealth : binding.OnlineDuel.BotHealth });
            if (_role == "host" && selected && stone && hit.TargetStableId == 1)
            {
                _report.attributedStoneHit = true;
                if (hit.Response == EarthCharacterImpactResponse.Knockout)
                {
                    _report.attributedStoneKill = true; _report.stoneKillResponseId = hit.ResponseId; _report.stoneKillSourceId = hit.SourceStableId;
                    _report.stoneKillArenaResetCount = binding.OnlineDuel.ArenaResetCount;
                    _report.stoneKillWorldRevision = CurrentWorldRevision;
                }
            }
        }
        private void AdvanceStoneScenario()
        {
            // Bounded attempts through the installed PlayerInput. No spawn, teleport,
            // direct ability call, damage injection or arbitrary replica state.
            Report host = TryReadPeer();
            if (host != null && host.attributedStoneKill && _report.impacts.Exists(hit =>
                hit.responseId == host.stoneKillResponseId && hit.sourceId == host.stoneKillSourceId && hit.response == "Knockout"))
            {
                _report.replicatedStoneKill = true;
                if (_resetWaitDeadline == 0)
                {
                    _resetWaitDeadline = Time.realtimeSinceStartup + 25f;
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                    InputSystem.QueueStateEvent(_mouse, new MouseState { position = _mouse.position.ReadValue() });
                    _report.scenario = "waiting-for-respawn-with-preserved-arena";
                }
                bool respawned = host.playerHealth >= 99.99f && binding.OnlineDuel.PlayerHealth >= 99.99f;
                _report.lifeRespawnPreservedArena = host.arenaResetCount == host.stoneKillArenaResetCount &&
                    host.worldRevision == host.stoneKillWorldRevision && host.worldRevision == CurrentWorldRevision &&
                    host.worldReady && binding.WorldReady && !host.arenaResetInProgress && respawned;
                if (_report.lifeRespawnPreservedArena || Time.realtimeSinceStartup >= _resetWaitDeadline) CompleteClientScenario();
                return;
            }
            if (_report.attemptedShots >= 12 && _shotStep == 0) { CompleteClientScenario(); return; }
            float now = Time.realtimeSinceStartup;
            if (_shotStep == 0)
            {
                Vector3 up = binding.ActorTwo.Body.transform.up;
                Vector3 toward = Vector3.ProjectOnPlane(binding.ActorOne.Body.position - binding.ActorTwo.Body.position, up).normalized;
                Vector3 side = Vector3.Cross(up, toward);
                Vector3 seed = binding.ActorTwo.Body.position + toward * 2f + side * ((_report.attemptedShots % 3 - 1) * .65f);
                if (!Physics.Raycast(seed + up * 3f, -up, out RaycastHit ground, 12f, ~0, QueryTriggerInteraction.Ignore))
                    throw new InvalidOperationException("No real arena surface in reach for a stone shot.");
                _shotAt = now; _report.attemptedShots++;
                _shotStep = QueueMouseAt(true, ground.point, "client-acquire") ? 1 : 4;
            }
            else if (_shotStep == 1 && now - _shotAt >= .12f)
            { InputSystem.QueueStateEvent(_mouse, new MouseState { position = _mouse.position.ReadValue() }); _shotStep = 2; }
            else if (_shotStep == 2 && now - _shotAt >= 1.0f)
            {
                // Click the visible target exactly as a player does. Do not
                // compensate projectile/camera parallax with an off-screen ray.
                _shotStep = QueueMouseAt(true, binding.ActorOne.Collider.bounds.center, "client-fire") ? 3 : 4;
            }
            else if (_shotStep == 3 && now - _shotAt >= 1.12f)
            { InputSystem.QueueStateEvent(_mouse, new MouseState { position = _mouse.position.ReadValue() }); _shotStep = 4; }
            else if (_shotStep == 4 && now - _shotAt >= 3.5f) _shotStep = 0;
            _report.scenario = "client-real-stone-attempts";
        }
        private bool QueueMouseAt(bool pressed, Vector3 target, string stage)
        {
            Vector3 point = binding.ActorTwo.EarthInput.CastCamera.WorldToScreenPoint(target);
            bool visible = point.z > 0 && point.x >= 0 && point.x < Screen.width && point.y >= 0 && point.y < Screen.height;
            Vector2 screen = new Vector2(point.x, point.y);
            RecordShot(stage, target, screen, visible, binding.ActorTwo.Executor.HeldBody, 0, 0);
            if (!visible) { _report.unavailableAimCount++; return false; }
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = screen }.WithButton(MouseButton.Left, pressed));
            return true;
        }
        private void RecordShot(string stage, Vector3 target, Vector2 screen, bool visible, Rigidbody source, uint id, uint sequence)
        {
            if (_report.shots.Count >= 40) return;
            Ray ray = binding.ActorTwo.EarthInput.CastCamera.ScreenPointToRay(screen);
            Vector3 origin = source != null ? source.worldCenterOfMass : binding.ActorTwo.Body.worldCenterOfMass;
            Vector3 offset = target - origin;
            float along = Vector3.Dot(offset, ray.direction);
            _report.shots.Add(new ShotEvidence { stage = stage, sequence = sequence, sourceId = id,
                sourcePosition = origin, sourceMass = source != null ? source.mass : 0, hasHeldBody = source != null,
                targetPosition = target, targetExtents = binding.ActorOne.Collider.bounds.extents,
                screenPoint = screen, visible = visible, quickPrimed = binding.ActorTwo.MagicInput.IsQuickStonePrimed,
                rayOrigin = ray.origin, rayDirection = ray.direction, targetDistanceAlongRay = along,
                parallelMissMeters = (offset - ray.direction * Mathf.Max(0, along)).magnitude });
        }
        private void ApplyNetworkSimulation()
        {
            if (_lagApplied) return;
            if (!(session.Network.NetworkConfig.NetworkTransport is UnityTransport utp))
                throw new InvalidOperationException("The probe needs the real Unity Transport driver.");
            ref NetworkDriver driver = ref utp.GetNetworkDriver();
            if (!driver.IsCreated || !driver.CurrentSettings.TryGet<NetworkSimulatorParameter>(out _))
                throw new InvalidOperationException("UTP network simulator is unavailable; requested lag cannot be claimed.");
            driver.ModifyNetworkSimulatorParameters(new NetworkSimulatorParameter {
                SendDelayMS = (uint)_report.sendDelayMs, SendJitterMS = (uint)_report.sendJitterMs,
                SendPacketLossPercent = _report.sendLossPercent });
            if (!driver.CurrentSettings.TryGet<NetworkSimulatorParameter>(out var applied) ||
                applied.SendDelayMS != _report.sendDelayMs || applied.SendJitterMS != _report.sendJitterMs ||
                applied.SendPacketLossPercent != _report.sendLossPercent)
                throw new InvalidOperationException("UTP simulator did not retain the requested parameters.");
            _lagApplied = _report.simulatorApplied = true;
            Debug.Log($"[OnlineDevelopmentProbe] Gameplay UTP simulation: send delay {_report.sendDelayMs} ms, jitter {_report.sendJitterMs} ms, loss {_report.sendLossPercent}%.", this);
        }
        private static int BoundedArgument(string[] arguments, string option, int maximum)
        {
            string text = Argument(arguments, option);
            if (text == null) return 0;
            if (!int.TryParse(text, out int value) || value < 0 || value > maximum)
                throw new ArgumentException(option + " must be an integer from 0 to " + maximum + ".");
            return value;
        }
        private void QueueMouse(bool pressed)
        {
            Camera camera = binding.ActorTwo.EarthInput.CastCamera;
            Vector3 projected = camera.WorldToScreenPoint(binding.ActorOne.Collider.bounds.center);
            _report.attackAimVisible = projected.z > 0 && projected.x >= 0 && projected.x < Screen.width && projected.y >= 0 && projected.y < Screen.height;
            Vector2 position = _report.attackAimVisible ? new Vector2(projected.x, projected.y) : new Vector2(Screen.width * .5f, Screen.height * .5f);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position }.WithButton(MouseButton.Left, pressed));
            _report.scenario = "client-two-primary-clicks";
        }
        private void CleanupInput()
        {
            PlayerInput player = _playerInput; InputDevice[] original = _originalDevices;
            Keyboard keyboard = _keyboard; Mouse mouse = _mouse;
            _playerInput = null; _originalDevices = null; _keyboard = null; _mouse = null;
            try
            {
                if (player != null && player.isActiveAndEnabled && player.user.valid && original != null && original.Length > 0)
                    player.SwitchCurrentControlScheme(original);
            }
            catch (Exception error) { Debug.LogWarning("[OnlineDevelopmentProbe] Input restore: " + error.Message); }
            foreach (InputDevice device in new InputDevice[] { keyboard, mouse })
            {
                if (device == null || !device.added) continue;
                try { InputSystem.RemoveDevice(device); }
                catch (Exception error) { Debug.LogWarning("[OnlineDevelopmentProbe] Device cleanup: " + error.Message); }
            }
        }
        private void OnDestroy()
        {
            if (_driverConstructor != null && ReferenceEquals(UnityTransport.s_DriverConstructor, _driverConstructor))
                UnityTransport.s_DriverConstructor = null;
            if (_observing && binding != null && binding.ActorTwo?.SemanticInput != null)
            {
                binding.ActorTwo.SemanticInput.RemoteFrameApplied -= ObserveRemoteInput;
                binding.ActorOne.Impact.WorldResponseRequested -= ObserveImpact;
                binding.ActorTwo.Impact.WorldResponseRequested -= ObserveImpact;
            }
            CleanupInput();
        }
        private Report TryReadPeer()
        {
            try { return File.Exists(_peerFile) ? ReadReport(_peerFile) : null; }
            catch (IOException) { return null; }
        }
        private static Report ReadReport(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return JsonUtility.FromJson<Report>(reader.ReadToEnd());
        }
        private void PublishPending()
        {
            if (_pendingJson == null) return;
            string temporary = _resultPath + ".writing";
            try
            {
                File.WriteAllText(temporary, _pendingJson);
                if (File.Exists(_resultPath)) File.Replace(temporary, _resultPath, null);
                else File.Move(temporary, _resultPath);
                _pendingJson = null;
            }
            catch (IOException error)
            {
                // A short external reader may omit FileShare.Delete. Retry the
                // atomic swap next update; never fail gameplay or publish partial JSON.
                _nextPublish = Time.realtimeSinceStartup + .25f;
                if (Time.realtimeSinceStartup >= _nextIoWarning)
                { _nextIoWarning = Time.realtimeSinceStartup + 10f; Debug.LogWarning("[OnlineDevelopmentProbe] Report publish will retry: " + error.Message); }
            }
        }

        private static string Argument(string[] arguments, string option)
        {
            string value = null;
            for (int i = 0; i < arguments.Length; i++)
                if (arguments[i] == option)
                {
                    if (value != null || ++i == arguments.Length) throw new ArgumentException("Pass " + option + " once followed by a value.");
                    value = arguments[i];
                }
            return value;
        }
        private static string RequiredAbsoluteFile(string value, string option)
        {
            if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value))
                throw new ArgumentException(option + " must name an absolute JSON file path.");
            string full = Path.GetFullPath(value);
            if (!string.Equals(Path.GetExtension(full), ".json", StringComparison.OrdinalIgnoreCase) ||
                !Directory.Exists(Path.GetDirectoryName(full)))
                throw new ArgumentException(option + " must be a .json file in an existing test-output directory.");
            return full;
        }
#endif
    }
}
