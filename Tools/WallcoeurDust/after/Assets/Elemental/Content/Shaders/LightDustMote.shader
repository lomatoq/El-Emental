Shader "Elemental/Light Dust Mote"
{
    Properties
    {
        _BaseMap("Soft Particle Alpha", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1.0, 0.86, 0.58, 0.62)
        _Brightness("Brightness", Range(0, 4)) = 1.55
        _FlipbookBlending("Particle Sheet Frame Blending", Range(0, 1)) = 0
        _FlipbookColumns("Particle Sheet Columns", Float) = 1
        _FlipbookRows("Particle Sheet Rows", Float) = 1
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
                half _FlipbookBlending;
                half _FlipbookColumns;
                half _FlipbookRows;
                half _ShadowFloor;
                half _ProceduralRadialMask;
                half _SoftParticleNearDistance;
                half _SoftParticleInvDistance;
            CBUFFER_END
            float _ElementalNight01;

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                // Shuriken UV + UV2 pack current/next frame coordinates in
                // TEXCOORD0; AnimBlend is TEXCOORD1.x. The sheet module owns
                // frame selection, never a global clock shared by all puffs.
                float4 uv : TEXCOORD0;
                float animBlend : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 uv : TEXCOORD1;
                half animBlend : TEXCOORD2;
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
                output.animBlend = input.animBlend;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 centered = input.uv.xy * 2.0 - 1.0;
                float radiusSquared = dot(centered, centered);
                half radialMask = saturate((1.0h - (half)radiusSquared) * 4.0h);
                radialMask *= radialMask;
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.xy);
                if (_FlipbookBlending > .5h)
                {
                    half4 nextSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.zw);
                    // Interpolate premultiplied frames: transparent black texels
                    // must not darken the silhouette during cross-fade.
                    half blend = saturate(input.animBlend);
                    half frameAlpha = lerp(baseSample.a, nextSample.a, blend);
                    half3 frameRgb = lerp(baseSample.rgb * baseSample.a,
                        nextSample.rgb * nextSample.a, blend);
                    baseSample = half4(frameRgb / max(frameAlpha, .0001h), frameAlpha);
                }
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
                // The gameplay ambient/key can be deliberately held bright at night.
                // Cosmetic dust follows the celestial exposure so it cannot glow white.
                half nightExposure=lerp(1.0h,.08h,saturate(_ElementalNight01));
                half3 lighting = saturate(ambientLighting + directLighting)*nightExposure;

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
