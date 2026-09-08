using Unity.Mathematics;

namespace Elemental.Simulation.Fire
{
    // Pure data. The Runtime adapter owns Collider/Rigidbody and validates
    // surface identity; no UnityEngine object belongs in this structure.
    public struct FireContactPatch
    {
        public float3 Point;
        public float Radius;
        public float3 Normal;
        public float FrontDepth;
        public float3 Tangent;
        public float RecoveryDepth;
        public float3 SurfaceVelocity;
        public float SpreadFraction;
        public float3 AngularVelocity;
        public float ResponseRate;
        public float Skin;
        public bool Active;
    }

    public static class FireContactMath
    {
        public static float3 SafeNormal(float3 v, float3 fallback)
        {
            float q = math.lengthsq(v);
            return q > 1e-10f ? v * math.rsqrt(q) : fallback;
        }

        public static float3 Tangent(float3 n)
        {
            float3 a = math.abs(n.y) < 0.9f
                ? new float3(0, 1, 0) : new float3(1, 0, 0);
            return SafeNormal(math.cross(a, n), new float3(1, 0, 0));
        }

        public static float3 Limit(float3 v, float speed)
        {
            return v * math.min(1f, math.max(speed, 0f) / math.max(math.length(v), 1e-6f));
        }

        public static float3 RedirectRelative(float3 relative, float3 normal,
            float3 outward, float fraction)
        {
            float incoming = math.max(-math.dot(relative, normal), 0f);
            return Limit(relative + normal * incoming
                + outward * incoming * math.saturate(fraction), math.length(relative));
        }

        // Mirrors EF_ResolveContact, for unit tests and optional low-tier renderer.
        // This solves a finite, locally planar approximation, not arbitrary mesh CCD.
        public static bool ResolveSwept(in FireContactPatch patch,
            float3 oldPosition, float localTime, float dt, float particleRadius,
            float phase, ref float3 position, ref float3 velocity)
        {
            if (!patch.Active || patch.Radius <= 0f) return false;
            float3 n = SafeNormal(patch.Normal, new float3(0, 1, 0));
            float skin = math.max(particleRadius + patch.Skin, 0f);
            float3 c0 = patch.Point + patch.SurfaceVelocity * localTime;
            float3 c1 = c0 + patch.SurfaceVelocity * dt;
            float d0 = math.dot(oldPosition - c0, n) - skin;
            float d1 = math.dot(position - c1, n) - skin;
            if (d1 > 1e-5f) return false;
            bool crossing = d0 >= 0f;
            bool recovery = d0 < 0f && d0 >= -math.max(patch.RecoveryDepth, 0f);
            if (!crossing && !recovery) return false;
            float toi = crossing ? math.saturate(d0 / math.max(d0 - d1, 1e-8f)) : 1f;
            float3 q = math.lerp(oldPosition, position, toi) - math.lerp(c0, c1, toi);
            float3 lateral = q - n * math.dot(q, n);
            if (math.lengthsq(lateral) > patch.Radius * patch.Radius) return false;
            position -= n * math.min(d1, 0f);
            float3 surface = patch.SurfaceVelocity
                + math.cross(patch.AngularVelocity, position - c1);
            float3 relative = velocity - surface;
            if (d1 < -1e-5f && crossing && math.dot(relative, n) < 0f)
            {
                float3 tangent = SafeNormal(patch.Tangent - n * math.dot(patch.Tangent, n), Tangent(n));
                float3 fallback = tangent * math.cos(phase) + math.cross(n, tangent) * math.sin(phase);
                relative = RedirectRelative(relative, n, SafeNormal(lateral, fallback), patch.SpreadFraction);
            }
            velocity = surface + relative - n * math.min(math.dot(relative, n), 0f);
            return true;
        }
    }
}
