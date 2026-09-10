Shader "Elemental/Fire/ContinuousBoltTail"
{
 Properties { [HideInInspector]_FlowTime("Time",Float)=0 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-1" "RenderType"="Transparent"}
  Pass
  {
   Tags {"LightMode"="ElementalValleyCloud"}
   Blend 0 One OneMinusSrcAlpha Cull Front ZWrite Off ZTest Always
   Blend 1 One One
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   #include "../Shaders/FireDofOutput.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float _FlowTime;float4 _SpanA[48],_SpanB[48],_SpanAge[48];
   CBUFFER_END
   float _EarthSeismicVision;
   struct A {float3 position:POSITION;float4 center:TEXCOORD0;float4 bound:TEXCOORD1;float4 group:TEXCOORD2;};
   struct V {float4 position:SV_POSITION;float3 world:TEXCOORD0;nointerpolation float4 center:TEXCOORD1;nointerpolation float4 bound:TEXCOORD2;nointerpolation float4 group:TEXCOORD3;};
   V Vert(A i){V o;o.world=i.position;o.position=TransformWorldToHClip(i.position);o.center=i.center;o.bound=i.bound;o.group=i.group;return o;}
   float Hash(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
   float Noise(float3 p)
   {
    float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);
    return lerp(lerp(lerp(Hash(i),Hash(i+float3(1,0,0)),f.x),lerp(Hash(i+float3(0,1,0)),Hash(i+float3(1,1,0)),f.x),f.y),lerp(lerp(Hash(i+float3(0,0,1)),Hash(i+float3(1,0,1)),f.x),lerp(Hash(i+float3(0,1,1)),Hash(i+1),f.x),f.y),f.z);
   }
   float4 ShadeFire(V v,out float2 fireDepth)
   {
    fireDepth=0;
    if(v.group.x<1)discard;
    float3 ro=GetCameraPositionWS(),rd=normalize(v.world-ro);
    if(unity_OrthoParams.w>.5){rd=-UNITY_MATRIX_I_V._m02_m12_m22;ro=v.world-rd*dot(v.world-ro,rd);}
    float3 local=ro-v.center.xyz,ray=rd,bound=v.bound.xyz;
    float3 inverse=(step(0,ray)*2-1)/max(abs(ray),.000001);
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
    
    float stepLength=(exit-enter)/16,transmission=1;float3 light=0;
    [loop]for(int march=0;march<16;march++)
    {
     float t=enter+(march+.5)*stepLength;float3 world=ro+rd*t;
     float crossRadius=100,age=1;
     [unroll]for(int segment=0;segment<6;segment++)
     {
      if(segment>=(int)v.group.x)break;int index=(int)v.bound.w+segment;
      float3 a=_SpanA[index].xyz,b=_SpanB[index].xyz,ab=b-a;
      float along=saturate(dot(world-a,ab)/max(.00001,dot(ab,ab)));
      float radius=lerp(_SpanA[index].w,_SpanB[index].w,along);
      float distance=length(world-lerp(a,b,along))/max(.001,radius);
      if(distance<crossRadius){crossRadius=distance;age=lerp(_SpanAge[index].x,_SpanAge[index].y,along);}
     }
     if(crossRadius>=1)continue;
     // Shade the union once, without additive lumps where neighbouring supports overlap.
     float3 flow=world*3.4+float3(_FlowTime*.7,-_FlowTime*2.6,_FlowTime*1.3)+v.group.y;
     float broad=Noise(flow),fine=Noise(flow*2.7+float3(7.1,3.4,9.8));
     float field=broad*.72+fine*.28;
     float edge=.74+field*.26;
     float density=(1-smoothstep(edge-.24,edge,crossRadius))*(.45+smoothstep(.24,.72,field)*1.5);
     density*=1-smoothstep(.60,.98,age);
     // The old end erodes into small tongues, where the transported smoke continues.
     density*=lerp(1,smoothstep(.3,.65,field),smoothstep(.5,.94,age));
     float heat=saturate(.45+field*.65-crossRadius*.3-age*.22);
     float3 color=lerp(float3(1.1,.065,.002),float3(2.7,.65,.025),smoothstep(.22,.7,heat));
     float youngCore=(1-smoothstep(.12,.62,age))*(1-smoothstep(.25,.9,crossRadius));
     color=lerp(color,float3(4.6,3.5,1.15),youngCore*smoothstep(.43,.8,heat));
     FireDepthAccumulate(fireDepth,ro+rd*t,density*stepLength);
     float alpha=1-exp(-density*stepLength*3.8);light+=transmission*alpha*color;transmission*=1-alpha;
    }
    float peak=max(light.r,max(light.g,light.b));light/=1+peak/1.8;
    return float4(light,min(.8,1-transmission))*(1-saturate(_EarthSeismicVision));
   }
      FireDofOutput Frag(V v){float2 moments;float4 color=ShadeFire(v,moments);return FireDofPack(color,moments);}
   ENDHLSL
  }
 }
}
