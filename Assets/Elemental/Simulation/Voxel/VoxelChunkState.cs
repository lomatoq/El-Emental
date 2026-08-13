namespace Elemental.Simulation.Voxel
{
    public sealed class VoxelChunkState
    {
        public VoxelChunkState(ChunkCoord coord)
        {
            Coord = coord;
            IsDirty = true;
        }

        public ChunkCoord Coord { get; }
        public uint Version { get; private set; }
        public ulong ContentHash { get; private set; }
        public bool IsDirty { get; private set; }

        public void MarkDirty()
        {
            Version++;
            IsDirty = true;
        }

        public void MarkBuilt(ulong contentHash)
        {
            ContentHash = contentHash;
            IsDirty = false;
        }

        public bool TryMarkBuilt(uint expectedVersion, ulong contentHash)
        {
            if (expectedVersion != Version)
            {
                return false;
            }

            MarkBuilt(contentHash);
            return true;
        }
    }
}
