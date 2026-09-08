#ifndef ELEMENTAL_SEISMIC_VISION_INCLUDED
#define ELEMENTAL_SEISMIC_VISION_INCLUDED
float _EarthSeismicVision;
float _EarthSeismicMotion01;
float4 _EarthSeismicWaves16[16];
float _EarthSeismicStrengths16[16];
float _EarthSeismicRadiusTravels16[16];

float EarthSeismicTemporalPulse(float radialDistance, float currentRadius,
                                 float radiusTravel, float width)
{
    currentRadius = max(0.0, currentRadius);
    float previousRadius = max(0.0, currentRadius - max(0.0, radiusTravel));
    float previousPulse = 1.0 - smoothstep(
        width, width + 0.16, abs(radialDistance - previousRadius));
    float currentPulse = 1.0 - smoothstep(
        width, width + 0.16, abs(radialDistance - currentRadius));
    return (previousPulse + currentPulse) * 0.5;
}

float3 EarthSeismicWorldAt(float2 uv)
{
    float depth = SampleSceneDepth(uv);
    #if !UNITY_REVERSED_Z
        depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
    #endif
    return ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
}

float3 EarthSeismicSurfaceNormal(float2 uv, float3 center)
{
    // A two-pixel baseline suppresses depth quantization noise. Choose the
    // nearest neighbour on each axis so a background pixel cannot tilt a
    // foreground character's reconstructed normal at its silhouette.
    float2 pixel = 2.0 / _ScaledScreenParams.xy;
    float3 left = center - EarthSeismicWorldAt(uv - float2(pixel.x, 0));
    float3 right = EarthSeismicWorldAt(uv + float2(pixel.x, 0)) - center;
    float3 down = center - EarthSeismicWorldAt(uv - float2(0, pixel.y));
    float3 up = EarthSeismicWorldAt(uv + float2(0, pixel.y)) - center;
    float3 dx = dot(left, left) < dot(right, right) ? left : right;
    float3 dy = dot(down, down) < dot(up, up) ? down : up;
    return SafeNormalize(cross(dy, dx));
}

void EarthSeismicField(float3 positionWS, out half wave, out half reveal)
{
    wave = 0.0h;
    reveal = 0.0h;
    [unroll] for (int i = 0; i < 16; i++)
    {
        if (_EarthSeismicStrengths16[i] <= 0.0) continue;
        float radialDistance = distance(positionWS, _EarthSeismicWaves16[i].xyz);
        float currentRadius = max(0.0, _EarthSeismicWaves16[i].w);

        // Integrate the previous and current shell samples over the rendered
        // interval. At 10 m/s the 0.16 m edge feather is crossed inside one 30 Hz
        // frame; evaluating only the current radius can therefore jump a pixel
        // straight from dark to peak. The two-sample box filter gives that crossing
        // a half-coverage frame without broadening a stationary front.
        float width = clamp(fwidth(radialDistance) * 0.7, 0.06, 0.12);
        float pulse = EarthSeismicTemporalPulse(
            radialDistance, currentRadius, _EarthSeismicRadiusTravels16[i], width) *
            _EarthSeismicStrengths16[i];
        wave = max(wave, (half)pulse);

        float delta = radialDistance - currentRadius;
        reveal = max(reveal, (half)((1.0 - smoothstep(-0.12, 0.35, delta)) *
            (1.0 - smoothstep(3.0, 18.0, -delta)) * _EarthSeismicStrengths16[i]));
    }
}

half4 ApplyEarthSeismicVision(half4 source, float2 uv)
{
    // Inactive output stays pixel-identical and never reads scene depth.
    if (_EarthSeismicVision <= 0.0) return source;
    half blend = (half)saturate(_EarthSeismicVision);
    float depth = SampleSceneDepth(uv);
    #if UNITY_REVERSED_Z
        if (depth <= 0.00001) return half4(lerp(source.rgb, half3(0.004h, 0.004h, 0.004h), blend), source.a);
    #else
        if (depth >= 0.99999) return half4(lerp(source.rgb, half3(0.004h, 0.004h, 0.004h), blend), source.a);
        depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
    #endif
    float3 positionWS = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
    float3 normalWS = EarthSeismicSurfaceNormal(uv, positionWS);
    half shape = 0.12h + 0.24h * abs(dot(normalWS, SafeNormalize(float3(0.35, 0.83, 0.43))));
    half wave, reveal;
    EarthSeismicField(positionWS, wave, reveal);
    half waveGain = lerp(0.70h, 0.58h, (half)saturate(_EarthSeismicMotion01));
    half luminance = 0.008h + shape * reveal + wave * waveGain;
    return half4(lerp(source.rgb, luminance.xxx, blend), source.a);
}
#endif
