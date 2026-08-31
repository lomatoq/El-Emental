Shader "Hidden/Elemental/Duel Shadow Debug"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Duel Shadow Only"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/Elemental/Content/GraphicsVNext/Rendering/ElementalDuelShadow.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float rawDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool hasGeometry = rawDepth > 0.00001;
                    float deviceDepth = rawDepth;
                #else
                    bool hasGeometry = rawDepth < 0.99999;
                    float deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif
                if (!hasGeometry)
                    return half4(0.0h, 0.0h, 0.0h, 1.0h);

                float3 positionWS = ComputeWorldSpacePosition(
                    uv,
                    deviceDepth,
                    UNITY_MATRIX_I_VP);
                half shadow = ElementalSampleDuelShadow(positionWS);
                return half4(shadow, shadow, shadow, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
