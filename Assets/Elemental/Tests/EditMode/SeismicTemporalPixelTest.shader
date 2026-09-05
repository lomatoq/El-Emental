Shader "Hidden/Elemental/Tests/Seismic Temporal Pixel"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/Elemental/Content/Shaders/EarthSeismicVision.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _TestMode;
            float _TestRadialDistance;
            float _TestCurrentRadius;
            float _TestRadiusTravel;
            float _TestWidth;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(uint vertexId : SV_VertexID)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(vertexId);
                output.uv = GetFullScreenTriangleTexCoord(vertexId);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (_TestMode < 0.5) return source;
                if (_TestMode < 1.5) return ApplyEarthSeismicVision(source, input.uv);
                half pulse = _TestMode < 2.5
                    ? (half)EarthSeismicTemporalPulse(
                        _TestRadialDistance, _TestCurrentRadius,
                        _TestRadiusTravel, _TestWidth)
                    : (half)(1.0 - smoothstep(
                        _TestWidth, _TestWidth + 0.32,
                        abs(_TestRadialDistance - _TestCurrentRadius)));
                return half4(pulse.xxx, 1.0h);
            }
            ENDHLSL
        }
    }
}
