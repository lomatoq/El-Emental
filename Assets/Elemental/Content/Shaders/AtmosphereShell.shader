Shader "Elemental/Atmosphere Shell"
{
    Properties
    {
        _RayleighColor("Rayleigh", Color) = (0.24,0.52,1,1)
        _MieColor("Mie", Color) = (1,0.48,0.2,1)
        _RayleighStrength("Rayleigh Strength", Range(0,8)) = 2.1
        _MieStrength("Mie Strength", Range(0,8)) = 0.7
        _HorizonStrength("Horizon Strength", Range(0,8)) = 2.4
        _NightOpacity("Night Opacity", Range(0,1)) = 0.12
    }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Atmosphere"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Front
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _RayleighColor, _MieColor;
            float _RayleighStrength, _MieStrength, _HorizonStrength, _NightOpacity;
            float4 _ElementalSunDirection;
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS); return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float3 view = SafeNormalize(_WorldSpaceCameraPos - input.positionWS);
                float3 normal = SafeNormalize(input.normalWS);
                float rim = pow(saturate(1.0 - abs(dot(view, normal))), max(0.2, _HorizonStrength));
                // `view` points from the shell toward the camera, while the sky
                // shaders use the camera ray. Keep the forward Mie lobe on the
                // visible sun side of the atmosphere limb.
                float sun = pow(saturate(dot(-view, SafeNormalize(_ElementalSunDirection.xyz))), 18.0);
                float daylight = saturate(dot(normal, SafeNormalize(_ElementalSunDirection.xyz)) * 0.5 + 0.5);
                half3 color = _RayleighColor.rgb * rim * _RayleighStrength + _MieColor.rgb * sun * rim * _MieStrength;
                half alpha = saturate(rim * lerp(_NightOpacity, 0.72, daylight));
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
