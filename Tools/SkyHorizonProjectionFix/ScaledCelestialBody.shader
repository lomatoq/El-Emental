Shader "Elemental/Scaled Celestial Body"
{
    Properties
    {
        _BaseColor("Surface Color", Color) = (0.52,0.57,0.68,1)
        _NightFill("Night Fill", Range(0,0.25)) = 0.025
        _SunGain("Sun Gain", Range(0,3)) = 1.25
        _RimStrength("Rim Strength", Range(0,1)) = 0.08
        [HideInInspector] _CelestialVisibility("Celestial Visibility", Range(0,1)) = 0
        [HideInInspector] _IsMoon("Is Moon", Range(0,1)) = 0
        [HideInInspector] _MoonPhase01("Moon Phase", Range(0,1)) = 1
    }
    SubShader
    {
        // Draw after the skybox but before the existing fullscreen atmosphere pass.
        // With no depth write the atmosphere integrates over the body instead of
        // treating it as foreground geometry; opaque world geometry still occludes it.
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-100" }
        Pass
        {
            Name "Celestial Phase"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _NightFill;
                half _SunGain;
                half _RimStrength;
                half _CelestialVisibility;
                half _IsMoon;
                half _MoonPhase01;
            CBUFFER_END
            float4 _ElementalSunDirection;
            float4 _ElementalPlanetCenterRadius;
            float4 _ElementalMieColor;
            float _ElementalTwilight01;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 normalOS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.normalOS = normalize(input.normalOS);
                return output;
            }

            half MoonDisk(half3 normalOS, half3 center, half innerCos, half outerCos)
            {
                return smoothstep(outerCos, innerCos,
                    dot(normalOS, normalize(center)));
            }

            half MoonCrater(
                half3 normalOS,
                half3 center,
                half innerCos,
                half outerCos,
                half depth)
            {
                half alignment = dot(normalOS, normalize(center));
                half bowl = smoothstep(outerCos, innerCos, alignment);
                half rimWidth = max(0.0015h, (innerCos - outerCos) * 0.24h);
                half rim = smoothstep(outerCos - rimWidth, outerCos, alignment) -
                    smoothstep(outerCos, innerCos, alignment);
                return rim * depth * 0.62h - bowl * depth;
            }

            half MoonSurface(half3 normalOS)
            {
                // Broad maria, varied crater rims and a restrained high-frequency
                // grain make the Moon read as stone at close FOV while all detail
                // remains object-space and therefore stable through its orbit.
                half detail = 1.0h;
                half maria = 0.0h;
                maria += MoonDisk(normalOS, half3( 0.25h,  0.24h, -0.94h), 0.954h, 0.900h) * 0.13h;
                maria += MoonDisk(normalOS, half3(-0.31h,  0.37h, -0.88h), 0.970h, 0.928h) * 0.10h;
                maria += MoonDisk(normalOS, half3( 0.47h, -0.21h, -0.86h), 0.976h, 0.942h) * 0.085h;
                maria += MoonDisk(normalOS, half3(-0.10h, -0.42h, -0.90h), 0.982h, 0.956h) * 0.065h;

                detail += MoonCrater(normalOS, half3( 0.34h,  0.16h, -0.93h), 0.992h, 0.976h, 0.105h);
                detail += MoonCrater(normalOS, half3(-0.42h,  0.31h, -0.85h), 0.987h, 0.964h, 0.090h);
                detail += MoonCrater(normalOS, half3( 0.08h, -0.47h, -0.88h), 0.994h, 0.981h, 0.075h);
                detail += MoonCrater(normalOS, half3( 0.61h, -0.18h, -0.77h), 0.990h, 0.972h, 0.080h);
                detail += MoonCrater(normalOS, half3(-0.66h, -0.08h, -0.74h), 0.995h, 0.986h, 0.060h);
                detail += MoonCrater(normalOS, half3( 0.55h,  0.43h, -0.72h), 0.996h, 0.988h, 0.054h);
                detail += MoonCrater(normalOS, half3(-0.13h,  0.64h, -0.76h), 0.996h, 0.989h, 0.048h);
                detail += MoonCrater(normalOS, half3( 0.20h, -0.68h, -0.71h), 0.997h, 0.991h, 0.042h);
                detail += MoonCrater(normalOS, half3(-0.51h, -0.46h, -0.73h), 0.997h, 0.992h, 0.038h);
                detail += MoonCrater(normalOS, half3( 0.72h,  0.09h, -0.68h), 0.997h, 0.993h, 0.034h);

                half grainA = sin(dot(normalOS, half3(41.7h, 67.3h, 89.1h)) + 0.7h);
                half grainB = sin(dot(normalOS, half3(97.4h, 53.8h, 31.6h)) - 1.9h);
                half grainC = sin(dot(normalOS, half3(73.2h, 109.7h, 47.5h)) + 2.4h);
                half grain = grainA * grainB * grainC;
                return saturate(detail - saturate(maria) + grain * 0.018h);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = SafeNormalize(input.normalWS);
                half3 sun = SafeNormalize(_ElementalSunDirection.xyz);
                half diffuse = smoothstep(-0.025h, 0.16h, dot(normal, sun));
                half3 view = SafeNormalize(_WorldSpaceCameraPos - input.positionWS);
                half rim = pow(saturate(1.0h - dot(normal, view)), 3.5h);
                half isMoon = saturate(_IsMoon);
                half moonEarthshine = _NightFill * lerp(1.0h, 0.30h, isMoon);
                half phaseRim = rim * _RimStrength * lerp(1.0h, 0.45h + 0.55h * _MoonPhase01, isMoon);
                half illumination = moonEarthshine + diffuse * _SunGain + phaseRim;
                half surface = lerp(1.0h, MoonSurface(normalize(input.normalOS)), isMoon);

                // A new Moon must not become an opaque black disc. Premultiplied
                // coverage follows physical solar illumination; the atmosphere pass
                // then scatters over this result. The system planet remains opaque
                // once its altitude visibility gate opens.
                half moonAlpha = saturate(illumination * 1.08h);
                half visibility = saturate(_CelestialVisibility);
                half alpha = visibility * lerp(1.0h, moonAlpha, isMoon);
                // The authored legacy material is strongly blue. The atmosphere
                // should supply the blue zenith and warm horizon tint; a neutral,
                // slightly warm lunar albedo prevents a generic ice-planet look.
                half materialLuminance = dot(_BaseColor.rgb,
                    half3(0.2126h, 0.7152h, 0.0722h));
                half lunarLuminance = lerp(0.44h, 0.67h,
                    saturate(materialLuminance * 1.45h));
                half3 lunarStone = half3(1.00h, 0.965h, 0.90h) * lunarLuminance;
                half3 observerUp = SafeNormalize(
                    _WorldSpaceCameraPos - _ElementalPlanetCenterRadius.xyz);
                half3 bodyDirection = SafeNormalize(
                    input.positionWS - _WorldSpaceCameraPos);
                half bodyAltitude = dot(bodyDirection, observerUp);
                half horizonMoon = 1.0h - smoothstep(0.025h, 0.30h, abs(bodyAltitude));
                half warmAmount = horizonMoon *
                    (0.24h + saturate((half)_ElementalTwilight01) * 0.68h);
                half miePeak = max(0.15h, max(
                    (half)_ElementalMieColor.r,
                    max((half)_ElementalMieColor.g, (half)_ElementalMieColor.b)));
                half3 mieChroma = (half3)_ElementalMieColor.rgb / miePeak;
                half3 horizonStone = lunarStone * lerp(
                    half3(1.04h, 0.88h, 0.72h),
                    mieChroma * 1.08h,
                    0.58h);
                half3 tintedLunarStone = lerp(lunarStone, horizonStone, warmAmount);
                half3 albedo = lerp(_BaseColor.rgb, tintedLunarStone, isMoon);
                half3 radiance = albedo * max(0.0h, illumination) * surface;
                return half4(radiance * visibility, alpha);
            }
            ENDHLSL
        }
    }
}
