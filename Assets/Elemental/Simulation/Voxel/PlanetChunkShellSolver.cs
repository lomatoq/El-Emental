using Unity.Mathematics;

namespace Elemental.Simulation.Voxel
{
    public static class PlanetChunkShellSolver
    {
        public static bool IntersectsSurfaceShell(
            int3 coord,
            float chunkWorldSize,
            float radius,
            float surfaceMargin)
        {
            float size = math.max(0.01f, chunkWorldSize);
            float3 minimum = new float3(coord.x, coord.y, coord.z) * size;
            float3 maximum = minimum + size;
            float3 nearest = math.clamp(float3.zero, minimum, maximum);
            float3 farthest = math.select(maximum, minimum, math.abs(minimum) > math.abs(maximum));
            float minimumDistance = math.length(nearest);
            float maximumDistance = math.length(farthest);
            float margin = math.max(0f, surfaceMargin);
            return minimumDistance <= radius + margin && maximumDistance >= math.max(0f, radius - margin);
        }
    }
}
