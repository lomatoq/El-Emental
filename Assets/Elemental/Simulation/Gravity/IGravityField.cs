using Unity.Mathematics;

namespace Elemental.Simulation.Gravity
{
    public interface IGravityField
    {
        GravityFieldId Id { get; }
        GravitySample Sample(float3 worldPosition, uint tick);
    }
}
