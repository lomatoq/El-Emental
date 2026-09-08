using System;
using System.IO;
using Elemental.Simulation.Voxel;
using UnityEngine;

namespace Elemental.Runtime.World
{
    public sealed partial class VoxelPlanetBehaviour
    {
        public event Action<ulong> ArenaSnapshotRestored;
        public byte[] CaptureArenaSnapshot()
        {
            if (!GeometryReady) throw new InvalidOperationException("Capture an arena only after its canonical colliders are ready.");
            using var stream = new MemoryStream(); VoxelSaveCodec.Write(stream, _state); return stream.ToArray();
        }
        public static ulong ArenaSnapshotHash(byte[] bytes)
        { ulong hash = 14695981039346656037UL; foreach (byte value in bytes) { hash ^= value; hash *= 1099511628211UL; } return hash; }
        public void RestoreArenaSnapshot(byte[] snapshot, bool canonicalReplica = false)
        {
            if (!HasOnlineSimulationAuthority && !canonicalReplica)
                throw new InvalidOperationException("Only the host restores canonical terrain.");
            if (HasOnlineSimulationAuthority && canonicalReplica)
                throw new InvalidOperationException("Authority cannot consume a replica reset.");
            using var stream = new MemoryStream(snapshot, false);
            VoxelPlanetState restored = VoxelSaveCodec.Read(stream);
            if (restored.Radius != _state.Radius || restored.Seed != _state.Seed ||
                restored.ChunkResolution != _state.ChunkResolution || restored.CellSize != _state.CellSize || restored.NoiseAmplitude != _state.NoiseAmplitude)
                throw new InvalidOperationException("Arena reset cannot replace the authored planet configuration.");
            _pendingTransactions.Clear(); _renderQueue.Clear(); _colliderQueue.Clear();
            _renderQueued.Clear(); _colliderQueued.Clear();
            foreach (RuntimeChunk chunk in _runtimeChunks.Values)
            {
                chunk.GameObject.SetActive(false);
                if (chunk.ActiveMesh != null && !chunk.ActiveMeshShared) Destroy(chunk.ActiveMesh);
                if (chunk.StagingMesh != null && !chunk.StagingMeshShared) Destroy(chunk.StagingMesh);
                Destroy(chunk.GameObject);
            }
            _runtimeChunks.Clear(); _state = restored;
            // Keep edit/receipt IDs monotonic: old pending handles never identify a new edit.
            if (restored.EditCount != 0 || !TryHydrateBaseCache()) { QueueInitialChunks(); QueueDirtyChunks(); }
            if (HasOnlineSimulationAuthority) ArenaSnapshotRestored?.Invoke(ArenaSnapshotHash(snapshot));
        }
    }
}
