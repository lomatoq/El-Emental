using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    /// <summary>Caller-owned mesh/plane section. No allocations or scene queries.</summary>
    public static class EarthWaveSurfaceContactSolver
    {
        public static int Slice(float3[] vertices, int[] triangles, float4 plane,
            float3[] points, out float lowest, out float highest)
        {
            lowest = float.PositiveInfinity; highest = float.NegativeInfinity;
            for (int i = 0; i < vertices.Length; i++)
            {
                float d = math.dot(plane.xyz, vertices[i]) + plane.w;
                lowest = math.min(lowest, d); highest = math.max(highest, d);
            }
            if (lowest > 0f || highest < 0f) return 0;
            int count = 0;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            for (int edge = 0; edge < 3; edge++)
            {
                float3 a = vertices[triangles[i + edge]], b = vertices[triangles[i + (edge + 1) % 3]];
                float da = math.dot(plane.xyz, a) + plane.w, db = math.dot(plane.xyz, b) + plane.w;
                if ((da < 0f) == (db < 0f) || math.abs(da - db) < 1e-7f) continue;
                float3 p = math.lerp(a, b, da / (da - db));
                bool duplicate = false;
                for (int j = 0; j < count; j++) if (math.distancesq(p, points[j]) < 1e-7f) { duplicate = true; break; }
                if (!duplicate && count < points.Length) points[count++] = p;
            }
            return count;
        }
    }
}
