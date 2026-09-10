Shader "Elemental/VFX/Gold Respawn Ring"
{
    Properties
    {
        [HDR] _GoldColor("Gold radiance", Color) = (1,.61,.16,1)
        _Intensity("Intensity", Range(0,2)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "GoldRespawnRing"
            Blend One OneMinusSrcAlpha
            Cull Off ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _GoldColor;
                half _Intensity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            { Varyings output; output.positionCS=TransformObjectToHClip(input.positionOS.xyz); output.uv=input.uv; return output; }
            half4 Frag(Varyings input) : SV_Target
            {
                float radius=length(input.uv*2-1);
                float aa=max(fwidth(radius),.001);
                half ring=1-smoothstep(.024,.024+aa,abs(radius-.82));
                half halo=(1-smoothstep(.02,.13,abs(radius-.82)))*.12h;
                half alpha=saturate((ring+halo)*_Intensity);
                // The existing final atmosphere composites this surface once.
                return half4(_GoldColor.rgb*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
