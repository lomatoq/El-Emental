Shader "Elemental/Fire/Additive Meteor Bow"
{
 Properties
 {
  [HideInInspector] _HipsRadius("Hips and body radius",Vector)=(0,0,0,.3)
  [HideInInspector] _HeadPower("Head and energy",Vector)=(0,1,0,0)
  [HideInInspector] _Forward("Flow direction",Vector)=(0,0,1,0)
  [HideInInspector] _Up("Local up",Vector)=(0,1,0,0)
  [HideInInspector] _BoundsMin("Bounds minimum",Vector)=(-1,-1,-1,0)
  [HideInInspector] _BoundsMax("Bounds maximum",Vector)=(1,1,1,0)
  [HideInInspector] _FlowTime("Clock",Float)=0
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Pass
  {
   Name "Meteor Bow" Tags {"LightMode"="ElementalValleyCloud"}
   Blend 0 One One
   Blend 1 One One Cull Front ZWrite Off ZTest Always
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   #include "../Shaders/FireDofOutput.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _HipsRadius,_HeadPower,_Forward,_Up,_BoundsMin,_BoundsMax;float _FlowTime;
   CBUFFER_END
   float _EarthSeismicVision;
   struct A{float3 position:POSITION;};struct V{float4 position:SV_POSITION;float3 world:TEXCOORD0;};
   V Vert(A i){V o;o.world=i.position;o.position=TransformWorldToHClip(i.position);return o;}
   float3 Closest(float3 p,float3 a,float3 b){float3 v=b-a;return a+v*saturate(dot(p-a,v)/max(.0001,dot(v,v)));}
   float4 ShadeFire(V i,out float2 fireDepth)
   {
    fireDepth=0;
    float3 forward=normalize(_Forward.xyz),up=normalize(_Up.xyz),side=normalize(cross(up,forward));
    float3 ro=GetCameraPositionWS(),rd=normalize(i.world-ro);
    if(unity_OrthoParams.w>.5){rd=-UNITY_MATRIX_I_V._m02_m12_m22;ro=i.world-rd*dot(i.world-ro,rd);}
    float3 safeRay=(step(0,rd)*2-1)*max(abs(rd),.00001);
    float3 t0=(_BoundsMin.xyz-ro)/safeRay,t1=(_BoundsMax.xyz-ro)/safeRay;
    float3 nearT=min(t0,t1),farT=max(t0,t1);
    float enter=max(0,max(nearT.x,max(nearT.y,nearT.z))),exit=min(farT.x,min(farT.y,farT.z));
    float2 screen=GetNormalizedScreenSpaceUV(i.position);float raw=SampleSceneDepth(screen);
    #if UNITY_REVERSED_Z
    bool opaque=raw>0;
    #else
    bool opaque=raw<1;raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
    #endif
    if(opaque){float3 world=ComputeWorldSpacePosition(screen,raw,UNITY_MATRIX_I_VP);if(all(isfinite(world)))exit=min(exit,dot(world-ro,rd));}
    if(exit<=enter)discard;
    float lengthStep=(exit-enter)/24;float3 emission=0;
    [loop]for(int n=0;n<24;n++)
    {
     float3 p=ro+rd*(enter+(n+.5)*lengthStep);
     float3 body=Closest(p,_HipsRadius.xyz,_HeadPower.xyz),delta=p-body;
     float radius=length(delta);float3 normal=delta/max(radius,.001);
     float3 offset=p-(_HipsRadius.xyz+_HeadPower.xyz)*.5;
     float3 q=float3(dot(offset,side),dot(offset,up),dot(offset,forward));
     float clock=_FlowTime;
     // Disturbance advects backwards. Surface itself changes, rather than only a scrolling color.
     float broad=sin(q.x*8+q.z*5+clock*4.8)*sin(q.y*7-q.z*3+clock*2.1);
     float fine=sin(q.x*17+q.y*11+q.z*6+clock*7.1);
     float deformation=.07*(broad*.65+fine*.35);
     float shell=1-smoothstep(.025,.105,abs(radius-_HipsRadius.w-deformation));
     float facing=dot(normal,forward);
     float front=smoothstep(-.28,.4,facing);
     float filaments=smoothstep(-.65,.55,broad+fine*.35);
     float density=shell*front*(.22+filaments*.78);
     // An open, cooling skirt joins the body sheath to the existing backwards hand/foot gas.
     float3 wakeEnd=_HipsRadius.xyz-forward*.48;
     float3 wakeAxis=Closest(p,_HipsRadius.xyz,wakeEnd);
     float downstream=saturate(dot(p-_HipsRadius.xyz,-forward)/.48);
     float wakeRadius=_HipsRadius.w*(1+.25*downstream);
     float wake=1-smoothstep(.025,.105,abs(length(p-wakeAxis)-wakeRadius-deformation));
     wake*=smoothstep(0,.13,downstream)*(1-smoothstep(.45,1,downstream))*filaments*.35;
     density=max(density,wake)*_HeadPower.w;
     float temperature=smoothstep(.65,.98,facing)*front;
     float3 color=lerp(float3(3.8,.42,.015),float3(5.7,2.15,.18),filaments);
     color=lerp(color,float3(8.8,7.4,5.1),temperature);
     FireDepthAccumulate(fireDepth,p,density*lengthStep);
     emission+=color*density*lengthStep*2.6;
    }
    return float4(emission*(1-saturate(_EarthSeismicVision)),0);
   }
   FireDofOutput Frag(V i){float2 moments;float4 color=ShadeFire(i,moments);return FireDofPack(color,moments);}
   ENDHLSL
  }
 }
}
