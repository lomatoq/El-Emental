using System;
using System.Collections.Generic;
using Elemental.Runtime.World;
using Elemental.Simulation.Voxel;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Online
{
    /// <summary>Preserves complete ordered batch boundaries and validates every touched chunk.</summary>
    public sealed class OnlineTerrainReplication : MonoBehaviour
    {
        [SerializeField] private VoxelPlanetBehaviour planet;
        private readonly HashSet<ChunkCoord> _touched = new HashSet<ChunkCoord>();
        private NgoGameplayTransport _transport;
        private bool _authority, _configured, _connected, _transactional;
        private uint _nextTransaction, _receivingTransaction;
        private SdfEdit[] _incoming;
        private int _received, _pendingHashes;
        public bool HasReferences => planet != null;
        public bool WorldSynchronized => _configured && _connected && _incoming == null && _pendingHashes == 0 && planet.GeometryReady;
        public event Action<string> Failed;
        public void Configure(bool authority, NgoGameplayTransport transport)
        {
            if (planet == null || planet.State == null || planet.State.EditCount != 0)
                throw new InvalidOperationException("Online arena requires a freshly loaded, unedited planet. Reload the gameplay scene before connecting.");
            _authority = authority; _transport = transport; _configured = true; _connected = false;
            _nextTransaction = _receivingTransaction = 0; _incoming = null; _pendingHashes = 0;
            planet.ConfigureOnlineTerrainAuthority(authority);
            planet.OnlineEditBatchApplied += Committed;
        }
        public void ConnectionEstablished() => _connected = true;
        private void Committed(EditBatch batch, bool transactional)
        {
            if (!_configured || !_authority || !_connected) return;
            try
            {
                if (batch.Count < 1 || batch.Count > 256) throw new InvalidOperationException("Terrain transaction exceeds 256 edits.");
                uint transaction = ++_nextTransaction;
                _transport.Publish(new OnlinePacket { Kind = OnlineMessage.TerrainBegin, Id = transaction, Aux = (uint)batch.Count, Flags = transactional ? 1u : 0u });
                for (int i = 0; i < batch.Count; i++)
                {
                    SdfEdit edit = batch[i];
                    _transport.Publish(new OnlinePacket { Kind = OnlineMessage.TerrainEdit, Id = edit.Sequence, Aux = transaction,
                        Flags = (uint)edit.Kind | ((uint)edit.Material.Value << 8),
                        A = (Vector3)edit.PointA, B = (Vector3)edit.PointB, Value = edit.Radius });
                }
                CollectTouched(batch);
                _transport.Publish(new OnlinePacket { Kind = OnlineMessage.TerrainCommit, Id = transaction, Aux = (uint)_touched.Count });
                foreach (ChunkCoord coord in _touched)
                {
                    uint version = planet.State.Chunks.TryGet(coord, out VoxelChunkState chunk) ? chunk.Version : 0;
                    _transport.Publish(new OnlinePacket { Kind = OnlineMessage.TerrainChunkHash, Id = transaction, Aux = version,
                        A = new Vector3(coord.X, coord.Y, coord.Z), WorldHash = planet.State.ComputeChunkHash(coord) });
                }
            }
            catch (Exception error) { Failed?.Invoke(error.Message); }
        }
        private void CollectTouched(EditBatch batch)
        {
            _touched.Clear();
            for (int i = 0; i < batch.Count; i++)
            {
                VoxelBounds bounds = batch[i].GetBounds(); float3 halo = new float3(planet.State.CellSize);
                ChunkCoord min = ChunkCoord.FromPlanetLocal(bounds.Min - halo, planet.State.ChunkWorldSize);
                ChunkCoord max = ChunkCoord.FromPlanetLocal(bounds.Max + halo, planet.State.ChunkWorldSize);
                if ((long)(max.X - min.X + 1) * (max.Y - min.Y + 1) * (max.Z - min.Z + 1) > 4096)
                    throw new InvalidOperationException("Terrain edit touches too many chunks.");
                for (int z = min.Z; z <= max.Z; z++) for (int y = min.Y; y <= max.Y; y++) for (int x = min.X; x <= max.X; x++)
                    _touched.Add(new ChunkCoord(x, y, z));
            }
            if (_touched.Count > 4096) throw new InvalidOperationException("Terrain transaction touches too many chunks.");
        }
        public bool Apply(in OnlinePacket packet, out string error)
        {
            error = null;
            if (_authority || !_configured || !_connected) { error = "Terrain replica is not connected."; return false; }
            try
            {
                switch (packet.Kind)
                {
                    case OnlineMessage.TerrainBegin:
                        if (_incoming != null || _pendingHashes != 0 || packet.Id <= _receivingTransaction || packet.Aux < 1 || packet.Aux > 256 || packet.Flags > 1)
                            throw new InvalidOperationException("Invalid or overlapping terrain transaction.");
                        _receivingTransaction = packet.Id; _incoming = new SdfEdit[packet.Aux]; _received = 0; _transactional = packet.Flags != 0; return true;
                    case OnlineMessage.TerrainEdit:
                        uint kind = packet.Flags & 255, material = (packet.Flags >> 8) & 255;
                        if (_incoming == null || packet.Aux != _receivingTransaction || _received >= _incoming.Length || kind < 1 || kind > 4 || material == 0 || packet.Value <= 0 || packet.Value > 128)
                            throw new InvalidOperationException("Invalid ordered terrain edit.");
                        _incoming[_received++] = new SdfEdit(packet.Id, (SdfEditKind)kind, (float3)packet.A, (float3)packet.B,
                            packet.Value, new VoxelMaterialId((byte)material)); return true;
                    case OnlineMessage.TerrainCommit:
                        if (_incoming == null || packet.Id != _receivingTransaction || _received != _incoming.Length || packet.Aux > 4096)
                            throw new InvalidOperationException("Incomplete terrain transaction.");
                        var batch = new EditBatch(_incoming); CollectTouched(batch);
                        if (packet.Aux != _touched.Count) throw new InvalidOperationException("Touched chunk list differs.");
                        planet.ApplyOnlineCanonicalBatch(batch, _transactional); _incoming = null; _pendingHashes = (int)packet.Aux; return true;
                    case OnlineMessage.TerrainChunkHash:
                        var coord = new ChunkCoord((int)packet.A.x, (int)packet.A.y, (int)packet.A.z);
                        if (_pendingHashes <= 0 || packet.Id != _receivingTransaction || !_touched.Remove(coord))
                            throw new InvalidOperationException("Unexpected terrain chunk checkpoint.");
                        uint version = planet.State.Chunks.TryGet(coord, out VoxelChunkState chunk) ? chunk.Version : 0;
                        if (version != packet.Aux || planet.State.ComputeChunkHash(coord) != packet.WorldHash)
                            throw new InvalidOperationException("Terrain content/version mismatch at " + coord);
                        --_pendingHashes; return true;
                    default: error = "Unsupported terrain message."; return false;
                }
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }
        public void Stop()
        {
            if (planet != null) { planet.OnlineEditBatchApplied -= Committed; planet.ConfigureOnlineTerrainAuthority(true); }
            _configured = _connected = false; _incoming = null; _pendingHashes = 0; _touched.Clear();
        }
    }
}
