Shader "Elemental/Earth Indirect Debris"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.22, 0.09, 0.035, 1)
        _Roughness("Roughness", Range(0, 1)) = 0.78
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            StructuredBuffer<float4x4> _EarthDebrisTransforms;
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Roughness;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float4x4 objectToWorld = _EarthDebrisTransforms[input.instanceID];
                float3 positionWS = mul(objectToWorld, float4(input.positionOS, 1)).xyz;
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = normalize(mul((float3x3)objectToWorld, input.normalOS));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                Light mainLight = GetMainLight();
                half ndl = saturate(dot(normalize(input.normalWS), mainLight.direction));
                half diffuse = 0.25h + ndl * 0.75h;
                half3 color = _BaseColor.rgb * diffuse * mainLight.color;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
