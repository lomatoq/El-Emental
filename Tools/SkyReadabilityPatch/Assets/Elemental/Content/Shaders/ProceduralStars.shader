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
        _SunDiscDegrees("Sun Disc Diameter Degrees", Range(0.05,2))=0.44
        _SunGlow("Sun Glow", Range(0,2))=0.72
        _StarCube("Baked Equal-Area Stars", Cube)="black" {}
        _LocalUp("Observer Radial Up", Vector)=(0,1,0,0)
        _DuskPink("Dusk Pink", Color)=(0.72,0.24,0.39,1)
        _Twilight01("Twilight", Range(0,1))=0
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

            half4 _Tint, _ZenithColor, _HorizonColor, _SunColor, _DuskPink;
            float _Exposure, _MilkyWayStrength, _Rotation, _Seed, _StarVisibility;
            float _SunDiscDegrees, _SunGlow;
            float3 _SunDirection, _LocalUp;
            float4 _ElementalPlanetCenterRadius;
            float _Twilight01;
            TEXTURECUBE(_StarCube); SAMPLER(sampler_StarCube);

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

            half4 Frag(V i):SV_Target
            {
                float a=radians(_Rotation);
                float2x2 r=float2x2(cos(a),-sin(a),sin(a),cos(a));
                float3 d=normalize(i.dir);
                float3 starsDirection=d;
                starsDirection.xz=mul(r,starsDirection.xz);

                float elevation=dot(d,normalize(_LocalUp));
                float horizon01=saturate(abs(elevation));
                float zenithBlend=smoothstep(0.0,0.72,horizon01);
                half3 baseColor=lerp(_HorizonColor.rgb,_ZenithColor.rgb,zenithBlend);
                float rawSunFacing=saturate(dot(d,normalize(_SunDirection)));
                float facingSun=rawSunFacing*0.5+0.5;
                float horizonBand=exp2(-elevation*elevation*20.0);
                float sunHaze=pow(rawSunFacing,6.0);
                float pinkBand=_Twilight01*(horizonBand*(0.24+facingSun*0.76)+sunHaze*0.58);
                baseColor=lerp(baseColor,_DuskPink.rgb,saturate(pinkBand*0.64));

                float visibility=saturate(_StarVisibility);
                float night=visibility*visibility;
                float3 galacticNormal=normalize(float3(0.22,0.79,0.57));
                float bandDistance=abs(dot(starsDirection,galacticNormal));
                float wideBand=exp2(-bandDistance*bandDistance*34.0);
                float coreBand=exp2(-bandDistance*bandDistance*180.0);
                // Broad smooth wisps only: no thresholded direction lattice in the star field.
                float cloudNoise=sin(dot(starsDirection,float3(17.1,9.3,27.7))) *
                                 sin(dot(starsDirection,float3(31.2,-16.7,8.4)))*0.5+0.5;
                float milkyMask=saturate(wideBand*(0.20+cloudNoise*0.45)+coreBand*0.12);
                baseColor+=_Tint.rgb*milkyMask*_MilkyWayStrength*0.075*night;

                half3 stars=SAMPLE_TEXTURECUBE(_StarCube,sampler_StarCube,starsDirection).rgb;
                float horizonFade=lerp(0.72,1.0,smoothstep(0.0,0.26,horizon01));
                stars*=horizonFade*_Exposure*visibility;

                float sunDot=saturate(dot(d,normalize(_SunDirection)));
                float sunRadius=max(0.0000001,1.0-cos(radians(_SunDiscDegrees*0.5)));
                float edgeWidth=max(fwidth(sunDot),0.0000001);
                float sunDisc=smoothstep(1.0-sunRadius-edgeWidth,1.0-sunRadius+edgeWidth,sunDot);
                float sunGlow=pow(sunDot,256.0)*_SunGlow;
                // The small planet's horizon is depressed for elevated cameras. Occlude
                // against its actual sphere instead of fading at a flat tangent horizon.
                float3 offset=_WorldSpaceCameraPos-_ElementalPlanetCenterRadius.xyz;
                float b=dot(offset,d);
                float c=dot(offset,offset)-_ElementalPlanetCenterRadius.w*_ElementalPlanetCenterRadius.w;
                float discriminant=b*b-c;
                float occulted=(_ElementalPlanetCenterRadius.w>0 && b<0 && discriminant>0 &&
                    -b+sqrt(max(0,discriminant))>0) ? 1.0 : 0.0;
                half3 lowSunColor=lerp(_SunColor.rgb,half3(1.0h,0.31h,0.13h),0.58h);
                half3 visibleSunColor=lerp(_SunColor.rgb,lowSunColor,saturate(_Twilight01));
                float visibleSunEnergy=sunDisc*lerp(1.0,0.78,saturate(_Twilight01))+
                                       sunGlow*lerp(1.0,0.58,saturate(_Twilight01));
                half3 sun=visibleSunColor*visibleSunEnergy*(1.0-occulted);

                return half4(baseColor+stars+sun,1);
            }
            ENDHLSL
        }
    }
}
