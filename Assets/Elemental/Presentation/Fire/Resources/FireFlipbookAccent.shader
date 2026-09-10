Shader "Elemental/Fire/TransportedFlipbookAccent"
{
 Properties{[HideInInspector]_BurnPalette("Attached burning thermal palette",Range(0,1))=0 _FireMotion("Hovl fire motion",2D)="gray"{} _SmokeMotion("Hovl smoke motion",2D)="gray"{} _FlameAtlas("Hovl fire RGBA 8x8",2D)="white"{} _SmokeAtlas("Hovl smoke RGBA 8x8",2D)="white"{} _HeatDistortionPixels("Accent heat pixels",Range(0,1))=.6}
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" "RenderType"="Transparent"}
  Pass
  {
   Name "FireAtlasAccent" Tags{"LightMode"="ElementalValleyCloud"}
   Blend 0 SrcAlpha OneMinusSrcAlpha
   Blend 1 One One Cull Off ZWrite Off ZTest Always
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #define FIRE_ACCENT_DOF 1
   #include "../Shaders/FireFlipbookAccent.hlsl"
   ENDHLSL
  }
  Pass
  {
   Name "FireAtlasHeat" Tags{"LightMode"="ElementalFireHeat"}
   Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off ZTest Always
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #define FIRE_ACCENT_HEAT 1
   #include "../Shaders/FireFlipbookAccent.hlsl"
   ENDHLSL
  }
 }
}
