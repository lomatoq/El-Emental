#ifndef ELEMENTAL_DUEL_SHADOW_INCLUDED
#define ELEMENTAL_DUEL_SHADOW_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D_SHADOW(_ElementalDuelShadowMap);
SAMPLER_CMP(sampler_ElementalDuelShadowMap);

float4x4 _ElementalDuelWorldToShadow;
// x: enabled, y: strength, z: inverse resolution, w: PCF radius (1/2/3)
float4 _ElementalDuelShadowParams;

half ElementalSampleDuelShadow(float3 positionWS)
{
    UNITY_BRANCH
    if (_ElementalDuelShadowParams.x < 0.5)
        return 1.0h;

    float4 shadowPosition = mul(_ElementalDuelWorldToShadow, float4(positionWS, 1.0));
    float3 shadowCoord = shadowPosition.xyz / max(abs(shadowPosition.w), 1e-6);
    if (any(shadowCoord < 0.0) || any(shadowCoord > 1.0))
        return 1.0h;

    int radius = clamp((int)round(_ElementalDuelShadowParams.w), 1, 3);
    half attenuation = 0.0h;
    half sampleCount = 0.0h;
    [loop]
    for (int y = -3; y <= 3; y++)
    {
        [loop]
        for (int x = -3; x <= 3; x++)
        {
            if (abs(x) > radius || abs(y) > radius)
                continue;
            float2 offset = float2(x, y) * _ElementalDuelShadowParams.z;
            attenuation += SAMPLE_TEXTURE2D_SHADOW(
                _ElementalDuelShadowMap,
                sampler_ElementalDuelShadowMap,
                float3(shadowCoord.xy + offset, shadowCoord.z));
            sampleCount += 1.0h;
        }
    }

    attenuation /= max(sampleCount, 1.0h);
    return lerp(1.0h, attenuation, (half)_ElementalDuelShadowParams.y);
}

#endif
