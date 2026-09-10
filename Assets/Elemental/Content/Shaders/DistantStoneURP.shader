Shader "Elemental/Environment/DistantStoneURP"
{
    Properties
    {
        _BaseColor("Stone albedo",Color)=(0.66,0.43,0.26,1)
        _DetailTex("Macro detail (repeat)",2D)="gray"{}
        _DetailScale("World detail scale",Float)=0.035
        _DetailStrength("Quiet brush contrast",Range(0,0.3))=0.08
        _ShadowTint("Cool shadow tint",Color)=(0.38,0.48,0.61,1)
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
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma target 3.5
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
            TEXTURE2D(_DetailTex);SAMPLER(sampler_DetailTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor,_ShadowTint;
            float _DetailScale,_DetailStrength,_BandSoftness;
            CBUFFER_END
            half3 ElementalAdditionalResponse(Light light, half3 n)
            {
                half lambert=saturate(dot(n,light.direction)*.75h+.25h);
                half s=max(.02h,_BandSoftness);
                half bands=.30h+.34h*smoothstep(.22h-s,.22h+s,lambert)+.36h*smoothstep(.65h-s,.65h+s,lambert);
                half3 tint=lerp(_ShadowTint.rgb,half3(1,1,1),bands);
                return light.color*light.distanceAttenuation*light.shadowAttenuation*bands*tint;
            }
            #include "ElementalAdditionalRadiance.hlsl"
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
                half3 w=pow(abs(n),4);w/=max(.001,w.x+w.y+w.z);
                float3 p=i.positionWS*_DetailScale;
                half detail=SAMPLE_TEXTURE2D(_DetailTex,sampler_DetailTex,p.yz).r*w.x+SAMPLE_TEXTURE2D(_DetailTex,sampler_DetailTex,p.xz).r*w.y+SAMPLE_TEXTURE2D(_DetailTex,sampler_DetailTex,p.xy).r*w.z;
                half3 albedo=_BaseColor.rgb*(1+(detail-.5)*_DetailStrength);
                #if defined(_DBUFFER)
                ApplyDecalToBaseColor(i.positionCS,albedo);
                #endif
                half3 ambient=max(0.0h,SampleSH(n))*.45h;
                half3 direct=ElementalAdditionalResponse(sun,n)+ElementalAdditionalRadiance(
                    i.positionWS,GetNormalizedScreenSpaceUV(i.positionCS),n);
                half3 col=albedo*(ambient+direct);
                // Existing depth-aware AtmosphereFullscreenFeature is the sole aerial/fog pass.
                // Unity supplies linear material/light colors; do not gamma-convert them again.
                return half4(col,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals" Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma vertex vertNormal
            #pragma fragment fragNormal
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 positionCS:SV_POSITION;half3 normalWS:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            V vertNormal(A i){V o=(V)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.normalWS=TransformObjectToWorldNormal(i.normalOS);return o;}
            half4 fragNormal(V i):SV_Target
            {
                half3 normalWS=normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct=PackNormalOctQuadEncode(normalWS);
                return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
                #else
                return half4(normalWS,0);
                #endif
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
