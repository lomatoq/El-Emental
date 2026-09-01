#ifndef ELEMENTAL_CAPSULE_CONTACT_SHADOW_INCLUDED
#define ELEMENTAL_CAPSULE_CONTACT_SHADOW_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#define ELEMENTAL_MAX_CAPSULE_SHADOW_PROXIES 32

float4 _ElementalCapsuleShadowStartRadius[ELEMENTAL_MAX_CAPSULE_SHADOW_PROXIES];
float4 _ElementalCapsuleShadowEndSoftness[ELEMENTAL_MAX_CAPSULE_SHADOW_PROXIES];
// x: enabled, y: strength, z: maximum contact distance, w: proxy count
float4 _ElementalCapsuleShadowParams;
// x: surface bias, y: receiver-normal bias, z: shadow-only debug, w: reserved
float4 _ElementalCapsuleShadowBiasDebugParams;

void ElementalClosestSegmentParameters(
    float3 firstStart,
    float3 firstEnd,
    float3 secondStart,
    float3 secondEnd,
    out float firstParameter,
    out float secondParameter)
{
    const float epsilon = 0.000001;
    float3 firstDirection = firstEnd - firstStart;
    float3 secondDirection = secondEnd - secondStart;
    float3 separation = firstStart - secondStart;
    float firstLengthSquared = dot(firstDirection, firstDirection);
    float secondLengthSquared = dot(secondDirection, secondDirection);
    float secondProjection = dot(secondDirection, separation);

    if (firstLengthSquared <= epsilon && secondLengthSquared <= epsilon)
    {
        firstParameter = 0.0;
        secondParameter = 0.0;
        return;
    }
    if (firstLengthSquared <= epsilon)
    {
        firstParameter = 0.0;
        secondParameter = saturate(secondProjection / secondLengthSquared);
        return;
    }

    float firstProjection = dot(firstDirection, separation);
    if (secondLengthSquared <= epsilon)
    {
        secondParameter = 0.0;
        firstParameter = saturate(-firstProjection / firstLengthSquared);
        return;
    }

    float crossProjection = dot(firstDirection, secondDirection);
    float denominator = firstLengthSquared * secondLengthSquared -
        crossProjection * crossProjection;
    firstParameter = abs(denominator) > epsilon
        ? saturate((crossProjection * secondProjection -
                    firstProjection * secondLengthSquared) / denominator)
        : 0.0;
    secondParameter =
        (crossProjection * firstParameter + secondProjection) /
        secondLengthSquared;
    if (secondParameter < 0.0)
    {
        secondParameter = 0.0;
        firstParameter = saturate(-firstProjection / firstLengthSquared);
    }
    else if (secondParameter > 1.0)
    {
        secondParameter = 1.0;
        firstParameter = saturate(
            (crossProjection - firstProjection) / firstLengthSquared);
    }
}

half ElementalSampleOneCapsuleContactShadow(
    float3 positionWS,
    half3 normalWS,
    half3 directionToLightWS,
    float4 startRadius,
    float4 endSoftness)
{
    float maximumDistance = max(0.05, _ElementalCapsuleShadowParams.z);
    float surfaceBias = clamp(
        _ElementalCapsuleShadowBiasDebugParams.x,
        0.001,
        maximumDistance * 0.25);
    float normalBias = clamp(
        _ElementalCapsuleShadowBiasDebugParams.y,
        0.0,
        maximumDistance * 0.25);
    half3 safeNormal = SafeNormalize(normalWS);
    half3 safeDirectionToLight = SafeNormalize(directionToLightWS);
    float3 rayStart = positionWS +
        safeNormal * normalBias +
        safeDirectionToLight * surfaceBias;
    float3 rayEnd = rayStart + safeDirectionToLight * maximumDistance;
    float rayParameter;
    float capsuleParameter;
    ElementalClosestSegmentParameters(
        rayStart,
        rayEnd,
        startRadius.xyz,
        endSoftness.xyz,
        rayParameter,
        capsuleParameter);
    float3 rayPoint = lerp(rayStart, rayEnd, rayParameter);
    float3 capsulePoint = lerp(
        startRadius.xyz,
        endSoftness.xyz,
        capsuleParameter);
    float radius = max(0.0, startRadius.w);
    float softness = max(0.001, endSoftness.w);
    float capsuleDistance = distance(rayPoint, capsulePoint);
    half coverage = 1.0h - smoothstep(
        max(0.0, radius - softness),
        radius + softness,
        capsuleDistance);
    float rayDistance = rayParameter * maximumDistance;
    half startGate = smoothstep(surfaceBias, surfaceBias * 2.0, rayDistance);
    half distanceFade = 1.0h - saturate(rayParameter);
    half occlusion = saturate(coverage * startGate * distanceFade);
    return 1.0h - occlusion * saturate((half)_ElementalCapsuleShadowParams.y);
}

half ElementalSampleCapsuleContactShadow(
    float3 positionWS,
    half3 normalWS,
    half3 directionToLightWS)
{
    UNITY_BRANCH
    if (_ElementalCapsuleShadowParams.x < 0.5)
        return 1.0h;

    int proxyCount = clamp(
        (int)round(_ElementalCapsuleShadowParams.w),
        0,
        ELEMENTAL_MAX_CAPSULE_SHADOW_PROXIES);
    half attenuation = 1.0h;
    [loop]
    for (int proxyIndex = 0;
         proxyIndex < ELEMENTAL_MAX_CAPSULE_SHADOW_PROXIES;
         proxyIndex++)
    {
        if (proxyIndex >= proxyCount)
            break;
        attenuation = min(
            attenuation,
            ElementalSampleOneCapsuleContactShadow(
                positionWS,
                normalWS,
                directionToLightWS,
                _ElementalCapsuleShadowStartRadius[proxyIndex],
                _ElementalCapsuleShadowEndSoftness[proxyIndex]));
    }
    return attenuation;
}

#endif
