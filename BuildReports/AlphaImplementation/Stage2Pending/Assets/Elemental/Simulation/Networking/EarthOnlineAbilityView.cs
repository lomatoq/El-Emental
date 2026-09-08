using Elemental.Simulation.Bending;
using Unity.Mathematics;

namespace Elemental.Simulation.Networking
{
    /// <summary>Read-only accepted action state for existing animation, HUD and field VFX.</summary>
    public struct EarthOnlineAbilityView
    {
        public uint Ability;
        public BendPhase Phase;
        public BendOriginMode Origin;
        public EarthActionOwner Owner;
        public bool VectorActive, GravityActive, RepairActive, ArmorActive, QuickPrimed;
        public float Amount, Charge, Focus, GravityStrength, VectorCharge, ArmorPhase, QuickPrime, ResonanceCharge, SurfSpeed;
        public float3 Target, GravityFocus, VectorPoint, VectorDirection;
    }
}
