Shader "Elemental/Atmosphere Fullscreen"
{
    Properties
    {
        _CloudNoise("Cloud Noise", 2D) = "gray" {}
        _SunDustStrength("Sun Dust Shafts / Intensity", Range(0, 0.25)) = 0
        _SunDustDistance("Sun Dust Shafts / Distance (m)", Range(2, 40)) = 22
        _SunDustWidth("Sun Dust Shafts / Width (m)", Range(0.5, 10)) = 3.5
        _SunDustHeight("Sun Dust Shafts / Height (m)", Range(1, 20)) = 9
        _SunDustColor("Sun Dust Shafts / Tint", Color) = (1, 0.83, 0.58, 1)
        _GameplayEdgeBlur("Gameplay Vignette / Blur Mix", Range(0, 0.75)) = 0.4
        _GameplayEdgeRadius("Gameplay Vignette / Radius at 1080p (px)", Range(0, 8)) = 4
        _GameplayEdgeDarkness("Gameplay Vignette / Darkness", Range(0, 0.3)) = 0.2
        _GameplayEdgeClear("Gameplay Vignette / Clear Centre", Range(0.2, 0.8)) = 0.42
        _GameplayEdgeFeather("Gameplay Vignette / Feather End", Range(0.9, 1.5)) = 1.25
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "EarthSeismicVision.hlsl"

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
            float _SunDustStrength, _SunDustDistance, _SunDustWidth, _SunDustHeight;
            half4 _SunDustColor;
            float _GameplayEdgeBlur, _GameplayEdgeRadius, _GameplayEdgeDarkness;
            float _GameplayEdgeClear, _GameplayEdgeFeather;

            float GameplayEdgeMask(float2 uv)
            {
                float feather = smoothstep(_GameplayEdgeClear,
                    max(_GameplayEdgeClear + 0.2, _GameplayEdgeFeather), length((uv - 0.5) * 2.0));
                return feather * feather;
            }

            half4 SoftEdgeSource(float2 uv)
            {
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float amount = GameplayEdgeMask(uv) * saturate(_GameplayEdgeBlur);
                if (amount < 0.001 || _GameplayEdgeRadius < 0.01) return source;
                // Compact symmetric tent: clear centre, broad feather, constant
                // visual radius across resolutions. No additional render target.
                float2 stepUv = _BlitTexture_TexelSize.xy * clamp(_GameplayEdgeRadius, 0, 8) *
                    (_BlitTexture_TexelSize.w / 1080.0);
                half3 blurred = source.rgb * 0.25h;
                blurred += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(stepUv.x, 0)).rgb * 0.125h;
                blurred += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(stepUv.x, 0)).rgb * 0.125h;
                blurred += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0, stepUv.y)).rgb * 0.125h;
                blurred += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(0, stepUv.y)).rgb * 0.125h;
                blurred += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + stepUv).rgb * 0.0625h;
                blurred += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - stepUv).rgb * 0.0625h;
                blurred += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(stepUv.x, -stepUv.y)).rgb * 0.0625h;
                blurred += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-stepUv.x, stepUv.y)).rgb * 0.0625h;
                return half4(lerp(source.rgb, blurred, amount), source.a);
            }

            float _ElementalOverheadCloudCount;
            float4x4 _ElementalOverheadWorldToFrame;
            float4 _ElementalOverheadCloudCenters[8],_ElementalOverheadCloudRadii[8];
            float CloudSunVisibility(float3 world,float3 sun)
            {
                float3 origin=mul(_ElementalOverheadWorldToFrame,float4(world,1)).xyz;
                float3 direction=mul((float3x3)_ElementalOverheadWorldToFrame,sun);
                float optical=0;
                [loop] for(int i=0;i<(int)_ElementalOverheadCloudCount;i++)
                {
                    float3 radii=max(_ElementalOverheadCloudRadii[i].xyz,1);
                    float3 q=(origin-_ElementalOverheadCloudCenters[i].xyz)/radii;
                    float3 d=direction/radii;
                    float t=max(0,-dot(q,d)/max(dot(d,d),1e-6));
                    float3 closest=q+d*t;
                    optical+=pow(saturate(1-dot(closest,closest)),2)*1.6;
                }
                return exp(-optical);
            }
            half3 ApplySunDust(half3 source, float3 ray, float travel)
            {
                float daylight = saturate(1.0 - _ElementalNight01) * smoothstep(-0.06, 0.06, _ElementalSolarAltitude);
                if (_SunDustStrength < 0.001 || daylight < 0.001) return source;
                float3 sun = SafeNormalize(_ElementalSunDirection.xyz);
                float3 side = SafeNormalize(cross(sun, abs(sun.y) < 0.95 ? float3(0,1,0) : float3(1,0,0)));
                float3 across = cross(sun, side);
                float span = min(travel, max(2.0, _SunDustDistance));
                float density = 0;
                float cloudVisibility=CloudSunVisibility(_WorldSpaceCameraPos+ray*(span*.5),sun);
                // Integrate sunlight along the visible air segment. The existing
                // directional shadow atlas creates gaps behind real arena stones.
                [unroll] for (int index = 0; index < 16; index++)
                {
                    float3 airPosition = _WorldSpaceCameraPos + ray * (span * ((index + 0.5) / 16.0));
                    float3 relative = airPosition - _ElementalPlanetCenterRadius.xyz;
                    float height = max(0.0, length(relative) - _ElementalPlanetCenterRadius.w);
                    float2 uv = float2(dot(relative, side), dot(relative, across)) / max(0.5, _SunDustWidth);
                    uv = uv * 0.085 + float2(_Time.y * 0.0013, _Time.y * -0.0007);
                    half noise = SAMPLE_TEXTURE2D_LOD(_CloudNoise, sampler_CloudNoise, uv, 0).r;
                    float lit=MainLightRealtimeShadow(TransformWorldToShadowCoord(airPosition))*cloudVisibility;
                    density += lit * lerp(.28,1.0,smoothstep(.3,.7,noise)) * exp2(-height / max(1.0, _SunDustHeight));
                }
                float forward = pow(saturate(dot(ray, sun) * 0.5 + 0.5), 4.0);
                float amount = min(0.30, density / 16.0 * (1.0 - exp2(-span * 0.08)) *
                    _SunDustStrength * daylight * (0.65 + forward * 2.4));
                float3 tint=lerp(_SunDustColor.rgb,float3(1,.44,.16),saturate(_ElementalTwilight01));
                return source*(1-amount*.35)+tint*amount;
            }

            float3 RotateCloudDirection(float3 direction, float radians)
            {
                float sine;
                float cosine;
                sincos(radians, sine, cosine);
                return float3(
                    direction.x * cosine + direction.z * sine,
                    direction.y,
                    -direction.x * sine + direction.z * cosine);
            }

            half SampleDirectionCloudNoise(float3 direction)
            {
                // Latitude/longitude has an unavoidable pole: every longitude
                // collapses to one texel there. At dusk that produced the visible
                // triangular pinwheel beside the Moon. A softly blended cube-style
                // projection has no singular direction. Each plane spans one copy
                // of the authored repeatable noise, so it also keeps cloud features
                // at a bounded angular size instead of stretching one row around
                // the horizon.
                float3 p = SafeNormalize(direction);
                float3 weights = pow(abs(p), 4.0);
                weights /= max(0.0001, weights.x + weights.y + weights.z);

                float2 uvX = p.zy * 0.5 + 0.5;
                float2 uvY = p.xz * 0.5 + 0.5;
                float2 uvZ = p.xy * 0.5 + 0.5;
                // Axis-specific offsets decorrelate the three faces while the
                // repeatable source texture keeps opposite cube borders continuous.
                uvX += float2(0.173, 0.619);
                uvY += float2(0.487, 0.271);
                uvZ += float2(0.731, 0.043);
                half noiseX = SAMPLE_TEXTURE2D(
                    _CloudNoise, sampler_CloudNoise, uvX).r;
                half noiseY = SAMPLE_TEXTURE2D(
                    _CloudNoise, sampler_CloudNoise, uvY).r;
                half noiseZ = SAMPLE_TEXTURE2D(
                    _CloudNoise, sampler_CloudNoise, uvZ).r;
                return noiseX * (half)weights.x +
                       noiseY * (half)weights.y +
                       noiseZ * (half)weights.z;
            }

            half3 ApplyCloudCue(
                half3 baseColor,
                float3 ray,
                float3 cameraRadial,
                half day,
                half geometryMask)
            {
                if (geometryMask > 0.5h || _ElementalCloudParams.y <= 0.001)
                    return baseColor;

                float cloudTime = _Time.y * _ElementalCloudParams.w;
                // The former ray.xz / abs(ray.y) plane projection has zero
                // vertical derivative at the clamped horizon. It turns one noise
                // patch into a horizontal strip. Advect normalized world directions
                // over a sphere instead, preserving angular width and height.
                float angularTravel = cloudTime * 6.28318530718;
                float detailScale = max(0.1, _ElementalCloudParams.z);
                float3 cloudDirectionA = RotateCloudDirection(
                    ray, angularTravel * detailScale);
                // A cyclic axis permutation is a rigid rotation, not UV scaling.
                // It decorrelates the second sample without stretching or tiling it.
                float3 cloudDirectionB = RotateCloudDirection(
                    ray.zxy, -angularTravel * (0.37 + detailScale * 0.11) + 1.731);
                half cloudA = SampleDirectionCloudNoise(cloudDirectionA);
                half cloudB = SampleDirectionCloudNoise(cloudDirectionB);
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

            #include "ValleyAtmosphereV2.hlsl"

            half4 FragAtmosphere(Varyings input)
            {
                float2 uv = input.texcoord;
                half4 source = SoftEdgeSource(uv);
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
                    // Distant opaque depth can be smaller than 1e-5 with a short
                    // near plane. Only the exact cleared depth denotes sky.
                    bool hasGeometry = rawDepth > 0.0;
                #else
                    bool hasGeometry = rawDepth < 1.0;
                #endif
                if (_ElementalValleyEnabled > 0.5)
                    return ApplyValleyAtmosphere(source, uv, rawDepth, hasGeometry);
                // The fullscreen atmosphere owns sky/limb scattering only. Applying
                // a single midpoint-density estimate to nearby opaque geometry made
                // broad screen-space altitude bands crawl across the arena, character
                // and shadows. Those bands survived the albedo/unlit debug views and
                // vanished only when this pass was disabled. Never apply that sky
                // density approximation to opaque surfaces. The separate, bounded
                // four-sample sunlight cue integrates only air before their depth.
                if (hasGeometry)
                {
                    float deviceDepth = rawDepth;
                    #if !UNITY_REVERSED_Z
                        deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                    #endif
                    float3 surface = ComputeWorldSpacePosition(uv, deviceDepth, UNITY_MATRIX_I_VP);
                    float travel = distance(surface, _WorldSpaceCameraPos);
                    // Integrate only air in front of the nearest opaque surface.
                    return half4(ApplySunDust(source.rgb, ray, travel), source.a);
                }
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

                return half4(ApplySunDust(composed, ray, _SunDustDistance), source.a);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = ApplyEarthSeismicVision(FragAtmosphere(input), input.texcoord);
                color.rgb *= 1.0h - (half)(GameplayEdgeMask(input.texcoord) * clamp(_GameplayEdgeDarkness, 0, 0.3));
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "Seismic Hostile Silhouette"
            ZWrite Off ZTest Always Cull Back
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VertHostile
            #pragma fragment FragHostile
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "EarthSeismicVision.hlsl"
            struct HostileVaryings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };
            HostileVaryings VertHostile(float3 positionOS : POSITION, float3 normalOS : NORMAL)
            {
                HostileVaryings output;
                output.positionWS = TransformObjectToWorld(positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(normalOS);
                return output;
            }
            half4 FragHostile(HostileVaryings input) : SV_Target
            {
                half wave, reveal;
                EarthSeismicField(input.positionWS, wave, reveal);
                half3 normal = normalize(input.normalWS);
                half rim = 1.0h - abs(dot(normal, GetWorldSpaceNormalizeViewDir(input.positionWS)));
                half shade = 0.85h + 0.22h * abs(dot(normal, normalize(half3(.35h, .83h, .43h))));
                half light = shade + rim * rim * 0.35h;
                return half4(light.xxx, saturate(max(wave, reveal) * (half)_EarthSeismicVision));
            }
            ENDHLSL
        }
    }
}
