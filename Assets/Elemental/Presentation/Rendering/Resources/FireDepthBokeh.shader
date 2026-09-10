Shader "Hidden/Elemental/FireDepthBokeh"
{
 SubShader {Tags{"RenderPipeline"="UniversalPipeline"} Pass {ZTest Always ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma target 3.5
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
 TEXTURE2D_X(_FireBefore);TEXTURE2D_X(_FireMoments);
 float4 _ElementalFireDofParams;float4 _FireDofTexelRadius;
 float Radius(float depth){return min(_FireDofTexelRadius.z,max(saturate((_ElementalFireDofParams.x-depth)/max(.01,_ElementalFireDofParams.z)),saturate((depth-_ElementalFireDofParams.y)/max(.01,_ElementalFireDofParams.w)))*_FireDofTexelRadius.z);}
 float4 Frag(Varyings i):SV_Target
 {
  float2 uv=i.texcoord;float4 after=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv),before=SAMPLE_TEXTURE2D_X(_FireBefore,sampler_LinearClamp,uv);
  float4 center=SAMPLE_TEXTURE2D_X(_FireMoments,sampler_PointClamp,uv);
  // Any real sharp fire at this pixel protects the hand/body even behind far flame.
  if(center.z>.025)return after;
  float raw=SampleSceneDepth(uv);float surface=LinearEyeDepth(raw,_ZBufferParams);
  #if UNITY_REVERSED_Z
  if(raw<=.00001)surface=1e8;
  #else
  if(raw>=.99999)surface=1e8;
  #endif
  float centerRadius=center.y>.0001?Radius(center.x/center.y):0;
  float3 result=before.rgb+(centerRadius<1?after.rgb-before.rgb:0);
  float3 spread=0;float maximum=max(1,_FireDofTexelRadius.z);
  // Uniform disk samples approximate an aperture footprint; source CoC, not opaque
  // background depth, determines whether its flame contribution reaches this pixel.
  [unroll]for(int tap=0;tap<16;tap++)
  {
   float r=sqrt((tap+.5)/16.0)*maximum,angle=tap*2.39996323;
   float2 sampleUv=uv+float2(cos(angle),sin(angle))*r*_FireDofTexelRadius.xy;
   if(any(sampleUv<0)||any(sampleUv>1))continue;
   float4 m=SAMPLE_TEXTURE2D_X(_FireMoments,sampler_PointClamp,sampleUv);if(m.y<=.0001||m.z>.025)continue;
   float depth=m.x/m.y,radius=Radius(depth);if(radius<1||r>radius+.5||surface<depth-.15)continue;
   float3 delta=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,sampleUv).rgb-SAMPLE_TEXTURE2D_X(_FireBefore,sampler_LinearClamp,sampleUv).rgb;
   float weight=saturate(radius+.5-r)*min(16,maximum*maximum/max(1,radius*radius))/16;
   spread+=delta*weight;
  }
  return float4(max(0,result+spread),after.a);
 }
 ENDHLSL } }
}
