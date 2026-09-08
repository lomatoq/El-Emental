Shader "ElEmental/Environment/DistantStoneURP"
{
    Properties
    {
        _BaseColor("Stone albedo",Color)=(0.66,0.43,0.26,1)
        _DetailTex("Macro detail (repeat)",2D)="gray"{}
        _DetailScale("World detail scale",Float)=0.035
        _DetailStrength("Quiet brush contrast",Range(0,0.3))=0.08
        _ShadowTint("Cool shadow tint",Color)=(0.38,0.48,0.61,1)
        _HazeColor("Sky-matched haze",Color)=(0.47,0.65,0.83,1)
        _HazeStart("Haze begins (meters)",Float)=180
        _HazeDensity("Extinction / meter",Range(0,0.004))=0.00065
        _HazeMax("Maximum blend",Range(0,1))=0.86
        _BandSoftness("Soft toon bands",Range(0.02,0.3))=0.13
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Forward" Tags { "LightMode"="UniversalForward" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_DetailTex);SAMPLER(sampler_DetailTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor,_ShadowTint,_HazeColor;
            float _DetailScale,_DetailStrength,_HazeStart,_HazeDensity,_HazeMax,_BandSoftness;
            CBUFFER_END
            struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A i)
            {
                V o=(V)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p=GetVertexPositionInputs(i.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;
                o.normalWS=TransformObjectToWorldNormal(i.normalOS);return o;
            }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 n=normalize(i.normalWS);Light sun=GetMainLight();
                half ndl=dot(n,sun.direction);half lambert=saturate(ndl*.75+.25);
                half s=max(.02,_BandSoftness);
                half bands=.30+.34*smoothstep(.22-s,.22+s,lambert)+.36*smoothstep(.65-s,.65+s,lambert);
                half3 w=pow(abs(n),4);w/=max(.001,w.x+w.y+w.z);
                float3 p=i.positionWS*_DetailScale;
                half detail=SAMPLE_TEXTURE2D(_DetailTex,sampler_DetailTex,p.yz).r*w.x+SAMPLE_TEXTURE2D(_DetailTex,sampler_DetailTex,p.xz).r*w.y+SAMPLE_TEXTURE2D(_DetailTex,sampler_DetailTex,p.xy).r*w.z;
                half3 albedo=_BaseColor.rgb*(1+(detail-.5)*_DetailStrength);
                half3 ambient=max(half3(.10,.12,.16),SampleSH(n));
                half3 lightTint=lerp(_ShadowTint.rgb,half3(1,1,1),bands);
                half3 col=albedo*lightTint*(ambient*.45+sun.color*bands);
                float distanceToCamera=distance(i.positionWS,GetCameraPositionWS());
                half haze=min(_HazeMax,1-exp(-max(0,distanceToCamera-_HazeStart)*_HazeDensity));
                col=lerp(col,_HazeColor.rgb,haze);
                return half4(col,1);
            }
            ENDHLSL
        }
        // Deliberately no ShadowCaster pass: these decorative distant masses do not
        // pollute cascaded shadows or the arena's lighting. Actual arena uses its own materials.
        Pass
        {
            Name "DepthOnly" Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A{float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 positionCS:SV_POSITION;UNITY_VERTEX_OUTPUT_STEREO};
            V vertDepth(A i){V o=(V)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(i.positionOS.xyz);return o;}
            half4 fragDepth(V i):SV_Target{return 0;}
            ENDHLSL
        }
    }
    Fallback Off
}
