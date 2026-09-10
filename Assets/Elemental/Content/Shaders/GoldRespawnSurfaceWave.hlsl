#ifndef ELEMENTAL_GOLD_RESPAWN_SURFACE_WAVE_INCLUDED
#define ELEMENTAL_GOLD_RESPAWN_SURFACE_WAVE_INCLUDED
// One production GoldRespawnPresenter owns two fixed actor slots. xyz = reserved
// terrain point / local up, w = inward radius / bounded brightness. No material edits.
float4 _GoldRespawnWaveCenters[2];
float4 _GoldRespawnWaveUps[2];
half3 ElementalGoldRespawnSurfaceWave(float3 positionWS, half3 geometryNormalWS, half surfaceMode)
{
    // Character materials use SurfaceMode=1, including both actual Linebreaker skins.
    if (surfaceMode > 0.5h) return 0;
    float response = 0;
    [unroll] for (int actor = 0; actor < 2; actor++)
    {
        float4 center = _GoldRespawnWaveCenters[actor];
        float4 up = _GoldRespawnWaveUps[actor];
        if (up.w <= 0 || center.w <= 0 || center.w > 3.21) continue;
        float3 delta = positionWS - center.xyz;
        float height = dot(delta, up.xyz);
        float radius = length(delta - up.xyz * height);
        // Bound both the horizontal footprint and vertical projection. This is
        // shaded geometry: slopes/steps participate, empty air and occluded faces do not.
        float heightFade = 1 - smoothstep(0.75, 1.25, abs(height));
        float receiver = smoothstep(0.25, 0.65, dot(geometryNormalWS, up.xyz));
        float aa = max(fwidth(radius), 0.008);
        float band = 1 - smoothstep(0.10, 0.10 + aa * 2 + 0.07, abs(radius - center.w));
        float footprint = 1 - smoothstep(3.35, 3.50, radius);
        response += band * heightFade * receiver * footprint * saturate(up.w);
    }
    // Simultaneous arrivals cannot add an unbounded peak.
    return half3(1.0h, 0.61h, 0.16h) * min(response, 1.25) * 1.6h;
}
#endif
