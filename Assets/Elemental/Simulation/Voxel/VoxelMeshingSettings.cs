using System;

namespace Elemental.Simulation.Voxel
{
    public readonly struct VoxelMeshingSettings
    {
        public VoxelMeshingSettings(int resolution, float cellSize)
        {
            if (resolution <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(resolution));
            }

            if (!float.IsFinite(cellSize) || cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            }

            Resolution = resolution;
            CellSize = cellSize;
        }

        public int Resolution { get; }
        public float CellSize { get; }
        public float ChunkWorldSize => Resolution * CellSize;
    }
}
