Shader "Elemental/Fire/TransportedFlowParcel"
{
 Properties
 {
  _DetailTex("Detailed burning tongues",2D)="white" {}
  [HideInInspector] _DetailMix("Detailed tongues enabled",Float)=0
  [HideInInspector] _ShapeFloor("Outside tongue density",Float)=.24
  [HideInInspector] _SmokeOpacity("Cooling smoke opacity",Float)=.24
  [HideInInspector] _SourceRadianceCap("Young source radiance cap",Float)=.30
  [HideInInspector] _WhitePhaseEnd("White source hot-age endpoint",Float)=.18
  [HideInInspector] _WhiteCapEnd("White source radiance endpoint",Float)=.25
  [HideInInspector] _RingMediumRefinement("Ring soft volume refinement",Float)=1
  [HideInInspector] _RingMediumVisibility("Ring medium QA visibility",Float)=1
  [HideInInspector] _MediumAuthoredShape("Ring medium follows authored density",Float)=0
  [HideInInspector] _SmokeMode("Cooling smoke mode",Float)=0
  [HideInInspector] _SrcBlend("Source blend",Float)=1
  [HideInInspector] _DstBlend("Destination blend",Float)=1
  _FlameAtlas("Confirmed Wallcoeur rounded flame atlas",2D)="white" {}
  _AuthoredShapeMix("Authored rounded tongue accents",Range(0,1))=0
  [HDR] _Edge("Cooling red edge",Color)=(1.1,.065,.002,1)
  [HDR] _Body("Hot orange gas",Color)=(2.7,.58,.018,1)
  [HDR] _Core("Fresh gold core",Color)=(5.0,2.8,.20,1)
  [HideInInspector] _TailAgeScale("Short jet tail age",Float)=1
  [HideInInspector] _TailRefinement("Hand stream tail refinement",Float)=1
  [HideInInspector] _BlueSource("Weak jet blue source",Float)=0
  [HideInInspector] _WhiteSource("White-hot hand source",Float)=0
  _CoreGlowScale("Inner glow strength",Range(0,1))=0.75
  _Emission("Additive emission per metre",Range(.1,8))=1.45
  _DebugView("QA 0 radiance 1 support 2 depth-free support 3 depth-free radiance",Float)=0
  _HeatDistortionPixels("Gentle heat distortion pixels",Range(0,2))=1.25
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-2" "RenderType"="Transparent"}
  Pass
  {
   Tags {"LightMode"="ElementalValleyCloud"}
   Blend 0 [_SrcBlend] [_DstBlend]
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
   float4 _Edge,_Body,_Core,_FlameAtlas_TexelSize;
   float _Emission,_DebugView,_AuthoredShapeMix,_HeatDistortionPixels;
   float _WhiteSource,_BlueSource;
   float _SourceRadianceCap,_WhitePhaseEnd,_WhiteCapEnd;
   float _MediumAuthoredShape,_RingMediumRefinement,_RingMediumVisibility;
   float _CoreGlowScale,_SmokeMode,_TailRefinement,_TailAgeScale,_DetailMix,_ShapeFloor,_SmokeOpacity;
   CBUFFER_END
   TEXTURE2D(_FlameAtlas);SAMPLER(sampler_FlameAtlas);
   TEXTURE2D(_DetailTex);SAMPLER(sampler_DetailTex);
   TEXTURE2D_X(_ElementalHeatSource);
   float4 _ElementalHeatSource_TexelSize;
   float _EarthSeismicVision;
   bool ParcelOpaqueWorld(float2 uv,out float3 world)
   {
    float raw=SampleSceneDepth(uv);world=0;
    // The clear-depth sky is not an opaque receiver. In particular an infinite
    // projection can reconstruct it with w=0. Test the raw texture sentinel
    // before the non-reversed clip-range conversion or any world reconstruction.
    #if UNITY_REVERSED_Z
    if(raw<=0.0)return false;
    #else
    if(raw>=1.0)return false;
    raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
    #endif
    world=ComputeWorldSpacePosition(uv,raw,UNITY_MATRIX_I_VP);
    return all(isfinite(world));
   }
   float AuthoredFrame(float2 uv,float frame)
   {
    float2 cell=float2(fmod(frame,3),2-floor(frame/3));
    uv=clamp(uv,_FlameAtlas_TexelSize.xy*1.5,1-_FlameAtlas_TexelSize.xy*1.5);
    float4 sampled=SAMPLE_TEXTURE2D(_FlameAtlas,sampler_FlameAtlas,(cell+uv)/3);
    return sampled.r*sampled.a;
   }
   float AuthoredParcelMask(float2 uv,float age,float phase)
   {
    if(sin(phase)>0)uv.x=1-uv.x;
    float mask=1;
    if(_DetailMix>.5){uv.x+=sin(uv.y*9+_Time.y*7+phase)*.025*uv.y;mask=SAMPLE_TEXTURE2D_LOD(_DetailTex,sampler_DetailTex,uv,0).r;}
    else
    {
     float frame=saturate(age)*8.999,first=floor(frame);
     mask=lerp(AuthoredFrame(uv,first),AuthoredFrame(uv,min(first+1,8)),smoothstep(0,1,frac(frame)));
    }
    return mask;
   }
   struct A {float3 position:POSITION;float4 centre:TEXCOORD0;float4 axis:TEXCOORD1;float4 up:TEXCOORD2;float4 clipA:TEXCOORD3;float4 clipB:TEXCOORD4;float4 kind:TEXCOORD5;float3 thermal:TEXCOORD6;};
   struct V {float4 position:SV_POSITION;float3 world:TEXCOORD0;nointerpolation float4 centre:TEXCOORD1;nointerpolation float4 axis:TEXCOORD2;nointerpolation float4 up:TEXCOORD3;nointerpolation float4 clipA:TEXCOORD4;nointerpolation float4 clipB:TEXCOORD5;nointerpolation float4 kind:TEXCOORD6;nointerpolation float3 thermal:TEXCOORD7;};
   V Vert(A v){V o;o.position=TransformWorldToHClip(v.position);o.world=v.position;o.centre=v.centre;o.axis=v.axis;o.up=v.up;o.clipA=v.clipA;o.clipB=v.clipB;o.kind=v.kind;o.thermal=v.thermal;return o;}
   float4 ShadeFire(V v,out float2 fireDepth)
   {
    fireDepth=0;
    float3 ro=_WorldSpaceCameraPos;
    float3 rd=normalize(v.world-ro);
    if(unity_OrthoParams.w>.5){rd=-UNITY_MATRIX_I_V._m02_m12_m22;ro=v.world-rd*dot(v.world-ro,rd);}
    v.axis.xyz=normalize(v.axis.xyz);
    v.up.xyz=normalize(v.up.xyz-v.axis.xyz*dot(v.up.xyz,v.axis.xyz));
    float3 side=normalize(cross(v.up.xyz,v.axis.xyz));
    float3 relative=ro-v.centre.xyz;
    float3 local=float3(dot(relative,side),dot(relative,v.up.xyz),dot(relative,v.axis.xyz)/max(v.kind.w,.1))/v.centre.w;
    float3 ray=float3(dot(rd,side),dot(rd,v.up.xyz),dot(rd,v.axis.xyz)/max(v.kind.w,.1))/v.centre.w;
    float aa=dot(ray,ray),bb=dot(local,ray),cc=dot(local,local)-1;
    float discriminant=bb*bb-aa*cc;if(discriminant<=0)discard;
    float root=sqrt(discriminant),enter=max(0,(-bb-root)/aa),exit=(-bb+root)/aa;
    float2 uv=v.position.xy/_ScaledScreenParams.xy;
    float3 opaque;
    if(_DebugView<1.5 && ParcelOpaqueWorld(uv,opaque))exit=min(exit,dot(opaque-ro,rd));
    if(exit<=enter)discard;
    if(_DebugView>.5 && _DebugView<2.5)return float4(.08,.5,.2,0);
    #if defined(ELEMENTAL_TRANSPORTED_HEAT)
    if(_SmokeMode>.5||v.kind.x>.5||_HeatDistortionPixels<=0)discard;
    float middle=(enter+exit)*.5;float3 middleWorld=ro+rd*middle;float3 middleLocal=local+ray*middle;
    if(dot(v.clipA.xyz,v.clipA.xyz)>.5&&dot(float4(middleWorld,1),v.clipA)<0)discard;
    if(dot(v.clipB.xyz,v.clipB.xyz)>.5&&dot(float4(middleWorld,1),v.clipB)<0)discard;
    float mask=smoothstep(0,.65,1-dot(middleLocal,middleLocal))*smoothstep(0,.08,v.axis.w)*(1-smoothstep(.35,.85,v.axis.w));
    mask*=saturate((exit-enter)/.18)*(1-saturate(_EarthSeismicVision));
    float2 flow=float2(sin(middleLocal.z*4+v.up.w+v.kind.z*2.4),cos(middleLocal.x*5-v.up.w-v.kind.z*1.9));
    float2 shifted=clamp(uv+flow*_ElementalHeatSource_TexelSize.xy*min(_HeatDistortionPixels,2),_ElementalHeatSource_TexelSize.xy,1-_ElementalHeatSource_TexelSize.xy);
    float3 shiftedWorld;
    if(ParcelOpaqueWorld(shifted,shiftedWorld) && dot(shiftedWorld-ro,rd)<enter)shifted=uv;
    return float4(SAMPLE_TEXTURE2D_X(_ElementalHeatSource,sampler_LinearClamp,shifted).rgb,mask*.10*saturate(_HeatDistortionPixels));
    #else
    // Two real collision planes prevent density swelling through a contacted wall/corner.
    if(_SmokeMode>.5&&v.kind.x>.5)discard;
    // Contact mixing can cool a young parcel before its age-based tail begins.
    float contacted=step(.5,dot(v.clipA.xyz,v.clipA.xyz));
    float hotAge=v.thermal.z;
    float cooling=1-smoothstep(lerp(.28,.42,contacted),lerp(.62,.76,contacted),v.thermal.x);
    float tail=_TailRefinement*(1-step(.5,v.kind.x))*max(smoothstep(.48,.90,hotAge*_TailAgeScale),cooling);
    float3 radiance=0;float sootIntegral=0,bodyIntegral=0;
    float stepLength=(exit-enter)/10;
    [unroll]for(int i=0;i<10;i++)
    {
     float t=enter+(i+.5)*stepLength;float3 p=ro+rd*t;
     if(dot(v.clipA.xyz,v.clipA.xyz)>.5&&dot(float4(p,1),v.clipA)<0)continue;
     if(dot(v.clipB.xyz,v.clipB.xyz)>.5&&dot(float4(p,1),v.clipB)<0)continue;
     float3 q=local+ray*t;
     float phase=v.up.w;
     float twist=phase+q.z*3.8-v.kind.z*2.4;
     float2 transverse=float2(q.x*cos(twist)-q.y*sin(twist),q.x*sin(twist)+q.y*cos(twist));
     float broad=sin(transverse.x*5+q.z*3+phase)*sin(transverse.y*4-q.z*4-phase*.6);
     float fine=sin(q.x*13+q.y*7+phase+v.kind.z*3.2)*sin(q.y*11-q.z*9-v.kind.z*2.7);
     float field=1-dot(q,q)+broad*lerp(.22,.36,tail)+fine*lerp(.07,.16,tail);
     float density=(1-smoothstep(.68,1,dot(q,q)))*smoothstep(0,.48,field)*smoothstep(0,.035,v.kind.z)*(1-smoothstep(.55,1,hotAge));
     // Break up the late ellipsoid into winding tongues inside its safe support.
     // Middle parcels keep their accepted density and colored extinction.
     // Two separated winding volumes, not a nonzero ellipsoid filled underneath.
     // Their domain is transported/rotated with velocity and clipped by contact planes.
     float2 bend=float2(sin(q.z*4.8+phase-v.kind.z*2.1),cos(q.z*3.7-phase+v.kind.z*1.9))*.18;
     float2 a=(transverse-bend-float2(.26,0))/float2(.22,.36);
     float2 b=(transverse+bend+float2(.26,0))/float2(.18,.30);
     float tongue=max(1-smoothstep(.12,1,dot(a,a)),1-smoothstep(.12,1,dot(b,b)));
     float eroded=smoothstep(.14,.56,field);
     density*=lerp(1,tongue*eroded*.72,tail);
     float authored=1;
     if((_SmokeMode<.5||max(tail,_MediumAuthoredShape)>.001)&&_AuthoredShapeMix>.001&&v.kind.x<.5)
     {
      // Authored shape lives inside transported 3D density, never a camera-facing sheet.
      float2 uvA=float2(transverse.x,q.z)*.5+.5,uvB=float2(transverse.y,q.z)*.5+.5;
      float maskA=AuthoredParcelMask(uvA,hotAge,phase),maskB=AuthoredParcelMask(uvB,hotAge,phase+1.7);
      authored=max(maskA,maskB);
      // A union of projected masks fills each other's transparent cuts. Only
      // ring extinction uses their soft intersection; bright flame emission
      // and the validated ribbon keep the band continuous between tongues.
      float ringMedium=saturate(_MediumAuthoredShape*_RingMediumRefinement)*step(.5,_SmokeMode);
      authored=lerp(authored,sqrt(saturate(maskA*maskB)),ringMedium);
      // Tail extinction must share the emission silhouette; otherwise it fills
      // every transparent gap with a red ellipsoid. Preserve the hot middle.
      float authoredWeight=_AuthoredShapeMix*(_SmokeMode>.5?max(tail,_MediumAuthoredShape):1);
      density*=lerp(1,lerp(_ShapeFloor,1.3,authored),authoredWeight);
     }
     FireDepthAccumulate(fireDepth,ro+rd*t,density*stepLength);
     if(_SmokeMode>.5)
     {
      float smokeShape=(1-smoothstep(.48,1,dot(q,q)))*smoothstep(-.1,.62,field);
      smokeShape*=lerp(1,.7,_TailRefinement*smoothstep(.48,.90,v.axis.w));
      sootIntegral+=smokeShape*v.thermal.y*(1-smoothstep(.35,.6,v.thermal.x))*(1-smoothstep(.78,1,v.axis.w))*stepLength;
      bodyIntegral+=density*1.2*smoothstep(.18,.5,v.thermal.x)*stepLength;
      continue;
     }
     float heat=saturate(v.thermal.x*(.45+field*.8));
     density*=1.2*smoothstep(.16,.45,v.thermal.x);
     float3 color=lerp(_Edge.rgb,_Body.rgb,smoothstep(.2,.7,heat));
     color=lerp(color,_Body.rgb,authored*_AuthoredShapeMix*.35);
     color=lerp(color,_Core.rgb*_CoreGlowScale,smoothstep(.65,.98,heat)*(1-smoothstep(.15,.65,hotAge)));
     color=lerp(color,lerp(float3(7,6.5,5.4),float3(.3,1.7,7),saturate(_BlueSource)),saturate(_WhiteSource)*smoothstep(.75,.98,heat)*(1-smoothstep(.03,_WhitePhaseEnd,hotAge)));
     if(v.kind.x>.5){color=_Core.rgb*2.8;density=smoothstep(0,.85,1-dot(q,q))*(1-smoothstep(.3,1,v.axis.w));}
     // Additive radiance remains depth/geometry clipped; zero output alpha preserves scene alpha.
     float muzzle=1+.20*_TailRefinement*(1-smoothstep(.03,.16,hotAge));
     radiance+=density*stepLength*color*_Emission*(v.kind.x>.5?10:muzzle);

    }
    if(_SmokeMode>.5)
    {
     float smokeAlpha=0; // Cooling coverage is rendered by the shared dust material on transported area samples.
     float3 smokeColor=lerp(float3(.19,.175,.16),float3(.3,.22,.16),saturate(v.thermal.x));
          // Colored extinction gives the hot gas a readable body over bright sky.
     // Reuse the existing alpha-blended medium draw; emission remains additive.
     float ringMedium=saturate(_MediumAuthoredShape*_RingMediumRefinement);
     float bodyAlpha=(1-exp(-bodyIntegral*lerp(12.0,4.5,ringMedium)))*.90;
     bodyAlpha*=lerp(1,saturate(_RingMediumVisibility),saturate(_MediumAuthoredShape));
     float3 bodyColor=lerp(float3(.85,.095,.006),float3(1,.46,.025),smoothstep(.4,.9,v.thermal.x));
     float mediumAlpha=1-(1-smokeAlpha)*(1-bodyAlpha);
     float3 mediumColor=(bodyColor*bodyAlpha+smokeColor*smokeAlpha*(1-bodyAlpha))/max(mediumAlpha,.0001);
     return float4(mediumColor,mediumAlpha*(1-saturate(_EarthSeismicVision)));
    }
    // Bound one parcel's energy before additive overlap; preserve chromatic ratios.
    // Tiny sparks keep a separate higher peak, while the broad stream stays orange/gold.
    float peak=max(radiance.r,max(radiance.g,radiance.b));
    float sourceCap=lerp(.30,_SourceRadianceCap,(1-smoothstep(.04,_WhiteCapEnd,hotAge))*saturate(_WhiteSource));
    radiance/=1+peak/(v.kind.x>.5?3.0:sourceCap);
    return float4(radiance,0)*(1-saturate(_EarthSeismicVision));
    #endif
   }
      FireDofOutput Frag(V v){float2 moments;float4 color=ShadeFire(v,moments);return FireDofPack(color,moments);}
   ENDHLSL
  }
  Pass
  {
   Name "FlowHeatHaze"
   Tags {"LightMode"="ElementalFireHeat"}
   Blend SrcAlpha OneMinusSrcAlpha
   Cull Front ZWrite Off ZTest Always
   HLSLPROGRAM
   #define ELEMENTAL_TRANSPORTED_HEAT 1

   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _Edge,_Body,_Core,_FlameAtlas_TexelSize;
   float _Emission,_DebugView,_AuthoredShapeMix,_HeatDistortionPixels;
   float _WhiteSource,_BlueSource;
   float _SourceRadianceCap,_WhitePhaseEnd,_WhiteCapEnd;
   float _MediumAuthoredShape,_RingMediumRefinement,_RingMediumVisibility;
   float _CoreGlowScale,_SmokeMode,_TailRefinement,_TailAgeScale,_DetailMix,_ShapeFloor,_SmokeOpacity;
   CBUFFER_END
   TEXTURE2D(_FlameAtlas);SAMPLER(sampler_FlameAtlas);
   TEXTURE2D(_DetailTex);SAMPLER(sampler_DetailTex);
   TEXTURE2D_X(_ElementalHeatSource);
   float4 _ElementalHeatSource_TexelSize;
   float _EarthSeismicVision;
   bool ParcelOpaqueWorld(float2 uv,out float3 world)
   {
    float raw=SampleSceneDepth(uv);world=0;
    // The clear-depth sky is not an opaque receiver. In particular an infinite
    // projection can reconstruct it with w=0. Test the raw texture sentinel
    // before the non-reversed clip-range conversion or any world reconstruction.
    #if UNITY_REVERSED_Z
    if(raw<=0.0)return false;
    #else
    if(raw>=1.0)return false;
    raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
    #endif
    world=ComputeWorldSpacePosition(uv,raw,UNITY_MATRIX_I_VP);
    return all(isfinite(world));
   }
   float AuthoredFrame(float2 uv,float frame)
   {
    float2 cell=float2(fmod(frame,3),2-floor(frame/3));
    uv=clamp(uv,_FlameAtlas_TexelSize.xy*1.5,1-_FlameAtlas_TexelSize.xy*1.5);
    float4 sampled=SAMPLE_TEXTURE2D(_FlameAtlas,sampler_FlameAtlas,(cell+uv)/3);
    return sampled.r*sampled.a;
   }
   float AuthoredParcelMask(float2 uv,float age,float phase)
   {
    if(sin(phase)>0)uv.x=1-uv.x;
    float mask=1;
    if(_DetailMix>.5){uv.x+=sin(uv.y*9+_Time.y*7+phase)*.025*uv.y;mask=SAMPLE_TEXTURE2D_LOD(_DetailTex,sampler_DetailTex,uv,0).r;}
    else
    {
     float frame=saturate(age)*8.999,first=floor(frame);
     mask=lerp(AuthoredFrame(uv,first),AuthoredFrame(uv,min(first+1,8)),smoothstep(0,1,frac(frame)));
    }
    return mask;
   }
   struct A {float3 position:POSITION;float4 centre:TEXCOORD0;float4 axis:TEXCOORD1;float4 up:TEXCOORD2;float4 clipA:TEXCOORD3;float4 clipB:TEXCOORD4;float4 kind:TEXCOORD5;float3 thermal:TEXCOORD6;};
   struct V {float4 position:SV_POSITION;float3 world:TEXCOORD0;nointerpolation float4 centre:TEXCOORD1;nointerpolation float4 axis:TEXCOORD2;nointerpolation float4 up:TEXCOORD3;nointerpolation float4 clipA:TEXCOORD4;nointerpolation float4 clipB:TEXCOORD5;nointerpolation float4 kind:TEXCOORD6;nointerpolation float3 thermal:TEXCOORD7;};
   V Vert(A v){V o;o.position=TransformWorldToHClip(v.position);o.world=v.position;o.centre=v.centre;o.axis=v.axis;o.up=v.up;o.clipA=v.clipA;o.clipB=v.clipB;o.kind=v.kind;o.thermal=v.thermal;return o;}
   float4 Frag(V v):SV_Target
   {
    float3 ro=_WorldSpaceCameraPos;
    float3 rd=normalize(v.world-ro);
    if(unity_OrthoParams.w>.5){rd=-UNITY_MATRIX_I_V._m02_m12_m22;ro=v.world-rd*dot(v.world-ro,rd);}
    v.axis.xyz=normalize(v.axis.xyz);
    v.up.xyz=normalize(v.up.xyz-v.axis.xyz*dot(v.up.xyz,v.axis.xyz));
    float3 side=normalize(cross(v.up.xyz,v.axis.xyz));
    float3 relative=ro-v.centre.xyz;
    float3 local=float3(dot(relative,side),dot(relative,v.up.xyz),dot(relative,v.axis.xyz)/max(v.kind.w,.1))/v.centre.w;
    float3 ray=float3(dot(rd,side),dot(rd,v.up.xyz),dot(rd,v.axis.xyz)/max(v.kind.w,.1))/v.centre.w;
    float aa=dot(ray,ray),bb=dot(local,ray),cc=dot(local,local)-1;
    float discriminant=bb*bb-aa*cc;if(discriminant<=0)discard;
    float root=sqrt(discriminant),enter=max(0,(-bb-root)/aa),exit=(-bb+root)/aa;
    float2 uv=v.position.xy/_ScaledScreenParams.xy;
    float3 opaque;
    if(_DebugView<1.5 && ParcelOpaqueWorld(uv,opaque))exit=min(exit,dot(opaque-ro,rd));
    if(exit<=enter)discard;
    if(_DebugView>.5 && _DebugView<2.5)return float4(.08,.5,.2,0);
    #if defined(ELEMENTAL_TRANSPORTED_HEAT)
    if(_SmokeMode>.5||v.kind.x>.5||_HeatDistortionPixels<=0)discard;
    float middle=(enter+exit)*.5;float3 middleWorld=ro+rd*middle;float3 middleLocal=local+ray*middle;
    if(dot(v.clipA.xyz,v.clipA.xyz)>.5&&dot(float4(middleWorld,1),v.clipA)<0)discard;
    if(dot(v.clipB.xyz,v.clipB.xyz)>.5&&dot(float4(middleWorld,1),v.clipB)<0)discard;
    float mask=smoothstep(0,.65,1-dot(middleLocal,middleLocal))*smoothstep(0,.08,v.axis.w)*(1-smoothstep(.35,.85,v.axis.w));
    mask*=saturate((exit-enter)/.18)*(1-saturate(_EarthSeismicVision));
    float2 flow=float2(sin(middleLocal.z*4+v.up.w+v.kind.z*2.4),cos(middleLocal.x*5-v.up.w-v.kind.z*1.9));
    float2 shifted=clamp(uv+flow*_ElementalHeatSource_TexelSize.xy*min(_HeatDistortionPixels,2),_ElementalHeatSource_TexelSize.xy,1-_ElementalHeatSource_TexelSize.xy);
    float3 shiftedWorld;
    if(ParcelOpaqueWorld(shifted,shiftedWorld) && dot(shiftedWorld-ro,rd)<enter)shifted=uv;
    return float4(SAMPLE_TEXTURE2D_X(_ElementalHeatSource,sampler_LinearClamp,shifted).rgb,mask*.10*saturate(_HeatDistortionPixels));
    #else
    // Two real collision planes prevent density swelling through a contacted wall/corner.
    if(_SmokeMode>.5&&v.kind.x>.5)discard;
    // Contact mixing can cool a young parcel before its age-based tail begins.
    float contacted=step(.5,dot(v.clipA.xyz,v.clipA.xyz));
    float hotAge=v.thermal.z;
    float cooling=1-smoothstep(lerp(.28,.42,contacted),lerp(.62,.76,contacted),v.thermal.x);
    float tail=_TailRefinement*(1-step(.5,v.kind.x))*max(smoothstep(.48,.90,hotAge*_TailAgeScale),cooling);
    float3 radiance=0;float sootIntegral=0,bodyIntegral=0;
    float stepLength=(exit-enter)/10;
    [unroll]for(int i=0;i<10;i++)
    {
     float t=enter+(i+.5)*stepLength;float3 p=ro+rd*t;
     if(dot(v.clipA.xyz,v.clipA.xyz)>.5&&dot(float4(p,1),v.clipA)<0)continue;
     if(dot(v.clipB.xyz,v.clipB.xyz)>.5&&dot(float4(p,1),v.clipB)<0)continue;
     float3 q=local+ray*t;
     float phase=v.up.w;
     float twist=phase+q.z*3.8-v.kind.z*2.4;
     float2 transverse=float2(q.x*cos(twist)-q.y*sin(twist),q.x*sin(twist)+q.y*cos(twist));
     float broad=sin(transverse.x*5+q.z*3+phase)*sin(transverse.y*4-q.z*4-phase*.6);
     float fine=sin(q.x*13+q.y*7+phase+v.kind.z*3.2)*sin(q.y*11-q.z*9-v.kind.z*2.7);
     float field=1-dot(q,q)+broad*lerp(.22,.36,tail)+fine*lerp(.07,.16,tail);
     float density=(1-smoothstep(.68,1,dot(q,q)))*smoothstep(0,.48,field)*smoothstep(0,.035,v.kind.z)*(1-smoothstep(.55,1,hotAge));
     // Break up the late ellipsoid into winding tongues inside its safe support.
     // Middle parcels keep their accepted density and colored extinction.
     // Two separated winding volumes, not a nonzero ellipsoid filled underneath.
     // Their domain is transported/rotated with velocity and clipped by contact planes.
     float2 bend=float2(sin(q.z*4.8+phase-v.kind.z*2.1),cos(q.z*3.7-phase+v.kind.z*1.9))*.18;
     float2 a=(transverse-bend-float2(.26,0))/float2(.22,.36);
     float2 b=(transverse+bend+float2(.26,0))/float2(.18,.30);
     float tongue=max(1-smoothstep(.12,1,dot(a,a)),1-smoothstep(.12,1,dot(b,b)));
     float eroded=smoothstep(.14,.56,field);
     density*=lerp(1,tongue*eroded*.72,tail);
     float authored=1;
     if((_SmokeMode<.5||max(tail,_MediumAuthoredShape)>.001)&&_AuthoredShapeMix>.001&&v.kind.x<.5)
     {
      // Authored shape lives inside transported 3D density, never a camera-facing sheet.
      float2 uvA=float2(transverse.x,q.z)*.5+.5,uvB=float2(transverse.y,q.z)*.5+.5;
      float maskA=AuthoredParcelMask(uvA,hotAge,phase),maskB=AuthoredParcelMask(uvB,hotAge,phase+1.7);
      authored=max(maskA,maskB);
      // A union of projected masks fills each other's transparent cuts. Only
      // ring extinction uses their soft intersection; bright flame emission
      // and the validated ribbon keep the band continuous between tongues.
      float ringMedium=saturate(_MediumAuthoredShape*_RingMediumRefinement)*step(.5,_SmokeMode);
      authored=lerp(authored,sqrt(saturate(maskA*maskB)),ringMedium);
      // Tail extinction must share the emission silhouette; otherwise it fills
      // every transparent gap with a red ellipsoid. Preserve the hot middle.
      float authoredWeight=_AuthoredShapeMix*(_SmokeMode>.5?max(tail,_MediumAuthoredShape):1);
      density*=lerp(1,lerp(_ShapeFloor,1.3,authored),authoredWeight);
     }
     if(_SmokeMode>.5)
     {
      float smokeShape=(1-smoothstep(.48,1,dot(q,q)))*smoothstep(-.1,.62,field);
      smokeShape*=lerp(1,.7,_TailRefinement*smoothstep(.48,.90,v.axis.w));
      sootIntegral+=smokeShape*v.thermal.y*(1-smoothstep(.35,.6,v.thermal.x))*(1-smoothstep(.78,1,v.axis.w))*stepLength;
      bodyIntegral+=density*1.2*smoothstep(.18,.5,v.thermal.x)*stepLength;
      continue;
     }
     float heat=saturate(v.thermal.x*(.45+field*.8));
     density*=1.2*smoothstep(.16,.45,v.thermal.x);
     float3 color=lerp(_Edge.rgb,_Body.rgb,smoothstep(.2,.7,heat));
     color=lerp(color,_Body.rgb,authored*_AuthoredShapeMix*.35);
     color=lerp(color,_Core.rgb*_CoreGlowScale,smoothstep(.65,.98,heat)*(1-smoothstep(.15,.65,hotAge)));
     color=lerp(color,lerp(float3(7,6.5,5.4),float3(.3,1.7,7),saturate(_BlueSource)),saturate(_WhiteSource)*smoothstep(.75,.98,heat)*(1-smoothstep(.03,_WhitePhaseEnd,hotAge)));
     if(v.kind.x>.5){color=_Core.rgb*2.8;density=smoothstep(0,.85,1-dot(q,q))*(1-smoothstep(.3,1,v.axis.w));}
     // Additive radiance remains depth/geometry clipped; zero output alpha preserves scene alpha.
     float muzzle=1+.20*_TailRefinement*(1-smoothstep(.03,.16,hotAge));
     radiance+=density*stepLength*color*_Emission*(v.kind.x>.5?10:muzzle);

    }
    if(_SmokeMode>.5)
    {
     float smokeAlpha=0; // Cooling coverage is rendered by the shared dust material on transported area samples.
     float3 smokeColor=lerp(float3(.19,.175,.16),float3(.3,.22,.16),saturate(v.thermal.x));
          // Colored extinction gives the hot gas a readable body over bright sky.
     // Reuse the existing alpha-blended medium draw; emission remains additive.
     float ringMedium=saturate(_MediumAuthoredShape*_RingMediumRefinement);
     float bodyAlpha=(1-exp(-bodyIntegral*lerp(12.0,4.5,ringMedium)))*.90;
     bodyAlpha*=lerp(1,saturate(_RingMediumVisibility),saturate(_MediumAuthoredShape));
     float3 bodyColor=lerp(float3(.85,.095,.006),float3(1,.46,.025),smoothstep(.4,.9,v.thermal.x));
     float mediumAlpha=1-(1-smokeAlpha)*(1-bodyAlpha);
     float3 mediumColor=(bodyColor*bodyAlpha+smokeColor*smokeAlpha*(1-bodyAlpha))/max(mediumAlpha,.0001);
     return float4(mediumColor,mediumAlpha*(1-saturate(_EarthSeismicVision)));
    }
    // Bound one parcel's energy before additive overlap; preserve chromatic ratios.
    // Tiny sparks keep a separate higher peak, while the broad stream stays orange/gold.
    float peak=max(radiance.r,max(radiance.g,radiance.b));
    float sourceCap=lerp(.30,_SourceRadianceCap,(1-smoothstep(.04,_WhiteCapEnd,hotAge))*saturate(_WhiteSource));
    radiance/=1+peak/(v.kind.x>.5?3.0:sourceCap);
    return float4(radiance,0)*(1-saturate(_EarthSeismicVision));
    #endif
   }

   ENDHLSL
  }

 }
}
