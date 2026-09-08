#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Assets/Elemental/Presentation/Fire/Shaders/FireCpuFlame.hlsl"
CBUFFER_START(UnityPerMaterial)
float4 _EdgeColor,_BodyColor,_CoreColor;
float4 _FlameAtlas_TexelSize;
float _UseFlipbook;
#if defined(ELEMENTAL_COLUMN_HEAT)
float _CoreEmission,_FireTime,_Opacity,_ShapeFPS,_Distortion,_SoftDistance,_NearStart,_NearRange;
#else
float _CoreEmission,_FireTime,_Opacity,_ShapeFPS,_Distortion,_SoftDistance,_NearStart,_NearRange,_ParcelOpacityScale;
#endif
float _HeatDistortionPixels;
CBUFFER_END
TEXTURE2D_X(_ElementalHeatSource);
float4 _ElementalHeatSource_TexelSize;
float _EarthSeismicVision;
struct HeatAttributes
{
    float3 centreWS:POSITION;
    float2 uv:TEXCOORD0;
    float4 fire:TEXCOORD1;
    float2 extent:TEXCOORD2;
    float3 directionWS:TEXCOORD3;
};
struct HeatVaryings
{
    float4 positionCS:SV_POSITION;
    float2 uv:TEXCOORD0;
    float3 positionWS:TEXCOORD1;
    float3 fire:TEXCOORD2;
    float ember:TEXCOORD3;
};
HeatVaryings HeatVert(HeatAttributes input)
{
    HeatVaryings output;
    float3 viewAxis=UNITY_MATRIX_I_V._m02_m12_m22;
    float3 cameraUp=UNITY_MATRIX_I_V._m01_m11_m21;
    float3 travel=input.directionWS-viewAxis*dot(input.directionWS,viewAxis);
    float3 up=normalize(travel+cameraUp*1.5+float3(0,.0001,0));
    float3 right=normalize(cross(up,viewAxis));
    float width=input.fire.w;
    float3 axisExtent=.5*width*(abs(right)+abs(up)*input.extent.x);
    width*=min(1,.99*input.fire.w*input.extent.x/max(max(axisExtent.x,max(axisExtent.y,axisExtent.z)),1e-6));
    output.positionWS=input.centreWS+right*(input.uv.x-.5)*width+up*(input.uv.y-.5)*width*input.extent.x;
    output.positionCS=TransformWorldToHClip(output.positionWS);
    output.uv=input.uv; output.fire=input.fire.xyz; output.ember=input.extent.y;
    return output;
}
float HeatEyeDepth(float2 uv)
{
    float raw=SampleSceneDepth(uv);
    if(unity_OrthoParams.w>.5)
    {
        #if UNITY_REVERSED_Z
        raw=1-raw;
        #endif
        return lerp(_ProjectionParams.y,_ProjectionParams.z,raw);
    }
    return LinearEyeDepth(raw,_ZBufferParams);
}
float4 HeatFrag(HeatVaryings input):SV_Target
{
    clip(.5-input.ember);
    float2 screenUv=GetNormalizedScreenSpaceUV(input.positionCS);
    float eye=-TransformWorldToView(input.positionWS).z;
    float mask=saturate(1-dot((input.uv-float2(.5,.57))*2.2,(input.uv-float2(.5,.57))*2.2));
    mask*=mask*smoothstep(0,.1,input.fire.y)*(1-smoothstep(.4,.9,input.fire.y));
    mask*=saturate((HeatEyeDepth(screenUv)-eye)/.18);
    mask*=saturate((eye-_NearStart)/max(_NearRange,.001))*(1-saturate(_EarthSeismicVision));
    clip(mask-.002);
    float2 noiseUv=input.uv*3+input.fire.x;
    float2 flow=float2(EF_Noise2(noiseUv+float2(_FireTime*.3,-_FireTime)),
        EF_Noise2(noiseUv*1.23+float2(-_FireTime*.2,-_FireTime*.7)))*2-1;
    float2 shifted=clamp(screenUv+flow*_ElementalHeatSource_TexelSize.xy*_HeatDistortionPixels,
        _ElementalHeatSource_TexelSize.xy,1-_ElementalHeatSource_TexelSize.xy);
    // Do not pull a closer occluder's color across its silhouette.
    if(HeatEyeDepth(shifted)<eye) shifted=screenUv;
    float3 color=SAMPLE_TEXTURE2D_X(_ElementalHeatSource,sampler_LinearClamp,shifted).rgb;
    return float4(color,mask*.24*saturate(_HeatDistortionPixels));
}
