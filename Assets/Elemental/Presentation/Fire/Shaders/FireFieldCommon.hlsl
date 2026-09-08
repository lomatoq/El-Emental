#ifndef EL_ELEMENTAL_FIRE_FIELD_COMMON_INCLUDED
#define EL_ELEMENTAL_FIRE_FIELD_COMMON_INCLUDED

// Original reference implementation. Units: metres, seconds, radians.
// Buffers contain float4 rows; no compiler-dependent mixed-type structs.
#define EF_NODE_ROWS 6u
#define EF_CONTACT_ROWS 6u
#define EF_MAX_NODES 6u
#define EF_MAX_CONTACTS 8u

float3 EF_Normalize(float3 v, float3 fallback)
{
    float q = dot(v, v);
    return q > 1e-10 ? v * rsqrt(q) : fallback;
}

float3 EF_Tangent(float3 n)
{
    float3 a = abs(n.y) < 0.9 ? float3(0, 1, 0) : float3(1, 0, 0);
    return EF_Normalize(cross(a, n), float3(1, 0, 0));
}

float3 EF_Limit(float3 v, float speed)
{
    return v * min(1.0, max(speed, 0.0) / max(length(v), 1e-6));
}

float3 EF_ClosestSegment(float3 p, float3 a, float3 b)
{
    float3 ab = b - a;
    return a + ab * saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-8));
}

// This unweighted analytic field has zero divergence. Multiplying by a
// spatial envelope / clamping changes that property; this is not a fluid solver.
float3 EF_CoherentNoise(float3 p, float t, float frequency, float phase)
{
    float3 q = p * max(frequency, 0.001) + phase;
    return 0.5 * float3(
        sin(q.y + t) + cos(q.z - 0.7 * t),
        sin(q.z + 0.6 * t) + cos(q.x + t),
        sin(q.x - 0.8 * t) + cos(q.y + 0.4 * t));
}

// Node rows:
// 0: A.xyz, radius. 1: B.xyz, shell half-thickness.
// 2: base flow.xyz, response rate. 3: local up.xyz, lift speed.
// 4: angular speed, noise speed, spatial frequency, shape (0 capsule, 1 shell, 2 vortex).
// 5: density, active, phase, maximum target speed.
void EF_SampleField(StructuredBuffer<float4> nodes, uint count,
    float3 p, float time, out float3 target, out float response)
{
    float total = 0.0;
    target = 0.0;
    response = 0.0;
    [loop] for (uint i = 0u; i < min(count, EF_MAX_NODES); ++i)
    {
        uint k = i * EF_NODE_ROWS;
        float4 a = nodes[k];
        float4 b = nodes[k + 1u];
        float4 flow = nodes[k + 2u];
        float4 upLift = nodes[k + 3u];
        float4 dyn = nodes[k + 4u];
        float4 state = nodes[k + 5u];
        if (state.y < 0.5 || state.x <= 0.0) continue;
        float radius = max(a.w, 0.001);
        float3 up = EF_Normalize(upLift.xyz, float3(0, 1, 0));
        float3 axis = EF_Normalize(b.xyz - a.xyz, up);
        float3 centre = EF_ClosestSegment(p, a.xyz, b.xyz);
        float3 radial = p - centre;
        float dist = length(radial);
        float w = 1.0 - smoothstep(radius * 0.6, radius, dist);
        float3 v = flow.xyz + up * upLift.w;
        if (dyn.w > 0.5 && dyn.w < 1.5)
        {
            radial = p - a.xyz;
            dist = length(radial);
            float3 normal = EF_Normalize(radial, up);
            float thickness = max(b.w, 0.001);
            w = 1.0 - smoothstep(thickness * 0.6, thickness, abs(dist - radius));
            v -= normal * dot(v, normal);
            // Relax toward the shell without parenting positions to its transform.
            v -= normal * (dist - radius) * max(flow.w, 0.0);
        }
        v += cross(axis, radial) * dyn.x;
        v += EF_CoherentNoise(p, time, dyn.z, state.z) * dyn.y;
        v = EF_Limit(v, max(state.w, 0.0));
        w *= saturate(state.x);
        target += v * w;
        response += max(flow.w, 0.0) * w;
        total += w;
    }
    if (total > 1e-6)
    {
        target /= total;
        response = (response / total) * saturate(total);
    }
}

