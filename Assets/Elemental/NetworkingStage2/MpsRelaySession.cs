using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Elemental.Online
{
    public enum OnlineSessionPhase { Idle, Authenticating, Connecting, Waiting, Leaving, Failed }

    /// <summary>Real Sessions/Relay lifecycle. Room existence never means gameplay readiness.</summary>
    [DisallowMultipleComponent]
    public sealed class MpsRelaySession : MonoBehaviour
    {
        [SerializeField] private NetworkManager network;
        [SerializeField, Tooltip("The scene's session owns its dedicated NGO root, even after NGO moves it to DontDestroyOnLoad.")]
        private bool destroyNetworkWithOwner = true;
        [SerializeField] private string authenticationProfile = "default";
        private ISession _session;
        private uint _generation;
        private bool _busy;
        private bool _disposed;
        private bool _leaving;
        private string _terminalFailure;
        private Task _connectOperation, _leaveOperation;
        public OnlineSessionPhase Phase { get; private set; }
        public string Code => _session?.Code ?? string.Empty;
        public string Status { get; private set; } = string.Empty;
        public bool Busy => _busy || _leaving;
        public bool Connected => network != null && network.IsListening && _session != null;
        public NetworkManager Network => network;
        public event Action Changed;
        public event Action<string> ConnectionLost;

        public void Configure(NetworkManager manager, string profile)
        {
            if (Busy || _session != null) throw new InvalidOperationException("Leave before configuring networking.");
            UnsubscribeNetwork(); network = manager;
            authenticationProfile = string.IsNullOrWhiteSpace(profile) ? "default" : profile;
            SubscribeNetwork();
        }

        private void OnEnable() => SubscribeNetwork();
        private void OnDisable() { UnsubscribeNetwork(); _ = CancelAsync(); }
        private void OnDestroy()
        {
            _disposed = true; UnsubscribeNetwork(); _ = CancelAsync();
            // Cancel keeps this manager reusable. Scene-owner destruction does
            // not: NGO's persistent root must not leak into the next arena.
            // Editor Undo owns edit-mode removal; never destroy outside its group.
            if (Application.isPlaying && destroyNetworkWithOwner && network != null)
                Destroy(network.gameObject);
        }
        private void SubscribeNetwork()
        {
            if (network == null) return;
            UnsubscribeNetwork();
            network.OnClientDisconnectCallback += Disconnected;
            network.OnTransportFailure += TransportFailed;
        }
        private void UnsubscribeNetwork()
        {
            if (network == null) return;
            network.OnClientDisconnectCallback -= Disconnected;
            network.OnTransportFailure -= TransportFailed;
        }

        public Task HostAsync() => StartConnect(null);
        public Task JoinAsync(string code)
        {
            string normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
            if (normalized.Length < 4 || normalized.Length > 16)
            { SetState(OnlineSessionPhase.Failed, "Enter a valid room code."); return Task.CompletedTask; }
            foreach (char c in normalized)
                if (!char.IsLetterOrDigit(c))
                { SetState(OnlineSessionPhase.Failed, "Room codes contain letters and digits only."); return Task.CompletedTask; }
            return StartConnect(normalized);
        }

        private Task StartConnect(string code)
        {
            if (Busy || _session != null || _disposed) return Task.CompletedTask;
            return _connectOperation = ConnectAsync(code);
        }

        private async Task ConnectAsync(string code)
        {
            if (Busy || _session != null || _disposed) return;
            if (network == null || network.NetworkConfig.NetworkTransport == null ||
                NetworkManager.Singleton != network)
            { SetState(OnlineSessionPhase.Failed, "The Relay network manager/transport is not configured."); return; }
            _busy = true; _terminalFailure = null;
            uint operation = ++_generation;
            ISession pending = null;
            try
            {
                SetState(OnlineSessionPhase.Authenticating, "Signing in…");
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync(new InitializationOptions().SetProfile(authenticationProfile));
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                if (operation != _generation || _disposed) return;
                SetState(OnlineSessionPhase.Connecting, code == null ? "Creating Relay room…" : "Joining Relay room…");
                pending = code == null
                    ? await MultiplayerService.Instance.CreateSessionAsync(new SessionOptions { MaxPlayers = 2 }.WithRelayNetwork())
                    : await MultiplayerService.Instance.JoinSessionByCodeAsync(code, new JoinSessionOptions());
                if (operation != _generation || _disposed)
                { await LeaveSafely(pending); pending = null; if (network != null && network.IsListening) network.Shutdown(); return; }
                _session = pending; pending = null;
                _session.RemovedFromSession += Removed;
                _session.SessionHostChanged += HostChanged;
                SetState(OnlineSessionPhase.Waiting, "Connected. Waiting for both players and arena readiness.");
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // UGS wraps package failures in aggregate initialization errors.
                // Keep the inner cause in development logs instead of only the
                // generic user-facing outer message.
                Debug.LogException(exception, this);
#endif
                if (pending != null) await LeaveSafely(pending);
                if (operation == _generation && !_disposed)
                {
                    if (network != null && network.IsListening) network.Shutdown();
                    SetState(OnlineSessionPhase.Failed, "Online connection failed: " + exception.Message);
                }
            }
            finally
            {
                _busy = false;
                if (operation != _generation && _session == null && !_disposed && !_leaving)
                    SetState(string.IsNullOrEmpty(_terminalFailure) ? OnlineSessionPhase.Idle : OnlineSessionPhase.Failed, _terminalFailure ?? "Connection cancelled.");
            }
        }

        public Task CancelAsync() => BeginCancel(null);
        private Task BeginCancel(string failure)
        {
            if (_leaveOperation != null && !_leaveOperation.IsCompleted) return _leaveOperation;
            return _leaveOperation = CancelCoreAsync(failure);
        }
        private async Task CancelCoreAsync(string failure)
        {
            if (_leaving) return;
            _leaving = true; _terminalFailure = failure;
            ++_generation;
            ISession leaving = _session; _session = null;
            if (leaving != null)
            {
                leaving.RemovedFromSession -= Removed;
                leaving.SessionHostChanged -= HostChanged;
            }
            if (!_disposed) SetState(OnlineSessionPhase.Leaving, "Leaving room…");
            try
            {
                try { if (network != null && network.IsListening) network.Shutdown(); }
                catch (Exception exception) { _terminalFailure = "Transport shutdown failed: " + exception.Message; }
                if (leaving != null) await LeaveSafely(leaving);
                // MPS has no CancellationToken. Await the superseded create/join so
                // its late Session is left before a scene reload or a new connection.
                if (_connectOperation != null) await _connectOperation;
            }
            finally
            {
                _leaving = false;
                if (!_busy && !_disposed) SetState(string.IsNullOrEmpty(_terminalFailure) ? OnlineSessionPhase.Idle : OnlineSessionPhase.Failed, _terminalFailure ?? "");
            }
        }

        private async Task LeaveSafely(ISession session)
        {
            try { await session.LeaveAsync(); }
            catch (Exception exception)
            {
                // A lost network can prevent service cleanup. Report it; do not
                // fabricate a successful leave or expose a reusable stale code.
                _terminalFailure = "Disconnected; service cleanup failed: " + exception.Message;
                if (!_disposed) SetState(OnlineSessionPhase.Failed, _terminalFailure);
            }
        }
        private void Removed() => Lost("Removed from the online session.");
        private void HostChanged(string _) => Lost("The host left. Return to the menu and create a new room.");
        private void TransportFailed() => Lost("Relay transport failed. Check your connection and try again.");
        private void Disconnected(ulong clientId)
        {
            if (_session == null || Phase == OnlineSessionPhase.Leaving) return;
            if (!network.IsHost || clientId != network.LocalClientId)
                Lost("The other player disconnected. The online round has ended.");
        }
        private void Lost(string reason)
        { ConnectionLost?.Invoke(reason); _ = BeginCancel(reason); }
        private void SetState(OnlineSessionPhase phase, string message)
        { Phase = phase; Status = message; if (!_disposed) Changed?.Invoke(); }
    }
}

