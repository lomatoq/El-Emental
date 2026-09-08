Shader "Elemental/Fire/CoherentBody"
{
    Properties
    {
        [HDR] _EdgeColor("Outer tongues",Color)=(.9,.035,.003,1)
        [HDR] _BodyColor("Continuous body",Color)=(1.2,.28,.012,1)
        [HDR] _CoreColor("Hot core",Color)=(1.5,.95,.24,1)
        _CoreEmission("Core HDR boost",Range(0,12))=.65
        _Opacity("Opacity",Range(0,1))=.94
        _FireTime("Scaled simulation time",Float)=0
        _BodyFade("Lifecycle fade",Range(0,1))=1
        _SoftDistance("Contact depth softness",Float)=.06
        _NearStart("Near fade start",Float)=.12
        _NearRange("Near fade range",Float)=.16
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-2" "RenderType"="Transparent" }
        Pass
        {
            Name "ConnectedFireBody"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _EdgeColor, _BodyColor, _CoreColor;
                float _CoreEmission, _Opacity, _FireTime, _BodyFade, _SoftDistance, _NearStart, _NearRange;
            CBUFFER_END
            float _EarthSeismicVision;
            int _ContactCount;
            float4 _ContactPointRadius[8];
            float4 _ContactNormalSkin[8];
            struct Attributes { float3 positionWS:POSITION; float2 uv:TEXCOORD0; float2 shape:TEXCOORD1; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float2 uv:TEXCOORD1; float2 shape:TEXCOORD2; };
            Varyings vert(Attributes v)
            {
                Varyings o; o.positionWS=v.positionWS; o.positionCS=TransformWorldToHClip(v.positionWS);
                o.uv=v.uv; o.shape=v.shape; return o;
            }
            float4 frag(Varyings input):SV_Target
            {
                // Clip the full ribbon width against the same finite contact discs
                // used to redirect its centreline; never extend a wall to an infinite plane.
                for(int c=0;c<min(_ContactCount,8);c++)
                {
                    float4 patch=_ContactPointRadius[c];
                    if(patch.w<=0)continue;
                    float3 normal=normalize(_ContactNormalSkin[c].xyz);
                    float3 q=input.positionWS-patch.xyz;
                    float signedDistance=dot(q,normal);
                    float3 lateral=q-normal*signedDistance;
                    if(dot(lateral,lateral)<patch.w*patch.w)
                        clip(signedDistance-_ContactNormalSkin[c].w);
                }
                float travel=input.uv.y*2.6-_FireTime*3.2;
                float phase=input.shape.y;
                float wave=sin(travel+phase)*.065+sin(travel*.53-phase)*.045;
                float side=abs(input.uv.x+wave*(.3+.7*input.shape.x));
                float aa=max(fwidth(side),.008);
                float coverage=1-smoothstep(.82-aa,1+aa,side);
                // Three bands run continuously down the whole stream, never a core
                // and outline repeated once per particle. Macro motion advects along arc length.
                float body=1-smoothstep(.68,.86,side);
                float core=(1-smoothstep(.24,.53,side))*(1-smoothstep(.45,.94,input.shape.x));
                core*=.82+.12*sin(travel*.65+phase);
                float3 color=lerp(_EdgeColor.rgb,_BodyColor.rgb,body);
                color=lerp(color,_CoreColor.rgb,core);
                color+=_CoreColor.rgb*core*_CoreEmission;
                float alpha=coverage*_Opacity*_BodyFade;
                alpha*=smoothstep(0,.035,input.shape.x)*(1-smoothstep(.88,1,input.shape.x));
                float eye=-TransformWorldToView(input.positionWS).z;
                if(_SoftDistance>0)
                {
                    float raw=SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS));
                    float sceneEye=LinearEyeDepth(raw,_ZBufferParams);
                    if(unity_OrthoParams.w>.5)
                    {
                        #if UNITY_REVERSED_Z
                        raw=1-raw;
                        #endif
                        sceneEye=lerp(_ProjectionParams.y,_ProjectionParams.z,raw);
                    }
                    alpha*=saturate((sceneEye-eye)/max(_SoftDistance,.0001));
                }
                alpha*=saturate((eye-_NearStart)/max(_NearRange,.0001));
                return float4(color,alpha*(1-saturate(_EarthSeismicVision)));
            }
            ENDHLSL
        }
    }
}
