Shader "Elemental/Fire/Shared Dust Smoke"
{
    Properties
    {
        _BaseMap("Soft Particle Alpha", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1.0, 0.86, 0.58, 0.62)
        _Brightness("Brightness", Range(0, 4)) = 1.55
        _FlipbookBlending("Particle Sheet Frame Blending", Range(0, 1)) = 0
        _FlipbookColumns("Particle Sheet Columns", Float) = 1
        _FlipbookRows("Particle Sheet Rows", Float) = 1
        _FlipbookSpeed("Sheet Playback Speed", Range(1, 5)) = 1
        _SoftMap("Original Soft Dust", 2D) = "white" {}
        _SoftMix("Original Soft Dust Mix", Range(0, 1)) = 0
        _EdgeSoftness("Sheet edge feather in tile UV",Range(0,0.08))=0
        _FlowStrength("Continuous smoke deformation",Range(0,0.08))=0
        [HideInInspector] _SurfaceWisp("Curved surface wisp",Float)=0
        [HideInInspector] _WispWaveHeight("Wisp wave height",Float)=.055
        _NightVisibility("Night Ambient Visibility", Range(0.02, 0.2)) = 0.09
        _ShadowFloor("Shadow Visibility", Range(0, 0.25)) = 0.03
        _ShadowSoftnessMeters("Dust Volume Shadow Width (m)", Range(0, 1.5)) = 0.35
        _ProceduralRadialMask("Procedural Radial Mask", Range(0, 1)) = 1
        _SoftParticleNearDistance("Soft Particle Near Distance", Range(0, 4)) = 0
        _SoftParticleInvDistance("Soft Particle Inverse Distance", Range(0.1, 12)) = 3.5
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+6"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "Fire Cooling Dust" Tags {"LightMode"="ElementalValleyCloud"}
            Blend 0 One OneMinusSrcAlpha
            Blend 1 One One
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma editor_sync_compilation
            #pragma target 3.5
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma vertex Vert
            #pragma fragment FireDustDofFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #define ELEMENTAL_FIRE_DUST 1
            #include "../../../Content/Shaders/ElementalDustShared.hlsl"
            #include "../Shaders/FireDofOutput.hlsl"
            FireDofOutput FireDustDofFrag(Varyings i){float4 color=Frag(i);float2 moments=0;FireDepthAccumulate(moments,i.positionWS,1);return FireDofPack(color,moments);}
            ENDHLSL
        }
    }
}
