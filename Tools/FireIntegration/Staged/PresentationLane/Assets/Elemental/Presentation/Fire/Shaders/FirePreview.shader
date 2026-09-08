Shader "Elemental/Fire/ReferencePreview"
{
    Properties
    {
        [HDR] _EdgeColor("Edge", Color) = (0.65, 0.018, 0.003, 1)
        [HDR] _BodyColor("Body", Color) = (1, 0.19, 0.008, 1)
        [HDR] _CoreColor("Core", Color) = (1, 0.72, 0.19, 1)
        _CoreEmission("Core HDR boost", Range(0, 12)) = 3
        _FireTime("Scaled simulation time", Float) = 0
        _Phase("Stable phase, radians", Float) = 1.3
        _Age01("Normalized age", Range(0, 1)) = 0.35
        _Heat01("Heat", Range(0, 1)) = 1
        _Opacity("Opacity", Range(0, 1)) = 0.9
        _ShapeFPS("Shape FPS; 0 = smooth", Float) = 0
        _Distortion("Distortion", Range(0, 2)) = 1
        _SoftDistance("Depth fade metres; 0 = off", Float) = 0.08
        _NearStart("Near fade start metres", Float) = 0.12
        _NearRange("Near fade range metres", Float) = 0.16
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "FireUnlit"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/Elemental/Presentation/Fire/Shaders/FireFlame.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _EdgeColor, _BodyColor, _CoreColor;
                float _CoreEmission, _FireTime, _Phase, _Age01;
                float _Heat01, _Opacity, _ShapeFPS, _Distortion;
                float _SoftDistance, _NearStart, _NearRange;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                return output;
            }
            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 color;
                float alpha;
                EF_Flame_float(input.uv, _FireTime, _Phase, _Age01,
                    _Heat01, _Opacity, _ShapeFPS, _Distortion,
                    _EdgeColor.rgb, _BodyColor.rgb, _CoreColor.rgb,
                    _CoreEmission, color, alpha);
                float eye = -TransformWorldToView(input.positionWS).z;
                if (_SoftDistance > 0.0)
                {
                    float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    float raw = SampleSceneDepth(screenUV);
                    float sceneEye;
                    if (unity_OrthoParams.w > 0.5)
                    {
                        #if UNITY_REVERSED_Z
                            raw = 1.0 - raw;
                        #endif
                        sceneEye = lerp(_ProjectionParams.y, _ProjectionParams.z, raw);
                    }
                    else sceneEye = LinearEyeDepth(raw, _ZBufferParams);
                    alpha *= saturate((sceneEye - eye) / max(_SoftDistance, 1e-4));
                }
                alpha *= saturate((eye - _NearStart) / max(_NearRange, 1e-4));
                return float4(color, alpha);
            }
            ENDHLSL
        }
    }
}
