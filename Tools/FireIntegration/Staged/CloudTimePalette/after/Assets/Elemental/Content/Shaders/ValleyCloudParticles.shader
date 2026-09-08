Shader "Elemental/Valley Image Cloud Particles"
{
    Properties{_BaseMap("Cumulus RGBA",2D)="white"{} _Opacity("Opacity",Range(0,1))=0.78 _SoftDistance("Depth softness metres",Float)=35}
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            // Drawn only by the existing atmosphere owner after its veil composition.
            Tags{"LightMode"="ElementalValleyCloud"}
            Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float _Opacity,_SoftDistance;
            CBUFFER_END
            float _ElementalValleyEnabled,_ElementalValleyParticleCloudsEnabled,_ElementalNight01;
            float4x4 _ElementalWorldToValley;
            float4 _ElementalValleyFog,_ElementalValleyFar,_ElementalPlanetCenterRadius;
            float4 _ElementalValleyTimeCloudTop,_ElementalValleyTimeCloudBottom;
            struct A{float3 position:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct V{float4 position:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;half4 color:COLOR;};
            V vert(A a){V o;o.world=TransformObjectToWorld(a.position);o.position=TransformWorldToHClip(o.world);o.uv=a.uv;o.color=a.color;return o;}
            float Above(float low,float high,float distance,float falloff)
            {float x=(high-low)/falloff;return distance*exp(-low/falloff)*(x<0.001?1-x*0.5+x*x/6:(1-exp(-x))/max(x,1e-7));}
            float Integral(float h0,float h1,float distance,float falloff)
            {float low=min(h0,h1),high=max(h0,h1);if(high<=0)return distance;if(low>=0)return Above(low,high,distance,falloff);float below=distance*(-low)/max(high-low,1e-7);return below+Above(0,high,distance-below,falloff);}
            half4 frag(V i):SV_Target
            {
                if(_ElementalValleyEnabled<0.5 || _ElementalValleyParticleCloudsEnabled<0.5)return 0;
                half4 art=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
                float2 edge=min(i.uv,1-i.uv);
                float alpha=art.a*i.color.a*_Opacity*smoothstep(0,0.075,edge.x)*smoothstep(0,0.075,edge.y);
                float2 screen=GetNormalizedScreenSpaceUV(i.position);float raw=SampleSceneDepth(screen);
                float sceneEye=LinearEyeDepth(raw,_ZBufferParams);
                if(unity_OrthoParams.w>0.5)
                {
                    #if UNITY_REVERSED_Z
                    float linearDepth=1-raw;
                    #else
                    float linearDepth=raw;
                    #endif
                    sceneEye=lerp(_ProjectionParams.y,_ProjectionParams.z,linearDepth);
                }
                float eye=-TransformWorldToView(i.world).z;
                alpha*=saturate((sceneEye-eye)/max(1,_SoftDistance));
                float3 local=mul(_ElementalWorldToValley,float4(i.world,1)).xyz;
                float3 origin=mul(_ElementalWorldToValley,float4(_WorldSpaceCameraPos,1)).xyz;
                float distance=length(i.world-_WorldSpaceCameraPos);
                alpha*=smoothstep(_ElementalValleyFar.x,_ElementalValleyFar.x+100,distance);
                float optical=Integral(origin.y-_ElementalValleyFog.x,local.y-_ElementalValleyFog.x,distance,_ElementalValleyFog.y)*_ElementalValleyFog.z;
                alpha*=exp(-max(0,optical));
                // Keep the upper playable planet cap clear even in external reverse views.
                #if UNITY_REVERSED_Z
                bool geometry=raw>0.000001;
                float deviceDepth=raw;
                #else
                bool geometry=raw<0.999999;
                float deviceDepth=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
                #endif
                if(geometry)
                {
                    float3 surface=mul(_ElementalWorldToValley,float4(ComputeWorldSpacePosition(screen,deviceDepth,UNITY_MATRIX_I_VP),1)).xyz;
                    float r=_ElementalPlanetCenterRadius.w;
                    float protection=smoothstep(r+80,r+140,length(surface));
                    alpha*=lerp(1,protection,smoothstep(r*0.45,r*0.75,surface.y));
                }
                float day=saturate(1-_ElementalNight01);
                float3 tint=lerp(_ElementalValleyTimeCloudBottom.rgb,_ElementalValleyTimeCloudTop.rgb,smoothstep(.15,.85,i.uv.y));
                return half4(art.rgb*i.color.rgb*tint,alpha);
            }
            ENDHLSL
        }
    }
}
