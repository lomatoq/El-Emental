Shader "Elemental/Procedural Stars"
{
    Properties { _Tint("Tint", Color)=(0.42,0.58,1,1) _Exposure("Exposure", Range(0,2))=1 _Rotation("Rotation", Range(0,360))=0 _Seed("Seed", Float)=3607 }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            half4 _Tint; float _Exposure, _Rotation, _Seed;
            struct A { float4 positionOS:POSITION; }; struct V { float4 positionCS:SV_POSITION; float3 dir:TEXCOORD0; };
            V Vert(A i)
            {
                V o;
                o.positionCS=TransformObjectToHClip(i.positionOS.xyz);
                // A custom skybox has to sit on the hardware far plane. Without this,
                // reversed-Z D3D can reject the background cube in a standalone player.
                o.positionCS.z=UNITY_RAW_FAR_CLIP_VALUE*o.positionCS.w;
                o.dir=i.positionOS.xyz;
                return o;
            }
            float Hash(float3 p) { p=frac(p*0.1031); p+=dot(p,p.yzx+33.33+_Seed); return frac((p.x+p.y)*p.z); }
            float StableHash(float3 p) { return frac(sin(dot(p,float3(127.1,311.7,74.7))+_Seed*0.0137)*43758.5453); }
            half4 Frag(V i):SV_Target
            {
                float a=radians(_Rotation); float2x2 r=float2x2(cos(a),-sin(a),sin(a),cos(a));
                float3 d=normalize(i.dir); d.xz=mul(r,d.xz);
                float3 fineCell=floor(d*640.0); float fineNoise=StableHash(fineCell);
                float3 heroCell=floor(d*155.0); float heroNoise=StableHash(heroCell+29.0);
                float fineStar=smoothstep(0.9982,0.99996,fineNoise);
                float heroStar=smoothstep(0.99935,0.99998,heroNoise)*2.0;
                float temperature=StableHash(fineCell+71.0);
                half3 starTint=lerp(half3(1.0,0.72,0.5),half3(0.62,0.78,1.0),temperature);
                float nebula=pow(saturate(1.0-abs(d.y+sin(d.x*4.0)*0.12)),8.0)*0.026;
                half3 baseColor=half3(0.0025,0.005,0.018)+_Tint.rgb*nebula;
                return half4((baseColor+starTint*(fineStar+heroStar)*1.45)*_Exposure,1);
            }
            ENDHLSL
        }
    }
}
