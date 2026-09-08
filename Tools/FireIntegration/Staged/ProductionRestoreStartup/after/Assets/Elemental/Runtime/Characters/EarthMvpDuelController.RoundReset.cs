using System;
using Elemental.Runtime.World;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    public sealed partial class EarthMvpDuelController
    {
        private EarthArenaRoundSnapshot _arenaSnapshot;
        private bool _arenaRestoreRequested, _arenaRestoreApplied, _resumeAfterArenaRestore, _arenaMatchDirty, _respawnAfterArenaRestore;
        public bool ArenaResetInProgress => _arenaRestoreRequested;
        public bool ArenaBaselineCaptured => _arenaSnapshot != null;
        public int ArenaResetCount { get; private set; }
        public string ArenaResetError { get; private set; }
        public event Action ArenaRestoreCompleted;
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
            if (!HasSimulationAuthority || _arenaRestoreRequested || !_arenaMatchDirty) return;
            CaptureArenaBaselineIfReady();
            if (_arenaSnapshot == null) return;
            ArenaResetError = null; _arenaRestoreRequested = true; _arenaMatchDirty = false;
            _arenaRestoreApplied = _resumeAfterArenaRestore = false;
            SetRoundReady(false);
        }
        private bool StepArenaMatchRestore()
        {
            if (!_arenaRestoreRequested) return false;
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
            return true;
        }
    }
}
