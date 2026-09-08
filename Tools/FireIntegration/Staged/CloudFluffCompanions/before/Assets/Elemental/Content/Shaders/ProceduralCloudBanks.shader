Shader "Elemental/Procedural Cloud Banks"
{
 Properties { _Density("Extinction per metre", Range(0.001,0.04))=0.014 _TopColor("Lit cream blue",Color)=(0.93,0.97,1,1) _BaseColor("Cool base",Color)=(0.64,0.76,0.87,1) }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent"}
  Pass
  {
   Tags {"LightMode"="ElementalValleyCloud"}
   Cull Front ZWrite Off ZTest Always Blend One OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _TopColor,_BaseColor;
   float _Density;
   CBUFFER_END
   float _ElementalValleyEnabled,_ElementalNight01;
   float4x4 _ElementalWorldToValley;
   float4 _ElementalValleyFog;
   struct A {float3 p:POSITION;};
   struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;};
   V vert(A a){V o;o.world=TransformObjectToWorld(a.p);o.p=TransformWorldToHClip(o.world);return o;}
   float lobe(float3 p,float3 c,float3 r){float3 q=(p-c)/r;return 1-smoothstep(0.35,1,dot(q,q));}
   float density(float3 p)
   {
    // Large connected sculpted lobes: no texture, temporal jitter or screen-space grain.
    float d=lobe(p,float3(0,-.17,0),float3(.45,.18,.38));
    d=max(d,lobe(p,float3(-.30,-.04,-.03),float3(.17,.22,.27)));
    d=max(d,lobe(p,float3(-.10,.10,.02),float3(.20,.30,.31)));
    d=max(d,lobe(p,float3(.15,.04,-.01),float3(.17,.24,.28)));
    d=max(d,lobe(p,float3(.33,-.10,.03),float3(.14,.17,.22)));
    d=max(d,lobe(p,float3(-.04,.02,-.25),float3(.19,.23,.19)));
    return d;
   }
   half4 frag(V i):SV_Target
   {
    if(_ElementalValleyEnabled<.5)return 0;
    float2 uv=GetNormalizedScreenSpaceUV(i.p);
    float3 origin=_WorldSpaceCameraPos,ray=normalize(i.world-origin);
    if(unity_OrthoParams.w>.5)
    {
     #if UNITY_REVERSED_Z
     float nearDepth=1;
     #else
     float nearDepth=UNITY_NEAR_CLIP_VALUE;
     #endif
     origin=ComputeWorldSpacePosition(uv,nearDepth,UNITY_MATRIX_I_VP);ray=normalize(i.world-origin);
    }
    float3 ro=TransformWorldToObject(origin),rd=mul((float3x3)unity_WorldToObject,ray);
    float3 safe=lerp(-max(abs(rd),1e-7),max(abs(rd),1e-7),step(0,rd));
    float3 a=(-.5-ro)/safe,b=(.5-ro)/safe,lo=min(a,b),hi=max(a,b);
    float enter=max(0,max(lo.x,max(lo.y,lo.z))),leave=min(hi.x,min(hi.y,hi.z));
    float raw=SampleSceneDepth(uv);
    #if !UNITY_REVERSED_Z
    raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
    #endif
    float3 opaque=ComputeWorldSpacePosition(uv,raw,UNITY_MATRIX_I_VP);
    leave=min(leave,max(0,dot(opaque-origin,ray)));
    if(leave<=enter)return 0;
    float span=(leave-enter)/12,trans=1;float3 color=0;
    float day=saturate(1-_ElementalNight01);
    [unroll] for(int s=0;s<12;s++)
    {
     float3 p=ro+rd*(enter+(s+.5)*span);
     float d=density(p),alpha=1-exp(-d*_Density*span);
     float3 tint=lerp(_BaseColor.rgb,_TopColor.rgb,smoothstep(-.35,.35,p.y));
     tint*=lerp(float3(.22,.28,.40),float3(1,1,1),day);
     color+=trans*alpha*tint;trans*=1-alpha;
    }
    // Complement the opaque lower veil; a submerged bank cannot shine through it.
    float3 middle=origin+ray*((enter+leave)*.5);
    float h=mul(_ElementalWorldToValley,float4(middle,1)).y-_ElementalValleyFog.x;
    float haze=exp(-exp(-max(0,h)/max(1,_ElementalValleyFog.y))*_ElementalValleyFog.z*((enter+leave)*.5));
    return half4(color*haze,(1-trans)*haze);
   }
   ENDHLSL
  }
 }
}
