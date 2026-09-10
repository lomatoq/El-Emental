using System;
using Elemental.Runtime.World;
using UnityEngine;
using Unity.Profiling;

namespace Elemental.Runtime.Characters
{
    public sealed partial class EarthMvpDuelController
    {
        private static readonly ProfilerMarker ArenaRestoreMarker = new("Elemental.Duel.ArenaRestore");
        private EarthArenaRoundSnapshot _arenaSnapshot;
        private bool _arenaRestoreRequested, _arenaRestoreApplied, _resumeAfterArenaRestore, _arenaMatchDirty, _respawnAfterArenaRestore;
        public bool ArenaResetInProgress => _arenaRestoreRequested;
        public bool ArenaBaselineCaptured => _arenaSnapshot != null;
        public int ArenaResetCount { get; private set; }
        public string ArenaResetError { get; private set; }
        // Applied is the online snapshot publication boundary; Finished additionally
        // guarantees rebuilt geometry and external readiness acknowledgements.
        public event Action ArenaRestoreCompleted;
        public event Action ArenaRestoreFinished;
        public Func<bool> ArenaRestoreReady { private get; set; }
        public void CaptureArenaBaselineIfReady()
        {
            if (_arenaSnapshot != null || !HasSimulationAuthority) return;
            foreach (Elemental.Runtime.Physics.EarthPlanetRockScatter scatter in EarthArenaRoundSnapshot.SceneComponents<Elemental.Runtime.Physics.EarthPlanetRockScatter>(gameObject.scene))
                if (scatter.isActiveAndEnabled && !scatter.IsComplete) return;
            foreach (VoxelPlanetBehaviour planet in EarthArenaRoundSnapshot.SceneComponents<VoxelPlanetBehaviour>(gameObject.scene))
                if (planet.gameObject.activeInHierarchy && planet.GeometryReady)
                { _arenaSnapshot = new EarthArenaRoundSnapshot(planet); return; }
        }
        // Online teardown hands restoration to the persistent scene's offline owner.
        public void ReleaseArenaRestorationOwnership()
        {
            _arenaRestoreRequested = _arenaRestoreApplied = _resumeAfterArenaRestore = false;
            _respawnAfterArenaRestore = _arenaMatchDirty = false; ArenaResetError = null;
        }
        public void MarkArenaMatchStarted()
        { CaptureArenaBaselineIfReady(); _arenaMatchDirty = true; }
        public void RestoreArenaForMatchBoundary()
        {
            CancelRespawnPresentations();
            if (!HasSimulationAuthority || _arenaRestoreRequested || !_arenaMatchDirty) return;
            CaptureArenaBaselineIfReady();
            if (_arenaSnapshot == null) return;
            ArenaResetError = null; _arenaRestoreRequested = true; _arenaMatchDirty = false;
            _arenaRestoreApplied = _resumeAfterArenaRestore = false;
            SetRoundReady(false);
        }
        // Terrain/collider rebuild queues also run in Update. A local frontend may
        // freeze physics/time while the restore transaction remains in flight.
        private void Update()
        {
            if (HasSimulationAuthority && _arenaRestoreRequested) StepArenaMatchRestore();
        }
        private bool StepArenaMatchRestore()
        {
            if (!_arenaRestoreRequested) return false;
            using var marker = ArenaRestoreMarker.Auto();
            if (ArenaResetError != null) return true;
            if (!_arenaRestoreApplied)
            {
                try { _arenaSnapshot.Restore(); ArenaRestoreCompleted?.Invoke(); _arenaRestoreApplied = true; }
                catch (Exception error) { ArenaResetError = error.Message; Debug.LogException(error, this); return true; }
            }
            if (!_arenaSnapshot.Ready || ArenaRestoreReady != null && !ArenaRestoreReady()) return true;
            _arenaRestoreRequested = false; ArenaResetCount++;
            bool resume = _resumeAfterArenaRestore; _resumeAfterArenaRestore = false;
            if (_respawnAfterArenaRestore)
            { _respawnAfterArenaRestore = false; RespawnPlayer(); RespawnBot(); }
            SetRoundReady(resume);
            if (resume) _arenaMatchDirty = true;
            ArenaRestoreFinished?.Invoke();
            return true;
        }
    }
}
