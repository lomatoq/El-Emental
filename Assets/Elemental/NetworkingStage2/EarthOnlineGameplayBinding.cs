using System;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Input.Gestures;
using Elemental.Input.Actions;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Combat;
using Elemental.Simulation.Networking;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Online
{
    /// <summary>
    /// Authored actor inputs execute the existing Earth toolkit on the host. Clients
    /// predict their motor and receive canonical terrain, world geometry and impact facts.
    /// </summary>
    [DefaultExecutionOrder(-5000)]
    public sealed class EarthOnlineGameplayBinding : OnlineGameplayBinding
    {
        [Serializable]
        public sealed class Actor
        {
            public GameObject Root;
            public Rigidbody Body;
            public PlanetMotor Motor;
            public EarthCharacterImpactTarget Impact;
            public ActiveRagdollPuppet Puppet;
            public HumanoidRagdollRig Rig;
            public Animator Animator;
            public Collider Collider;
            public Transform CameraFrame;
            public MonoBehaviour AuthoredMotorInput;
            public EarthInputAdapter SemanticInput;
            public OnlineLocalMotorInput LocalInput;
            public OnlineRemoteMotorInput RemoteInput;
            public OnlineEarthInputBridge EarthInput;
            public MagicExecutor Executor;
            public MagicInputController MagicInput;
            public EarthActionRouterBehaviour ActionRouter;
            public EarthDualMouseAbilityController DualMouse;
            public EarthRockDebrisPool DebrisPool;
            public Behaviour[] LocalOnlyControls = Array.Empty<Behaviour>();
            public Behaviour[] EarthMutationControls = Array.Empty<Behaviour>();
            [NonSerialized] internal bool Captured, Active, Kinematic, MotorEnabled, AuthoredInputEnabled, SemanticInputEnabled;
            [NonSerialized] internal bool[] EnabledControls, EnabledEarthControls;
            [NonSerialized] internal MonoBehaviour PreviousInput;
            [NonSerialized] internal Transform PreviousFrame;
            [NonSerialized] internal Vector3 Position;
            [NonSerialized] internal Quaternion Rotation;
            [NonSerialized] internal uint PreviousActorId;
            [NonSerialized] internal EarthMvpDuelController PreviousMagicDuel, PreviousRouterDuel, PreviousDualDuel;
            [NonSerialized] internal EarthDuelFighterId PreviousMagicFighter, PreviousRouterFighter, PreviousDualFighter;
            [NonSerialized] internal EarthMvpDuelController PreviousImpactDuel;
            [NonSerialized] internal EarthDuelFighterId PreviousImpactFighter;
            [NonSerialized] internal uint PreviousImpactId;
        }

        [SerializeField] private Actor actorOne;
        [SerializeField] private Actor actorTwo;
        [SerializeField] private EarthMvpDuelController onlineDuel;
        [SerializeField] private EarthMvpDuelController offlineDuel;
        [SerializeField] private GameObject[] offlineOnlyRoots = Array.Empty<GameObject>();
        private bool[] _offlineRootActive;
        [SerializeField] private EarthSceneReadinessGate readiness;
        [SerializeField] private VoxelPlanetBehaviour planet;
        [SerializeField] private OnlineBodyReplicas bodies;
        [SerializeField] private OnlineWorldReplicaRegistry world;
        [SerializeField] private OnlineTerrainReplication terrain;
        [SerializeField] private ulong buildHash;
        [SerializeField] private ulong initialWorldHash;
        private NgoGameplayTransport _transport;
        private bool _authority, _prepared, _running, _offlineEnabled;
        private byte _localActor;
        private readonly OnlineAuthorityClock _clock = new OnlineAuthorityClock();
        private uint _tick { get => _clock.Tick; set => _clock.Reset(value); }
        private float _nextState;
        public override OnlineCapabilities Capabilities => CompleteActorReferences() && CompleteWorldReferences()
            ? OnlineCapabilities.All
            : OnlineCapabilities.None;
        public override ulong BuildHash => buildHash;
        public override ulong InitialWorldHash => initialWorldHash;
        public override bool CanHandshake => _prepared && readiness != null && readiness.IsReady && planet != null && planet.GeometryReady &&
            world.InitialSourcesReady && actorOne.DebrisPool.PhysicsPrepared && actorTwo.DebrisPool.PhysicsPrepared;
        public override bool WorldReady => CanHandshake && world.WorldSynchronized && terrain.WorldSynchronized;
        public override uint AuthorityTick => _tick;
        public bool IsOnlineAuthority => _prepared && _authority;
        public bool RequiresFreshWorld => planet == null || planet.State == null || planet.State.EditCount != 0;
        public byte LocalActorId => _localActor;
        public bool IsPrepared => _prepared;
        public bool LocalGameplayInputSuppressed { get; private set; }
        private readonly bool[] _pauseControls = new bool[3];
        public Actor ActorOne => actorOne;
        public Actor ActorTwo => actorTwo;
        public EarthMvpDuelController OnlineDuel => onlineDuel;
        public EarthMvpDuelController OfflineDuel => offlineDuel;
        public event Action<byte> Prepared;
        public event Action Ended;

        private static bool Complete(Actor actor) => actor != null && actor.Root != null && actor.Body != null &&
            actor.Motor != null && actor.Impact != null && actor.Puppet != null && actor.Rig != null &&
            actor.Animator != null && actor.Collider != null && actor.CameraFrame != null &&
            actor.AuthoredMotorInput is PlanetInputReader && actor.SemanticInput != null && actor.LocalInput != null && actor.RemoteInput != null &&
            actor.AuthoredMotorInput != actor.LocalInput && actor.AuthoredMotorInput != actor.RemoteInput &&
            actor.LocalOnlyControls != null && actor.EarthMutationControls != null && DistinctControlSets(actor) &&
            actor.EarthInput != null && actor.EarthInput.HasReferences && actor.Executor != null &&
            actor.MagicInput != null && actor.MagicInput.EarthExecutor == actor.Executor && actor.ActionRouter != null && actor.DebrisPool != null &&
            Array.IndexOf(actor.EarthMutationControls, actor.MagicInput) >= 0 &&
            Array.IndexOf(actor.EarthMutationControls, actor.ActionRouter) >= 0 && actor.DualMouse != null &&
            Array.IndexOf(actor.EarthMutationControls, actor.DualMouse) >= 0 && Array.IndexOf(actor.EarthMutationControls, actor.Executor) >= 0;
        private static bool DistinctControlSets(Actor actor)
        {
            if (Array.IndexOf(actor.LocalOnlyControls, actor.SemanticInput) >= 0 || Array.IndexOf(actor.EarthMutationControls, actor.SemanticInput) >= 0) return false;
            for (int i = 0; i < actor.LocalOnlyControls.Length; i++)
                for (int j = 0; j < actor.EarthMutationControls.Length; j++)
                    if (actor.LocalOnlyControls[i] != null && actor.LocalOnlyControls[i] == actor.EarthMutationControls[j]) return false;
            return true;
        }
        private bool CompleteActorReferences() => Complete(actorOne) && Complete(actorTwo) && actorOne.Root != actorTwo.Root &&
            actorOne.Executor != actorTwo.Executor && actorOne.Executor.VoxelPlanet == planet && actorTwo.Executor.VoxelPlanet == planet &&
            actorOne.Executor.FragmentPool != actorTwo.Executor.FragmentPool && actorOne.Executor.FragmentPool != null && actorTwo.Executor.FragmentPool != null &&
            actorOne.Executor.MatterKernel != null && actorOne.Executor.MatterKernel == actorTwo.Executor.MatterKernel &&
            onlineDuel != null && onlineDuel != offlineDuel && bodies != null && planet != null && readiness != null;
        private bool CompleteWorldReferences() => world != null && world.HasReferences && terrain != null && terrain.HasReferences &&
            !world.ContainsSource(actorOne.Body.transform) && !world.ContainsSource(actorTwo.Body.transform) && !world.ContainsSource(planet.transform);

        public override bool Prepare(bool authority, byte localActor, NgoGameplayTransport transport, out string error)
        {
            error = null;
            if (_prepared || !CompleteActorReferences() || !CompleteWorldReferences() || buildHash == 0 || initialWorldHash == 0 ||
                localActor != (authority ? 1 : 2))
            { error = "Bind two distinct authored player actors, dedicated online duel, input frames, world identity and readiness."; return false; }
            _authority = authority; _localActor = localActor; _transport = transport;
            offlineDuel?.MarkArenaMatchStarted();
            try
            {
                world.Configure(authority, transport);
                terrain.Configure(authority, transport);
                world.Failed += ReplicationFailed; terrain.Failed += ReplicationFailed;
            }
            catch (Exception exception)
            {
                world.Stop(); terrain.Stop(); error = exception.Message; return false;
            }
            _prepared = true; _tick = 1; _nextState = 0;
            _offlineEnabled = offlineDuel != null && offlineDuel.enabled;
            if (offlineDuel != null) offlineDuel.enabled = false;
            _offlineRootActive = new bool[offlineOnlyRoots.Length];
            for (int i = 0; i < offlineOnlyRoots.Length; i++)
                if (offlineOnlyRoots[i] != null) { _offlineRootActive[i] = offlineOnlyRoots[i].activeSelf; offlineOnlyRoots[i].SetActive(false); }
            bodies.Clear(); bodies.Configure(transport, authority);
            PrepareActor(actorOne, 1); PrepareActor(actorTwo, 2);
            onlineDuel.enabled = true;
            onlineDuel.ConfigureOnlineAuthority(authority);
            onlineDuel.Configure(actorOne.Puppet, actorOne.Body, null, null, null, actorTwo.Motor,
                actorTwo.Body, actorTwo.Collider, actorTwo.Animator, actorOne.Rig, actorTwo.Rig,
                actorOne.Impact, actorTwo.Impact);
            onlineDuel.SetRoundReady(false);
            onlineDuel.ConfigureRoundControls(actorOne.MagicInput, actorOne.ActionRouter, actorOne.DualMouse,
                actorTwo.MagicInput, actorTwo.ActionRouter, actorTwo.DualMouse);
            world.CheckpointGeometryReady = () => terrain.WorldSynchronized;
            onlineDuel.ArenaRestoreReady = () => WorldReady;
            onlineDuel.ArenaRestoreCompleted += RestoredArena;
            actorOne.Impact.AuthorityImpactCommitted += ResolvedImpact;
            actorTwo.Impact.AuthorityImpactCommitted += ResolvedImpact;
            onlineDuel.StateChanged += MatchChanged;
            Prepared?.Invoke(_localActor);
            return true;
        }

        public void SetLocalGameplayInputSuppressed(bool suppressed)
        {
            if (!_prepared || LocalGameplayInputSuppressed == suppressed) return;
            Actor local = _localActor == 1 ? actorOne : actorTwo;
            LocalGameplayInputSuppressed = suppressed;
            local.EarthInput.SetLocalInputSuppressed(suppressed);
            local.SemanticInput.SetGameplayInputSuppressed(suppressed);
            if (suppressed)
            {
                _pauseControls[0] = local.MagicInput.enabled;
                _pauseControls[1] = local.ActionRouter.enabled;
                _pauseControls[2] = local.DualMouse.enabled;
                local.MagicInput.enabled = false; local.ActionRouter.enabled = false; local.DualMouse.enabled = false;
                if (_authority)
                    foreach (Behaviour control in local.EarthMutationControls)
                    {
                        if (control is EarthSurfController surf) surf.Cancel();
                        else if (control is EarthResonanceController resonance) resonance.Cancel();
                    }
            }
            else
            {
                local.MagicInput.enabled = _pauseControls[0];
                local.ActionRouter.enabled = _pauseControls[1];
                local.DualMouse.enabled = _pauseControls[2];
            }
            // Motor/physics, host's remote actor, world replication and match clock
            // remain active. Only the chosen local semantic input is neutralized.
        }
        public override void ConnectionEstablished(uint epoch)
        {
            if (!_prepared) return;
            terrain.ConnectionEstablished();
            world.BeginSynchronization();
        }
        private void ReplicationFailed(string error) => _transport.FailConnection("World replication: " + error);

        private void PrepareActor(Actor actor, byte id)
        {
            actor.Active = actor.Root.activeSelf; actor.Kinematic = actor.Body.isKinematic;
            actor.MotorEnabled = actor.Motor.enabled; actor.PreviousInput = actor.Motor.ConfiguredInputSource;
            actor.AuthoredInputEnabled = actor.AuthoredMotorInput.enabled;
            actor.SemanticInputEnabled = actor.SemanticInput.enabled;
            actor.PreviousFrame = actor.Motor.ConfiguredCameraFrame;
            actor.Position = actor.Body.position; actor.Rotation = actor.Body.rotation;
            actor.PreviousActorId = actor.Puppet.StableActorId;
            actor.PreviousImpactDuel = actor.Impact.BoundDuel; actor.PreviousImpactFighter = actor.Impact.FighterId;
            actor.PreviousImpactId = actor.Impact.StableFighterId;
            actor.PreviousMagicDuel = actor.MagicInput.BoundDuel; actor.PreviousMagicFighter = actor.MagicInput.BoundDuelFighter;
            actor.PreviousRouterDuel = actor.ActionRouter.BoundDuel; actor.PreviousRouterFighter = actor.ActionRouter.BoundDuelFighter;
            actor.PreviousDualDuel = actor.DualMouse.BoundDuel; actor.PreviousDualFighter = actor.DualMouse.BoundDuelFighter;
            EarthDuelFighterId fighter = id == 1 ? EarthDuelFighterId.Player : EarthDuelFighterId.Bot;
            actor.MagicInput.BindDuel(onlineDuel, fighter); actor.ActionRouter.BindDuel(onlineDuel, fighter);
            actor.DualMouse.BindDuel(onlineDuel, fighter);
            actor.Executor.ConfigureOnlineAuthority(_authority);
            actor.MagicInput.ConfigureOnlinePresentation(!_authority);
            actor.EnabledControls = CaptureAndSet(actor.LocalOnlyControls, false);
            actor.EnabledEarthControls = CaptureAndSet(actor.EarthMutationControls, false);
            actor.SemanticInput.enabled = false;
            actor.Captured = true;
            if (id == 2) actor.EarthInput.Configure(this, _transport, !_authority,
                ((PlanetInputReader)actor.AuthoredMotorInput).TapJumpThresholdSeconds);
            actor.Root.SetActive(true);
            actor.Puppet.ConfigureActorIdentity(id);
            actor.Impact.Configure(id == 1 ? EarthDuelFighterId.Player : EarthDuelFighterId.Bot, id, actor.Body, onlineDuel);
            actor.Impact.ConfigureOnlineAuthority(_authority);
            actor.Motor.ClearImpactStun();
            actor.Body.isKinematic = !_authority && id != _localActor;
            if (id == _localActor)
            {
                actor.LocalInput.Configure(actor.AuthoredMotorInput, this, _transport, actor.CameraFrame, !_authority, actor.EarthInput);
                actor.Motor.ConfigureOnlineInput(actor.LocalInput, actor.CameraFrame);
            }
            else
            {
                actor.RemoteInput.Configure(actor.CameraFrame);
                // Host router owns tap/pillar/cushion choice and movement consumption.
                // Its authored reader consumes the remotely fed semantic adapter.
                actor.Motor.ConfigureOnlineInput(_authority ? actor.AuthoredMotorInput : actor.RemoteInput, actor.CameraFrame);
            }
            actor.Motor.enabled = false;
            actor.AuthoredMotorInput.enabled = false;
            bodies.Register(id, actor.Body, !_authority && id == _localActor);
        }

        public override void BeginRound(uint authorityTick)
        {
            if (!_prepared || !WorldReady) throw new InvalidOperationException("Both authored actors and arena must be prepared.");
            _tick = authorityTick; _running = true;
            if (_authority) { onlineDuel.RestartRound(); onlineDuel.SetRoundReady(true); }
            SetActorControls(actorOne, 1); SetActorControls(actorTwo, 2);
        }
        private void SetActorControls(Actor actor, byte id)
        {
            for (int i = 0; i < actor.LocalOnlyControls.Length; i++)
                if (actor.LocalOnlyControls[i] != null) actor.LocalOnlyControls[i].enabled = id == _localActor;
            actor.Motor.enabled = _authority || id == _localActor;
            for (int i = 0; i < actor.EarthMutationControls.Length; i++)
                if (actor.EarthMutationControls[i] != null) actor.EarthMutationControls[i].enabled = _authority;
            actor.AuthoredMotorInput.enabled = _authority;
            // The host's remote adapter must keep advancing semantic press/release
            // frames even though that actor has no local hardware PlayerInput.
            actor.SemanticInput.enabled = _authority || id == _localActor;
        }

        private void FixedUpdate()
        {
            if (!_running) return;
            _clock.Advance(_authority);
            if (!_authority || _transport == null || !_transport.Running || Time.unscaledTime < _nextState) return;
            _nextState = Time.unscaledTime + .05f;
            PublishActor(actorOne, 1); PublishActor(actorTwo, 2); PublishMatch();
        }
        private void PublishActor(Actor actor, byte id)
        {
            bool first = id == 1;
            _transport.Publish(new OnlinePacket { Kind = OnlineMessage.CharacterState, Id = id, Tick = _tick,
                A = actor.Body.position, B = actor.Body.linearVelocity, C = actor.Body.angularVelocity, Rotation = actor.Body.rotation,
                Flags = (uint)(first ? onlineDuel.PlayerPhase : onlineDuel.BotPhase) |
                    ((uint)onlineDuel.GetKnockdownPhase(first ? EarthDuelFighterId.Player : EarthDuelFighterId.Bot) << 8),
                Value = actor.Motor.ImpactStunRemaining,
                Value2 = first ? onlineDuel.PlayerRespawnRemaining : onlineDuel.BotRespawnRemaining });
            _transport.Publish(OnlineAbilityViewCodec.Capture(actor.MagicInput, actor.Executor, id, _tick, world.SourceBodyId(actor.Executor.HeldBody)));
        }
        private void MatchChanged() { if (_running && _authority && _transport.Running) PublishMatch(); }
        private void PublishMatch() => _transport.Publish(new OnlinePacket { Kind = OnlineMessage.MatchState,
            Tick = _tick, Id = 1, A = new Vector3(onlineDuel.PlayerHealth, onlineDuel.BotHealth, 0),
            Aux = (uint)onlineDuel.PlayerScore, Seed = (uint)onlineDuel.BotScore,
            Value = onlineDuel.RoundRemainingSeconds, Flags = onlineDuel.CombatAllowed ? 1u : 0u });

        private void RestoredArena() { if (_authority && _running) world.CheckpointRestoredArena(); }
        private void ResolvedImpact(EarthResolvedImpactSnapshot snapshot)
        {
            if (!_running || !_authority || !_transport.Running) return;
            EarthWorldResponseEvent response = snapshot.Response;
            _transport.Publish(new OnlinePacket { Kind = OnlineMessage.CombatState, Tick = _tick,
                Id = response.TargetStableId, Aux = response.ResponseId, Seed = response.SourceStableId,
                Flags = (uint)response.Response | ((uint)response.SourceKind << 8) | ((uint)response.Kind << 16) |
                    ((uint)response.HitRegion << 24),
                A = (Vector3)response.Point, B = (Vector3)response.Direction, C = (Vector3)snapshot.RootVelocityChange,
                D = (Vector3)response.Normal, Value = snapshot.ReactionVelocity, Value2 = snapshot.StunSeconds,
                Value3 = response.Impulse, Value4 = response.KineticEnergy, Value5 = response.Intensity01 });
        }

        public override bool ApplyClientIntent(byte actor, in OnlinePacket intent, out string rejection)
        {
            rejection = null;
            if (_prepared && _authority && actor == 2 && intent.Kind == OnlineMessage.WorldAck)
            {
                if (world.AcceptAcknowledgement(intent)) return true;
                rejection = "Unexpected initial world acknowledgement."; return false;
            }
            if (!_authority) { rejection = "not-authority"; return false; }
            if (!_running) { rejection = "round-not-running"; return false; }
            if (actor != 2 || intent.Id != 2) { rejection = "wrong-actor"; return false; }
            long delta = (long)intent.Tick - _tick;
            if (delta > 12) { rejection = "future-authority-tick"; return false; }
            if (delta < -180) { rejection = "expired-authority-tick"; return false; }
            if (intent.Kind == OnlineMessage.ControlIntent)
            {
                if (!actorTwo.EarthInput.Receive(intent)) { rejection = "Invalid Earth control frame, camera or input queue overflow."; return false; }
                return true;
            }
            if (intent.Kind != OnlineMessage.MotorInput || !actorTwo.RemoteInput.Receive(intent))
            { rejection = "Invalid movement or aim frame."; return false; }
            return true;
        }
        public override bool ApplyAuthorityState(in OnlinePacket state, out string failure)
        {
            failure = null;
            if (_authority || !_prepared) { failure = "Replica is not prepared."; return false; }
            switch (state.Kind)
            {
                case OnlineMessage.CommandDecision:
                    return true; // Rejected input is informational; no outcome is predicted locally.
                case OnlineMessage.ArenaReset:
                case OnlineMessage.TerrainBegin: case OnlineMessage.TerrainEdit:
                case OnlineMessage.TerrainCommit: case OnlineMessage.TerrainChunkHash:
                    return terrain.Apply(state, out failure);
                case OnlineMessage.WorldBegin: case OnlineMessage.WorldEnd:
                case OnlineMessage.MeshBegin: case OnlineMessage.MeshChunk: case OnlineMessage.MeshEnd:
                case OnlineMessage.BodySpawn: case OnlineMessage.BodyDespawn: case OnlineMessage.BodyState:
                case OnlineMessage.ArenaPose:
                case OnlineMessage.BodyMaterial: case OnlineMessage.BodyCollider: case OnlineMessage.BodyVisual: case OnlineMessage.NodeCommit:
                    return world.Apply(state, out failure);
            }
            if (!_running && (state.Kind == OnlineMessage.CharacterState || state.Kind == OnlineMessage.RegionState))
                return true; // An unreliable pose may overtake reliable Begin; next snapshot refreshes it.
            if (!_running) { failure = "Actor state arrived before the round started."; return false; }
            if (state.Tick != 0 && (state.Kind == OnlineMessage.CharacterState || state.Kind == OnlineMessage.MatchState))
                _clock.Observe(state.Tick); // Reliable match and unreliable pose channels may arrive out of order.
            if (state.Kind == OnlineMessage.MatchState)
            {
                if (state.Aux > int.MaxValue || state.Seed > int.MaxValue ||
                    !onlineDuel.ApplyReplicaMatch(state.A.x, state.A.y, (int)state.Aux, (int)state.Seed, state.Value, (state.Flags & 1) != 0))
                { failure = "Invalid canonical match snapshot."; return false; }
                return true;
            }
            if (state.Id != 1 && state.Id != 2) { failure = "Unknown actor."; return false; }
            Actor actor = state.Id == 1 ? actorOne : actorTwo;
            if (state.Kind == OnlineMessage.RegionState)
            {
                if (!OnlineAbilityViewCodec.Decode(state, out EarthOnlineAbilityView view))
                { failure = "Invalid accepted ability presentation state."; return false; }
                actor.Executor.ApplyOnlinePresentation(view, world.ReplicaBody((uint)state.BuildHash));
                actor.MagicInput.ApplyOnlinePresentation(view); return true;
            }
            if (state.Kind == OnlineMessage.CharacterState)
            {
                var phase = (EarthDuelFighterPhase)(state.Flags & 255);
                var knockdown = (EarthRecoverableKnockdownPhase)((state.Flags >> 8) & 255);
                if (state.Value < 0 || state.Value > .6f || !onlineDuel.ApplyReplicaFighter(
                    state.Id == 1 ? EarthDuelFighterId.Player : EarthDuelFighterId.Bot, phase, state.Value2, knockdown))
                { failure = "Invalid fighter phase snapshot."; return false; }
                actor.Motor.ApplyReplicaStun(state.Value);
                OnlinePacket pose = state; pose.Kind = OnlineMessage.BodyState;
                if (!bodies.Apply(pose)) { failure = "Invalid actor root pose."; return false; }
                actor.Motor.enabled = state.Id == _localActor && phase == EarthDuelFighterPhase.Active &&
                    knockdown == EarthRecoverableKnockdownPhase.Inactive;
                return true;
            }
            if (state.Kind == OnlineMessage.CombatState)
            {
                var response = (EarthCharacterImpactResponse)(state.Flags & 255);
                var source = (EarthCharacterImpactSourceKind)((state.Flags >> 8) & 255);
                var kind = (EarthWorldResponseKind)((state.Flags >> 16) & 255);
                var hitRegion = (EarthHitRegion)(state.Flags >> 24);
                if (response > EarthCharacterImpactResponse.Knockout || source > EarthCharacterImpactSourceKind.FallLanding ||
                    kind > EarthWorldResponseKind.Knockout || ((byte)hitRegion > 10 && (byte)hitRegion != 255) ||
                    state.Value < 0 || state.Value2 < 0 || state.Value2 > .6f)
                { failure = "Invalid resolved impact."; return false; }
                var fact = new EarthWorldResponseEvent(state.Aux, state.Tick, state.Seed, state.Id, kind,
                    source, response, (float3)state.A, (float3)state.D, (float3)state.B, state.Value3, state.Value4, state.Value5, hitRegion);
                var impact = new EarthResolvedImpactSnapshot(fact, state.Value, state.Value2, (float3)state.C);
                if (!actor.Impact.ApplyReplicaImpact(state.Sequence, impact))
                { failure = "Resolved impact could not be applied."; return false; }
                return true;
            }
            failure = "The required authority-state module is not installed: " + state.Kind;
            return false;
        }

        public override void EndRound()
        {
            if (!_prepared) return;
            SetLocalGameplayInputSuppressed(false);
            _running = false;
            world.Failed -= ReplicationFailed; terrain.Failed -= ReplicationFailed;
            onlineDuel.ArenaRestoreCompleted -= RestoredArena; onlineDuel.ArenaRestoreReady = null;
            world.Stop(); terrain.Stop();
            actorOne.Impact.AuthorityImpactCommitted -= ResolvedImpact;
            actorTwo.Impact.AuthorityImpactCommitted -= ResolvedImpact;
            onlineDuel.StateChanged -= MatchChanged; onlineDuel.SetRoundReady(false);
            onlineDuel.enabled = false;
            RestoreActor(actorOne); RestoreActor(actorTwo); bodies.Clear();
            onlineDuel.ConfigureOnlineAuthority(true);
            if (offlineDuel != null) offlineDuel.enabled = _offlineEnabled;
            if (_offlineRootActive != null)
                for (int i = 0; i < offlineOnlyRoots.Length; i++) if (offlineOnlyRoots[i] != null) offlineOnlyRoots[i].SetActive(_offlineRootActive[i]);
            offlineDuel?.RestoreArenaForMatchBoundary();
            onlineDuel.ReleaseArenaRestorationOwnership();
            _prepared = false;
            Ended?.Invoke();
        }
        private static bool[] CaptureAndSet(Behaviour[] controls, bool enabled)
        {
            var prior = new bool[controls.Length];
            for (int i = 0; i < controls.Length; i++)
                if (controls[i] != null) { prior[i] = controls[i].enabled; controls[i].enabled = enabled; }
            return prior;
        }
        private static void RestoreActor(Actor actor)
        {
            if (!actor.Captured) return;
            actor.EarthInput.Stop();
            actor.MagicInput.BindDuel(actor.PreviousMagicDuel, actor.PreviousMagicFighter);
            actor.ActionRouter.BindDuel(actor.PreviousRouterDuel, actor.PreviousRouterFighter);
            actor.DualMouse.BindDuel(actor.PreviousDualDuel, actor.PreviousDualFighter);
            actor.Executor.ConfigureOnlineAuthority(true); actor.MagicInput.ConfigureOnlinePresentation(false);
            actor.Rig.ResetToAnimated(); actor.Impact.ConfigureOnlineAuthority(true);
            actor.Impact.Configure(actor.PreviousImpactFighter, actor.PreviousImpactId, actor.Body, actor.PreviousImpactDuel);
            actor.Puppet.ConfigureActorIdentity(actor.PreviousActorId);
            actor.Motor.ClearImpactStun(); actor.Motor.ConfigureOnlineInput(actor.PreviousInput, actor.PreviousFrame);
            actor.Body.isKinematic = actor.Kinematic; actor.Body.position = actor.Position; actor.Body.rotation = actor.Rotation;
            if (!actor.Body.isKinematic) { actor.Body.linearVelocity = Vector3.zero; actor.Body.angularVelocity = Vector3.zero; }
            actor.Motor.ResetAfterTeleport(); actor.Motor.enabled = actor.MotorEnabled;
            for (int i = 0; i < actor.LocalOnlyControls.Length; i++)
                if (actor.LocalOnlyControls[i] != null) actor.LocalOnlyControls[i].enabled = actor.EnabledControls[i];
            for (int i = 0; i < actor.EarthMutationControls.Length; i++)
                if (actor.EarthMutationControls[i] != null) actor.EarthMutationControls[i].enabled = actor.EnabledEarthControls[i];
            actor.AuthoredMotorInput.enabled = actor.AuthoredInputEnabled;
            actor.SemanticInput.enabled = actor.SemanticInputEnabled;
            actor.Root.SetActive(actor.Active);
            actor.Captured = false;
        }
    }
}


