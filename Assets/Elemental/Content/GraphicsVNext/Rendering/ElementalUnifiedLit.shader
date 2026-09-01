Shader "Elemental/Graphics VNext/Unified Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Authored Surface Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (0.48, 0.38, 0.30, 1)
        [Normal] _NormalMap("Authored Normal Map", 2D) = "bump" {}
        _NormalStrength("Authored Normal Strength", Range(0, 1)) = 1
        _ShadowColor("Cool Shadow Tint", Color) = (0.17, 0.19, 0.23, 1)
        _WarmLightColor("Warm Light Tint", Color) = (1.04, 0.97, 0.88, 1)
        _EdgeColor("Broad Edge Tint", Color) = (0.58, 0.54, 0.50, 1)
        _FractureColor("Fresh Fracture", Color) = (0.48, 0.40, 0.34, 1)
        _EmissionColor("Emission", Color) = (0, 0, 0, 0)
        _TextureScale("Metric Texture Scale", Range(0.02, 2)) = 0.22
        _TextureStrength("Texture Strength", Range(0, 1)) = 0
        _TriplanarSharpness("Triplanar Sharpness", Range(1, 12)) = 4
        _Roughness("Visual Roughness", Range(0, 1)) = 0.86
        _AmbientStrength("Ambient Strength", Range(0, 2)) = 0.82
        _ShadowFloor("Shadow Floor", Range(0.3, 0.8)) = 0.58
        _SpecularStrength("Broad Specular", Range(0, 0.12)) = 0.035
        _RimStrength("Restrained Rim", Range(0, 0.08)) = 0.012
        _MagicAmount("Magic Emission Amount", Range(0, 1)) = 0
        [Enum(Character,0,SandstoneExterior,1,SandstoneInterior,2,PlanetGround,3,MagicConstruct,4)]
        _MaterialFamily("Material Family", Float) = 1
        [Enum(RockOrConstruct,0,Character,1)] _SurfaceMode("Surface Mode", Float) = 0
        [Toggle] _UsePlanetFrame("Use Shared Planet Frame", Float) = 0
        _PlanetCenter("Planet Center", Vector) = (0,0,0,0)
        [HideInInspector] _FractureMappingEnabled("Fracture Mapping Frame", Float) = 0
        [Toggle] _ReceiveSsao("Receive SSAO", Float) = 1
        _Fade("Visible Fraction", Range(0, 1)) = 1
        [HideInInspector] _Cutoff("Cutoff", Range(0,1)) = 0.5
        [HideInInspector] _Surface("Surface", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }
        LOD 260

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Elemental/Content/GraphicsVNext/Rendering/ElementalUnifiedLighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadowColor;
                half4 _WarmLightColor;
                half4 _EdgeColor;
                half4 _FractureColor;
                half4 _EmissionColor;
                half _NormalStrength;
                half _TextureScale;
                half _TextureStrength;
                half _TriplanarSharpness;
                half _Roughness;
                half _AmbientStrength;
                half _ShadowFloor;
                half _SpecularStrength;
                half _RimStrength;
                half _MagicAmount;
                half _MaterialFamily;
                half _SurfaceMode;
                half _UsePlanetFrame;
                float4 _PlanetCenter;
                half _FractureMappingEnabled;
                float4x4 _FractureLocalToStructure;
                half _ReceiveSsao;
                half _Fade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half3 tangentWS : TEXCOORD3;
                half3 bitangentWS : TEXCOORD4;
                half3 normalOS : TEXCOORD5;
                float2 uv : TEXCOORD6;
                half fogFactor : TEXCOORD7;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half3 BlendWeights(half3 normal)
            {
                half3 weights = pow(abs(normal), max(1.0h, _TriplanarSharpness));
                return weights / max(0.001h, weights.x + weights.y + weights.z);
            }

            half3 SampleMetricTriplanar(float3 position, half3 weights)
            {
                float scale = max(0.0001, _TextureScale);
                half3 x = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, position.zy * scale).rgb;
                half3 y = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, position.xz * scale).rgb;
                half3 z = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, position.xy * scale).rgb;
                return x * weights.x + y * weights.y + z * weights.z;
            }

            half ScreenDither(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(
                    pixel,
                    float2(0.06711056, 0.00583715))));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(
                    input.normalOS,
                    input.tangentOS);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = NormalizeNormalPerVertex(normals.normalWS);
                output.tangentWS = NormalizeNormalPerVertex(normals.tangentWS);
                output.bitangentWS = NormalizeNormalPerVertex(normals.bitangentWS);
                output.normalOS = normalize(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_Fade < 0.999h)
                    clip(_Fade - ScreenDither(input.positionCS.xy));

                half3 authoredNormalWS = NormalizeNormalPerPixel(input.normalWS);
                half characterMode = step(0.5h, _SurfaceMode);
                half3 normalWS = authoredNormalWS;
                UNITY_BRANCH
                if (characterMode > 0.5h && _NormalStrength > 0.001h)
                {
                    half3 normalTS = UnpackNormalScale(
                        SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv),
                        _NormalStrength);
                    normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(
                        normalTS,
                        half3x3(
                            SafeNormalize(input.tangentWS),
                            SafeNormalize(input.bitangentWS),
                            authoredNormalWS)));
                }

                float3 objectMappingPosition = input.positionOS;
                UNITY_BRANCH
                if (_FractureMappingEnabled > 0.5h)
                {
                    objectMappingPosition = mul(
                        _FractureLocalToStructure,
                        float4(input.positionOS, 1.0)).xyz;
                }
                float3 mappingPosition = lerp(
                    objectMappingPosition,
                    input.positionWS - _PlanetCenter.xyz,
                    saturate(_UsePlanetFrame));
                half3 mappingNormal = normalize(lerp(
                    input.normalOS,
                    authoredNormalWS,
                    saturate(_UsePlanetFrame)));
                half3 triplanar = SampleMetricTriplanar(
                    mappingPosition,
                    BlendWeights(mappingNormal));
                half3 authoredUv = SAMPLE_TEXTURE2D(
                    _BaseMap,
                    sampler_BaseMap,
                    input.uv).rgb;
                half3 sampledSurface = lerp(triplanar, authoredUv, characterMode);
                half3 albedo = _BaseColor.rgb * lerp(
                    half3(1.0h, 1.0h, 1.0h),
                    sampledSurface,
                    saturate(_TextureStrength));

                ElementalUnifiedLightingInput lightingInput;
                lightingInput.positionWS = input.positionWS;
                lightingInput.positionCS = input.positionCS;
                lightingInput.normalWS = normalWS;
                lightingInput.albedo = albedo;
                lightingInput.shadowTint = _ShadowColor.rgb;
                lightingInput.warmLightTint = _WarmLightColor.rgb;
                lightingInput.edgeTint = _EdgeColor.rgb;
                lightingInput.roughness = _Roughness;
                lightingInput.ambientStrength = _AmbientStrength;
                lightingInput.shadowFloor = _ShadowFloor;
                lightingInput.specularStrength = _SpecularStrength;
                lightingInput.rimStrength = _RimStrength;
                lightingInput.receiveSsao = _ReceiveSsao;
                half3 color = ElementalEvaluateUnifiedLighting(lightingInput);
                color += _EmissionColor.rgb * saturate(_MagicAmount);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadowColor;
                half4 _WarmLightColor;
                half4 _EdgeColor;
                half4 _FractureColor;
                half4 _EmissionColor;
                half _NormalStrength;
                half _TextureScale;
                half _TextureStrength;
                half _TriplanarSharpness;
                half _Roughness;
                half _AmbientStrength;
                half _ShadowFloor;
                half _SpecularStrength;
                half _RimStrength;
                half _MagicAmount;
                half _MaterialFamily;
                half _SurfaceMode;
                half _UsePlanetFrame;
                float4 _PlanetCenter;
                half _FractureMappingEnabled;
                float4x4 _FractureLocalToStructure;
                half _ReceiveSsao;
                half _Fade;
            CBUFFER_END

            struct DepthNormalsAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                half3 tangentWS : TEXCOORD1;
                half3 bitangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half DepthNormalsDither(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(
                    pixel,
                    float2(0.06711056, 0.00583715))));
            }

            DepthNormalsVaryings DepthNormalsVert(DepthNormalsAttributes input)
            {
                DepthNormalsVaryings output = (DepthNormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(
                    input.normalOS,
                    input.tangentOS);
                output.normalWS = NormalizeNormalPerVertex(normals.normalWS);
                output.tangentWS = NormalizeNormalPerVertex(normals.tangentWS);
                output.bitangentWS = NormalizeNormalPerVertex(normals.bitangentWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 DepthNormalsFrag(DepthNormalsVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_Fade < 0.999h)
                    clip(_Fade - DepthNormalsDither(input.positionCS.xy));
                half3 authoredNormalWS = NormalizeNormalPerPixel(input.normalWS);
                half characterMode = step(0.5h, _SurfaceMode);
                float3 normalWS = authoredNormalWS;
                UNITY_BRANCH
                if (characterMode > 0.5h && _NormalStrength > 0.001h)
                {
                    half3 normalTS = UnpackNormalScale(
                        SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv),
                        _NormalStrength);
                    normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(
                        normalTS,
                        half3x3(
                            SafeNormalize(input.tangentWS),
                            SafeNormalize(input.bitangentWS),
                            authoredNormalWS)));
                }
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
                    return half4(packedNormalWS, 0.0h);
                #else
                    return half4(normalWS, 0.0h);
                #endif
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    FallBack Off
}
