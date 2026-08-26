Shader "Elemental/Graphics V5/Rumble Rock Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Optional Surface Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Sunlit Rock", Color) = (0.50, 0.34, 0.23, 1)
        _ShadowColor("Soft Shadow Rock", Color) = (0.20, 0.15, 0.13, 1)
        _EdgeColor("Bevel Light Tint", Color) = (0.64, 0.47, 0.34, 1)
        _FractureColor("Fresh Fracture", Color) = (0.64, 0.47, 0.32, 1)
        _TextureScale("Metric Texture Scale", Range(0.02, 2.0)) = 0.24
        _TextureStrength("Texture Strength", Range(0.0, 1.0)) = 0.12
        _TriplanarSharpness("Triplanar Sharpness", Range(1.0, 12.0)) = 4.0
        _MacroScale("Macro Form Scale", Range(0.25, 12.0)) = 3.2
        _MacroStrength("Macro Variation", Range(0.0, 0.35)) = 0.10
        _FacetContrast("Facet Contrast", Range(0.0, 1.0)) = 0.34
        _Roughness("Visual Roughness", Range(0.0, 1.0)) = 0.82
        _BevelLight("Bevel Light", Range(0.0, 1.0)) = 0.42
        _SideShadingSmoothness("Vertical Side Shading Smoothness", Range(0.0, 1.0)) = 1.0
        _AmbientStrength("Ambient Strength", Range(0.0, 2.0)) = 0.82
        [Enum(Rock,0,Character,1)] _SurfaceMode("Surface Mode", Float) = 0
        [Toggle] _UsePlanetFrame("Use Shared Planet Frame", Float) = 0
        _PlanetCenter("Planet Center", Vector) = (0,0,0,0)
        _Fade("Visible Fraction", Range(0.0, 1.0)) = 1
        [Enum(Off,0,Mapping,1,Normals,2,BlendWeights,3,FaceData,4)] _DebugMode("Seam Debug", Float) = 0
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
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadowColor;
                half4 _EdgeColor;
                half4 _FractureColor;
                half _TextureScale;
                half _TextureStrength;
                half _TriplanarSharpness;
                half _MacroScale;
                half _MacroStrength;
                half _FacetContrast;
                half _Roughness;
                half _BevelLight;
                half _SideShadingSmoothness;
                half _AmbientStrength;
                half _SurfaceMode;
                half _UsePlanetFrame;
                float4 _PlanetCenter;
                half _Fade;
                half _DebugMode;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half3 normalOS : TEXCOORD3;
                half4 color : COLOR;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float3 p)
            {
                float3 cell = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = Hash31(cell + float3(0,0,0));
                float n100 = Hash31(cell + float3(1,0,0));
                float n010 = Hash31(cell + float3(0,1,0));
                float n110 = Hash31(cell + float3(1,1,0));
                float n001 = Hash31(cell + float3(0,0,1));
                float n101 = Hash31(cell + float3(1,0,1));
                float n011 = Hash31(cell + float3(0,1,1));
                float n111 = Hash31(cell + float3(1,1,1));
                float x00 = lerp(n000, n100, f.x);
                float x10 = lerp(n010, n110, f.x);
                float x01 = lerp(n001, n101, f.x);
                float x11 = lerp(n011, n111, f.x);
                return lerp(lerp(x00, x10, f.y), lerp(x01, x11, f.y), f.z);
            }

            float SoftMacro(float3 position)
            {
                float scale = max(0.01, _MacroScale);
                float first = ValueNoise(position / scale);
                float second = ValueNoise(position / (scale * 0.43) + 19.7);
                return first * 0.72 + second * 0.28;
            }

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

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.normalOS = normalize(input.normalOS);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half ScreenDither(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                if (_Fade < 0.999h)
                    clip(_Fade - ScreenDither(input.positionCS.xy));

                half3 geometryNormalWS = normalize(input.normalWS);
                half3 geometryNormalOS = normalize(input.normalOS);
                half characterMode = step(0.5h, _SurfaceMode);
                half verticalSide = 1.0h - smoothstep(0.42h, 0.78h, abs(geometryNormalOS.y));
                float3 radialNormalOS = float3(input.positionOS.x, 0.0, input.positionOS.z);
                radialNormalOS *= rsqrt(max(dot(radialNormalOS, radialNormalOS), 0.000001));
                half3 radialNormalWS = normalize(TransformObjectToWorldNormal(radialNormalOS));
                radialNormalWS *= dot(radialNormalWS, geometryNormalWS) < 0.0h ? -1.0h : 1.0h;
                half sideSmoothing = saturate(verticalSide * _SideShadingSmoothness) *
                                     (1.0h - characterMode);
                half3 normalWS = normalize(lerp(geometryNormalWS, radialNormalWS, sideSmoothing));
                half3 mappingNormal = normalize(lerp(input.normalOS, geometryNormalWS, saturate(_UsePlanetFrame)));
                float3 mappingPosition = lerp(
                    input.positionOS,
                    input.positionWS - _PlanetCenter.xyz,
                    saturate(_UsePlanetFrame));
                half3 weights = BlendWeights(mappingNormal);

                if (_DebugMode > 0.5h && _DebugMode < 1.5h)
                {
                    float3 grid = abs(frac(mappingPosition * 0.25) - 0.5) * 2.0;
                    return half4(saturate(grid), 1);
                }
                if (_DebugMode >= 1.5h && _DebugMode < 2.5h)
                    return half4(normalWS * 0.5h + 0.5h, 1);
                if (_DebugMode >= 2.5h && _DebugMode < 3.5h)
                    return half4(weights, 1);
                if (_DebugMode >= 3.5h)
                    return half4(input.color.rgb, 1);

                half3 textureSample = SampleMetricTriplanar(mappingPosition, weights);
                half textureLuma = dot(textureSample, half3(0.299h, 0.587h, 0.114h));
                half effectiveTextureStrength = lerp(_TextureStrength, _TextureStrength * 0.24h, characterMode);
                half textureModulation = lerp(1.0h, lerp(0.82h, 1.16h, textureLuma), effectiveTextureStrength);
                half macro = lerp(1.0h - _MacroStrength, 1.0h + _MacroStrength,
                                  SoftMacro(mappingPosition));
                half authoredFaceTone = lerp(0.88h, 1.08h, saturate(input.color.r));
                half faceTone = lerp(lerp(authoredFaceTone, 1.0h, sideSmoothing), 1.0h, characterMode);
                half bevelMask = saturate((input.color.a - 0.34h) * 2.65h);
                // Keep the authored chamfer geometry but do not light every seam
                // between adjacent side facets as a separate vertical bevel. Those
                // repeated highlights read as dense striping on slabs and pillars.
                half perimeterBevel = smoothstep(0.22h, 0.62h, abs(geometryNormalOS.y));
                bevelMask *= perimeterBevel * (1.0h - characterMode);
                half3 palette = lerp(_BaseColor.rgb, _EdgeColor.rgb, bevelMask * _BevelLight);
                half3 albedo = palette * macro * faceTone * textureModulation;

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndotl = dot(normalWS, mainLight.direction);
                half wrapped = saturate((ndotl + 0.28h) / 1.28h);
                half softDiffuse = smoothstep(0.04h, 0.96h, wrapped);
                // Near-vertical rock faces are especially prone to shadow-map
                // self-acne because their authored silhouette is made from many
                // short facets. They still cast onto the world; only self-shadow
                // reception is faded out on that belt.
                half receivedShadow = lerp(mainLight.shadowAttenuation, 1.0h, sideSmoothing);
                half shadow = lerp(0.34h, 1.0h, receivedShadow);
                half effectiveFacetContrast = lerp(_FacetContrast, _FacetContrast * 0.52h, characterMode);
                half facet = lerp(1.0h, smoothstep(0.18h, 0.88h, softDiffuse), effectiveFacetContrast);
                half3 directTint = lerp(_ShadowColor.rgb, half3(1,1,1), facet);
                half3 direct = albedo * directTint * mainLight.color * shadow *
                               mainLight.distanceAttenuation;

                half3 ambient = SampleSH(normalWS) * _AmbientStrength;
                ambient += _ShadowColor.rgb * 0.12h;
                half3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
                half smoothness = saturate(1.0h - _Roughness);
                half specularPower = lerp(5.0h, 30.0h, smoothness);
                half specular = pow(saturate(dot(normalWS, halfDirection)), specularPower) *
                                lerp(0.018h, 0.075h, smoothness) * shadow;
                half fresnel = pow(saturate(1.0h - dot(normalWS, viewDirection)), 4.0h);
                half3 color = direct + albedo * ambient +
                              mainLight.color * specular +
                              _EdgeColor.rgb * fresnel * 0.035h;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    FallBack "Universal Render Pipeline/Lit"
}
