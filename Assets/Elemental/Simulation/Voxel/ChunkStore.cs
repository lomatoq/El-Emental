using System;
using System.Collections.Generic;

namespace Elemental.Simulation.Voxel
{
    public sealed class ChunkStore
    {
        private readonly Dictionary<ChunkCoord, VoxelChunkState> _chunks;

        public ChunkStore(int initialCapacity = 64)
        {
            if (initialCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }

            _chunks = new Dictionary<ChunkCoord, VoxelChunkState>(initialCapacity);
        }

        public int Count => _chunks.Count;

        public VoxelChunkState GetOrCreate(ChunkCoord coord)
        {
            if (_chunks.TryGetValue(coord, out VoxelChunkState state))
            {
                return state;
            }

            state = new VoxelChunkState(coord);
            _chunks.Add(coord, state);
            return state;
        }

        public bool TryGet(ChunkCoord coord, out VoxelChunkState state)
        {
            return _chunks.TryGetValue(coord, out state);
        }

        public void MarkDirty(VoxelBounds bounds, float chunkWorldSize)
        {
            if (chunkWorldSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkWorldSize));
            }

            ChunkCoord min = ChunkCoord.FromPlanetLocal(bounds.Min, chunkWorldSize);
            ChunkCoord max = ChunkCoord.FromPlanetLocal(bounds.Max, chunkWorldSize);

            for (int z = min.Z; z <= max.Z; z++)
            {
                for (int y = min.Y; y <= max.Y; y++)
                {
                    for (int x = min.X; x <= max.X; x++)
                    {
                        GetOrCreate(new ChunkCoord(x, y, z)).MarkDirty();
                    }
                }
            }
        }

        public void CollectDirty(List<ChunkCoord> output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            foreach (KeyValuePair<ChunkCoord, VoxelChunkState> pair in _chunks)
            {
                if (pair.Value.IsDirty)
                {
                    output.Add(pair.Key);
                }
            }
        }
    }
}
