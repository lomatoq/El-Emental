using Unity.Mathematics;

namespace Elemental.Simulation.Voxel
{
    public readonly struct VoxelBounds
    {
        public VoxelBounds(float3 min, float3 max)
        {
            Min = math.min(min, max);
            Max = math.max(min, max);
        }

        public float3 Min { get; }
        public float3 Max { get; }
    }
}
