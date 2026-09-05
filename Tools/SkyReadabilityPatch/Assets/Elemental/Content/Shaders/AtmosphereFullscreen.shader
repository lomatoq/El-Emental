Shader "Elemental/Atmosphere Fullscreen"
{
    Properties
    {
        _CloudNoise("Cloud Noise", 2D) = "gray" {}
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Depth Aware Atmosphere"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4 _ElementalSunDirection;
            float4 _ElementalPlanetCenterRadius;
            float4 _ElementalAtmosphereParams;
            float4 _ElementalAerialPerspectiveParams;
            float4 _ElementalCloudParams;
            float4 _ElementalRayleighColor;
            TEXTURE2D(_CloudNoise);
            SAMPLER(sampler_CloudNoise);
            float4 _CloudNoise_ST;
            float4 _ElementalMieColor;
            float _ElementalNight01;
            float _ElementalNightOpacity;
            float _ElementalSolarAltitude;
            float _ElementalTwilight01;

            half3 ApplyCloudCue(
                half3 baseColor,
                float3 ray,
                float3 cameraRadial,
                half day,
                half geometryMask)
            {
                if (geometryMask > 0.5h || _ElementalCloudParams.y <= 0.001)
                    return baseColor;

                float rayDenominator = max(0.30, abs(ray.y) + 0.22);
                float2 cloudUv = ray.xz / rayDenominator;
                cloudUv *= max(0.1, _ElementalCloudParams.z);
                float cloudTime = _Time.y * _ElementalCloudParams.w;
                cloudUv += float2(cloudTime, cloudTime * 0.37);
                half cloudA = SAMPLE_TEXTURE2D(
                    _CloudNoise, sampler_CloudNoise, cloudUv).r;
                half cloudB = SAMPLE_TEXTURE2D(
                    _CloudNoise, sampler_CloudNoise,
                    cloudUv * 0.43 + float2(cloudTime * -0.31, cloudTime * 0.19)).r;
                half cloudNoise = cloudA * 0.68h + cloudB * 0.32h;
                half coverage = saturate((half)_ElementalCloudParams.x);
                half cloud = smoothstep(
                    coverage, min(0.98h, coverage + 0.18h), cloudNoise);
                half upDot = (half)dot(ray, cameraRadial);
                // The playable diorama camera looks down across the planet limb,
                // so its visible sky occupies negative camera-radial elevations.
                half cloudBand = smoothstep(-0.62h, -0.30h, upDot) *
                                 (1.0h - smoothstep(0.36h, 0.78h, upDot));
                half cloudAlpha = cloud * cloudBand *
                                  saturate((half)_ElementalCloudParams.y) *
                                  (1.0h - saturate((half)_ElementalNight01)) *
                                  (0.58h + day * 0.42h);
                // White clouds are appropriate at noon, but at low sun they were
                // erasing the authored orange Mie color and producing a cyan wall.
                half twilight = saturate((half)_ElementalTwilight01);
                half3 neutralCloud = lerp(
                    _ElementalMieColor.rgb,
                    half3(0.92h, 0.94h, 0.96h),
                    0.72h);
                half3 duskCloud = lerp(
                    _ElementalMieColor.rgb,
                    half3(1.0h, 0.30h, 0.32h),
                    0.22h);
                half3 cloudColor = lerp(neutralCloud, duskCloud, twilight * 0.82h);
                return lerp(baseColor, cloudColor, cloudAlpha);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float4 clip = float4(uv * 2.0 - 1.0, 1.0, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                    clip.y = -clip.y;
                #endif
                float4 worldFar = mul(UNITY_MATRIX_I_VP, clip);
                float3 ray = SafeNormalize(worldFar.xyz / max(0.00001, worldFar.w) - _WorldSpaceCameraPos);
                float3 center = _ElementalPlanetCenterRadius.xyz;
                float innerRadius = max(0.01, _ElementalPlanetCenterRadius.w);
                float outerRadius = innerRadius * max(1.001, _ElementalAtmosphereParams.x);
                float3 offset = _WorldSpaceCameraPos - center;
                float3 cameraRadial = SafeNormalize(offset);
                float3 sunDirection = SafeNormalize(_ElementalSunDirection.xyz);
                // Time of day belongs to the arena lighting anchor. A high or
                // laterally displaced camera must not retune global atmosphere.
                half cameraDay = (half)smoothstep(-0.10, 0.12, _ElementalSolarAltitude);
                float rawDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool hasGeometry = rawDepth > 0.00001;
                #else
                    bool hasGeometry = rawDepth < 0.99999;
                #endif
                // The fullscreen atmosphere owns sky/limb scattering only. Applying
                // a single midpoint-density estimate to nearby opaque geometry made
                // broad screen-space altitude bands crawl across the arena, character
                // and shadows. Those bands survived the albedo/unlit debug views and
                // vanished only when this pass was disabled. Preserve authored surface
                // shading exactly; distant world fog needs a dedicated multi-sample
                // aerial-perspective pass instead of this cheap sky approximation.
                if (hasGeometry)
                    return source;
                float b = dot(offset, ray);
                float c = dot(offset, offset) - outerRadius * outerRadius;
                float discriminant = b * b - c;
                if (discriminant <= 0.0)
                    return half4(ApplyCloudCue(
                        source.rgb, ray, cameraRadial, cameraDay,
                        0.0h), source.a);
                float root = sqrt(discriminant);
                float enter = max(0.0, -b - root);
                float leave = max(0.0, -b + root);
                float segment = max(0.0, leave - enter);
                if (segment <= 0.0001) return source;

                float thickness = max(0.01, outerRadius - innerRadius);
                float3 samplePoint = _WorldSpaceCameraPos + ray * (enter + segment * 0.5);
                float3 radial = SafeNormalize(samplePoint - center);
                float normalizedAltitude = saturate(
                    (distance(samplePoint, center) - innerRadius) / thickness);
                float heightDensity = exp2(
                    -normalizedAltitude * max(0.1, _ElementalAerialPerspectiveParams.z) * 3.0);
                float distanceDensity = 1.0 - exp2(
                    -segment / max(1.0, _ElementalAerialPerspectiveParams.y));
                float horizon = pow(
                    saturate(1.0 - abs(dot(ray, radial))),
                    max(0.2, _ElementalAtmosphereParams.w));
                float day = saturate(dot(radial, sunDirection) * 0.5 + 0.5);
                float forwardMie = pow(saturate(dot(ray, sunDirection)), 12.0);
                float opticalDepth = distanceDensity * heightDensity *
                                     max(0.0, _ElementalAerialPerspectiveParams.x) *
                                     (0.58 + horizon * 0.72);
                float nightVisibility = lerp(1.0, 0.38, saturate(_ElementalNight01));
                half extinction = min(
                    max(0.0, _ElementalAerialPerspectiveParams.w),
                    saturate(opticalDepth) * nightVisibility);

                // The old Rayleigh term saturated before composition (2.1 in the
                // production profile), washing low-sun Mie into white/cyan. Keep
                // noon blue, then let warm forward scatter own the low horizon.
                half3 scatter = _ElementalRayleighColor.rgb *
                                (_ElementalAtmosphereParams.y * (0.22 + day * 0.28));
                scatter += _ElementalMieColor.rgb * forwardMie *
                           (_ElementalAtmosphereParams.z * 0.72);
                half sunFacing = (half)smoothstep(
                    0.18, 0.98, dot(ray, sunDirection) * 0.5 + 0.5);
                half warmHorizon = saturate((half)_ElementalTwilight01) *
                                   (half)horizon * (0.28h + sunFacing * 0.72h);
                half3 duskScatter = lerp(
                    _ElementalMieColor.rgb,
                    half3(1.0h, 0.24h, 0.34h),
                    0.20h) * (0.48h + (half)forwardMie * 0.38h);
                scatter = lerp(scatter, duskScatter, saturate(warmHorizon * 0.88h));
                half nightScatter = lerp(1.0h, (half)_ElementalNightOpacity,
                                         saturate((half)_ElementalNight01));
                scatter = saturate(scatter * nightScatter);
                half transmittance = 1.0h - extinction;
                half3 composed = source.rgb * transmittance + scatter * extinction;

                composed = ApplyCloudCue(
                    composed, ray, cameraRadial, (half)day,
                    0.0h);

                return half4(composed, source.a);
            }
            ENDHLSL
        }
    }
}
