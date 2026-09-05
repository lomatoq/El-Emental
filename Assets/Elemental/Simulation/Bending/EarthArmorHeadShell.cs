using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    /// <summary>Fits a plate's plane outside the measured head, including its hair/helmet.</summary>
    public static class EarthArmorHeadShell
    {
        public const int FillerCount = 16;

        public static float3 FillerDirection(int index)
        {
            float angle = math.radians(22.5f + (index % 8) * 45f);
            return math.normalize(new float3(math.sin(angle), index < 8 ? 1.7f : .06f, math.cos(angle)));
        }

        public static float3 SurfacePoint(float3[] points, float3 center, float3 direction)
        {
            float3 normal = math.normalizesafe(direction, new float3(0, 1, 0));
            float support = 0f;
            for (int i = 0; i < points.Length; i++)
                support = math.max(support, math.dot(points[i] - center, normal));
            return center + normal * support;
        }
    }
}
