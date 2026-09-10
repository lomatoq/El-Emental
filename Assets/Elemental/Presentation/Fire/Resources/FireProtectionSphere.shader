Shader "Elemental/Fire/ContinuousProtectionSphere"
{
 Properties
 {
  [HideInInspector] _CenterRadius("World center and radius",Vector)=(0,0,0,2.3)
  [HideInInspector] _UpEnergy("Local up and power",Vector)=(0,1,0,1)
  [HideInInspector] _Wave("Expanding thin shell",Float)=0
  [HideInInspector] _Filled("Solid charged core",Float)=0
  [HideInInspector] _Coverage("Vertical sphere coverage",Float)=1.08
  [HideInInspector] _FlowTime("Flow clock",Float)=0
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-1" "RenderType"="Transparent"}
  Pass
  {
   Name "ProtectiveFireVolume"
   Tags {"LightMode"="ElementalValleyCloud"}
   Blend 0 One OneMinusSrcAlpha
   Cull Front ZWrite Off ZTest Always
   Blend 1 One One
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   #include "../Shaders/FireDofOutput.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _CenterRadius,_UpEnergy,_BoltDirection,_BoltShape,_BoltMotion;float _FlowTime,_Wave,_Filled,_Coverage;
   CBUFFER_END
   float _EarthSeismicVision;
   struct A {float3 position:POSITION;};
   struct V {float4 position:SV_POSITION;float3 world:TEXCOORD0;};
   V Vert(A i){V o;o.world=i.position;o.position=TransformWorldToHClip(i.position);return o;}
   float4 ShadeFire(V i,out float2 fireDepth)
   {
    fireDepth=0;
    float radius=max(.1,_CenterRadius.w),energy=saturate(_UpEnergy.w);
    float3 ro=GetCameraPositionWS(),rd=normalize(i.world-ro);
    if(unity_OrthoParams.w>.5){rd=-UNITY_MATRIX_I_V._m02_m12_m22;ro=i.world-rd*dot(i.world-ro,rd);}
    float3 offset=ro-_CenterRadius.xyz;float b=dot(offset,rd),c=dot(offset,offset)-radius*radius*1.1664;
    float discriminant=b*b-c;if(discriminant<=0)discard;
    float root=sqrt(discriminant),enter=max(0,-b-root),exit=-b+root;
    float2 screen=GetNormalizedScreenSpaceUV(i.position);float raw=SampleSceneDepth(screen);
    #if UNITY_REVERSED_Z
    bool opaque=raw>0;
    #else
    bool opaque=raw<1;raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
    #endif
    if(opaque)
    {
     float3 surface=ComputeWorldSpacePosition(screen,raw,UNITY_MATRIX_I_VP);
     if(all(isfinite(surface)))exit=min(exit,dot(surface-ro,rd));
    }
    if(exit<=enter)discard;
    float bolt=step(.5,_BoltDirection.w);
    float3 boltForward=normalize(_BoltDirection.xyz+(1-bolt)*float3(0,0,1));
    float3 boltSide=normalize(cross(boltForward,abs(boltForward.y)<.9?float3(0,1,0):float3(1,0,0))),boltAcross=cross(boltForward,boltSide);
    float3 up=normalize(_UpEnergy.xyz);
    float3 side=normalize(cross(up,abs(up.y)<.9?float3(0,1,0):float3(1,0,0))),across=cross(up,side);
    float gapStart=enter,gapEnd=enter;
    if(_Wave>.5)
    {
     float innerDiscriminant=b*b-dot(offset,offset)+radius*radius*.64;
     if(innerDiscriminant>0){float innerRoot=sqrt(innerDiscriminant);gapStart=clamp(-b-innerRoot,enter,exit);gapEnd=clamp(-b+innerRoot,enter,exit);}
    }
    float firstLength=gapStart-enter,gapLength=gapEnd-gapStart;
    float occupiedLength=exit-enter-gapLength;if(occupiedLength<=.00001)discard;
    float stepLength=occupiedLength/24,transmission=1;float3 radiance=0;float optical=0;
    // Twenty-four bounded samples; one volume draw instead of overlapping per-sector shells.
    [loop]for(int march=0;march<24;march++)
    {
     float traveled=(march+.5)*stepLength;
     float sampleDistance=enter+traveled+(traveled>firstLength?gapLength:0);
     float3 p=(ro+rd*sampleDistance-_CenterRadius.xyz)/radius;
     float3 q=float3(dot(p,side),dot(p,up),dot(p,across));
     if(bolt>.5)q=float3(dot(p,boltSide),dot(p,boltAcross),dot(p,boltForward));
     float r=length(q);if(bolt>.5&&r>=1)continue;
     float twist=_FlowTime*.9+q.y*2.7;
     float2 spun=float2(q.x*cos(twist)-q.z*sin(twist),q.x*sin(twist)+q.z*cos(twist));
     // Wave expansion must not multiply noise frequency beyond the fixed march budget.
     // Keep the approved stationary sphere domain exactly unchanged.
     float waveScale=max(1,radius/2.3);
     float3 flow=float3(spun.x,q.y,spun.y)*lerp(1,min(1.7,waveScale),saturate(_Wave));flow.y-=_FlowTime*.65;
     float3 warp=float3(sin(flow.y*4+flow.z*3),sin(flow.z*4-flow.x*3),sin(flow.x*4-flow.y*3))*.12;
     flow+=warp;
     float broad=sin(flow.x*6+flow.y*3)*sin(flow.y*5-flow.z*4)*sin(flow.z*5+flow.x*3);
     float fine=sin(flow.x*17+flow.y*11+_FlowTime)*sin(flow.y*13-flow.z*15-_FlowTime*1.3);
     if(bolt>.5)
     {
      float3 moving=q*float3(1.2,1.2,1.8)+float3(_BoltMotion.y,_BoltMotion.y*.63,-_BoltMotion.x*2.8);
      broad=sin(moving.x*4.7+moving.z*2.3)*sin(moving.y*5.2-moving.z*3.1);
      fine=sin(moving.x*13+moving.y*7+moving.z*11)*sin(moving.y*11-moving.z*9);
     }
     // Preserve tongue displacement in world metres as the wave radius grows.
     // Otherwise a 9 m shell acquires metre-wide repeated radial teeth.
     float outer=.96+(broad*.10+fine*.025)*lerp(1,rcp(waveScale),saturate(_Wave));
     float inner=lerp(.68,.82,saturate(_Wave)),innerEnd=lerp(.76,.86,saturate(_Wave));
     float shell=lerp(smoothstep(inner,innerEnd,r),1,saturate(_Filled))*(1-smoothstep(outer-.13,outer,r));
     shell*=1-smoothstep(_Coverage,_Coverage+.12,abs(q.y));
     if(bolt>.5)
     {
      float width=_BoltShape.x*(q.z>=0?sqrt(saturate(1-pow(q.z/_BoltShape.y,2))):pow(saturate(1+q.z/_BoltShape.z),.55));
      if(q.z>=_BoltShape.y||q.z<=-_BoltShape.z)continue;
      float2 transverse=q.xy-float2(_BoltShape.w*saturate(1-q.z*q.z),0);
      float shape=length(transverse)/max(.001,width);
      shell=1-smoothstep(.57+broad*.07,.91+fine*.065,shape);
     }
     float tongues=smoothstep(-.5,.55,broad+fine*.24);
     float density=shell*(.24+tongues*1.4)*energy;
     float heat=saturate((1-r)*2.5+.32+broad*.35+fine*.10);
     if(bolt>.5)heat=saturate(.78+q.z*.24+broad*.15+fine*.12-length(q.xy)*.28);
     float3 color=lerp(float3(1.1,.065,.002),float3(2.7,.58,.018),smoothstep(.2,.68,heat));
     color=lerp(color,float3(4.8,4.1,2.1),smoothstep(.72,.98,heat)*tongues);
     if(bolt>.5){float hotInterior=smoothstep(-.38,.22,q.z)*(1-smoothstep(.28,.74,length(q.xy)))*smoothstep(.48,.8,heat);color=lerp(color,float3(6.2,5.3,2.8),hotInterior);float nose=smoothstep(.2,.7,q.z)*smoothstep(.58,.86,heat);color=lerp(color,float3(7,6.7,5.5),nose);}
     FireDepthAccumulate(fireDepth,ro+rd*sampleDistance,density*stepLength);
     float alpha=1-exp(-density*stepLength*1.7);
     radiance+=transmission*alpha*color*1.3;transmission*=1-alpha;optical+=density*stepLength;
    }
    float peak=max(radiance.r,max(radiance.g,radiance.b));radiance/=1+peak/2.4;
    // The avatar's central space is empty; limited extinction leaves the front shell readable.
    float alpha=min(.72,1-transmission);
    return float4(radiance,alpha)*(1-saturate(_EarthSeismicVision));
   }
      FireDofOutput Frag(V i){float2 moments;float4 color=ShadeFire(i,moments);return FireDofPack(color,moments);}
   ENDHLSL
  }
 }
}
