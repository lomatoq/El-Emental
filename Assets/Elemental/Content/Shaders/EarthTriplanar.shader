Shader "Elemental/Earth Triplanar"
{
    Properties
    {
        [MainTexture] _BaseMap("Stone Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Earth Tint", Color) = (0.62, 0.42, 0.26, 1)
        [Normal] _NormalMap("Stone Normal", 2D) = "bump" {}
        _MaskMap("AO (R) Roughness (G)", 2D) = "white" {}
        _WorldTiling("World Tiling", Range(0.08, 2.0)) = 0.48
        _TriplanarSharpness("Projection Sharpness", Range(1.0, 12.0)) = 5.0
        _NormalStrength("Normal Strength", Range(0.0, 2.0)) = 0.7
        _MacroVariation("Macro Variation", Range(0.0, 0.35)) = 0.12
        _OcclusionStrength("Occlusion Strength", Range(0.0, 1.0)) = 0.55
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
                half4 _EmissionColor;
                half _WorldTiling;
                half _TriplanarSharpness;
                half _NormalStrength;
                half _MacroVariation;
                half _OcclusionStrength;
                half _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float3 ProjectionPosition(float3 positionWS)
            {
                float3 origin = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                float3 axisX = SafeNormalize(float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20));
                float3 axisY = SafeNormalize(float3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21));
                float3 axisZ = SafeNormalize(float3(unity_ObjectToWorld._m02, unity_ObjectToWorld._m12, unity_ObjectToWorld._m22));
                float3 delta = positionWS - origin;
                return float3(dot(delta, axisX), dot(delta, axisY), dot(delta, axisZ));
            }

            half3 ProjectionNormal(half3 normalWS)
            {
                half3 axisX = SafeNormalize(half3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20));
                half3 axisY = SafeNormalize(half3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21));
                half3 axisZ = SafeNormalize(half3(unity_ObjectToWorld._m02, unity_ObjectToWorld._m12, unity_ObjectToWorld._m22));
                return half3(dot(normalWS, axisX), dot(normalWS, axisY), dot(normalWS, axisZ));
            }

            half3 ProjectionToWorld(half3 normalPS)
            {
                half3 axisX = SafeNormalize(half3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20));
                half3 axisY = SafeNormalize(half3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21));
                half3 axisZ = SafeNormalize(half3(unity_ObjectToWorld._m02, unity_ObjectToWorld._m12, unity_ObjectToWorld._m22));
                return normalize(axisX * normalPS.x + axisY * normalPS.y + axisZ * normalPS.z);
            }

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
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

            half3 SampleTriplanarNormal(float3 positionPS, half3 normalPS, half3 weights)
            {
                float scale = max(0.001, _WorldTiling);
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

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionPS = ProjectionPosition(input.positionWS);
                half3 baseNormalWS = normalize(input.normalWS);
                half3 normalPS = ProjectionNormal(baseNormalWS);
                half3 weights = BlendWeights(normalPS);
                half3 normalWS = ProjectionToWorld(SampleTriplanarNormal(positionPS, normalPS, weights));
                half2 mask = SampleMask(positionPS, weights);
                float macroHash = frac(sin(dot(floor(positionPS * 0.075), float3(12.9898, 78.233, 37.719))) * 43758.5453);
                half macro = lerp(1.0h - _MacroVariation, 1.0h + _MacroVariation, macroHash);
                half3 albedo = SampleTriplanar(positionPS, weights) * _BaseColor.rgb * macro;
                albedo *= lerp(1.0h, mask.r, _OcclusionStrength);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half wrapped = saturate((ndotl + 0.18h) / 1.18h);
                half3 direct = mainLight.color * wrapped * mainLight.distanceAttenuation *
                               lerp(0.28h, 1.0h, mainLight.shadowAttenuation);
                // A restrained warm hemispheric fill preserves chipped silhouettes on
                // the night-side camera without flattening direct-light contrast.
                half3 ambient = SampleSH(normalWS) * 0.82h + half3(0.10h, 0.072h, 0.052h);

                half3 additional = 0;
                uint additionalCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(additionalCount)
                    Light light = GetAdditionalLight(lightIndex, input.positionWS);
                    half diffuse = saturate(dot(normalWS, light.direction));
                    additional += light.color * diffuse * light.distanceAttenuation * light.shadowAttenuation;
                LIGHT_LOOP_END

                half3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
                half materialSmoothness = saturate(_Smoothness + (1.0h - mask.g) * 0.24h);
                half specular = pow(saturate(dot(normalWS, halfDirection)), lerp(4.0h, 72.0h, materialSmoothness)) *
                                (materialSmoothness * 0.16h);
                half3 color = albedo * (ambient + direct + additional) +
                              (mainLight.color * specular) + _EmissionColor.rgb;
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
