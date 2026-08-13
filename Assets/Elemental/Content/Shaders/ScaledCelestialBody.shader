Shader "Elemental/Scaled Celestial Body"
{
    Properties
    {
        _BaseColor("Surface Color", Color) = (0.52,0.57,0.68,1)
        _NightFill("Night Fill", Range(0,0.25)) = 0.025
        _SunGain("Sun Gain", Range(0,3)) = 1.25
        _RimStrength("Rim Strength", Range(0,1)) = 0.08
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Celestial Phase"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _NightFill;
                half _SunGain;
                half _RimStrength;
            CBUFFER_END
            float4 _ElementalSunDirection;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = SafeNormalize(input.normalWS);
                half3 sun = SafeNormalize(_ElementalSunDirection.xyz);
                half diffuse = smoothstep(-0.025h, 0.16h, dot(normal, sun));
                half3 view = SafeNormalize(_WorldSpaceCameraPos - input.positionWS);
                half rim = pow(saturate(1.0h - dot(normal, view)), 3.5h);
                half latitude = sin(normal.y * 31.0h + normal.x * 17.0h) * 0.035h;
                half illumination = _NightFill + diffuse * _SunGain + rim * _RimStrength;
                return half4(_BaseColor.rgb * max(0.0h, illumination + latitude), _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
