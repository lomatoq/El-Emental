Shader "Hidden/Elemental/Cinematic Depth Of Field"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        TEXTURE2D_X(_EarthDofPackedTexture);
        TEXTURE2D_X(_EarthDofNearTexture);
        TEXTURE2D_X(_EarthDofFarTexture);
        float4 _EarthDofParams;       // sharp near/far, near/far transition
        float4 _EarthDofGatherParams; // half-resolution radius in pixels
        float _EarthDofDebugMode;

        static const float2 kElementalDofDisk[12] =
        {
            float2( 0.0000,  0.0000),
            float2( 0.5278, -0.0859),
            float2(-0.0401,  0.5361),
            float2(-0.6704, -0.1799),
            float2( 0.1610, -0.7080),
            float2( 0.7896,  0.3978),
            float2(-0.4332,  0.8174),
            float2(-0.9162,  0.3437),
            float2(-0.6504, -0.7318),
            float2( 0.2656,  0.9391),
            float2( 0.8998, -0.4198),
            float2( 0.1057, -0.9742)
        };

        float SignedCoc(float eyeDepth)
        {
            float sharpNear = max(0.0001, _EarthDofParams.x);
            float sharpFar = max(sharpNear, _EarthDofParams.y);
            if (eyeDepth < sharpNear)
                return -saturate(
                    (sharpNear - eyeDepth) /
                    max(0.0001, _EarthDofParams.z));
            if (eyeDepth > sharpFar)
                return saturate(
                    (eyeDepth - sharpFar) /
                    max(0.0001, _EarthDofParams.w));
            return 0.0;
        }

        float CocAt(float2 uv)
        {
            float rawDepth = SampleSceneDepth(saturate(uv));
            return SignedCoc(LinearEyeDepth(rawDepth, _ZBufferParams));
        }

        half4 FragPack(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            float2 quarterTexel = _BlitTexture_TexelSize.xy * 0.5;
            float2 uv0 = uv + float2(-quarterTexel.x, -quarterTexel.y);
            float2 uv1 = uv + float2( quarterTexel.x, -quarterTexel.y);
            float2 uv2 = uv + float2(-quarterTexel.x,  quarterTexel.y);
            float2 uv3 = uv + float2( quarterTexel.x,  quarterTexel.y);
            half3 c0 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv0).rgb;
            half3 c1 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv1).rgb;
            half3 c2 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv2).rgb;
            half3 c3 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv3).rgb;
            float coc0 = CocAt(uv0);
            float coc1 = CocAt(uv1);
            float coc2 = CocAt(uv2);
            float coc3 = CocAt(uv3);

            float nearestCoc = min(min(coc0, coc1), min(coc2, coc3));
            float farthestCoc = max(max(coc0, coc1), max(coc2, coc3));
            float packedCoc = nearestCoc < -0.001 ? nearestCoc : farthestCoc;
            half3 color = (c0 + c1 + c2 + c3) * 0.25h;
            // At a subpixel foreground edge, carry the closest sample's colour
            // with its negative CoC. This prevents the downsample from baking a
            // bright background halo into the near layer before dilation.
            if (packedCoc < 0.0)
            {
                color = c0;
                if (coc1 < coc0) color = c1;
                if (coc2 < min(coc0, coc1)) color = c2;
                if (coc3 < min(min(coc0, coc1), coc2)) color = c3;
            }
            return half4(color, packedCoc);
        }

        half4 FragNear(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            float2 radius = _BlitTexture_TexelSize.xy * _EarthDofGatherParams.x;
            half3 sum = 0.0h;
            float sumWeight = 0.0;
            float maxCoverage = 0.0;
            float averageCoverage = 0.0;
            [unroll]
            for (int index = 0; index < 12; index++)
            {
                half4 tap = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    uv + kElementalDofDisk[index] * radius);
                float weight = saturate(-tap.a);
                // Slightly favour the centre while retaining foreground dilation.
                float shapedWeight = weight * (index == 0 ? 1.35 : 1.0);
                sum += tap.rgb * (half)shapedWeight;
                sumWeight += shapedWeight;
                maxCoverage = max(maxCoverage, weight);
                averageCoverage += weight;
            }
            half3 center = SAMPLE_TEXTURE2D_X(
                _BlitTexture, sampler_LinearClamp, uv).rgb;
            half3 color = sumWeight > 0.0001
                ? sum / (half)sumWeight
                : center;
            float coverage = saturate(
                maxCoverage * 0.82 + (averageCoverage / 12.0) * 0.46);
            return half4(color, coverage);
        }

        half4 FragFar(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            half4 center = SAMPLE_TEXTURE2D_X(
                _BlitTexture, sampler_LinearClamp, uv);
            float centerFar = saturate(center.a);
            if (centerFar <= 0.001)
                return half4(center.rgb, 0.0h);

            float2 radius = _BlitTexture_TexelSize.xy *
                            (_EarthDofGatherParams.x * centerFar);
            half3 sum = 0.0h;
            float sumWeight = 0.0;
            [unroll]
            for (int index = 0; index < 12; index++)
            {
                half4 tap = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    uv + kElementalDofDisk[index] * radius);
                float tapFar = saturate(tap.a);
                // Depth-order rejection: an in-focus or foreground tap is never
                // allowed to contaminate a background blur gather.
                float compatible = step(centerFar * 0.42, tapFar);
                float weight = max(0.001, tapFar) * compatible;
                sum += tap.rgb * (half)weight;
                sumWeight += weight;
            }
            half3 color = sumWeight > 0.0001
                ? sum / (half)sumWeight
                : center.rgb;
            return half4(color, centerFar);
        }

        half4 FragComposite(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            half4 source = SAMPLE_TEXTURE2D_X(
                _BlitTexture, sampler_LinearClamp, uv);
            half4 packed = SAMPLE_TEXTURE2D_X(
                _EarthDofPackedTexture, sampler_LinearClamp, uv);
            half4 nearLayer = SAMPLE_TEXTURE2D_X(
                _EarthDofNearTexture, sampler_LinearClamp, uv);
            half4 farLayer = SAMPLE_TEXTURE2D_X(
                _EarthDofFarTexture, sampler_LinearClamp, uv);

            // Classify the output pixel from the real full-resolution depth.
            // packed.a represents a conservative 2x2 block and nearLayer.a is
            // spatially dilated, so neither is allowed to soften a pixel whose
            // own depth lies inside the two-subject sharp envelope.
            float centerCoc = CocAt(uv);
            float centerIsSharp = abs(centerCoc) <= 0.001 ? 1.0 : 0.0;
            float nearWeight = saturate(nearLayer.a) * (1.0 - centerIsSharp);
            float farWeight = centerCoc > 0.0
                ? saturate(farLayer.a * centerCoc) * (1.0 - nearWeight)
                : 0.0;
            half3 composed = lerp(source.rgb, farLayer.rgb, (half)farWeight);
            composed = lerp(composed, nearLayer.rgb, (half)nearWeight);

            if (_EarthDofDebugMode > 0.5 && _EarthDofDebugMode < 1.5)
                return half4(saturate(-centerCoc), 0.045h, saturate(centerCoc), 1.0h);
            if (_EarthDofDebugMode >= 1.5 && _EarthDofDebugMode < 2.5)
                return half4(nearLayer.rgb, 1.0h);
            if (_EarthDofDebugMode >= 2.5 && _EarthDofDebugMode < 3.5)
                return half4(farLayer.rgb, 1.0h);
            if (_EarthDofDebugMode >= 3.5)
                return half4(nearWeight, farWeight, 0.0h, 1.0h);
            return half4(composed, source.a);
        }
        ENDHLSL

        Pass
        {
            Name "Signed CoC Downsample"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragPack
            ENDHLSL
        }
        Pass
        {
            Name "Near Foreground Gather"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragNear
            ENDHLSL
        }
        Pass
        {
            Name "Far Background Gather"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragFar
            ENDHLSL
        }
        Pass
        {
            Name "Foreground Safe Composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            ENDHLSL
        }
    }
    FallBack Off
}
