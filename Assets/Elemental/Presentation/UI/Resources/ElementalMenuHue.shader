Shader "Elemental/UI/Menu Hue"
{
 Properties
 {
  [PerRendererData] _MainTex("Sprite",2D)="white"{}
  _Color("Tint",Color)=(1,1,1,1)
  _HueShift("Hue shift",Float)=0
  _SourceHue("Authored plate hue",Float)=.2
  _Saturation("Saturation",Float)=1
  _StencilComp("Stencil comparison",Float)=8
  _Stencil("Stencil ID",Float)=0
  _StencilOp("Stencil operation",Float)=0
  _StencilWriteMask("Stencil write mask",Float)=255
  _StencilReadMask("Stencil read mask",Float)=255
  _ColorMask("Color mask",Float)=15
  [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Alpha clipping",Float)=0
 }
 SubShader
 {
  Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True"}
  Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
  Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
  Blend SrcAlpha OneMinusSrcAlpha
  ColorMask [_ColorMask]
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
   #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
   #include "UnityCG.cginc"
   #include "UnityUI.cginc"
   struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct v2f {float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float4 local:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
   sampler2D _MainTex;float4 _Color,_ClipRect,_TextureSampleAdd;float _HueShift,_Saturation,_SourceHue;
   v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.local=v.vertex;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;return o;}
   float4 frag(v2f i):SV_Target
   {
    float4 c=tex2D(_MainTex,i.uv)+_TextureSampleAdd;
    float4 k=float4(0,-1.0/3.0,2.0/3.0,-1);
    float4 p=lerp(float4(c.bg,k.wz),float4(c.gb,k.xy),step(c.b,c.g));
    float4 q=lerp(float4(p.xyw,c.r),float4(c.r,p.yzx),step(p.x,c.r));
    float d=q.x-min(q.w,q.y);float h=abs(q.z+(q.w-q.y)/(6*d+1e-10));float s=d/(q.x+1e-10);
    // The artwork combines green glass and golden foil. Keep their value and
    // saturation, but bring both into the chosen school's hue family.
    float hue=abs(_HueShift)<.00001?h:_SourceHue+_HueShift+clamp(h-_SourceHue,-.07,.07)*.18;
    float3 rgb=saturate(abs(frac(hue+float3(0,2.0/3.0,1.0/3.0))*6-3)-1);
    c.rgb=q.x*lerp(1.0.xxx,rgb,s*_Saturation);c*=i.color;
    #ifdef UNITY_UI_CLIP_RECT
    c.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
    #endif
    #ifdef UNITY_UI_ALPHACLIP
    clip(c.a-.001);
    #endif
    return c;
   }
   ENDCG
  }
 }
}
