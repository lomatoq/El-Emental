namespace Elemental.Simulation.Voxel
{
    public readonly struct SdfSample
    {
        public SdfSample(float density, VoxelMaterialId material)
        {
            Density = density;
            Material = material;
        }

        public float Density { get; }
        public VoxelMaterialId Material { get; }
        public bool IsSolid => Density <= 0f;
    }
}
