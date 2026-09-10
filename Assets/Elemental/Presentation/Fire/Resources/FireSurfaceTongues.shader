Shader "Elemental/Fire/Surface Tongues"
{
 Properties { _MainTex("Tongues",2D)="white"{} _NoiseTex("Turbulence",2D)="gray"{} _Heat("Heat",Float)=1 _Phase("Phase",Float)=0 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
  Pass
  {
   Blend One OneMinusSrcAlpha ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);TEXTURE2D(_NoiseTex);SAMPLER(sampler_NoiseTex);
   CBUFFER_START(UnityPerMaterial) float _Heat,_Phase; CBUFFER_END
   struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
   struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
   V vert(A a){V o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.uv=a.uv;return o;}
   half4 frag(V i):SV_Target
   {
    float2 uv=i.uv;float t=_Time.y;
    float n=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,uv*float2(1.8,1.2)+float2(_Phase*.13,-t*.65)).r;
    uv.x+=(n-.5)*.13*uv.y;
    uv.y+=(SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,uv*2+float2(t*.09,-t*.9)).r-.5)*.055;
    float shape=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).r;
    float alpha=smoothstep(.045,.5,shape)*saturate(_Heat)*smoothstep(0,.07,i.uv.y)*(1-smoothstep(.92,1,i.uv.y));
    float core=saturate(shape*1.3*(1-i.uv.y*.85));
    half3 color=lerp(half3(1.7,.13,.006),half3(2.2,1.05,.07),core);
    return half4(color*alpha,alpha*.86);
   }
   ENDHLSL
  }
 }
}
