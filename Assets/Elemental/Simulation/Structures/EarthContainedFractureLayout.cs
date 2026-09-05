using Unity.Mathematics;

namespace Elemental.Simulation.Structures
{
    /// <summary>Disjoint child cells along a chord whose endpoints belong to the parent convex.</summary>
    public readonly struct EarthContainedFractureCell
    {
        public readonly float3 Center;
        public readonly float3 Axis;
        public readonly float HalfWidth;
        public EarthContainedFractureCell(float3 center, float3 axis, float halfWidth)
        { Center = center; Axis = axis; HalfWidth = halfWidth; }
        public bool Contains(float3 point) => math.abs(math.dot(point - Center, Axis)) <= HalfWidth;
    }

    public static class EarthContainedFractureLayout
    {
        public static bool TryGetCell(float3 first, float3 last, int index, int count,
            out EarthContainedFractureCell cell)
        {
            cell = default;
            if (count < 2 || count > 4 || index < 0 || index >= count ||
                !math.all(math.isfinite(first)) || !math.all(math.isfinite(last))) return false;
            float length = math.distance(first, last);
            if (length < .0001f) return false;
            cell = new EarthContainedFractureCell(math.lerp(first, last, (index + .5f) / count),
                (last - first) / length, length / (2f * count) * .98f);
            return true;
        }
    }
}
