namespace Elemental.Simulation.Voxel
{
    public interface IChunkMesher
    {
        void Build(
            IVoxelField field,
            ChunkCoord coord,
            VoxelMeshingSettings settings,
            ChunkMeshBuffers output);
    }
}
