#ifndef ELEMENTAL_UNIFIED_LIGHTING_INCLUDED
#define ELEMENTAL_UNIFIED_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl"
#include "Assets/Elemental/Content/GraphicsVNext/Rendering/ElementalDuelShadow.hlsl"
#include "Assets/Elemental/Content/GraphicsVNext/Rendering/CapsuleContactShadow.hlsl"

struct ElementalUnifiedLightingInput
{
    float3 positionWS;
    float4 positionCS;
    half3 normalWS;
    half3 albedo;
    half3 shadowTint;
    half3 warmLightTint;
    half3 edgeTint;
    half roughness;
    half ambientStrength;
    half shadowFloor;
    half specularStrength;
    half rimStrength;
    half receiveSsao;
};

half ElementalUnifiedDiffuseRamp(half normalLightDot)
{
    half wrapped = saturate((normalLightDot + 0.24h) / 1.24h);
    return smoothstep(0.08h, 0.92h, wrapped);
}

half3 ElementalEvaluateUnifiedLighting(ElementalUnifiedLightingInput input)
{
    input.normalWS = SafeNormalize(input.normalWS);
    Light mainLight = GetMainLight();
    half diffuseRamp = ElementalUnifiedDiffuseRamp(
        dot(input.normalWS, mainLight.direction));
    half duelShadow = ElementalSampleDuelShadow(input.positionWS);
    half capsuleShadow = ElementalSampleCapsuleContactShadow(
        input.positionWS,
        input.normalWS,
        mainLight.direction);

    half directOcclusion = 1.0h;
    half indirectOcclusion = 1.0h;
    #if defined(_SCREEN_SPACE_OCCLUSION)
        AmbientOcclusionFactor screenAo = GetScreenSpaceAmbientOcclusion(
            GetNormalizedScreenSpaceUV(input.positionCS));
        directOcclusion = lerp(
            1.0h,
            screenAo.directAmbientOcclusion,
            saturate(input.receiveSsao));
        indirectOcclusion = lerp(
            1.0h,
            screenAo.indirectAmbientOcclusion,
            saturate(input.receiveSsao));
    #endif

    half combinedShadow = saturate(duelShadow * capsuleShadow);
    half shadowWeight = lerp(
        saturate(input.shadowFloor),
        1.0h,
        combinedShadow);
    half3 formTint = lerp(
        input.shadowTint,
        input.warmLightTint,
        diffuseRamp);
    half3 direct = input.albedo * formTint * mainLight.color *
        mainLight.distanceAttenuation * shadowWeight * directOcclusion;

    half3 ambient = SampleSH(input.normalWS) * input.ambientStrength;
    ambient += input.shadowTint * 0.10h;
    half3 indirect = input.albedo * ambient * indirectOcclusion;

    half3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
    half3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
    half smoothness = saturate(1.0h - input.roughness);
    half broadPower = lerp(7.0h, 28.0h, smoothness);
    half specular = pow(
        saturate(dot(input.normalWS, halfDirection)),
        broadPower) * saturate(input.specularStrength) * shadowWeight;
    half rim = pow(
        saturate(1.0h - dot(input.normalWS, viewDirection)),
        4.0h) * saturate(input.rimStrength);

    half3 color = direct + indirect +
        mainLight.color * specular +
        input.edgeTint * rim;
    UNITY_BRANCH
    if (_ElementalCapsuleShadowBiasDebugParams.z > 0.5)
        return capsuleShadow.xxx;
    return color;
}

#endif
