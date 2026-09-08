Shader "Elemental/UI/Contour Bloom"
{
 Properties
 {
  [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
  _Color ("Bloom tint", Color) = (0.84,1,0.42,1)
  _SpriteRect ("Atlas UV bounds", Vector) = (0,0,1,1)
  _BlurUV ("Blur footprint", Vector) = (.02,.02,0,0)
  _StencilComp ("Stencil comparison", Float) = 8
  _Stencil ("Stencil ID", Float) = 0
  _StencilOp ("Stencil operation", Float) = 0
  _StencilWriteMask ("Stencil write mask", Float) = 255
  _StencilReadMask ("Stencil read mask", Float) = 255
  _ColorMask ("Color mask", Float) = 15
  [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Alpha clipping", Float) = 0
 }
 SubShader
 {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
  Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
  Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
  Blend One OneMinusSrcAlpha
  ColorMask [_ColorMask]
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
   #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
   #include "UnityCG.cginc"
   #include "UnityUI.cginc"
   struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct v2f { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 local:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
   sampler2D _MainTex; float4 _MainTex_TexelSize, _SpriteRect, _BlurUV, _Color, _ClipRect;
   v2f vert(appdata v)
   {
    v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.local=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;return o;
   }
   float4 Sprite(float2 uv)
   {
    float inside=step(_SpriteRect.x,uv.x)*step(_SpriteRect.y,uv.y)*step(uv.x,_SpriteRect.z)*step(uv.y,_SpriteRect.w);
    float2 safeUV=clamp(uv,_SpriteRect.xy+_MainTex_TexelSize.xy*.5,_SpriteRect.zw-_MainTex_TexelSize.xy*.5);
    return tex2D(_MainTex,safeUV)*inside;
   }
   float Bright(float2 uv)
   {
    float4 c=Sprite(uv);return c.a*smoothstep(.28,.8,max(c.r,max(c.g,c.b)));
   }
   float4 frag(v2f i):SV_Target
   {
    float blur=0;
    // Two bounded rings blur bright sprite areas. Samples never bleed from adjacent atlas symbols.
    [unroll] for(int k=0;k<8;k++)
    {
     float angle=k*.785398163;float2 d=float2(cos(angle),sin(angle));
     blur+=Bright(i.uv+d*_BlurUV.xy*.42)*.075;
    }
    [unroll] for(int j=0;j<16;j++)
    {
     float angle=(j+.5)*.392699082;float2 d=float2(cos(angle),sin(angle));
     blur+=Bright(i.uv+d*_BlurUV.xy)*.025;
    }
    // Exclude the original opaque core: this layer cannot show a second copy of the symbol.
    float intensity=blur*(1-Sprite(i.uv).a)*i.color.a;
    #ifdef UNITY_UI_CLIP_RECT
    intensity*=UnityGet2DClipping(i.local.xy,_ClipRect);
    #endif
    float alpha=saturate(intensity*1.8);
    #ifdef UNITY_UI_ALPHACLIP
    clip(alpha-.001);
    #endif
    return float4(i.color.rgb*intensity*3.2,alpha);
   }
   ENDCG
  }
 }
}
