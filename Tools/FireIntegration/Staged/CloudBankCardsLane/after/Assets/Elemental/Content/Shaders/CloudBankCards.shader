Shader "Elemental/Cloud Bank Art"
{
    Properties
    {
        _BaseMap("Cumulus RGBA",2D)="white"{}
        _Opacity("Opacity",Range(0,1))=0.88
        _EdgeFade("Rectangle margin feather",Range(0.01,0.2))=0.07
        _SoftDistance("Scene depth softness metres",Float)=24
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent"}
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            Cull Off ZWrite Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float _Opacity,_EdgeFade,_SoftDistance;
            CBUFFER_END
            struct A{float3 p:POSITION;float2 uv:TEXCOORD0;};
            struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;};
            V vert(A a){V o;o.world=TransformObjectToWorld(a.p);o.p=TransformWorldToHClip(o.world);o.uv=a.uv;return o;}
            half4 frag(V i):SV_Target
            {
                half4 art=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
                float2 border=min(i.uv,1-i.uv);
                float alpha=art.a*_Opacity*smoothstep(0,_EdgeFade,border.x)*smoothstep(0,_EdgeFade,border.y);
                float raw=SampleSceneDepth(GetNormalizedScreenSpaceUV(i.p));
                float sceneEye=LinearEyeDepth(raw,_ZBufferParams);
                if(unity_OrthoParams.w>0.5)
                {
                    #if UNITY_REVERSED_Z
                    raw=1-raw;
                    #endif
                    sceneEye=lerp(_ProjectionParams.y,_ProjectionParams.z,raw);
                }
                float eye=-TransformWorldToView(i.world).z;
                alpha*=saturate((sceneEye-eye)/max(_SoftDistance,0.001));
                Light sun=GetMainLight();
                float3 up=normalize(unity_ObjectToWorld._m01_m11_m21);
                float3 ambient=max(SampleSH(up),float3(0.025,0.035,0.055));
                // Read current lighting only; retain original soft neutral RGBA artwork.
                float3 light=ambient+sun.color*0.65;
                return half4(art.rgb*light,alpha);
            }
            ENDHLSL
        }
    }
}
