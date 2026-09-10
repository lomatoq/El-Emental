using System;
using Elemental.Presentation.UI;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Combat;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Elemental.Presentation.VFX
{
    [DefaultExecutionOrder(12000), DisallowMultipleComponent]
    public sealed class GoldRespawnPresenter : MonoBehaviour
    {
        [Serializable] private sealed class Actor
        {
            public Transform Root; public Animator Animator; public PlanetMotor Motor; public HumanoidRagdollRig Rig;
        }
        private sealed class Slot
        {
            public readonly RespawnVisualProxy Proxy = new();

            public EarthRespawnCue Cue; public uint LastGeneration; public bool Active;
        }
        [SerializeField] private EarthMvpDuelController duel;
        [SerializeField] private FrontendFlowController frontend;
        [SerializeField] private Shader ringShader;
        [SerializeField] private Actor player = new(), bot = new();
        private static readonly ProfilerMarker TickMarker = new("Elemental.Respawn.GoldPresentation");
        private static readonly ProfilerMarker CacheMarker = new("Elemental.Respawn.StandingPoseBake");
        private static readonly Color Gold = new(1f, .61f, .16f, 1);
        private readonly Slot[] _slots = { new(), new() };
        private GameObject _ownedRoot;
        private static readonly int WaveCentersId = Shader.PropertyToID("_GoldRespawnWaveCenters");
        private static readonly int WaveUpsId = Shader.PropertyToID("_GoldRespawnWaveUps");
        private readonly Vector4[] _waveCenters = new Vector4[2], _waveUps = new Vector4[2];
        private static GoldRespawnPresenter _surfaceWaveOwner;
        private bool _subscribed, _initialized;
        private int _createdFrame;
        public int UniqueCueCount { get; private set; }
        public int CompletedCueCount { get; private set; }
        public bool StandingPosesReady => _slots[0].Proxy.Ready && _slots[1].Proxy.Ready;
        public int ActiveEffectCount => (_slots[0].Active ? 1 : 0) + (_slots[1].Active ? 1 : 0);
        public int VisibleProxyCount => (_slots[0].Proxy.Visible ? 1 : 0) + (_slots[1].Proxy.Visible ? 1 : 0);
        public Transform OwnedRoot => _ownedRoot != null ? _ownedRoot.transform : null;
        public void Configure(EarthMvpDuelController owner, FrontendFlowController flow, Shader shader)
        {
            Unsubscribe(); duel = owner; frontend = flow; ringShader = shader;
            player = Bind(owner.PlayerTransform); bot = Bind(owner.BotTransform);
            if (Application.isPlaying) { Initialize(); Subscribe(); }
        }
        private static Actor Bind(Transform root) => new() { Root = root,
            Animator = root.GetComponentInChildren<Animator>(true), Motor = root.GetComponent<PlanetMotor>(),
            Rig = root.GetComponentInChildren<HumanoidRagdollRig>(true) };
        private void Start() => Initialize();
        private void Initialize()
        {
            if (_initialized) return;
            if (duel == null || frontend == null || player.Root == null || bot.Root == null ||
                player.Animator == null || bot.Animator == null || player.Motor == null || bot.Motor == null ||
                player.Rig == null || bot.Rig == null || !duel.RespawnPresentationConfigured)
            { Debug.LogError("Gold respawn requires explicit production duel, gravity, frontend and both Humanoid bindings. Run Install Gold Respawn outside Play.", this); enabled = false; return; }
            _createdFrame = Time.frameCount;
            _ownedRoot = new GameObject("Gold Respawn — owned render proxies");
            SceneManager.MoveGameObjectToScene(_ownedRoot, gameObject.scene);
            _initialized = true; AcquireSurfaceWave(); Subscribe();
        }
        private void OnEnable()
        {
            Subscribe();
            if (!_initialized) return;
            AcquireSurfaceWave();
            _ownedRoot.SetActive(true);
            if (duel.TryGetRespawnCue(EarthDuelFighterId.Player, out var first)) OnCue(first);
            if (duel.TryGetRespawnCue(EarthDuelFighterId.Bot, out var second)) OnCue(second);
        }
        private void Subscribe()
        {
            if (_subscribed || duel == null) return;
            duel.RespawnCueChanged += OnCue; duel.RespawnCompleted += OnComplete; duel.RespawnCuesCancelled += StopAll;
            duel.RespawnCueCancelled += OnCancelled;
            _subscribed = true;
        }
        private void Unsubscribe()
        {
            if (!_subscribed || duel == null) return;
            duel.RespawnCueChanged -= OnCue; duel.RespawnCompleted -= OnComplete; duel.RespawnCuesCancelled -= StopAll;
            duel.RespawnCueCancelled -= OnCancelled;
            _subscribed = false;
        }
        private void OnCue(EarthRespawnCue cue)
        {
            if (!_initialized || !isActiveAndEnabled || !duel.HasSimulationAuthority || duel.IsRoundOver) return;
            Slot slot = _slots[cue.Fighter == EarthDuelFighterId.Player ? 0 : 1];
            if (slot.Active && slot.Cue.LifeGeneration == cue.LifeGeneration && slot.Cue.Revision >= cue.Revision) return;
            if (!slot.Proxy.Ready)
            { Debug.LogError("Gold respawn standing pose was not cached while the actor was active; reveal rejected.", this); return; }
            Stop(slot);
            if (slot.LastGeneration != cue.LifeGeneration) { slot.LastGeneration = cue.LifeGeneration; UniqueCueCount++; }
            slot.Cue = cue; slot.Active = true;
        }
        private void OnComplete(EarthRespawnCue cue)
        {
            Slot slot = _slots[cue.Fighter == EarthDuelFighterId.Player ? 0 : 1];
            if (slot.LastGeneration == cue.LifeGeneration) CompletedCueCount++;
            // The duel invokes this after canonical rig/motor restoration in the same fixed tick.
            Stop(slot);
        }
        private void OnCancelled(EarthDuelFighterId fighter) => Stop(_slots[fighter == EarthDuelFighterId.Player ? 0 : 1]);
        private void LateUpdate()
        {
            using var allocationScope = Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Measure(
                Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Path.GoldLate);
            if (!_initialized) return;
            using var marker = TickMarker.Auto();
            if (!duel.HasSimulationAuthority || duel.IsRoundOver) { StopAll(); return; }
            TryCache(player, _slots[0], duel.PlayerPhase); TryCache(bot, _slots[1], duel.BotPhase);
            Array.Clear(_waveCenters, 0, 2); Array.Clear(_waveUps, 0, 2);
            for (int index = 0; index < _slots.Length; index++)
            {
                Slot slot = _slots[index]; if (!slot.Active) continue;
                GoldRespawnFrame frame = GoldRespawnTimeline.Sample(in slot.Cue, duel.RespawnPresentationTime, frontend.Preferences.ReducedMotion);
                if (!frame.Visible) { Stop(slot); continue; }
                Vector3 feet = Vector(slot.Cue.FeetPosition), up = Vector(slot.Cue.Up);
                Quaternion rotation = Rotation(slot.Cue.Rotation);
                _waveCenters[index] = new Vector4(feet.x, feet.y, feet.z, frame.RingRadius * 3.2f);
                _waveUps[index] = new Vector4(up.x, up.y, up.z, frame.RingAlpha * (frontend.Preferences.ReducedMotion ? .7f : 1f));
                if (frame.ShowProxy) slot.Proxy.RenderAtActorPose(Vector(slot.Cue.RootPosition), rotation, frame.Scale, frame.Lift, up, Gold * (frame.Emission * 2.4f));
            }
            PublishSurfaceWave();
        }
        private void TryCache(Actor actor, Slot slot, EarthDuelFighterPhase phase)
        {
            if (slot.Proxy.Ready || Time.frameCount <= _createdFrame + 2 || phase != EarthDuelFighterPhase.Active ||
                actor.Rig.IsRagdollActive || !actor.Animator.isInitialized || !actor.Animator.enabled || !actor.Motor.HasStableSupport) return;
            using var marker = CacheMarker.Auto();
            try { slot.Proxy.Capture(actor.Root, _ownedRoot.transform, actor.Motor.SupportFeetPoint(actor.Motor.LocalUp)); }
            catch (Exception error) { Debug.LogException(error, this); enabled = false; }
        }
        private void Stop(Slot slot)
        {
            slot.Active = false; slot.Proxy.HideAndRestoreSource();
            int index = Array.IndexOf(_slots, slot);
            _waveCenters[index] = _waveUps[index] = Vector4.zero;
            PublishSurfaceWave();

        }
        private void StopAll() { foreach (var slot in _slots) Stop(slot); ClearSurfaceWave(); }
        private void OnDisable() { Unsubscribe(); StopAll(); if (_surfaceWaveOwner == this) _surfaceWaveOwner = null; }
        private void OnDestroy()
        {
            Unsubscribe(); StopAll(); foreach (var slot in _slots) slot.Proxy.Dispose();
            if (_ownedRoot != null) Destroy(_ownedRoot);
        }
        private void AcquireSurfaceWave()
        {
            if (_surfaceWaveOwner != null && _surfaceWaveOwner != this)
                throw new InvalidOperationException("Only the active production Gold presenter may own surface-wave globals.");
            _surfaceWaveOwner = this;
            ClearSurfaceWave();
        }
        private void PublishSurfaceWave()
        {
            if (_surfaceWaveOwner != this) return;
            Shader.SetGlobalVectorArray(WaveCentersId, _waveCenters);
            Shader.SetGlobalVectorArray(WaveUpsId, _waveUps);
        }
        private void ClearSurfaceWave()
        {
            Array.Clear(_waveCenters, 0, 2); Array.Clear(_waveUps, 0, 2);
            PublishSurfaceWave();
        }
        private static Vector3 Vector(float3 value) => new(value.x, value.y, value.z);
        private static Quaternion Rotation(quaternion value) => new(value.value.x, value.value.y, value.value.z, value.value.w);
    }
}
