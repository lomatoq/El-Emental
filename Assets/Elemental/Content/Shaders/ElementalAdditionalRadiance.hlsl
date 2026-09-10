#ifndef ELEMENTAL_ADDITIONAL_RADIANCE_INCLUDED
#define ELEMENTAL_ADDITIONAL_RADIANCE_INCLUDED
// The including material defines ElementalAdditionalResponse(Light, half3).
// URP 17.5: clustered non-main directionals precede the clustered punctual list.
// Forward enumerates the per-object list once. Neither path repeats GetMainLight.
half3 ElementalAdditionalRadiance(float3 positionWS, float2 screenUV, half3 normalWS)
{
    half3 radiance = 0;
#if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX) || USE_CLUSTER_LIGHT_LOOP
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalizedScreenSpaceUV = screenUV;
#if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, positionWS, half4(1,1,1,1));
        #if defined(_LIGHT_LAYERS)
        if (IsMatchingLightLayer(light.layerMask, GetMeshRenderingLayer()))
        #endif
        radiance += ElementalAdditionalResponse(light, normalWS);
    }
#endif
    uint lightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(lightCount)
        Light light = GetAdditionalLight(lightIndex, positionWS, half4(1,1,1,1));
        #if defined(_LIGHT_LAYERS)
        if (IsMatchingLightLayer(light.layerMask, GetMeshRenderingLayer()))
        #endif
        radiance += ElementalAdditionalResponse(light, normalWS);
    LIGHT_LOOP_END
#endif
    return radiance;
}
#endif
