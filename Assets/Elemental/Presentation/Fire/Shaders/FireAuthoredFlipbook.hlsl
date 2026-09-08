#ifndef EL_ELEMENTAL_FIRE_AUTHORED_FLIPBOOK_INCLUDED
#define EL_ELEMENTAL_FIRE_AUTHORED_FLIPBOOK_INCLUDED

// Shared Wallcoeur3x3 animation. All material inputs stay explicit so this
// include can be used by both normal transparent and post-fog decorative passes.
float EF_AuthoredFlameFrame(TEXTURE2D_PARAM(atlas, atlasSampler),
    float2 atlasTexelSize, float2 uv, float frame)
{
    float2 cell = float2(fmod(frame, 3), 2 - floor(frame / 3));
    uv = clamp(uv, atlasTexelSize * 1.5, 1 - atlasTexelSize * 1.5);
    float4 texel = SAMPLE_TEXTURE2D(atlas, atlasSampler, (cell + uv) / 3);
    return texel.r * texel.a;
}

float EF_AuthoredFlameExpansion(float age)
{
    return lerp(.85, 1.12, smoothstep(0, .75, age))
        * lerp(1, .55, smoothstep(.35, 1, age));
}

void EF_AuthoredFlame_float(TEXTURE2D_PARAM(atlas, atlasSampler),
    float2 atlasTexelSize, float2 uv, float time, float distortion, float phase, float age, float heat01,
    float opacity, float3 edgeColor, float3 bodyColor, float3 coreColor,
    float coreEmission, out float3 color, out float alpha)
{
    // Physical parcel age selects the dissipating sequence. A persistent phase
    // only mirrors the drawing; newborn parcels never jump to a dying frame.
    if (sin(phase) > 0) uv.x = 1 - uv.x;
    // Pair the authored silhouette with slow opposing noise flows, as in the
    // Vefects flame material. This deforms the drawing, not the camera image.
    // The base and outer border stay anchored; simulation time respects pause.
    float2 flowUv = uv * float2(2.1, 2.7) + phase;
    float2 flow = float2(
        EF_Noise2(flowUv + float2(time * .21, -time * .85)),
        EF_Noise2(flowUv * 1.37 + float2(-time * .17, -time * .61))) - .5;
    float edgeWindow = 16 * uv.x * (1 - uv.x) * uv.y * (1 - uv.y);
    uv += flow * (.07 * saturate(distortion) * edgeWindow);
    float frame = saturate(age) * 8.999;
    float first = floor(frame);
    float mask = lerp(
        EF_AuthoredFlameFrame(TEXTURE2D_ARGS(atlas, atlasSampler), atlasTexelSize, uv, first),
        EF_AuthoredFlameFrame(TEXTURE2D_ARGS(atlas, atlasSampler), atlasTexelSize, uv, min(first + 1, 8)),
        smoothstep(0, 1, frac(frame)));
    float heat = saturate((1 - age) * heat01);
    float core = (1 - smoothstep(.16, .48, abs(uv.x - .5))) * (1 - smoothstep(.2, .83, uv.y));
    color = lerp(edgeColor, bodyColor, smoothstep(.08, .55, heat));
    color = lerp(color, coreColor * (1 + coreEmission), core * smoothstep(.25, .8, heat));
    alpha = mask * opacity * smoothstep(0, .06, age) * pow(1 - smoothstep(.3, .88, age), 2);
}

#endif
