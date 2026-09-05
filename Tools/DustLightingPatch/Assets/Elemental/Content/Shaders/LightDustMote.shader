Shader "Elemental/Light Dust Mote"
{
    Properties
    {
        _BaseMap("Soft Particle Alpha", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1.0, 0.86, 0.58, 0.62)
        _Brightness("Brightness", Range(0, 4)) = 1.55
        _ShadowFloor("Shadow Visibility", Range(0, 0.25)) = 0.03
        _ProceduralRadialMask("Procedural Radial Mask", Range(0, 1)) = 1
        _SoftParticleNearDistance("Soft Particle Near Distance", Range(0, 4)) = 0
        _SoftParticleInvDistance("Soft Particle Inverse Distance", Range(0.1, 12)) = 3.5
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "Sunlit Dust"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Brightness;
                half _ShadowFloor;
                half _ProceduralRadialMask;
                half _SoftParticleNearDistance;
                half _SoftParticleInvDistance;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 centered = input.uv * 2.0 - 1.0;
                float radiusSquared = dot(centered, centered);
                half radialMask = saturate((1.0h - (half)radiusSquared) * 4.0h);
                radialMask *= radialMask;
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half mote = baseSample.a *
                    lerp(1.0h, radialMask, _ProceduralRadialMask);
                clip(mote - 0.001h);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                // Dust is volumetric and has no stable billboard surface normal.
                // Use the real directional radiance plus the scene SH ambient,
                // instead of an invented N.L or a clock-derived day/night tint.
                half shadow = lerp(_ShadowFloor, 1.0h, mainLight.shadowAttenuation);
                half3 directLighting = mainLight.color *
                    (mainLight.distanceAttenuation * shadow);
                half3 ambientLighting = max(0.0h, SampleSH(half3(0.0h, 1.0h, 0.0h)));
                // Neutral white key light at intensity one reproduces the former
                // unlit material. Ambient fills shadows/night but cannot brighten
                // ordinary daylight dust past its authored albedo.
                half3 lighting = saturate(ambientLighting + directLighting);

                float2 screenUv = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUv), _ZBufferParams);
                float particleDepth = -TransformWorldToView(input.positionWS).z;
                half softFade = saturate(
                    (sceneDepth - particleDepth - _SoftParticleNearDistance) *
                    _SoftParticleInvDistance);

                // Lighting changes radiance, not the authored cloud density/fade.
                // This preserves effect silhouette and opacity at dusk and night.
                half alpha = mote * input.color.a * _BaseColor.a * softFade;
                // Keep the complete legacy Particles/Unlit texture contract.
                // RumbleDustSoft is currently white RGB with authored alpha, but
                // sampling RGB prevents a future colored dust texture from being
                // silently flattened by this lighting shader.
                half3 color = baseSample.rgb * input.color.rgb * _BaseColor.rgb * lighting * _Brightness;
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
