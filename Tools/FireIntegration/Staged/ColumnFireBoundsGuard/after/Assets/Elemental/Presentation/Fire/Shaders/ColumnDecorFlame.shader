Shader "Elemental/Fire/Column Decor Flame"
{
    Properties
    {
        [HDR] _EdgeColor("Edge",Color)=(0.65,0.018,0.003,1)
        [HDR] _BodyColor("Body",Color)=(1,0.19,0.008,1)
        [HDR] _CoreColor("Core",Color)=(1,0.72,0.19,1)
        _CoreEmission("Core HDR boost",Range(0,12))=0.65
        _FireTime("Scaled simulation time",Float)=0
        _Opacity("Opacity",Range(0,1))=0.72
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
            Tags { "LightMode"="ElementalValleyCloud" }
            Blend SrcAlpha OneMinusSrcAlpha
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
            #include "Assets/Elemental/Presentation/Fire/Shaders/FireCpuFlame.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _EdgeColor,_BodyColor,_CoreColor;
            float _CoreEmission,_FireTime,_Opacity,_ShapeFPS,_Distortion,_SoftDistance,_NearStart,_NearRange;
            CBUFFER_END
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
                // Expand softly as a parcel cools; fit the final billboard to the CPU's per-axis bound below.
                float expansion=lerp(0.85,1.12,smoothstep(0,0.75,input.fire.y));
                float width=input.fire.w*expansion;
                float role=frac(input.fire.x*2.173);
                float ember=step(.96,role);
                float body=1-step(.66,role);
                width*=lerp(1,1.12,body);
                width*=lerp(1,.13,ember);
                float aspect=input.extent.x*lerp(1.45,.72,body);
                float3 axisExtent=.5*width*(abs(right)+abs(up)*aspect);
                float largestExtent=max(axisExtent.x,max(axisExtent.y,axisExtent.z));
                width*=min(1,.99*input.fire.w*input.extent.x/max(largestExtent,1e-6));
                output.positionWS=input.centreWS+right*(input.uv.x-0.5)*width
                    +up*(input.uv.y-0.5)*width*aspect;
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.uv=input.uv; output.fire=input.fire.xyz;
                return output;
            }
            float4 frag(Varyings input):SV_Target
            {
                float3 color; float alpha;
                float role=frac(input.fire.x*2.173);
                if(role<.66)
                {
                    // Most parcels form a connected luminous body, not dozens of separate spear icons.
                    float2 q=input.uv-float2(.5,.43);
                    float coherent=EF_Noise2(input.positionWS.xz*2.1+float2(_FireTime*.27,-_FireTime*.19));
                    q.x+=(coherent-.5)*.22*saturate(input.uv.y);
                    float egg=1-dot(q/float2(.47,.49),q/float2(.47,.49));
                    egg+=(EF_Noise2(input.positionWS.xy*4.2+float2(0,-_FireTime*1.2))-.5)*.25;
                    float edge=max(fwidth(egg),.035);
                    float coverage=smoothstep(-edge,.55+edge,egg);
                    float age=saturate(input.fire.y);
                    alpha=coverage*smoothstep(0,.08,age)*(1-smoothstep(.32,1,age))*_Opacity*.6;
                    alpha*=smoothstep(0,.08,input.uv.y)*(1-smoothstep(.93,1,input.uv.y));
                    float core=smoothstep(.7,1,egg)*(1-smoothstep(.12,.5,age))*.3;
                    color=lerp(_EdgeColor.rgb,_BodyColor.rgb,smoothstep(.05,.8,egg));
                    color=lerp(color,_CoreColor.rgb,core);
                }
                else
                {
                    EF_Flame_float(input.uv,_FireTime,input.fire.x,input.fire.y,input.fire.z,_Opacity,_ShapeFPS,_Distortion,
                        _EdgeColor.rgb,_BodyColor.rgb,_CoreColor.rgb,_CoreEmission,color,alpha);
                    // The minority of tapered tongues remains brighter, but no white vertical stripe cores.
                    color=min(color,_CoreColor.rgb*1.25);
                    alpha*=.85;
                }
                if(role>.96)
                {
                    float2 q=(input.uv-.5)*2;
                    alpha=(1-smoothstep(.15,1,dot(q,q)))*sin(saturate(input.fire.y)*3.14159)*.8;
                    color=lerp(_CoreColor.rgb,_BodyColor.rgb,saturate(input.fire.y));
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
                return float4(color,alpha);
            }
            ENDHLSL
        }
    }
}
