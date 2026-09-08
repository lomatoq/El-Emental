#include "Assets/Elemental/Presentation/Fire/Shaders/FireFieldCommon.hlsl"

// Init context. VFXRAND macros are evaluated only at particle birth.
// The graph assigns firePhase separately as a stored custom attribute.
void EF_FireInitialize(inout VFXAttributes attributes,
    in StructuredBuffer<float4> FireNodes, in uint FireNodeCount,
    in StructuredBuffer<float4> FireContacts, in uint FireContactCount,
    in float FireParticleRadius, in float3 FireOriginWS, in float FireTime,
    in float FireMinLifetime, in float FireMaxLifetime)
{
    uint count = min(FireNodeCount, EF_MAX_NODES);
    float weights[6];
    float total = 0.0;
    [unroll] for (uint q = 0u; q < EF_MAX_NODES; ++q) weights[q] = 0.0;
    [loop] for (uint i = 0u; i < count; ++i)
    {
        uint k = i * EF_NODE_ROWS;
        float4 a = FireNodes[k];
        float4 b = FireNodes[k + 1u];
        float4 dyn = FireNodes[k + 4u];
        float4 state = FireNodes[k + 5u];
        float r = max(a.w, 0.001);
        float volume;
        if (dyn.w > 0.5 && dyn.w < 1.5)
        {
            float lo = max(r - max(b.w, 0.001), 0.0);
            float hi = r + max(b.w, 0.001);
            volume = (hi * hi * hi - lo * lo * lo) * (4.0 / 3.0);
        }
        else volume = r * r * length(b.xyz - a.xyz) + (4.0 / 3.0) * r * r * r;
        // Common factor PI cancels. Spawn rate is per GROUP, never per node.
        weights[i] = max(volume, 1e-8) * saturate(state.x) * step(0.5, state.y);
        total += weights[i];
    }
    if (total <= 1e-8)
    {
        attributes.alive = false;
        return;
    }
    float selection = VFXRAND * total;
    uint chosen = count - 1u;
    [loop] for (uint i = 0u; i < count; ++i)
    {
        selection -= weights[i];
        if (selection < 0.0) { chosen = i; break; }
    }
    uint k = chosen * EF_NODE_ROWS;
    float4 a = FireNodes[k];
    float4 b = FireNodes[k + 1u];
    float4 dyn = FireNodes[k + 4u];
    float3 axis = EF_Normalize(b.xyz - a.xyz, float3(0, 1, 0));
    float3 tangent = EF_Tangent(axis);
    float3 bitangent = cross(axis, tangent);
    float4 rnd = VFXRAND4;
    float phi = rnd.x * 6.28318530718;
    float z = rnd.y * 2.0 - 1.0;
    float3 unitSphere = float3(sqrt(max(1.0 - z * z, 0.0)) * cos(phi),
        z, sqrt(max(1.0 - z * z, 0.0)) * sin(phi));
    float r = max(a.w, 0.001);
    float3 p;
    if (dyn.w > 0.5 && dyn.w < 1.5)
    {
        float lo = max(r - max(b.w, 0.001), 0.0);
        float hi = r + max(b.w, 0.001);
        float sampledRadius = pow(lerp(lo * lo * lo, hi * hi * hi, rnd.z), 1.0 / 3.0);
        p = a.xyz + unitSphere * sampledRadius;
    }
    else
    {
        float len = length(b.xyz - a.xyz);
        float cylinderVolume = r * r * len;
        float capsVolume = (4.0 / 3.0) * r * r * r;
        if (rnd.w * (cylinderVolume + capsVolume) < cylinderVolume)
            p = lerp(a.xyz, b.xyz, rnd.z)
                + (tangent * cos(phi) + bitangent * sin(phi)) * r * sqrt(rnd.y);
        else
            p = (dot(unitSphere, axis) >= 0.0 ? b.xyz : a.xyz)
                + unitSphere * r * pow(rnd.z, 1.0 / 3.0);
    }
    // Reject samples immediately behind a LOCAL trusted face. CPU admission
    // must still keep domain centres on a valid side and cover spawn volumes.
    // No unbounded retry loop; rejected births are counted in the graph/debug adapter.
    [loop] for (uint j = 0u; j < min(FireContactCount, EF_MAX_CONTACTS); ++j)
    {
        uint ck = j * EF_CONTACT_ROWS;
        float4 cp = FireContacts[ck];
        float4 cn = FireContacts[ck + 1u];
        float4 cs = FireContacts[ck + 5u];
        if (cs.y < 0.5 || cp.w <= 0.0) continue;
        float3 n = EF_Normalize(cn.xyz, float3(0, 1, 0));
        float3 q = p - cp.xyz;
        float signedDistance = dot(q, n);
        float3 lateral = q - n * signedDistance;
        if (dot(lateral, lateral) <= cp.w * cp.w
            && signedDistance < max(FireParticleRadius + cs.x, 0.0)
            && signedDistance > -2.0 * r)
        {
            attributes.alive = false;
            return;
        }
    }
    float3 velocity;
    float response;
    EF_SampleField(FireNodes, count, p, FireTime, velocity, response);
    attributes.position = p + FireOriginWS;
    attributes.velocity = velocity;
    attributes.age = 0.0;
    attributes.lifetime = lerp(max(FireMinLifetime, 0.01),
        max(FireMaxLifetime, max(FireMinLifetime, 0.01)), VFXRAND);
}
