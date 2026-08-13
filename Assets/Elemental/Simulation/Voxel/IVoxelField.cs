using Unity.Mathematics;

namespace Elemental.Simulation.Voxel
{
    public interface IVoxelField
    {
        SdfSample SampleDensityMaterial(float3 planetLocalPosition);
    }
}
