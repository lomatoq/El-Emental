// QA-only categorical pass. Production MPB tint, team colour, textures, fog and lights
// must never change the ID. Source geometry, culling and depth remain authoritative.
Shader "Hidden/Elemental/QA/TemporalRendererId"
{
    Properties { _TemporalRendererId ("Renderer ID", Color) = (0,0,0,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Back ZWrite On ZTest LEqual Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _TemporalRendererId;
            CBUFFER_END
            float4x4 _TemporalViewProjection;
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=mul(_TemporalViewProjection,mul(UNITY_MATRIX_M,float4(input.positionOS.xyz,1)));
                return output;
            }
            float4 Frag(Varyings input) : SV_Target { return _TemporalRendererId; }
            ENDHLSL
        }
    }
}
