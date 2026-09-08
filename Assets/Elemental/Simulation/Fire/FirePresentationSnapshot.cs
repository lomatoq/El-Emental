using Unity.Mathematics;

namespace Elemental.Simulation.Fire
{
    // Caller-owned transfer storage. Mutation here never changes canonical world state.
    public sealed class FirePresentationSnapshot
    {
        public FireGroupHandle Group;
        public FireLifecycle Lifecycle;
        public uint Seed;
        public float Time, Energy;
        public float3 Origin, FreeUp, BoundsMin, BoundsMax;
        public int NodeCount, ContactCount;
        public readonly FireFieldNode[] Nodes = new FireFieldNode[FireWorld.MaximumNodes];
        public readonly FireContactPatch[] Contacts = new FireContactPatch[FireWorld.MaximumContacts];
        public bool Emits => Lifecycle == FireLifecycle.Active;
    }
}
