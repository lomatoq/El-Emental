Shader "Elemental/Fire/CpuMeshFlame"
{
    Properties
    {
        _FlameAtlas("Wallcoeur 3x3 flame animation",2D)="white" {}
        _UseFlipbook("Use authored flame animation",Float)=0
        [HDR] _EdgeColor("Edge",Color)=(0.65,0.018,0.003,1)
        [HDR] _BodyColor("Body",Color)=(1,0.19,0.008,1)
        [HDR] _CoreColor("Core",Color)=(1,0.72,0.19,1)
        _CoreEmission("Core HDR boost",Range(0,12))=0.65
        _FireTime("Scaled simulation time",Float)=0
        _Opacity("Opacity",Range(0,1))=0.72
        _ParcelOpacityScale("Secondary parcel contribution",Range(0,1))=1
        _ShapeFPS("Shape FPS",Float)=0
        _Distortion("Distortion",Range(0,2))=1
        _SoftDistance("Depth fade metres; requires camera depth",Float)=0
        _NearStart("Near fade start metres",Float)=0.12
        _NearRange("Near fade range metres",Float)=0.16
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "FireMeshUnlit"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/Elemental/Presentation/Fire/Shaders/FireCpuFlame.hlsl"
            #include "Assets/Elemental/Presentation/Fire/Shaders/FireAuthoredFlipbook.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _EdgeColor,_BodyColor,_CoreColor;
            float4 _FlameAtlas_TexelSize;
            float _UseFlipbook;
            float _CoreEmission,_FireTime,_Opacity,_ShapeFPS,_Distortion,_SoftDistance,_NearStart,_NearRange,_ParcelOpacityScale;
            CBUFFER_END
            TEXTURE2D(_FlameAtlas); SAMPLER(sampler_FlameAtlas);
            float _EarthSeismicVision;
            struct Attributes
            {
                float3 centreWS:POSITION;
                float2 uv:TEXCOORD0;
                float4 fire:TEXCOORD1; // persistent phase, normalized age, heat, width
                float2 extent:TEXCOORD2; // aspect, reserved
                float3 directionWS:TEXCOORD3;
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float2 uv:TEXCOORD0;
                float3 positionWS:TEXCOORD1;
                float3 fire:TEXCOORD2;
            };
            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 right=UNITY_MATRIX_I_V._m00_m10_m20;
                float3 cameraUp=UNITY_MATRIX_I_V._m01_m11_m21;
                float3 viewAxis=UNITY_MATRIX_I_V._m02_m12_m22;
                float3 travel=input.directionWS-viewAxis*dot(input.directionWS,viewAxis);
                float3 up=normalize(travel+cameraUp*1.5+float3(0,0.0001,0));
                right=normalize(cross(up,viewAxis));
                // Expand softly as a parcel cools. Padding remains below the CPU width*aspect bound.
                float expansion=lerp(0.85,1.12,smoothstep(0,0.75,input.fire.y));
                if(_UseFlipbook>0.5) expansion=EF_AuthoredFlameExpansion(input.fire.y);
                float width=input.fire.w*expansion;
                output.positionWS=input.centreWS+right*(input.uv.x-0.5)*width
                    +up*(input.uv.y-0.5)*width*input.extent.x;
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.uv=input.uv; output.fire=input.fire.xyz;
                return output;
            }
            float4 frag(Varyings input):SV_Target
            {
                float3 color; float alpha;
                if(_UseFlipbook>0.5)
                {
                    EF_AuthoredFlame_float(TEXTURE2D_ARGS(_FlameAtlas, sampler_FlameAtlas),
                        _FlameAtlas_TexelSize.xy, input.uv, input.fire.x, input.fire.y, input.fire.z,
                        _Opacity, _EdgeColor.rgb, _BodyColor.rgb, _CoreColor.rgb, _CoreEmission, color, alpha);
                }
                else EF_Flame_float(input.uv,_FireTime,input.fire.x,input.fire.y,input.fire.z,_Opacity,_ShapeFPS,_Distortion,
                    _EdgeColor.rgb,_BodyColor.rgb,_CoreColor.rgb,_CoreEmission,color,alpha);
                float eye=-TransformWorldToView(input.positionWS).z;
                if(_SoftDistance>0)
                {
                    float raw=SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS));
                    float sceneEye=LinearEyeDepth(raw,_ZBufferParams);
                    if(unity_OrthoParams.w>0.5)
                    {
                        #if UNITY_REVERSED_Z
                        raw=1-raw;
                        #endif
                        sceneEye=lerp(_ProjectionParams.y,_ProjectionParams.z,raw);
                    }
                    alpha*=saturate((sceneEye-eye)/max(_SoftDistance,1e-4));
                }
                alpha*=saturate((eye-_NearStart)/max(_NearRange,1e-4));
                alpha*=_ParcelOpacityScale*(1-saturate(_EarthSeismicVision));
                return float4(color,alpha);
            }
            ENDHLSL
        }
    }
}
