#ifndef ELEMENTAL_SEISMIC_VISION_INCLUDED
#define ELEMENTAL_SEISMIC_VISION_INCLUDED
float _EarthSeismicVision;
float4 _EarthSeismicWaves[5];
float _EarthSeismicStrengths[5];

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

half4 ApplyEarthSeismicVision(half4 source, float2 uv)
{
    if (_EarthSeismicVision < 0.5) return source;
    float depth = SampleSceneDepth(uv);
    #if UNITY_REVERSED_Z
        if (depth <= 0.00001) return half4(0.004h, 0.004h, 0.004h, source.a);
    #else
        if (depth >= 0.99999) return half4(0.004h, 0.004h, 0.004h, source.a);
        depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
    #endif
    float3 positionWS = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
    float3 normalWS = EarthSeismicSurfaceNormal(uv, positionWS);
    half shape = 0.12h + 0.24h * abs(dot(normalWS, SafeNormalize(float3(0.35, 0.83, 0.43))));
    half wave = 0.0h;
    half reveal = 0.0h;
    [unroll] for (int i = 0; i < 5; i++)
    {
        float radialDistance = distance(positionWS, _EarthSeismicWaves[i].xyz);
        float delta = radialDistance - _EarthSeismicWaves[i].w;
        // A depth discontinuity is not a wide wavefront. Unbounded screen
        // derivatives made isolated bright pixels crawl along every silhouette.
        float width = clamp(fwidth(radialDistance) * 1.4, 0.12, 0.24);
        float pulse = (1.0 - smoothstep(width, width + 0.32, abs(delta))) * _EarthSeismicStrengths[i];
        wave = max(wave, (half)pulse);
        // An illuminated wake behind each front exposes shape without restoring albedo/color.
        reveal = max(reveal, (half)((1.0 - smoothstep(-0.12, 0.35, delta)) *
            (1.0 - smoothstep(3.0, 18.0, -delta)) * _EarthSeismicStrengths[i]));
    }
    half luminance = 0.008h + shape * reveal + wave * 1.45h;
    return half4(luminance.xxx, source.a);
}
#endif
