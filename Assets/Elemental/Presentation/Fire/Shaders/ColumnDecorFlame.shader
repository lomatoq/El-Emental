Shader "Elemental/Fire/Column Decor Flame"
{
    Properties
    {
        _FlameAtlas("Wallcoeur 3x3 flame animation",2D)="white" {}
        _UseFlipbook("Use authored flame animation",Float)=0
        [HDR] _EdgeColor("Edge",Color)=(0.65,0.018,0.003,1)
        [HDR] _BodyColor("Body",Color)=(1,0.19,0.008,1)
        [HDR] _CoreColor("Core",Color)=(1,0.72,0.19,1)
        _CoreEmission("Core HDR boost",Range(0,12))=0.65
        _AdditiveEnergyScale("Additive parcel energy",Range(0,1))=0.26
        _FireTime("Scaled simulation time",Float)=0
        _Opacity("Opacity",Range(0,1))=0.72
        _ShapeFPS("Shape FPS",Float)=0
        _Distortion("Distortion",Range(0,2))=1
        _HeatDistortionPixels("Heat haze pixels",Range(0,4))=2
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
            Tags { "LightMode"="ElementalValleyCloud" }
            Blend 0 SrcAlpha One
            Blend 1 One One
            ZWrite Off
            // Existing atmosphere raster pass exposes sampled depth, not a depth attachment.
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/Elemental/Presentation/Fire/Shaders/ColumnRestoredCpuFlame.hlsl"
            #include "Assets/Elemental/Presentation/Fire/Shaders/ColumnRestoredAuthoredFlipbook.hlsl"
            #include "FireDofOutput.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _EdgeColor,_BodyColor,_CoreColor;
            float4 _FlameAtlas_TexelSize;
            float _UseFlipbook;
            float _CoreEmission,_FireTime,_Opacity,_ShapeFPS,_Distortion,_SoftDistance,_NearStart,_NearRange;
            float _HeatDistortionPixels;
            float _AdditiveEnergyScale;
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
                float ember:TEXCOORD3;
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
                // Expand softly as a parcel cools; fit the final billboard to the CPU's per-axis bound below.
                float expansion=lerp(0.85,1.12,smoothstep(0,0.75,input.fire.y));
                float width=input.fire.w*expansion;
                float aspect=input.extent.x;
                if(_UseFlipbook>0.5)
                {
                    width=input.fire.w*EF_AuthoredFlameExpansion(input.fire.y);
                }
                else
                {
                    float role=frac(input.fire.x*2.173);
                    float ember=step(.96,role);
                    float body=1-step(.66,role);
                    width*=lerp(1,1.12,body);
                    width*=lerp(1,.13,ember);
                    aspect*=lerp(1.3,1.0,body);
                }
                float3 axisExtent=.5*width*(abs(right)+abs(up)*aspect);
                float largestExtent=max(axisExtent.x,max(axisExtent.y,axisExtent.z));
                width*=min(1,.99*input.fire.w*input.extent.x/max(largestExtent,1e-6));
                output.positionWS=input.centreWS+right*(input.uv.x-0.5)*width
                    +up*(input.uv.y-0.5)*width*aspect;
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.uv=input.uv; output.fire=input.fire.xyz; output.ember=input.extent.y;
                return output;
            }
            float4 ShadeColumn(Varyings input)
            {
                float3 color; float alpha;
                if(_UseFlipbook>0.5)
                {
                    EF_AuthoredFlame_float(TEXTURE2D_ARGS(_FlameAtlas, sampler_FlameAtlas),
                        _FlameAtlas_TexelSize.xy, input.uv, _FireTime, _Distortion, input.fire.x, input.fire.y, input.fire.z,
                        _Opacity, _EdgeColor.rgb, _BodyColor.rgb, _CoreColor.rgb, _CoreEmission, color, alpha);
                }
                else
                {
                    float role=frac(input.fire.x*2.173);
                    if(role<.66)
                    {
                        // Most parcels form a connected luminous body, not dozens of separate spear icons.
                        float2 q=input.uv-float2(.5,.38);
                        float coherent=EF_Noise2(input.positionWS.xz*1.15+float2(_FireTime*.27,-_FireTime*.19));
                        q.x+=(coherent-.5)*.22*saturate(input.uv.y);
                        float width=lerp(.43,.07,smoothstep(.2,.95,input.uv.y));
                        float egg=1-dot(q/float2(width,.57),q/float2(width,.57));
                        egg+=(EF_Noise2(input.positionWS.xy*1.7+float2(0,-_FireTime*1.7))-.5)*.22;
                        float edge=max(fwidth(egg),.035);
                        float coverage=smoothstep(-edge,.24+edge,egg);
                        float age=saturate(input.fire.y);
                        alpha=coverage*smoothstep(0,.06,age)*(1-smoothstep(.62,1,age))*_Opacity*.9;
                        alpha*=smoothstep(0,.08,input.uv.y)*(1-smoothstep(.93,1,input.uv.y));
                        // Body parcels merge in colour instead of painting a red
                        // ring around every egg. Only minority tongues own an edge.
                        float core=(1-smoothstep(.16,.70,input.uv.y))*.70*saturate(input.fire.z);
                        color=lerp(_BodyColor.rgb,_CoreColor.rgb,core);
                        color+=_CoreColor.rgb*core*_CoreEmission*saturate(input.fire.z);
                    }
                    else
                    {
                        EF_Flame_float(input.uv,_FireTime,input.fire.x,input.fire.y,input.fire.z,_Opacity,_ShapeFPS,_Distortion,
                            _EdgeColor.rgb,_BodyColor.rgb,_CoreColor.rgb,_CoreEmission,color,alpha);
                        // The minority of tapered tongues remains brighter, but no white vertical stripe cores.
                        color=min(color,_CoreColor.rgb*(1+_CoreEmission));
                        alpha*=.85;
                    }
                    if(role>.96)
                    {
                        float2 q=(input.uv-.5)*2;
                        alpha=(1-smoothstep(.15,1,dot(q,q)))*sin(saturate(input.fire.y)*3.14159)*.8;
                        color=lerp(_CoreColor.rgb,_BodyColor.rgb,saturate(input.fire.y));
                    }
                }
                if(input.ember>.5)
                {
                    float2 q=(input.uv-.5)*2;
                    alpha=exp2(-dot(q,q)*5)*smoothstep(0,.08,input.fire.y)*(1-smoothstep(.45,1,input.fire.y));
                    color=lerp(_BodyColor.rgb,_CoreColor.rgb,.65)*(2+_CoreEmission);
                }
                float eye=-TransformWorldToView(input.positionWS).z;
                // Mandatory depth rejection for the post-fog decorative pass.
                if(true)
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
                // Additive parcels accumulate; keep silhouette coverage independent of emitted energy.
                return float4(color*_AdditiveEnergyScale,alpha*(1-saturate(_EarthSeismicVision)));
            }
                        FireDofOutput frag(Varyings input){float4 color=ShadeColumn(input);float2 moments=0;FireDepthAccumulate(moments,input.positionWS,1);return FireDofPack(color,moments);}
            ENDHLSL
        }
        Pass
        {
            Name "FireHeatHaze"
            Tags { "LightMode"="ElementalFireHeat" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex HeatVert
            #pragma fragment HeatFrag
            #define ELEMENTAL_COLUMN_HEAT 1
            #include "Assets/Elemental/Presentation/Fire/Shaders/FireHeatHaze.hlsl"
            ENDHLSL
        }
    }
}
