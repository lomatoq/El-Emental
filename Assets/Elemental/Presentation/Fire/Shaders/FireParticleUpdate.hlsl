// Copy both files to the exact directory below, or update this include.
#include "Assets/Elemental/Presentation/Fire/Shaders/FireFieldCommon.hlsl"

// VFX Graph Custom HLSL block, Update context. Select EF_FireStep.
// IMPORTANT: disable the Update context's automatic position integration.
// Keep automatic age/reap enabled. Pass the graph's own Delta Time, not CPU dt.
void EF_FireStep(inout VFXAttributes attributes,
    in StructuredBuffer<float4> FireNodes, in uint FireNodeCount,
    in StructuredBuffer<float4> FireContacts, in uint FireContactCount,
    in float3 FireOriginWS, in float3 FireFreeUpWS,
    in float FireDeltaTime, in float FireTime,
    in float FireFreeDrag, in float FireFreeLift,
    in float FireMaxSpeed, in float FireParticleRadius,
    in uint FireSubsteps)
{
    attributes.fireAge = saturate(attributes.age / max(attributes.lifetime, 0.001));
    float dt = max(FireDeltaTime, 0.0);
    if (dt <= 0.0) return;
    uint steps = clamp(FireSubsteps, 1u, 4u);
    float h = dt / (float)steps; // Do not silently discard elapsed simulation time.
    float3 p = attributes.position - FireOriginWS;
    float3 v = attributes.velocity;
    float3 freeUp = EF_Normalize(FireFreeUpWS, float3(0, 1, 0));
    uint contactCount = min(FireContactCount, EF_MAX_CONTACTS);
    [loop] for (uint s = 0u; s < steps; ++s)
    {
        float t = (float)s * h;
        float3 old = p;
        float3 target;
        float response;
        EF_SampleField(FireNodes, FireNodeCount, p,
            FireTime - dt + t, target, response);
        if (response > 0.0)
            v = lerp(v, target, 1.0 - exp(-response * h));
        else
            v = (v + freeUp * max(FireFreeLift, 0.0) * h)
                * exp(-max(FireFreeDrag, 0.0) * h);
        [loop] for (uint j = 0u; j < contactCount; ++j)
            EF_SteerContact(FireContacts, j, p, t, FireParticleRadius,
                attributes.firePhase, h, v);
        v = EF_Limit(v, max(FireMaxSpeed, 0.0));
        p += v * h;
        // Sequential projections keep separate constraints at corners.
        // A bounded approximation, not exact CCD of arbitrary mesh geometry.
        [unroll] for (uint iteration = 0u; iteration < 3u; ++iteration)
            [loop] for (uint projectedContact = 0u; projectedContact < contactCount; ++projectedContact)
                EF_ResolveContact(FireContacts, projectedContact, old, t, h,
                    FireParticleRadius, attributes.firePhase, p, v);
    }
    attributes.position = p + FireOriginWS;
    attributes.velocity = v;
}


