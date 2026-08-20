Shader "Elemental/Procedural Stars"
{
    Properties
    {
        _Tint("Nebula Tint", Color)=(0.42,0.58,1,1)
        _ZenithColor("Zenith", Color)=(0.075,0.31,0.72,1)
        _HorizonColor("Horizon", Color)=(0.56,0.79,0.98,1)
        _StarVisibility("Star Visibility", Range(0,1))=0
        _Exposure("Star Exposure", Range(0,3))=1.15
        _MilkyWayStrength("Milky Way Strength", Range(0,2))=0.65
        _Rotation("Rotation", Range(0,360))=0
        _Seed("Seed", Float)=3607
        _SunDirection("Sun Direction", Vector)=(0,1,0,0)
        _SunColor("Sun Color", Color)=(1,0.88,0.62,1)
        _SunDiscDegrees("Sun Disc Degrees", Range(0.05,2))=0.44
        _SunGlow("Sun Glow", Range(0,2))=0.72
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            half4 _Tint, _ZenithColor, _HorizonColor, _SunColor;
            float _Exposure, _MilkyWayStrength, _Rotation, _Seed, _StarVisibility;
            float _SunDiscDegrees, _SunGlow;
            float3 _SunDirection;

            struct A { float4 positionOS:POSITION; };
            struct V { float4 positionCS:SV_POSITION; float3 dir:TEXCOORD0; };

            V Vert(A i)
            {
                V o;
                o.positionCS=TransformObjectToHClip(i.positionOS.xyz);
                o.positionCS.z=UNITY_RAW_FAR_CLIP_VALUE*o.positionCS.w;
                o.dir=i.positionOS.xyz;
                return o;
            }

            float StableHash(float3 p)
            {
                return frac(sin(dot(p,float3(127.1,311.7,74.7))+_Seed*0.0137)*43758.5453);
            }

            float StarLayer(float3 d, float cells, float threshold, float seedOffset, out float temperature)
            {
                float3 cell=floor(d*cells+seedOffset);
                float noise=StableHash(cell+seedOffset);
                temperature=StableHash(cell+seedOffset+71.0);
                return smoothstep(threshold,1.0,noise);
            }

            half3 StarTemperature(float t)
            {
                half3 warm=half3(1.00,0.68,0.44);
                half3 neutral=half3(1.00,0.96,0.86);
                half3 cool=half3(0.58,0.76,1.00);
                return t<0.46 ? lerp(warm,neutral,t/0.46) : lerp(neutral,cool,(t-0.46)/0.54);
            }

            half4 Frag(V i):SV_Target
            {
                float a=radians(_Rotation);
                float2x2 r=float2x2(cos(a),-sin(a),sin(a),cos(a));
                float3 d=normalize(i.dir);
                d.xz=mul(r,d.xz);

                float horizon01=saturate(abs(d.y));
                float zenithBlend=smoothstep(0.0,0.72,horizon01);
                half3 baseColor=lerp(_HorizonColor.rgb,_ZenithColor.rgb,zenithBlend);

                float visibility=saturate(_StarVisibility);
                float night=visibility*visibility;
                float3 galacticNormal=normalize(float3(0.22,0.79,0.57));
                float bandDistance=abs(dot(d,galacticNormal));
                float wideBand=exp2(-bandDistance*bandDistance*34.0);
                float coreBand=exp2(-bandDistance*bandDistance*180.0);
                float cloudNoise=StableHash(floor(d*58.0)+17.0)*0.62+
                                 StableHash(floor(d*131.0)+41.0)*0.38;
                float milkyMask=saturate(wideBand*(0.35+cloudNoise*0.85)+coreBand*0.42);
                baseColor+=_Tint.rgb*milkyMask*_MilkyWayStrength*0.075*night;

                float tFine,tMid,tHero;
                float fine=StarLayer(d,940.0,0.99795,3.0,tFine);
                float mid=StarLayer(d,420.0,0.99855,29.0,tMid)*1.32;
                float hero=StarLayer(d,165.0,0.99790,67.0,tHero)*2.35;
                float galacticFine=StarLayer(d,1180.0,0.99695,103.0,tFine)*milkyMask*0.72;
                float twinkle=0.90+0.10*sin(_Time.y*1.7+StableHash(floor(d*230.0))*31.0);
                half3 stars=(StarTemperature(tFine)*(fine+galacticFine)+
                             StarTemperature(tMid)*mid+
                             StarTemperature(tHero)*hero)*twinkle;
                float horizonFade=lerp(0.72,1.0,smoothstep(0.0,0.26,horizon01));
                stars*=horizonFade*_Exposure*visibility;

                float sunDot=saturate(dot(d,normalize(_SunDirection)));
                float sunRadius=max(0.00001,1.0-cos(radians(_SunDiscDegrees)));
                float sunDisc=smoothstep(1.0-sunRadius*2.2,1.0-sunRadius*0.2,sunDot);
                float sunGlow=pow(sunDot,256.0)*_SunGlow;
                half3 sun=_SunColor.rgb*(sunDisc+sunGlow);

                return half4(baseColor+stars+sun,1);
            }
            ENDHLSL
        }
    }
}