// Contact rows:
// 0: point.xyz, trusted finite footprint radius.
// 1: outward normal.xyz, steering distance in front of plane.
// 2: tangent.xyz, maximum initial-overlap recovery depth.
// 3: surface velocity at point.xyz, incoming-to-lateral fraction [0..1].
// 4: angular velocity.xyz, steering response rate.
// 5: skin, active, reserved, reserved.
// Contact point/normal must be refreshed by CPU; orientation is frozen per GPU
// step. Angular surface velocity is included, but rotating-geometry CCD is not.
void EF_SteerContact(StructuredBuffer<float4> contacts, uint i,
    float3 p, float localTime, float particleRadius, float phase,
    float dt, inout float3 velocity)
{
    uint k = i * EF_CONTACT_ROWS;
    float4 pr = contacts[k];
    float4 nd = contacts[k + 1u];
    float4 tr = contacts[k + 2u];
    float4 vs = contacts[k + 3u];
    float4 ag = contacts[k + 4u];
    float4 settings = contacts[k + 5u];
    if (settings.y < 0.5 || pr.w <= 0.0) return;
    float3 n = EF_Normalize(nd.xyz, float3(0, 1, 0));
    float3 patchPoint = pr.xyz + vs.xyz * localTime;
    float3 q = p - patchPoint;
    float signedDistance = dot(q, n);
    float distance = signedDistance - max(particleRadius + settings.x, 0.0);
    if (distance < -max(tr.w, 0.0) || distance > max(nd.w, 0.001)) return;
    float3 lateral = q - n * signedDistance;
    float r = length(lateral);
    if (r >= pr.w) return;
    float w = (1.0 - smoothstep(pr.w * 0.75, pr.w, r))
        * (1.0 - smoothstep(0.0, max(nd.w, 0.001), max(distance, 0.0)));
    float3 tangent = EF_Normalize(tr.xyz - n * dot(tr.xyz, n), EF_Tangent(n));
    float3 fallback = tangent * cos(phase) + cross(n, tangent) * sin(phase);
    float3 outward = EF_Normalize(lateral, fallback);
    float3 surface = vs.xyz + cross(ag.xyz, q);
    float3 relative = velocity - surface;
    float incoming = max(-dot(relative, n), 0.0);
    if (incoming <= 0.0) return;
    float3 redirected = relative + n * incoming
        + outward * incoming * saturate(vs.w);
    // Steering may redistribute existing relative speed; it must not compound it.
    redirected = EF_Limit(redirected, length(relative));
    float blend = 1.0 - exp(-max(ag.w, 0.0) * max(dt, 0.0) * w);
    velocity = surface + lerp(relative, redirected, blend);
}

bool EF_ResolveContact(StructuredBuffer<float4> contacts, uint i,
    float3 oldPosition, float localTime, float dt, float particleRadius, float phase,
    inout float3 position, inout float3 velocity)
{
    uint k = i * EF_CONTACT_ROWS;
    float4 pr = contacts[k];
    float4 nd = contacts[k + 1u];
    float4 tr = contacts[k + 2u];
    float4 vs = contacts[k + 3u];
    float4 ag = contacts[k + 4u];
    float4 settings = contacts[k + 5u];
    if (settings.y < 0.5 || pr.w <= 0.0) return false;
    float3 n = EF_Normalize(nd.xyz, float3(0, 1, 0));
    float skin = max(particleRadius + settings.x, 0.0);
    float3 c0 = pr.xyz + vs.xyz * localTime;
    float3 c1 = c0 + vs.xyz * dt;
    float d0 = dot(oldPosition - c0, n) - skin;
    float d1 = dot(position - c1, n) - skin;
    if (d1 > 1e-5) return false;
    bool crossing = d0 >= 0.0;
    bool recovery = d0 < 0.0 && d0 >= -max(tr.w, 0.0);
    if (!crossing && !recovery) return false;
    float toi = crossing ? saturate(d0 / max(d0 - d1, 1e-8)) : 1.0;
    float3 q = lerp(oldPosition, position, toi) - lerp(c0, c1, toi);
    float3 lateral = q - n * dot(q, n);
    if (dot(lateral, lateral) > pr.w * pr.w) return false;
    position -= n * min(d1, 0.0);
    float3 surface = vs.xyz + cross(ag.xyz, position - c1);
    float3 relative = velocity - surface;
    float incoming = max(-dot(relative, n), 0.0);
    if (d1 < -1e-5 && crossing && incoming > 0.0)
    {
        float3 tangent = EF_Normalize(tr.xyz - n * dot(tr.xyz, n), EF_Tangent(n));
        float3 fallback = tangent * cos(phase) + cross(n, tangent) * sin(phase);
        float3 outward = EF_Normalize(lateral, fallback);
        relative = EF_Limit(relative + n * incoming
            + outward * incoming * saturate(vs.w), length(relative));
    }
    // Also enforce velocity at touching contacts during later corner iterations.
    velocity = surface + relative - n * min(dot(relative, n), 0.0);
    return true;
}
#endif

