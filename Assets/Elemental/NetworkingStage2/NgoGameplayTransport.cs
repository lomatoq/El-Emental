using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Elemental.Online
{
    /// <summary>Real NGO custom messages over the Sessions-owned Relay connection.</summary>
    [DisallowMultipleComponent]
    public sealed class NgoGameplayTransport : MonoBehaviour
    {
        private const string Channel = "elemental.gameplay.v1";
        [SerializeField] private MpsRelaySession session;
        [SerializeField] private OnlineGameplayBinding binding;
        private readonly OnlineStartBarrier _barrier = new OnlineStartBarrier();
        private readonly OnlineSequenceWindow _received = new OnlineSequenceWindow();
        private readonly OnlineCountdownStateBuffer _countdownState = new OnlineCountdownStateBuffer();
        private readonly uint[] _sent = new uint[32];
        private NetworkManager _network;
        private uint _epoch;
        private bool _registered, _prepared, _requestedReady, _started, _worldStarted;
        private ulong _remoteClient;
        private float _nextHandshake, _rateStart;
        private int _intentCount;
        private bool _countingDown;
        private double _beginAt;
        private uint _beginTick;
        public float CountdownSeconds { get; set; } = 4f;
        public float SecondsUntilStart => _countingDown && _network != null ? Mathf.Max(0f, (float)(_beginAt - _network.ServerTime.Time)) : 0f;
        public bool Running => _started;
        public byte LocalActor => _network != null && _network.IsHost ? (byte)1 : (byte)2;
        public string Status { get; private set; } = "Offline";
        public event Action<string> StatusChanged;
        public event Action CountdownStarted;
        [Serializable]
        public sealed class InputDiagnostics
        {
            public uint submittedMotor, submittedControl, receivedMotor, receivedControl;
            public uint acceptedMotor, acceptedControl, rejectedMotor, rejectedControl;
            public uint motorSequenceGaps, controlSequenceGaps, lastMotorSequence, lastControlSequence;
            public uint rejectedEpochOrSequence, ignoredBeforeRound;
            public string lastRejection;
            public float lastRejectionRealtime;
            public uint lastRejectedSequence, lastRejectedPacketTick, lastRejectedAuthorityTick, lastRejectedActor;
            public long lastRejectedTickDelta;
        }
        public InputDiagnostics Inputs { get; private set; } = new InputDiagnostics();
        private float _nextRejectionLog;
        private void RecordInput(in OnlinePacket packet, bool accepted, string rejection)
        {
            if (packet.Kind != OnlineMessage.MotorInput && packet.Kind != OnlineMessage.ControlIntent) return;
            bool motor = packet.Kind == OnlineMessage.MotorInput;
            uint previous = motor ? Inputs.lastMotorSequence : Inputs.lastControlSequence;
            uint gap = packet.Sequence > previous + 1 ? packet.Sequence - previous - 1 : 0;
            if (motor) { Inputs.receivedMotor++; Inputs.motorSequenceGaps += gap; Inputs.lastMotorSequence = packet.Sequence;
                if (accepted) Inputs.acceptedMotor++; else Inputs.rejectedMotor++; }
            else { Inputs.receivedControl++; Inputs.controlSequenceGaps += gap; Inputs.lastControlSequence = packet.Sequence;
                if (accepted) Inputs.acceptedControl++; else Inputs.rejectedControl++; }
            if (accepted) return;
            Inputs.lastRejection = rejection; Inputs.lastRejectionRealtime = Time.realtimeSinceStartup;
            Inputs.lastRejectedSequence = packet.Sequence; Inputs.lastRejectedPacketTick = packet.Tick;
            Inputs.lastRejectedAuthorityTick = binding.AuthorityTick; Inputs.lastRejectedActor = packet.Id;
            Inputs.lastRejectedTickDelta = (long)packet.Tick - binding.AuthorityTick;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Time.realtimeSinceStartup >= _nextRejectionLog)
            {
                _nextRejectionLog = Time.realtimeSinceStartup + 1f;
                Debug.LogWarning($"[OnlineInput] {rejection}; kind={packet.Kind} actor={packet.Id} sequence={packet.Sequence} packetTick={packet.Tick} authorityTick={binding.AuthorityTick} delta={Inputs.lastRejectedTickDelta} realtime={Time.realtimeSinceStartup:F3}", this);
            }
#endif
        }


        public void Configure(MpsRelaySession configuredSession, OnlineGameplayBinding configuredBinding)
        {
            if (_registered) throw new InvalidOperationException("Leave networking before rebinding gameplay.");
            session = configuredSession; binding = configuredBinding;
        }
        public void SetReady(bool ready) => _requestedReady = ready;
        private void OnDisable() => Stop();
        private void Update()
        {
            if (session == null || !session.Connected)
            { if (_registered) Stop(); return; }
            if (!_registered && !Initialize()) return;
            if (_network.ConnectedClientsIds.Count > 2)
            { Fail("This arena supports exactly two peers."); return; }
            if (_network.IsHost)
            {
                if (_network.ConnectedClientsIds.Count != 2) return;
                for (int i = 0; i < _network.ConnectedClientsIds.Count; i++)
                {
                    ulong id = _network.ConnectedClientsIds[i];
                    if (id != _network.LocalClientId) _remoteClient = id;
                }
            }
            if (_countingDown)
            {
                if (SecondsUntilStart <= 0f) StartRound(_beginTick);
                return;
            }
            if (Time.unscaledTime < _nextHandshake || _started) return;
            _nextHandshake = Time.unscaledTime + .25f;
            if (!binding.CanHandshake || binding.InitialWorldHash == 0) return;
            if (_network.IsHost)
            {
                if (_epoch == 0)
                {
                    _epoch = BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 0);
                    if (_epoch == 0) _epoch = 1;
                    _barrier.Reset(_epoch, binding.BuildHash, binding.InitialWorldHash);
                }
                Send(Handshake(OnlineMessage.Hello), _remoteClient);
                _barrier.SetReady(1, _epoch, binding.BuildHash, binding.InitialWorldHash,
                    binding.Capabilities, _requestedReady && binding.WorldReady);
                if (_barrier.CanStart)
                {
                    OnlinePacket begin = Handshake(OnlineMessage.Begin); begin.Tick = binding.AuthorityTick;
                    begin.Value = (float)(_network.ServerTime.Time + Mathf.Clamp(CountdownSeconds, 1f, 30f));
                    Send(begin, _remoteClient); ScheduleRound(begin.Tick, begin.Value);
                }
            }
            else if (_epoch != 0)
            {
                OnlinePacket ready = Handshake(OnlineMessage.Ready);
                ready.Value = _requestedReady && binding.WorldReady ? 1f : 0f;
                Send(ready, NetworkManager.ServerClientId);
            }
        }

        private bool Initialize()
        {
            _network = session.Network;
            Inputs = new InputDiagnostics(); _nextRejectionLog = 0;
            if (binding == null || binding.Capabilities != OnlineCapabilities.All || binding.BuildHash == 0)
            { Fail("Online gameplay bindings are incomplete; configure both actors, authority and world replication."); return false; }
            _prepared = true; // EndRound must also unwind a partially prepared actor graph.
            try
            {
                if (!binding.Prepare(_network.IsHost, LocalActor, this, out string error))
                { Fail("Cannot prepare online arena: " + error); return false; }
            }
            catch (Exception error) { Fail("Cannot prepare online arena: " + error.Message); return false; }
            _network.CustomMessagingManager.RegisterNamedMessageHandler(Channel, Receive);
            _network.OnClientDisconnectCallback += Disconnected;
            _registered = true;
            SetStatus("Waiting for both players to load the same arena and press Ready.");
            return true;
        }
        private OnlinePacket Handshake(OnlineMessage kind) => new OnlinePacket
        {
            Kind = kind, Epoch = _epoch, BuildHash = binding.BuildHash,
            WorldHash = binding.InitialWorldHash, Flags = (uint)binding.Capabilities
        };
        private void Receive(ulong sender, FastBufferReader reader)
        {
            try
            {
                if (reader.Length > OnlinePacket.MaximumBytes) { Fail("Oversized online packet."); return; }
                reader.ReadValueSafe(out OnlinePacket packet);
                if (!packet.IsFiniteAndBounded()) { Fail("Invalid online packet."); return; }
                bool fromServer = sender == NetworkManager.ServerClientId;
                if (_network.IsHost ? sender != _remoteClient : !fromServer) return;
                if (packet.Kind == OnlineMessage.Hello && !_network.IsHost)
                {
                    if (_epoch != 0 && _epoch != packet.Epoch) { Fail("Session epoch changed unexpectedly."); return; }
                    if (!binding.CanHandshake || binding.InitialWorldHash == 0) return;
                    if (!Compatible(packet)) { Fail("Game build or arena content differs between players."); return; }
                    if (_epoch == 0) { _epoch = packet.Epoch; binding.ConnectionEstablished(_epoch); }
                    return;
                }
                if (_epoch == 0 || packet.Epoch != _epoch || !_received.Accept(packet.Kind, packet.Sequence))
                { if (packet.Kind == OnlineMessage.MotorInput || packet.Kind == OnlineMessage.ControlIntent) Inputs.rejectedEpochOrSequence++; return; }
                if (packet.Kind == OnlineMessage.Ready && _network.IsHost)
                {
                    if (!_barrier.SetReady(2, packet.Epoch, packet.BuildHash, packet.WorldHash,
                        (OnlineCapabilities)packet.Flags, packet.Value > .5f))
                    { Fail("The other peer has incompatible gameplay bindings or content."); return; }
                    if (!_worldStarted) { _worldStarted = true; binding.ConnectionEstablished(_epoch); }
                    return;
                }
                if (packet.Kind == OnlineMessage.Begin && !_network.IsHost)
                {
                    if (!Compatible(packet) || !_requestedReady || !binding.WorldReady)
                    { Fail("The host started before this arena was ready."); return; }
                    if (!_started && !_countingDown) ScheduleRound(packet.Tick, packet.Value); return;
                }
                if (!_started && OnlinePacket.IsClientIntent(packet.Kind) && packet.Kind != OnlineMessage.WorldAck)
                { Inputs.ignoredBeforeRound++; return; }
                if (_network.IsHost)
                {
                    if (!OnlinePacket.IsClientIntent(packet.Kind)) return;
                    if (packet.Id != 2) { RecordInput(packet, false, "wrong-actor"); return; }
                    if (Time.unscaledTime - _rateStart >= 1f) { _rateStart = Time.unscaledTime; _intentCount = 0; }
                    if (++_intentCount > 120) { Fail("Client command rate exceeded."); return; }
                    bool accepted = binding.ApplyClientIntent(2, packet, out string rejection);
                    RecordInput(packet, accepted, rejection);
                    if (!accepted)
                    {
                        OnlinePacket decision = new OnlinePacket { Kind = OnlineMessage.CommandDecision,
                            Tick = binding.AuthorityTick, Id = 2, Aux = packet.Sequence, Flags = 2 };
                        Publish(decision); SetStatus("Command rejected: " + rejection);
                    }
                }
                else if (!OnlinePacket.IsClientIntent(packet.Kind) && packet.Kind >= OnlineMessage.CommandDecision)
                {
                    // NGO receives before this MonoBehaviour's Update. The host
                    // may reach the shared deadline one frame before this peer.
                    // Retain reliable outcomes only after a validated Begin;
                    // regular world geometry continues to apply during setup.
                    if (!_started && _countingDown && OnlineCountdownStateBuffer.RequiresRound(packet.Kind))
                    { _countdownState.Enqueue(packet); return; }
                    if (!binding.ApplyAuthorityState(packet, out string error)) Fail("World replication failed: " + error);
                }
            }
            catch (Exception error) { Fail("Online packet could not be applied: " + error.Message); }
        }
        private bool Compatible(in OnlinePacket packet) => packet.BuildHash == binding.BuildHash &&
            packet.WorldHash == binding.InitialWorldHash && packet.Flags == (uint)OnlineCapabilities.All;

        public bool Submit(OnlinePacket intent)
        {
            if ((!_started && (intent.Kind != OnlineMessage.WorldAck || _epoch == 0)) || !OnlinePacket.IsClientIntent(intent.Kind)) return false;
            intent.Id = LocalActor;
            if (_network.IsHost) return binding.ApplyClientIntent(1, intent, out _);
            if (intent.Kind == OnlineMessage.MotorInput) Inputs.submittedMotor++;
            if (intent.Kind == OnlineMessage.ControlIntent) Inputs.submittedControl++;
            Send(intent, NetworkManager.ServerClientId); return true;
        }
        public void Publish(OnlinePacket state)
        {
            if (!_registered || _epoch == 0 || !_network.IsHost || state.Kind < OnlineMessage.CommandDecision || OnlinePacket.IsClientIntent(state.Kind))
                throw new InvalidOperationException("Only the connected host publishes canonical world state.");
            Send(state, _remoteClient);
        }
        private void Send(OnlinePacket packet, ulong target)
        {
            packet.Version = OnlinePacket.Protocol; packet.Epoch = _epoch;
            packet.Sequence = ++_sent[(int)packet.Kind];
            if (!packet.IsFiniteAndBounded()) throw new InvalidOperationException("Invalid outgoing online packet.");
            using FastBufferWriter writer = new FastBufferWriter(OnlinePacket.MaximumBytes, Allocator.Temp);
            writer.WriteValueSafe(packet);
            _network.CustomMessagingManager.SendNamedMessage(Channel, target, writer,
                OnlinePacket.IsTransient(packet.Kind) ? NetworkDelivery.UnreliableSequenced : NetworkDelivery.ReliableSequenced);
        }
        private void ScheduleRound(uint tick, double deadline)
        {
            double remaining = deadline - _network.ServerTime.Time;
            if (remaining < -2d || remaining > 31d) { Fail("Invalid or expired round countdown."); return; }
            _beginTick = tick; _beginAt = deadline; _countingDown = true;
            SetStatus("Both players ready. Match starts after the countdown."); CountdownStarted?.Invoke();
        }
        private void StartRound(uint tick)
        {
            binding.BeginRound(tick); _started = true; _countingDown = false;
            while (_countdownState.TryDequeue(out OnlinePacket state))
                if (!binding.ApplyAuthorityState(state, out string error))
                { Fail("World replication failed after countdown: " + error); return; }
            SetStatus("Online round running.");
        }
        private void Disconnected(ulong _) { if (_registered) Fail("A peer disconnected; the round has ended."); }
        public void FailConnection(string error) => Fail(error);
        private void Fail(string error)
        { Stop(); SetStatus(error); if (session != null) _ = session.CancelAsync(); }
        public void Stop()
        {
            if (_registered && _network != null)
            {
                _network.CustomMessagingManager?.UnregisterNamedMessageHandler(Channel);
                _network.OnClientDisconnectCallback -= Disconnected;
            }
            _registered = false; _started = false; _countingDown = false; _beginAt = 0d; _beginTick = 0;
            if (_prepared && binding != null) binding.EndRound();
            _prepared = false; _requestedReady = false;
            _epoch = 0; _nextHandshake = 0; _intentCount = 0; _worldStarted = false;
            _barrier.Disconnect(); _received.Clear(); _countdownState.Clear(); Array.Clear(_sent, 0, _sent.Length);
        }
        private void SetStatus(string status) { Status = status; StatusChanged?.Invoke(status); }
    }
}

