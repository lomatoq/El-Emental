#ifndef EL_ELEMENTAL_FIRE_CPU_FLAME_INCLUDED
#define EL_ELEMENTAL_FIRE_CPU_FLAME_INCLUDED

// CPU fallback presentation variant: broad, three-band parcels with a hot anchored base.
// Derived from the project procedural reference; native Shader Graph is unchanged.
// This file is for Shader Graph FRAGMENT stage, not a VFX compute block:
// fwidth requires pixel derivatives. Set the Custom Function precision to Float.
float EF_Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float EF_Noise2(float2 p)
{
    float2 cell = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = EF_Hash21(cell);
    float b = EF_Hash21(cell + float2(1, 0));
    float c = EF_Hash21(cell + float2(0, 1));
    float d = EF_Hash21(cell + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// Shader Graph Custom Function name: EF_Flame (File mode).
// Color output is STRAIGHT / unpremultiplied HDR RGB. Multiply by alpha only
// once in the chosen pipeline. Standard Alpha blend needs no manual multiply.
void EF_Flame_float(float2 UV, float SimTime, float Phase, float Age01,
    float Heat01, float Opacity, float ShapeFPS, float Distortion,
    float3 EdgeColor, float3 BodyColor, float3 CoreColor, float CoreEmission,
    out float3 Color, out float Alpha)
{
    float time = ShapeFPS > 0.5 ? floor(SimTime * ShapeFPS) / ShapeFPS : SimTime;
    float t = time * 1.7;
    float y = saturate(UV.y);
    float age = saturate(Age01);
    float body = 1.0 - step(0.66, frac(Phase * 2.173));
    float phase = Phase; // radians; stable per-particle seed, not a moving value.
    float n0 = EF_Noise2(float2(y * 3.3 + phase, y * 2.4 - t));
    float n1 = EF_Noise2(UV * float2(2.7, 4.0) + float2(phase, -t * 1.9));
    float x = UV.x - 0.5;
    x += (n0 - 0.5) * max(Distortion, 0.0) * (0.035 + 0.30 * y * y);
    float width = 0.46 * pow(max(1.0 - y, 0.0), 0.62)
        * smoothstep(0.01, 0.14, y) + 0.012;
    float field = 1.0 - abs(x) / max(width, 0.008);
    field += (n1 - 0.5) * 0.32 * y * y;
    // Moving narrow neck makes the upper tongue separate, rather than only fade.
    float neck = frac(time * 0.75 + phase * 0.15915494);
    float neckY = lerp(0.35, 0.86, neck);
    float pinch = exp(-pow((y - neckY) / 0.065, 2.0));
    field -= pinch * 0.20 * smoothstep(0.05, 0.25, neck);
    // The continuous body is a broad rounded parcel, never another narrow spear.
    // Its two large overlapping lobes break symmetry without producing texture grit.
    float2 bodyQ = float2(x, y - 0.44);
    float2 bodyEllipse = bodyQ / float2(0.46, 0.48);
    float bodyField = 1.0 - dot(bodyEllipse, bodyEllipse);
    float2 shoulder = (bodyQ - float2((n0 - 0.5) * 0.16, 0.15)) / float2(0.34, 0.34);
    bodyField = max(bodyField, 1.0 - dot(shoulder, shoulder));
    field = lerp(field, bodyField, body);
    // Old detached parcels contract and disappear instead of drifting as pink ghosts.
    field -= smoothstep(0.48, 0.84, age) * lerp(0.35 + 0.45 * y, 0.75, body);
    float aa = max(fwidth(field), 0.008);
    float coverage = smoothstep(-0.06-aa, 0.22+aa, field);
    // Preserve a bold filled silhouette; derivatives soften its boundary only.
    float envelope = smoothstep(0.015, 0.055, y)
        * (1.0 - smoothstep(0.91, 0.985, y));
    envelope *= smoothstep(0.005, 0.04, UV.x)
        * (1.0 - smoothstep(0.96, 0.995, UV.x));
    float birth = smoothstep(0.0, 0.10, age);
    float death = 1.0 - smoothstep(lerp(0.50, 0.42, body), 0.84, age);
    death *= death;
    float parcelOpacity = lerp(0.78, 1.0, frac(phase * 2.173));
    Alpha = saturate(coverage * envelope * birth * death * parcelOpacity * saturate(Opacity));
    float band1 = smoothstep(0.12, 0.32, field);
    float core = smoothstep(0.55, 0.80, field)
        * (1.0 - smoothstep(0.25, 0.75, y))
        * (1.0 - smoothstep(0.4, 1.0, age)) * lerp(0.72, 1.0, frac(phase * 3.117));
    Color = lerp(EdgeColor, BodyColor, band1);
    Color = lerp(Color, CoreColor, core);
    if (body > 0.5)
    {
        // The majority overlaps into one luminous mass. A dark border and
        // circular core on every parcel make a pile of separate flame icons.
        // Keep the silhouette in alpha; reserve red edging for rare tongues.
        core = (1.0 - smoothstep(0.16, 0.70, y)) * 0.70 * saturate(Heat01);
        Color = lerp(BodyColor, CoreColor, core);
    }
    Color += max(CoreColor, 0.0) * core * saturate(Heat01) * max(CoreEmission, 0.0);
}
#endif
