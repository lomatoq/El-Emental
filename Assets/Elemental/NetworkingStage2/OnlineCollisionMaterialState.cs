using System;
using UnityEngine;

namespace Elemental.Online
{
    /// <summary>Exact contact properties for runtime-generated Earth physics materials.</summary>
    public static class OnlineCollisionMaterialState
    {
        private const uint InlineFlag = 1u << 21;
        private const uint MaterialFlags = 31u << 21;
        public static bool IsInline(in OnlinePacket packet) => (packet.Flags & InlineFlag) != 0;
        public static void Capture(PhysicsMaterial material, ref OnlinePacket packet)
        {
            if (material == null || packet.Seed != 0) throw new InvalidOperationException("Inline collision properties require a runtime material without a catalog ID.");
            uint friction = (uint)material.frictionCombine, bounce = (uint)material.bounceCombine;
            if (friction > 3 || bounce > 3) throw new InvalidOperationException("Unsupported collision material combine rule.");
            packet.Flags = (packet.Flags & ~MaterialFlags) | InlineFlag | friction << 22 | bounce << 24;
            packet.Value3 = material.dynamicFriction; packet.Value4 = material.staticFriction; packet.Value5 = material.bounciness;
            Validate(packet);
        }
        public static PhysicsMaterial Create(in OnlinePacket packet)
        {
            Validate(packet);
            return new PhysicsMaterial("Online canonical collision")
            {
                dynamicFriction = packet.Value3, staticFriction = packet.Value4, bounciness = packet.Value5,
                frictionCombine = (PhysicsMaterialCombine)((packet.Flags >> 22) & 3),
                bounceCombine = (PhysicsMaterialCombine)((packet.Flags >> 24) & 3)
            };
        }
        private static void Validate(in OnlinePacket packet)
        {
            if (!IsInline(packet) || packet.Seed != 0 || !Unit(packet.Value3) || !Unit(packet.Value4) || !Unit(packet.Value5))
                throw new InvalidOperationException("Invalid canonical collision material properties.");
        }
        private static bool Unit(float value) => float.IsFinite(value) && value >= 0 && value <= 1;
        public static ulong Signature(PhysicsMaterial material)
        {
            if (material == null) return 0;
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                hash = (hash ^ (uint)material.dynamicFriction.GetHashCode()) * 1099511628211UL;
                hash = (hash ^ (uint)material.staticFriction.GetHashCode()) * 1099511628211UL;
                hash = (hash ^ (uint)material.bounciness.GetHashCode()) * 1099511628211UL;
                hash = (hash ^ (uint)material.frictionCombine) * 1099511628211UL;
                return (hash ^ (uint)material.bounceCombine) * 1099511628211UL;
            }
        }
    }
}
