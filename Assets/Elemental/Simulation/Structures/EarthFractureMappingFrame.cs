using Unity.Mathematics;

namespace Elemental.Simulation.Structures
{
    public readonly struct EarthFractureMappingFrame
    {
        public EarthFractureMappingFrame(bool isValid, float4x4 localToStructure)
        {
            IsValid = isValid;
            LocalToStructure = localToStructure;
        }

        public bool IsValid { get; }
        public float4x4 LocalToStructure { get; }

        public float3 TransformPoint(float3 localPoint) =>
            math.transform(LocalToStructure, localPoint);
    }

    /// <summary>
    /// Captures one immutable render-local to intact-structure mapping frame.
    /// Fracture pieces keep this rest frame after release, so object-local texture
    /// projection follows the body without restarting at every piece centroid.
    /// </summary>
    public static class EarthFractureMappingFrameSolver
    {
        private const float MinimumDeterminant = 0.000001f;

        public static EarthFractureMappingFrame Resolve(float4x4 localToStructure)
        {
            bool finite = math.all(math.isfinite(localToStructure.c0)) &&
                          math.all(math.isfinite(localToStructure.c1)) &&
                          math.all(math.isfinite(localToStructure.c2)) &&
                          math.all(math.isfinite(localToStructure.c3));
            float determinant = finite ? math.determinant(localToStructure) : 0f;
            if (!finite || !math.isfinite(determinant) ||
                math.abs(determinant) < MinimumDeterminant)
            {
                return new EarthFractureMappingFrame(false, float4x4.identity);
            }

            return new EarthFractureMappingFrame(true, localToStructure);
        }
    }
}
