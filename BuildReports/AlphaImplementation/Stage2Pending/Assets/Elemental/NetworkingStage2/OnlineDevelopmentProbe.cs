using System;
using System.IO;
using Elemental.Presentation.UI;
using UnityEngine;

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
        private sealed class Report
        {
            public string utc, role, outcome, code, sessionPhase, sessionStatus, transportStatus, frontendState;
            public string buildHash, worldHash;
            public ulong localClientId;
            public int connectedPeerCount;
            public bool listening, connected, canHandshake, worldReady, running;
            public float remainingCountdown, elapsedSeconds, playerHealth, botHealth;
            public uint authorityTick;
        }
        private string _role, _resultPath, _joinFile;
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
            _resultPath = RequiredAbsoluteFile(Argument(arguments, "--online-result"), "--online-result");
            if (_role == "join") _joinFile = RequiredAbsoluteFile(Argument(arguments, "--online-join-file"), "--online-join-file");
            if (frontend == null || session == null || transport == null || binding == null)
                throw new InvalidOperationException("Online development probe needs explicit installed scene bindings.");
            _startedAt = Time.realtimeSinceStartup;
            _report.role = _role;
            Snapshot("waiting-for-menu");
        }

        private void Update()
        {
            if (_finished || _role == null || Time.realtimeSinceStartup < _nextPoll) return;
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
                    try { host = JsonUtility.FromJson<Report>(File.ReadAllText(_joinFile)); }
                    catch (IOException) { return; }
                    if (host == null || !host.connected || string.IsNullOrEmpty(host.code)) return;
                    _requested = true; flow.OpenJoin(); flow.JoinRoom(host.code);
                }
            }
            if (!_ready && session.Connected)
            { _ready = true; flow.SetNetworkReady(); }
            if (transport.Running && flow.State == FrontendState.Combat && binding.WorldReady)
            {
                if (_combatAt < 0f) _combatAt = Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - _combatAt >= 10f) { Finish("connected-world-and-combat-10s"); return; }
            }
            else if (_combatAt >= 0f) { Finish("combat-interrupted"); return; }
            Snapshot("in-progress");
        }

        private void Snapshot(string outcome)
        {
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
            _report.playerHealth = duel != null ? duel.PlayerHealth : 0f;
            _report.botHealth = duel != null ? duel.BotHealth : 0f;
            // A temporary sibling plus atomic replacement prevents the other process reading partial JSON.
            string temporary = _resultPath + ".writing";
            File.WriteAllText(temporary, JsonUtility.ToJson(_report, true));
            if (File.Exists(_resultPath)) File.Replace(temporary, _resultPath, null);
            else File.Move(temporary, _resultPath);
        }
        private void Finish(string outcome)
        {
            Snapshot(outcome); _finished = true;
            Debug.Log("[OnlineDevelopmentProbe] " + outcome + ": " + _resultPath);
            // Leave both real peers running for manual play and disconnect checks.
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
