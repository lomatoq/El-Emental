using System;
using Elemental.Runtime.Physics;
using UnityEngine;

namespace Elemental.Runtime.World
{
    /// <summary>Explicit scene loading boundary; neither physics nor commands advance before exact ground is ready.</summary>
    [DefaultExecutionOrder(-32000), DisallowMultipleComponent]
    public sealed class EarthSceneReadinessGate : MonoBehaviour
    {
        [SerializeField] private VoxelPlanetBehaviour planet;
        [SerializeField] private EarthRockDebrisPool debris;
        [SerializeField] private Behaviour[] pausedControls = Array.Empty<Behaviour>();
        [SerializeField, Min(5f)] private float timeoutSeconds = 120f;
        private bool[] _previousEnabled;
        private float _previousTimeScale;
        private bool _ownsPause;
        private double _started;
        public bool IsReady { get; private set; }
        public bool Failed { get; private set; }
        public string Status { get; private set; } = "Loading";
        public double ReadyMilliseconds { get; private set; }
        public void Configure(VoxelPlanetBehaviour ground, EarthRockDebrisPool pool, Behaviour[] controls)
        { planet = ground; debris = pool; pausedControls = controls ?? Array.Empty<Behaviour>(); }

        private void Awake()
        {
            _started = Time.realtimeSinceStartupAsDouble;
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0;
            _ownsPause = true;
            _previousEnabled = new bool[pausedControls.Length];
            for (int i = 0; i < pausedControls.Length; i++)
            {
                if (pausedControls[i] == null) continue;
                _previousEnabled[i] = pausedControls[i].enabled;
                pausedControls[i].enabled = false;
            }
        }

        private void Update()
        {
            if (IsReady || Failed) return;
            if (planet == null || debris == null)
            { Fail("Scene readiness gate needs explicit planet and debris-pool references."); return; }
            Status = !planet.GeometryReady ? "Preparing ground" : !debris.PhysicsPrepared ? "Preparing stones" : "Ready";
            if (planet.GeometryReady && debris.PhysicsPrepared)
            {
                UnityEngine.Physics.SyncTransforms();
                ReadyMilliseconds = (Time.realtimeSinceStartupAsDouble - _started) * 1000;
                IsReady = true;
                RestorePause();
                Debug.Log($"[StartupCache] Scene ready in {ReadyMilliseconds:F2}ms; ground={planet.BaseCacheStatus}, cacheLoad={debris.BakedCacheLoadMilliseconds:F2}ms, bakedPlans={debris.BakedFracturePlanCount}, planMisses={debris.BakedFracturePlanMissCount}, cookedMeshes={debris.CookedBakedMeshCount}/{debris.ScheduledBakedMeshCount}, backgroundCooking={debris.BackgroundCookingWallMilliseconds:F2}ms, peakCookingPoll={debris.PeakStartupCookingMilliseconds:F2}ms.", this);
            }
            else if (Time.realtimeSinceStartupAsDouble - _started > timeoutSeconds)
                Fail($"Scene preparation timed out: {Status}; terrain={planet.PendingRenderCount}/{planet.PendingColliderCount}, physics={debris.PendingBakedCookingCount}. Rebake startup caches or inspect preparation errors.");
        }

        private void Fail(string reason) { Failed = true; Status = "Loading failed"; Debug.LogError(reason, this); }
        private void OnGUI()
        {
            if (IsReady) return;
            int depth = GUI.depth; Color color = GUI.color;
            GUI.depth = -32768; GUI.color = new Color(.055f, .065f, .085f, 1);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width * .5f - 80, Screen.height * .5f - 12, 250, 40), Failed ? "Loading failed" : "Loading…");
            GUI.color = color; GUI.depth = depth;
        }
        private void RestorePause()
        {
            if (!_ownsPause) return;
            _ownsPause = false; Time.timeScale = _previousTimeScale;
            for (int i = 0; i < pausedControls.Length; i++)
                if (pausedControls[i] != null) pausedControls[i].enabled = _previousEnabled[i];
        }
        private void OnDestroy() => RestorePause();
    }
}
