#include "Assets/Elemental/Presentation/Fire/Shaders/FireFlame.hlsl"
void EF_FireGraphFlame_float(float2 UV, float FireTime, float FirePhase, float FireAge, float FireHeat,
    out float3 Color, out float Alpha)
{
    EF_Flame_float(UV, FireTime, FirePhase, FireAge, FireHeat, 0.9, 0.0, 1.0,
        float3(0.65,0.018,0.003), float3(1,0.19,0.008), float3(1,0.72,0.19), 3.0, Color, Alpha);
}
