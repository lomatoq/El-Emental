using Unity.Mathematics;

namespace Elemental.Simulation.Rendering
{
    /// <summary>CPU reference for the object's scale-correct shader projection frame.</summary>
    public static class TriplanarProjectionFrameSolver
    {
        public static float3 Project(
            float3 worldPosition,
            float3 origin,
            float3 axisX,
            float3 axisY,
            float3 axisZ)
        {
            float3 delta = worldPosition - origin;
            float3 x = math.normalizesafe(axisX, new float3(1f, 0f, 0f));
            float3 y = math.normalizesafe(axisY, new float3(0f, 1f, 0f));
            float3 z = math.normalizesafe(axisZ, new float3(0f, 0f, 1f));
            return new float3(math.dot(delta, x), math.dot(delta, y), math.dot(delta, z));
        }
    }
}
