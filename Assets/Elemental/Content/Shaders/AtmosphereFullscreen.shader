Shader "Elemental/Atmosphere Fullscreen"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Depth Aware Atmosphere"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4 _ElementalSunDirection;
            float4 _ElementalPlanetCenterRadius;
            float4 _ElementalAtmosphereParams;
            float4 _ElementalRayleighColor;
            float4 _ElementalMieColor;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float4 clip = float4(uv * 2.0 - 1.0, 1.0, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                    clip.y = -clip.y;
                #endif
                float4 worldFar = mul(UNITY_MATRIX_I_VP, clip);
                float3 ray = SafeNormalize(worldFar.xyz / max(0.00001, worldFar.w) - _WorldSpaceCameraPos);
                float3 center = _ElementalPlanetCenterRadius.xyz;
                float innerRadius = max(0.01, _ElementalPlanetCenterRadius.w);
                float outerRadius = innerRadius * max(1.001, _ElementalAtmosphereParams.x);
                float3 offset = _WorldSpaceCameraPos - center;
                float b = dot(offset, ray);
                float c = dot(offset, offset) - outerRadius * outerRadius;
                float discriminant = b * b - c;
                if (discriminant <= 0.0) return source;
                float root = sqrt(discriminant);
                float enter = max(0.0, -b - root);
                float leave = max(0.0, -b + root);
                float segment = max(0.0, leave - enter);
                float rawDepth = SampleSceneDepth(uv);
                float eyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                if (rawDepth > 0.00001 && rawDepth < 0.99999) segment = min(segment, eyeDepth);
                float thickness = max(0.01, outerRadius - innerRadius);
                float density = saturate(segment / (thickness * 5.0));
                float3 samplePoint = _WorldSpaceCameraPos + ray * (enter + segment * 0.5);
                float3 radial = SafeNormalize(samplePoint - center);
                float horizon = pow(saturate(1.0 - abs(dot(ray, radial))), max(0.2, _ElementalAtmosphereParams.w));
                float3 sunDirection = SafeNormalize(_ElementalSunDirection.xyz);
                float day = saturate(dot(radial, sunDirection) * 0.5 + 0.5);
                float forwardMie = pow(saturate(dot(ray, sunDirection)), 18.0);
                half3 scatter = _ElementalRayleighColor.rgb * _ElementalAtmosphereParams.y;
                scatter += _ElementalMieColor.rgb * forwardMie * _ElementalAtmosphereParams.z;
                half alpha = saturate(density * lerp(_ElementalAtmosphereParams.w * 0.08, 0.5, day) * (0.3 + horizon));
                return half4(source.rgb + scatter * alpha, source.a);
            }
            ENDHLSL
        }
    }
}
