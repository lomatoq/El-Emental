#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
TEXTURE2D(_FlameAtlas);SAMPLER(sampler_FlameAtlas);
TEXTURE2D(_SmokeAtlas);SAMPLER(sampler_SmokeAtlas);
TEXTURE2D(_FireMotion);SAMPLER(sampler_FireMotion);
TEXTURE2D(_SmokeMotion);SAMPLER(sampler_SmokeMotion);
TEXTURE2D_X(_ElementalHeatSource);
float4 _ElementalHeatSource_TexelSize;
CBUFFER_START(UnityPerMaterial)
float _HeatDistortionPixels,_BurnPalette;
CBUFFER_END
struct A{float3 position:POSITION;float2 uv:TEXCOORD0;float4 data:TEXCOORD1;float4 planeA:TEXCOORD2;float4 planeB:TEXCOORD3;float4 center:TEXCOORD4;float4 axis:TEXCOORD5;};
struct V{float4 position:SV_POSITION;float3 world:TEXCOORD0;float2 uv:TEXCOORD1;nointerpolation float4 data:TEXCOORD2;nointerpolation float4 planeA:TEXCOORD3;nointerpolation float4 planeB:TEXCOORD4;};
V Vert(A i){V o;// Construct at draw time for THIS camera, including capture/reflection cameras.
 float3 facing=normalize(GetCameraPositionWS()-i.center.xyz);
 float3 up=i.axis.xyz-facing*dot(i.axis.xyz,facing);
 if(dot(up,up)<.0001)up=UNITY_MATRIX_I_V._m01_m11_m21;
 up=normalize(up-facing*dot(up,facing));
 float3 right=normalize(cross(up,facing));
 float angle=sin(i.data.z)*.65+(frac(i.data.z*.731)-.5)*i.data.x*.8+sin(i.data.z+i.data.x*1.4)*.22;
 float3 spunRight=right*cos(angle)+up*sin(angle);
 up=up*cos(angle)-right*sin(angle);
 float2 corner=i.uv*2-1;
 o.world=i.center.xyz+spunRight*corner.x*i.center.w+up*corner.y*i.axis.w;o.position=TransformWorldToHClip(o.world);o.uv=i.uv;o.data=i.data;o.planeA=i.planeA;o.planeB=i.planeB;return o;}
float2 FrameUV(float2 uv,float frame){return (float2(fmod(frame,8),7-floor(frame/8))+clamp(uv,.015,.985))/8;}
float4 AnimatedFrame(float2 uv,float frame,float smoke)
{
 // Cell-local clamping preserves true alpha: never pull dilated RGB from adjacent cells.
 float first=floor(frame),next=min(first+1,63),f=frac(frame);
 float2 local=uv;
 if(smoke<.5)local=float2(.1+uv.x*.8,.12+uv.y*.80);
 float2 flow=0;float4 a=0,b=0;
 if(smoke>.5)
 {
  flow=(1-2*SAMPLE_TEXTURE2D(_SmokeMotion,sampler_SmokeMotion,FrameUV(local,first)).rg)*.008;
  a=SAMPLE_TEXTURE2D(_SmokeAtlas,sampler_SmokeAtlas,FrameUV(local+flow*f,first));
  b=SAMPLE_TEXTURE2D(_SmokeAtlas,sampler_SmokeAtlas,FrameUV(local+flow*(f-1),next));
 }
 else
 {
  flow=(1-2*SAMPLE_TEXTURE2D(_FireMotion,sampler_FireMotion,FrameUV(local,first)).rg)*.008;
  a=SAMPLE_TEXTURE2D(_FlameAtlas,sampler_FlameAtlas,FrameUV(local+flow*f,first));
  b=SAMPLE_TEXTURE2D(_FlameAtlas,sampler_FlameAtlas,FrameUV(local+flow*(f-1),next));
 }
 return lerp(a,b,f);
}
float4 ShadeFireAccent(V i)
{
 if(i.data.w<=0)discard;
 if(dot(i.planeA.xyz,i.planeA.xyz)>.5&&dot(float4(i.world,1),i.planeA)<0)discard;
 if(dot(i.planeB.xyz,i.planeB.xyz)>.5&&dot(float4(i.world,1),i.planeB)<0)discard;
 float frame=saturate(i.data.x)*62.999;
 float4 atlas=AnimatedFrame(i.uv,frame,i.data.y);
 float2 screenUV=GetNormalizedScreenSpaceUV(i.position);
 float raw=SampleSceneDepth(screenUV);float fade=1;
 #if UNITY_REVERSED_Z
 bool clearDepth=raw<=.00001;
 #else
 bool clearDepth=raw>=.99999;
 #endif
 if(!clearDepth){float surface=LinearEyeDepth(raw,_ZBufferParams);float card=-TransformWorldToView(i.world).z;fade=saturate((surface-card)/.12);}
 float alpha=(1-exp(-atlas.a*(i.data.y>.5?1.4:2.8)))*i.data.w*fade;
 #if defined(FIRE_ACCENT_HEAT)
 if(i.data.y>.5)discard;
 float2 wave=float2(sin(i.uv.y*15+i.data.z+i.data.x*17),cos(i.uv.x*13-i.data.z+i.data.x*11));
 float2 shifted=clamp(screenUV+wave*_ElementalHeatSource_TexelSize.xy*_HeatDistortionPixels,_ElementalHeatSource_TexelSize.xy,1-_ElementalHeatSource_TexelSize.xy);
 // Reject a shifted foreground sample; distortion must not pull opaque geometry
 // across the card silhouette. Clear sky remains a valid heat source.
 float shiftedRaw=SampleSceneDepth(shifted);
 #if UNITY_REVERSED_Z
 bool shiftedClear=shiftedRaw<=.00001;
 #else
 bool shiftedClear=shiftedRaw>=.99999;
 #endif
 float cardDepth=-TransformWorldToView(i.world).z;
 if(!shiftedClear&&LinearEyeDepth(shiftedRaw,_ZBufferParams)<cardDepth+.02)shifted=screenUV;
 return float4(SAMPLE_TEXTURE2D_X(_ElementalHeatSource,sampler_LinearClamp,shifted).rgb,alpha*.08);
 #else
 float3 color=atlas.rgb;
 if(i.data.y<.5)
 {
  float luminance=dot(color,float3(.2126,.7152,.0722));
  color=lerp(color,float3(1,.42,.045)*luminance,.15);
  // Attached surface burning needs a thermal hierarchy, not the atlas's broad
  // nearly-yellow plateaus becoming olive under night grading. Preserve red
  // radiance, detailed alpha and true white/gold hotspots; do not tint smoke.
  float3 thermal=lerp(float3(1,.11,.004),float3(1,.40,.025),smoothstep(.16,.64,luminance));
  thermal=lerp(thermal,float3(1,.78,.27),smoothstep(.72,.98,luminance));
  color=lerp(color,thermal*max(.001,color.r),saturate(_BurnPalette))*2.25;
 }
 return float4(color,alpha);
 #endif
}

#if defined(FIRE_ACCENT_DOF)
#include "FireDofOutput.hlsl"
FireDofOutput Frag(V i){float4 color=ShadeFireAccent(i);float2 moments=0;FireDepthAccumulate(moments,i.world,1);return FireDofPack(color,moments);}
#else
float4 Frag(V i):SV_Target{return ShadeFireAccent(i);}
#endif
