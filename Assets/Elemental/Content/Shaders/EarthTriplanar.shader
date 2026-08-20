Shader "Elemental/SG Earth Master"
{
    Properties
    {
        [MainTexture] _BaseMap("Stone Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Earth Tint", Color) = (0.62, 0.42, 0.26, 1)
        _ExteriorColor("Weathered Exterior", Color) = (0.38, 0.245, 0.155, 1)
        _InteriorColor("Fresh Fracture Interior", Color) = (0.205, 0.19, 0.165, 1)
        _DustColor("Settled Dust", Color) = (0.52, 0.405, 0.285, 1)
        [HDR] _MagicColor("Magic Accent", Color) = (1.15, 0.42, 0.075, 1)
        _MineralColor("Mineral Vein", Color) = (0.18, 0.24, 0.29, 1)
        [Enum(Basalt,0,Sandstone,1,Granite,2,Clay,3)] _StoneFamily("Stone Family", Float) = 1
        [Normal] _NormalMap("Stone Normal", 2D) = "bump" {}
        _MaskMap("AO (R) Roughness (G)", 2D) = "white" {}
        _WorldTiling("World Tiling", Range(0.08, 2.0)) = 0.48
        _MicroTiling("Micro Tiling", Range(1.0, 12.0)) = 4.2
        _MicroFadeStart("Micro Fade Start", Range(1.0, 40.0)) = 9.0
        _MicroFadeEnd("Micro Fade End", Range(2.0, 80.0)) = 24.0
        _MacroFrequency("Macro Frequency", Range(0.02, 0.25)) = 0.075
        _TriplanarSharpness("Projection Sharpness", Range(1.0, 12.0)) = 5.0
        _NormalStrength("Normal Strength", Range(0.0, 2.0)) = 0.7
        _ProceduralDetail("Procedural Detail", Range(0.0, 1.0)) = 0.72
        _ProceduralNormalStrength("Procedural Normal", Range(0.0, 1.5)) = 0.5
        _StrataScale("Strata Scale", Range(0.25, 6.0)) = 1.6
        _GrainScale("Grain Scale", Range(0.5, 12.0)) = 4.2
        _MacroVariation("Macro Variation", Range(0.0, 0.35)) = 0.12
        _OcclusionStrength("Occlusion Strength", Range(0.0, 1.0)) = 0.55
        _CavityStrength("Cavity Strength", Range(0.0, 1.0)) = 0.46
        _DustAmount("Dust Amount", Range(0.0, 1.0)) = 0.16
        _InteriorAmount("Interior Override", Range(0.0, 1.0)) = 0.0
        _MagicAmount("Magic Activation", Range(0.0, 1.0)) = 0.0
        _MineralAmount("Mineral Amount", Range(0.0, 0.25)) = 0.035
        [Toggle] _UsePlanetFrame("Use Shared Planet Projection", Float) = 0.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.08
        _EmissionColor("Warm Subsurface", Color) = (0.015, 0.004, 0.0, 0)
        [HideInInspector] _Cutoff("Cutoff", Range(0,1)) = 0.5
        [HideInInspector] _Surface("Surface", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        LOD 300

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _EARTH_DETAIL_LOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MaskMap);
            SAMPLER(sampler_MaskMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ExteriorColor;
                half4 _InteriorColor;
                half4 _DustColor;
                half4 _MagicColor;
                half4 _MineralColor;
                half4 _EmissionColor;
                half _WorldTiling;
                half _MicroTiling;
                half _MicroFadeStart;
                half _MicroFadeEnd;
                half _MacroFrequency;
                half _TriplanarSharpness;
                half _NormalStrength;
                half _StoneFamily;
                half _ProceduralDetail;
                half _ProceduralNormalStrength;
                half _StrataScale;
                half _GrainScale;
                half _MacroVariation;
                half _OcclusionStrength;
                half _CavityStrength;
                half _DustAmount;
                half _InteriorAmount;
                half _MagicAmount;
                half _MineralAmount;
                half _Smoothness;
                half _UsePlanetFrame;
                float4x4 _ProjectionWorldToPlanet;
                float4x4 _ProjectionPlanetToWorld;
            CBUFFER_END

            // Driven once by CelestialSystemBehaviour. This is deliberately global:
            // every stone family must retain the same restrained night readability
            // without cloning or mutating material instances.
            half _ElementalNight01;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float3 ProjectionPosition(float3 positionWS)
            {
                float3 origin = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                float3 axisX = SafeNormalize(float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20));
                float3 axisY = SafeNormalize(float3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21));
                float3 axisZ = SafeNormalize(float3(unity_ObjectToWorld._m02, unity_ObjectToWorld._m12, unity_ObjectToWorld._m22));
                float3 delta = positionWS - origin;
                float3 objectPosition = float3(dot(delta, axisX), dot(delta, axisY), dot(delta, axisZ));
                float3 planetPosition = mul(_ProjectionWorldToPlanet, float4(positionWS, 1.0)).xyz;
                return lerp(objectPosition, planetPosition, saturate(_UsePlanetFrame));
            }

            half3 ProjectionNormal(half3 normalWS)
            {
                half3 axisX = SafeNormalize(half3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20));
                half3 axisY = SafeNormalize(half3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21));
                half3 axisZ = SafeNormalize(half3(unity_ObjectToWorld._m02, unity_ObjectToWorld._m12, unity_ObjectToWorld._m22));
                half3 objectNormal = half3(dot(normalWS, axisX), dot(normalWS, axisY), dot(normalWS, axisZ));
                half3 planetNormal = SafeNormalize(mul((float3x3)_ProjectionWorldToPlanet, normalWS));
                return normalize(lerp(objectNormal, planetNormal, saturate(_UsePlanetFrame)));
            }

            half3 ProjectionToWorld(half3 normalPS)
            {
                half3 axisX = SafeNormalize(half3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20));
                half3 axisY = SafeNormalize(half3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21));
                half3 axisZ = SafeNormalize(half3(unity_ObjectToWorld._m02, unity_ObjectToWorld._m12, unity_ObjectToWorld._m22));
                half3 objectNormal = axisX * normalPS.x + axisY * normalPS.y + axisZ * normalPS.z;
                half3 planetNormal = mul((float3x3)_ProjectionPlanetToWorld, normalPS);
                return normalize(lerp(objectNormal, planetNormal, saturate(_UsePlanetFrame)));
            }

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

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
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half3 BlendWeights(half3 normalPS)
            {
                half3 weights = pow(abs(normalPS), max(1.0h, _TriplanarSharpness));
                weights /= max(0.001h, weights.x + weights.y + weights.z);
                return weights;
            }

            half3 SampleTriplanar(float3 positionPS, half3 weights)
            {
                float scale = max(0.001, _WorldTiling);
                half3 xProjection = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, positionPS.zy * scale).rgb;
                half3 yProjection = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, positionPS.xz * scale).rgb;
                half3 zProjection = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, positionPS.xy * scale).rgb;
                return (xProjection * weights.x) + (yProjection * weights.y) + (zProjection * weights.z);
            }

            half3 SampleTriplanarNormal(float3 positionPS, half3 normalPS, half3 weights, float scale)
            {
                scale = max(0.001, scale);
                half3 nx = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, positionPS.zy * scale), _NormalStrength);
                half3 ny = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, positionPS.xz * scale), _NormalStrength);
                half3 nz = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, positionPS.xy * scale), _NormalStrength);
                half3 xNormal = half3(nx.z * sign(normalPS.x), nx.y, nx.x);
                half3 yNormal = half3(ny.x, ny.z * sign(normalPS.y), ny.y);
                half3 zNormal = half3(nz.x, nz.y, nz.z * sign(normalPS.z));
                return normalize(xNormal * weights.x + yNormal * weights.y + zNormal * weights.z);
            }

            half2 SampleMask(float3 positionPS, half3 weights)
            {
                float scale = max(0.001, _WorldTiling);
                half2 mx = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, positionPS.zy * scale).rg;
                half2 my = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, positionPS.xz * scale).rg;
                half2 mz = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, positionPS.xy * scale).rg;
                return mx * weights.x + my * weights.y + mz * weights.z;
            }

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
                float n000 = Hash31(cell + float3(0, 0, 0));
                float n100 = Hash31(cell + float3(1, 0, 0));
                float n010 = Hash31(cell + float3(0, 1, 0));
                float n110 = Hash31(cell + float3(1, 1, 0));
                float n001 = Hash31(cell + float3(0, 0, 1));
                float n101 = Hash31(cell + float3(1, 0, 1));
                float n011 = Hash31(cell + float3(0, 1, 1));
                float n111 = Hash31(cell + float3(1, 1, 1));
                float x00 = lerp(n000, n100, f.x);
                float x10 = lerp(n010, n110, f.x);
                float x01 = lerp(n001, n101, f.x);
                float x11 = lerp(n011, n111, f.x);
                return lerp(lerp(x00, x10, f.y), lerp(x01, x11, f.y), f.z);
            }

            float StoneFbm(float3 p)
            {
                float value = ValueNoise(p) * 0.57;
                value += ValueNoise(p * 2.03 + 11.7) * 0.28;
                value += ValueNoise(p * 4.11 + 37.1) * 0.15;
                return value;
            }

            float FamilyHeight(float3 p)
            {
                float family = floor(_StoneFamily + 0.5);
                float coarse = StoneFbm(p * max(0.25, _StrataScale));
                float grain = ValueNoise(p * max(0.5, _GrainScale));
                if (family < 0.5)
                {
                    float cells = ValueNoise(p * 3.4);
                    float pores = 1.0 - smoothstep(0.035, 0.16, abs(cells - 0.16));
                    return saturate(coarse * 0.70 - pores * 0.26 + grain * 0.18);
                }
                if (family < 1.5)
                {
                    // Sandstone strata are deliberately warped in two horizontal
                    // axes. A plain y sine reads as synthetic zebra bands as soon
                    // as the object grows beyond hand-held scale.
                    float lateralWarp = StoneFbm(float3(p.x * 0.31, p.z * 0.24, p.y * 0.07));
                    float strataPhase = p.y + (coarse - 0.5) * 0.17 + (lateralWarp - 0.5) * 0.29;
                    float primary = sin(strataPhase * _StrataScale * 5.1) * 0.5 + 0.5;
                    float secondary = sin(strataPhase * _StrataScale * 10.7 + lateralWarp * 4.2) * 0.5 + 0.5;
                    float strata = lerp(primary, secondary, 0.24);
                    return saturate(coarse * 0.55 + strata * 0.22 + grain * 0.15);
                }
                if (family < 2.5)
                {
                    float crystal = smoothstep(0.72, 0.94, grain);
                    return saturate(coarse * 0.62 + grain * 0.20 + crystal * 0.28);
                }
                float clayBand = sin((p.y + StoneFbm(p * 0.45) * 0.35) * _StrataScale * 3.2) * 0.5 + 0.5;
                return saturate(coarse * 0.68 + clayBand * 0.20 + grain * 0.08);
            }

            half3 ApplyFamilyPalette(half3 sampled, float3 p, float height)
            {
                float family = floor(_StoneFamily + 0.5);
                if (family < 0.5)
                    return sampled * lerp(0.58h, 1.18h, height);
                if (family < 1.5)
                {
                    half lateralWarp = StoneFbm(float3(p.x * 0.31h, p.z * 0.24h, p.y * 0.07h));
                    half phase = p.y + (height - 0.5h) * 0.11h + (lateralWarp - 0.5h) * 0.29h;
                    half warmBand = 0.93h + 0.13h * sin(phase * _StrataScale * 5.1h);
                    half brokenBand = smoothstep(0.26h, 0.78h, height) * 0.09h;
                    return sampled * lerp(0.90h, warmBand + brokenBand, 0.44h);
                }
                if (family < 2.5)
                {
                    half crystal = smoothstep(0.76h, 0.94h, ValueNoise(p * _GrainScale));
                    return lerp(sampled * lerp(0.72h, 1.12h, height), _MineralColor.rgb, crystal * 0.42h);
                }
                return sampled * lerp(0.82h, 1.12h, smoothstep(0.18h, 0.84h, height));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionPS = ProjectionPosition(input.positionWS);
                half3 baseNormalWS = normalize(input.normalWS);
                half3 normalPS = ProjectionNormal(baseNormalWS);
                half3 weights = BlendWeights(normalPS);
                half distanceFade = 1.0h - saturate((distance(_WorldSpaceCameraPos, input.positionWS) -
                    _MicroFadeStart) / max(0.01h, _MicroFadeEnd - _MicroFadeStart));
#if defined(_EARTH_DETAIL_LOW)
                half3 normalWS = baseNormalWS;
#else
                half3 detailNormalWS = ProjectionToWorld(
                    SampleTriplanarNormal(positionPS, normalPS, weights, _MicroTiling));
                float relief = FamilyHeight(positionPS);
                const float reliefEpsilon = 0.035;
                float3 reliefGradient = float3(
                    FamilyHeight(positionPS + float3(reliefEpsilon, 0, 0)) - relief,
                    FamilyHeight(positionPS + float3(0, reliefEpsilon, 0)) - relief,
                    FamilyHeight(positionPS + float3(0, 0, reliefEpsilon)) - relief) / reliefEpsilon;
                half3 proceduralNormalWS = ProjectionToWorld(normalize(
                    normalPS - reliefGradient * _ProceduralNormalStrength));
                half3 mappedNormalWS = normalize(lerp(baseNormalWS, detailNormalWS, distanceFade));
                half3 normalWS = normalize(lerp(
                    mappedNormalWS,
                    proceduralNormalWS,
                    _ProceduralDetail * distanceFade));
#endif
                half2 mask = SampleMask(positionPS, weights);
                float macroHash = frac(sin(dot(floor(positionPS * max(0.001h, _MacroFrequency)),
                    float3(12.9898, 78.233, 37.719))) * 43758.5453);
                half macro = lerp(1.0h - _MacroVariation, 1.0h + _MacroVariation, macroHash);
                half classified = saturate(abs(input.color.r - input.color.g));
                half interior = saturate(max(_InteriorAmount, input.color.g * (1.0h - input.color.r)));
                half cavity = lerp(0.12h, input.color.a, classified);
                half3 surfaceTint = lerp(_ExteriorColor.rgb, _InteriorColor.rgb, interior);
                float familyHeight = FamilyHeight(positionPS);
                half3 baseStone = SampleTriplanar(positionPS, weights);
                half3 sampledStone = ApplyFamilyPalette(
                    baseStone, positionPS, familyHeight);
                half3 albedo = lerp(
                    baseStone,
                    sampledStone,
                    _ProceduralDetail) * surfaceTint * macro;
                albedo *= lerp(1.0h, mask.r, _OcclusionStrength);
                albedo *= lerp(1.0h, 1.0h - cavity * 0.62h, _CavityStrength);
                half dust = saturate(_DustAmount * saturate(normalPS.y * 0.75h + 0.25h) *
                    lerp(0.65h, 1.0h, cavity));
                albedo = lerp(albedo, _DustColor.rgb * macro, dust);
                half mineralNoise = step(1.0h - _MineralAmount,
                    frac(sin(dot(floor(positionPS * 1.7), float3(31.7, 11.3, 73.1))) * 15731.743));
                albedo = lerp(albedo, _MineralColor.rgb, mineralNoise * (1.0h - dust) * 0.32h);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half wrapped = saturate((ndotl + 0.18h) / 1.18h);
                half3 direct = mainLight.color * wrapped * mainLight.distanceAttenuation *
                               lerp(0.28h, 1.0h, mainLight.shadowAttenuation);
                // Preserve geological form at night with a cool hemispheric floor.
                // It is intentionally weaker than the moon key, so cavities and
                // chipped silhouettes remain dimensional instead of looking unlit.
                half3 nightFill = half3(0.20h, 0.245h, 0.38h) *
                                  saturate(_ElementalNight01);
                half3 ambient = SampleSH(normalWS) * 0.82h +
                                half3(0.10h, 0.072h, 0.052h) + nightFill;

                half3 additional = 0;
                uint additionalCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(additionalCount)
                    Light light = GetAdditionalLight(lightIndex, input.positionWS);
                    half diffuse = saturate(dot(normalWS, light.direction));
                    additional += light.color * diffuse * light.distanceAttenuation * light.shadowAttenuation;
                LIGHT_LOOP_END

                half3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
                half materialSmoothness = saturate(_Smoothness + (1.0h - mask.g) * 0.18h - interior * 0.035h);
                half specular = pow(saturate(dot(normalWS, halfDirection)), lerp(4.0h, 72.0h, materialSmoothness)) *
                                (materialSmoothness * 0.16h);
                half magicMask = saturate(_MagicAmount + input.color.b * classified);
                half magicEmission = lerp(0.18h, 0.54h, saturate(_ElementalNight01));
                half3 color = albedo * (ambient + direct + additional) +
                              (mainLight.color * specular) + _EmissionColor.rgb +
                              (_MagicColor.rgb * magicMask * magicEmission);
                color = MixFog(color, input.fogFactor);
                return half4(color, _BaseColor.a);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    FallBack "Universal Render Pipeline/Lit"
}
