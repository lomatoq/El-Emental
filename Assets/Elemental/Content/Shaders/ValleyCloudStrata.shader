Shader "Elemental/Valley Cloud Strata"
{
    Properties
    {
        _CloudNoise("Existing cloud noise",2D)="gray"{}
        _Density("Extinction per metre",Range(0.005,0.3))=0.024
        _Coverage("Cloud bank coverage",Range(0,1))=0.70
        _StepCount("Bounded ray steps",Range(8,24))=16
        _Drift("Local drift rate",Float)=0.002
        _TopColor("Sunlit top",Color)=(0.94,0.96,1,1)
        _BaseColor("Cool shaded base",Color)=(0.30,0.40,0.53,1)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent"}
        Pass
        {
            Cull Front ZWrite Off ZTest Always
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_CloudNoise); SAMPLER(sampler_CloudNoise);
            CBUFFER_START(UnityPerMaterial)
            float4 _TopColor,_BaseColor;
            float _Density,_Coverage,_StepCount,_Drift;
            CBUFFER_END
            float _EarthSeismicVision;
            struct A {float3 p:POSITION;};
            struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;};
            V vert(A a){V o;o.world=TransformObjectToWorld(a.p);o.p=TransformWorldToHClip(o.world);return o;}
            float noise(float3 p)
            {
                // Continuous adjacent slices of the existing repeatable 2D texture.
                float slice=floor(p.y);float f=frac(p.y);f=f*f*(3-2*f);
                float2 uv=p.xz+slice*float2(0.173,0.317);
                float a=SAMPLE_TEXTURE2D_LOD(_CloudNoise,sampler_CloudNoise,uv,0).r;
                float b=SAMPLE_TEXTURE2D_LOD(_CloudNoise,sampler_CloudNoise,uv+float2(0.173,0.317),0).r;
                return lerp(a,b,f);
            }
            float density(float3 p)
            {
                float radial=length(p.xz)*2;
                float boundary=1-smoothstep(0.67,0.99,radial);
                float3 size=float3(length(unity_ObjectToWorld._m00_m10_m20),length(unity_ObjectToWorld._m01_m11_m21),length(unity_ObjectToWorld._m02_m12_m22));
                // Metre-based features retain their size when the valley footprint expands.
                float3 q=p*size/260+float3(_Time.y*_Drift,0,_Time.y*_Drift*0.43);
                float bank=noise(q);
                float top=lerp(-0.02,0.40,smoothstep(0.30,0.77,bank));
                float height=smoothstep(-0.49,-0.25,p.y)*(1-smoothstep(top-0.14,top+0.04,p.y));
                float body=smoothstep(1-_Coverage,1-_Coverage+0.22,bank);
                if(body*height*boundary<0.001)return 0;
                float detail=noise(q*2.63+float3(0.21,1.3,0.67));
                return body*height*boundary*smoothstep(0.10,0.52,detail);
            }
            half4 frag(V input):SV_Target
            {
                float2 uv=GetNormalizedScreenSpaceUV(input.p);
                float3 origin=_WorldSpaceCameraPos;
                float3 ray=normalize(input.world-origin);
                if(unity_OrthoParams.w>0.5)
                {
                    #if UNITY_REVERSED_Z
                    float nearDepth=1;
                    #else
                    float nearDepth=UNITY_NEAR_CLIP_VALUE;
                    #endif
                    origin=ComputeWorldSpacePosition(uv,nearDepth,UNITY_MATRIX_I_VP);
                    ray=normalize(input.world-origin);
                }
                float3 ro=TransformWorldToObject(origin);
                // Keep t in world metres so density is independent of volume dimensions.
                float3 rd=mul((float3x3)unity_WorldToObject,ray);
                float3 safe=lerp(-max(abs(rd),1e-7),max(abs(rd),1e-7),step(0,rd));
                float3 a=(-0.5-ro)/safe,b=(0.5-ro)/safe;
                float3 lo=min(a,b),hi=max(a,b);
                float enter=max(0,max(lo.x,max(lo.y,lo.z)));
                float leave=min(hi.x,min(hi.y,hi.z));
                float raw=SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
                #endif
                float3 opaque=ComputeWorldSpacePosition(uv,raw,UNITY_MATRIX_I_VP);
                leave=min(leave,max(0,dot(opaque-origin,ray)));
                // Once inside a bank, 1.8km exceeds its optical extinction distance.
                // Bound integration span so grazing rays cannot skip an entire stratum.
                leave=min(leave,enter+1800);
                if(leave<=enter)return 0;
                int steps=clamp((int)_StepCount,8,24);
                float span=(leave-enter)/steps;
                float jitter=frac(sin(dot(floor(input.p.xy),float2(12.9898,78.233)))*43758.5453);
                float trans=1;float3 color=0;
                Light sun=GetMainLight();
                float3 ambient=max(SampleSH(normalize(unity_ObjectToWorld._m01_m11_m21)),float3(0.035,0.04,0.055));
                float3 lighting=ambient+sun.color*0.72;
                [loop] for(int i=0;i<24;i++)
                {
                    if(i>=steps || trans<0.015)break;
                    float t=enter+(i+lerp(0.2,0.8,jitter))*span;
                    float3 p=ro+rd*t;float d=density(p);
                    float alpha=1-exp(-d*_Density*span);
                    float height=saturate((p.y+0.3)*1.7);
                    // One bounded light-direction density probe shapes lit tops and cool pockets.
                    float3 towardSun=mul((float3x3)unity_WorldToObject,sun.direction)*55;
                    float shadeDensity=d>0.001?density(p+towardSun):0;
                    float selfLight=exp(-shadeDensity*3.4);
                    float3 radiance=ambient+sun.color*(0.25+0.75*selfLight);
                    float3 shade=lerp(_BaseColor.rgb,_TopColor.rgb,height)*radiance;
                    color+=trans*alpha*shade;trans*=1-alpha;
                }
                float visible=1-saturate(_EarthSeismicVision);
                return half4(color*visible,(1-trans)*visible);
            }
            ENDHLSL
        }
    }
}
