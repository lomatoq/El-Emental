using System;
using System.Threading.Tasks;
using Elemental.Presentation.UI;
using UnityEngine;

namespace Elemental.Online
{
    /// <summary>Owns frontend subscription and cancellation; Sessions owns Relay startup.</summary>
    [DefaultExecutionOrder(12000), DisallowMultipleComponent]
    public sealed class EarthOnlineFrontend : MonoBehaviour
    {
        [SerializeField] private FrontendFlowController flow;
        [SerializeField] private MpsRelaySession session;
        [SerializeField] private NgoGameplayTransport transport;
        [SerializeField] private OnlineGameplayBinding binding;
        private bool _subscribed, _operation, _returning, _disposed, _roundEntered;
        private string _transportStatus;
        private OnlineFreshArenaEntry _freshEntry;
        public FrontendFlowController Flow => flow;
        public string LastDisconnectReason { get; private set; }

        public void Configure(FrontendFlowController frontend, MpsRelaySession relay,
            NgoGameplayTransport gameplay, OnlineGameplayBinding world)
        { Unsubscribe(); flow = frontend; session = relay; transport = gameplay; binding = world; Subscribe(); }

        private void Start()
        {
            Subscribe();
            if (flow != null) flow.SetOnlineAvailable(session != null && transport != null && binding != null &&
                binding.Capabilities == OnlineCapabilities.All && binding.BuildHash != 0);
        }
        private void OnEnable() => Subscribe();
        private void Subscribe()
        {
            if (_subscribed || flow == null || session == null || transport == null) return;
            flow.HostRequested += Host; flow.JoinRequested += Join;
            flow.NetworkReadyRequested += Ready; flow.NetworkCancelRequested += Cancel;
            flow.NetworkPauseRequested += Pause;
            session.Changed += SessionChanged; session.ConnectionLost += Lost;
            transport.StatusChanged += StatusChanged; transport.CountdownStarted += CountdownStarted;
            _subscribed = true;
        }
        private void Unsubscribe()
        {
            if (!_subscribed) return;
            flow.HostRequested -= Host; flow.JoinRequested -= Join;
            flow.NetworkReadyRequested -= Ready; flow.NetworkCancelRequested -= Cancel;
            flow.NetworkPauseRequested -= Pause;
            session.Changed -= SessionChanged; session.ConnectionLost -= Lost;
            transport.StatusChanged -= StatusChanged; transport.CountdownStarted -= CountdownStarted;
            _subscribed = false;
        }
        private async void Host() => await Connect(null);
        private async void Join(string code) => await Connect(code);
        private async Task Connect(string code)
        {
            if (_operation || _returning || session.Busy || session.Connected) return;
            if (flow.HasEnteredCombat || binding is EarthOnlineGameplayBinding world && world.RequiresFreshWorld)
            {
                flow.SetNetworkStatus("PREPARING A FRESH ONLINE ARENA…");
                if (session.Network.IsListening) { flow.SetNetworkStatus("Leave the existing connection before reloading the arena.", true); return; }
                _freshEntry = OnlineFreshArenaEntry.Begin(gameObject.scene.path, code, session.Network);
                return;
            }
            _operation = true; _transportStatus = null; flow.SetConnecting(true);
            try { if (code == null) await session.HostAsync(); else await session.JoinAsync(code); }
            catch (Exception error) { if (!_disposed) flow.SetNetworkStatus("Connection failed: " + error.Message, true); }
            finally { _operation = false; if (!_disposed) { flow.SetConnecting(false); SessionChanged(); } }
        }
        private void Ready()
        {
            if (!session.Connected || _returning || transport.Running) return;
            transport.CountdownSeconds = flow.CountdownDurationSeconds;
            transport.SetReady(true); flow.SetNetworkStatus("READY — WAITING FOR THE OTHER PLAYER AND WORLD SYNC");
        }
        public void ResumeFreshEntry(string code)
        {
            if (binding is EarthOnlineGameplayBinding world && world.RequiresFreshWorld)
            { flow.SetNetworkStatus("The saved arena has no clean terrain baseline for online play.", true); return; }
            if (code == null) flow.OpenHost();
            else { flow.OpenJoin(); flow.JoinRoom(code); }
        }
        private void Pause(bool suppressed)
        {
            if (binding is EarthOnlineGameplayBinding world) world.SetLocalGameplayInputSuppressed(suppressed);
        }
        private bool InOnlineMatchScreen => _roundEntered && (flow.State is FrontendState.Combat or
            FrontendState.Starting or FrontendState.Paused or FrontendState.Ending or FrontendState.Settings);
        private void SessionChanged()
        {
            if (_disposed || flow == null) return;
            if (_returning) { flow.SetConnecting(session.Busy); return; }
            flow.SetConnecting(session.Busy && session.Phase != OnlineSessionPhase.Waiting);
            if (session.Phase == OnlineSessionPhase.Waiting)
                flow.ShowConnectedRoom(session.Code, transport.Running ? transport.Status : session.Status);
            else if (session.Phase == OnlineSessionPhase.Failed)
            {
                transport.Stop();
                if (InOnlineMatchScreen) _ = Leave(session.Status);
                else flow.SetNetworkStatus(session.Status, true);
            }
            else if (session.Phase is OnlineSessionPhase.Idle or OnlineSessionPhase.Leaving)
            {
                if (InOnlineMatchScreen)
                { _ = Leave(_transportStatus ?? "The online round has ended."); }
                else if (!_returning && flow.State is (FrontendState.Host or FrontendState.Join))
                    flow.SetNetworkStatus(_transportStatus ?? session.Status);
            }
            else if (flow.State is FrontendState.Host or FrontendState.Join) flow.SetNetworkStatus(session.Status);
        }
        private void StatusChanged(string status)
        { _transportStatus = status; if (!_disposed) flow.SetNetworkStatus(status); }
        private void CountdownStarted()
        {
            if (_disposed) return;
            _roundEntered = flow.BeginOnlineMatch(() => transport.SecondsUntilStart);
            if (!_roundEntered) Lost("The online round could not enter the combat screen.");
        }
        private async void Cancel() => await Leave(null);
        private async void Lost(string reason) => await Leave(reason);
        private async Task Leave(string reason)
        {
            if (_returning) return;
            if (_freshEntry != null) _freshEntry.CancelEntry();
            _returning = true; LastDisconnectReason = reason;
            try
            {
                transport.Stop();
                if (!_disposed) { flow.SetConnecting(true); flow.SetNetworkStatus(reason ?? "Leaving room…"); }
                await session.CancelAsync();
                if (!_disposed) { flow.SetConnecting(false); flow.ShowMain(reason); }
            }
            catch (Exception error) { if (!_disposed) flow.ShowMain("Disconnected: " + error.Message); }
            finally { _returning = false; _roundEntered = false; }
        }
        private void OnDisable()
        { Unsubscribe(); if (transport != null) transport.Stop(); if (session != null) _ = session.CancelAsync(); }
        private void OnDestroy() { _disposed = true; Unsubscribe(); }
    }
}



