Shader "Elemental/Fire/SupportedRingRibbon"
{
 Properties { [HideInInspector]_RingCenter("Center",Vector)=(0,0,0,0) [HideInInspector]_RingUp("Up",Vector)=(0,1,0,0) [HideInInspector]_FlowTime("Time",Float)=0 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-1" "RenderType"="Transparent"}
  Pass
  {
   Tags {"LightMode"="ElementalValleyCloud"}
   Blend 0 One One Cull Front ZWrite Off ZTest Always
   Blend 1 One One
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   #include "../Shaders/FireDofOutput.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _RingCenter,_RingUp;float _FlowTime;
   CBUFFER_END
   float _EarthSeismicVision;
   struct A {float3 position:POSITION;float4 center:TEXCOORD0;float4 axis:TEXCOORD1;float4 up:TEXCOORD2;};
   struct V {float4 position:SV_POSITION;float3 world:TEXCOORD0;nointerpolation float4 center:TEXCOORD1;nointerpolation float4 axis:TEXCOORD2;nointerpolation float4 up:TEXCOORD3;};
   V Vert(A i){V o;o.world=i.position;o.position=TransformWorldToHClip(i.position);o.center=i.center;o.axis=i.axis;o.up=i.up;return o;}
   float Hash(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
   float Noise(float3 p)
   {
    float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);
    return lerp(lerp(lerp(Hash(i),Hash(i+float3(1,0,0)),f.x),lerp(Hash(i+float3(0,1,0)),Hash(i+float3(1,1,0)),f.x),f.y),lerp(lerp(Hash(i+float3(0,0,1)),Hash(i+float3(1,0,1)),f.x),lerp(Hash(i+float3(0,1,1)),Hash(i+1),f.x),f.y),f.z);
   }
   float4 ShadeFire(V v,out float2 fireDepth)
   {
    fireDepth=0;
    if(v.center.w<=0)discard;
    float3 ro=GetCameraPositionWS(),rd=normalize(v.world-ro);
    if(unity_OrthoParams.w>.5){rd=-UNITY_MATRIX_I_V._m02_m12_m22;ro=v.world-rd*dot(v.world-ro,rd);}
    float3 axis=normalize(v.axis.xyz),up=normalize(v.up.xyz),side=normalize(cross(up,axis));
    float3 offset=ro-v.center.xyz,local=float3(dot(offset,side),dot(offset,up),dot(offset,axis)),ray=float3(dot(rd,side),dot(rd,up),dot(rd,axis));
    float3 bound=float3(v.center.w,v.center.w,v.axis.w+v.center.w),inverse=(step(0,ray)*2-1)/max(abs(ray),.000001);
    float3 low=(-bound-local)*inverse,high=(bound-local)*inverse,lo=min(low,high),hi=max(low,high);
    float enter=max(0,max(lo.x,max(lo.y,lo.z))),exit=min(hi.x,min(hi.y,hi.z));
    float2 uv=GetNormalizedScreenSpaceUV(v.position);float raw=SampleSceneDepth(uv);
    #if UNITY_REVERSED_Z
    bool opaque=raw>0;
    #else
    bool opaque=raw<1;raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
    #endif
    if(opaque){float3 p=ComputeWorldSpacePosition(uv,raw,UNITY_MATRIX_I_VP);if(all(isfinite(p)))exit=min(exit,dot(p-ro,rd));}
    if(exit<=enter)discard;
    float3 ringUp=normalize(_RingUp.xyz),ringSide=normalize(cross(ringUp,abs(ringUp.y)<.9?float3(0,1,0):float3(1,0,0))),ringAcross=cross(ringUp,ringSide);
    float twist=_FlowTime*.7,cs=cos(twist),sn=sin(twist),stepLength=(exit-enter)/24,transmission=1;float3 light=0;
    [loop]for(int march=0;march<24;march++)
    {
     float t=enter+(march+.5)*stepLength;float3 q=local+ray*t;
     float crossRadius=length(float3(q.xy,max(0,abs(q.z)-v.axis.w)))/v.center.w;
     if(crossRadius>=1)continue;
     float3 p=ro+rd*t-_RingCenter.xyz;
     float3 flow=float3(dot(p,ringSide),dot(p,ringUp)-_FlowTime*1.7,dot(p,ringAcross));
     flow.xz=float2(flow.x*cs-flow.z*sn,flow.x*sn+flow.z*cs);
     float broad=Noise(flow*2.4),fine=Noise(flow*6.5+float3(7.1,3.4,9.8));
     float field=broad*.75+fine*.25;
     float edge=.76+field*.24;
     // Validated endcaps remain inside their swept capsule, but do not
     // render as solid round red beads when only a short span survives.
     float endDistance=max(0,abs(q.z)-v.axis.w)/v.center.w;
     float endFade=1-smoothstep(.05,.85,endDistance);
     float cut=smoothstep(.28,.63,field);
     float density=(1-smoothstep(edge-.28,edge,crossRadius))*
       (.08+cut*1.65)*endFade*v.up.w;
     float heat=saturate(.35+field*.8-crossRadius*.32);
     float3 color=lerp(float3(1.1,.065,.002),float3(2.7,.58,.018),smoothstep(.25,.7,heat));
     color=lerp(color,float3(3.4,2.2,.35),smoothstep(.78,.96,heat));
     FireDepthAccumulate(fireDepth,ro+rd*t,density*stepLength);
     float alpha=1-exp(-density*stepLength*2.2);light+=transmission*alpha*color;transmission*=1-alpha;
    }
    float peak=max(light.r,max(light.g,light.b));light/=1+peak/1.4;
    // This supporting bridge supplies coherent transported emission only.
    // Real parcel medium already provides the ring's colored extinction;
    // repeating it here exposes opaque capsule silhouettes over bright ground.
    // Retain bounded radiance and actual sampled depth for the shared fire DOF.
    return float4(light*.65,0)*(1-saturate(_EarthSeismicVision));
   }
      FireDofOutput Frag(V v){float2 moments;float4 color=ShadeFire(v,moments);return FireDofPack(color,moments);}
   ENDHLSL
  }
 }
}
