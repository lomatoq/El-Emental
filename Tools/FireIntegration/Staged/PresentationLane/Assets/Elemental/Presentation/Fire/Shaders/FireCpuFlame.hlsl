#ifndef EL_ELEMENTAL_FIRE_CPU_FLAME_INCLUDED
#define EL_ELEMENTAL_FIRE_CPU_FLAME_INCLUDED

// CPU fallback presentation variant: wider soft parcels with rare hot cores.
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
    float phase = Phase; // radians; stable per-particle seed, not a moving value.
    float n0 = EF_Noise2(float2(y * 3.3 + phase, y * 2.4 - t));
    float n1 = EF_Noise2(UV * float2(5.0, 7.0) + float2(phase, -t * 1.9));
    float x = UV.x - 0.5;
    x += (n0 - 0.5) * max(Distortion, 0.0) * (0.035 + 0.30 * y * y);
    float width = 0.46 * pow(max(1.0 - y, 0.0), 0.62)
        * smoothstep(0.01, 0.14, y) + 0.012;
    float field = 1.0 - abs(x) / max(width, 0.008);
    field += (n1 - 0.5) * 0.7 * y * y;
    // Moving narrow neck makes the upper tongue separate, rather than only fade.
    float neck = frac(time * 0.75 + phase * 0.15915494);
    float neckY = lerp(0.35, 0.86, neck);
    float pinch = exp(-pow((y - neckY) / 0.065, 2.0));
    field -= pinch * 0.28 * smoothstep(0.05, 0.25, neck);
    field -= smoothstep(0.58, 1.0, age) * (0.35 + 0.45 * y);
    float aa = max(fwidth(field), 0.008);
    float coverage = smoothstep(-0.10-aa, 0.60+aa, field);
    coverage *= coverage;
    float envelope = smoothstep(0.015, 0.055, y)
        * (1.0 - smoothstep(0.91, 0.985, y));
    envelope *= smoothstep(0.005, 0.04, UV.x)
        * (1.0 - smoothstep(0.96, 0.995, UV.x));
    float birth = smoothstep(0.0, 0.10, age);
    float death = 1.0 - smoothstep(0.40, 1.0, age);
    float parcelOpacity = lerp(0.56, 1.0, frac(phase * 2.173));
    Alpha = saturate(coverage * envelope * birth * death * death * parcelOpacity * saturate(Opacity));
    float band1 = smoothstep(0.12, 0.70, field);
    float core = smoothstep(0.72, 1.03, field)
        * (1.0 - smoothstep(0.25, 0.75, y))
        * (1.0 - smoothstep(0.15, 0.70, age)) * lerp(0.35, 1.0, frac(phase * 3.117));
    Color = lerp(EdgeColor, BodyColor, band1);
    Color = lerp(Color, CoreColor, core);
    Color += max(CoreColor, 0.0) * core * saturate(Heat01) * max(CoreEmission, 0.0);
}
#endif
