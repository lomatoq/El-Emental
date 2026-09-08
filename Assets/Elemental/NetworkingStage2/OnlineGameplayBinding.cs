using UnityEngine;

namespace Elemental.Online
{
    /// <summary>
    /// Scene composition boundary. Implement against the authored runtime, never by
    /// executing authoritative spells on both peers. Missing bindings prevent Ready.
    /// </summary>
    public abstract class OnlineGameplayBinding : MonoBehaviour
    {
        public abstract OnlineCapabilities Capabilities { get; }
        public abstract ulong BuildHash { get; }
        public abstract ulong InitialWorldHash { get; }
        public abstract bool WorldReady { get; }
        public virtual bool CanHandshake => WorldReady;
        public virtual void ConnectionEstablished(uint epoch) { }
        public abstract uint AuthorityTick { get; }
        public abstract bool Prepare(bool authority, byte localActor, NgoGameplayTransport transport, out string error);
        public abstract void BeginRound(uint authorityTick);
        public abstract void EndRound();
        // Must validate range, recipe, resources, held-target ownership, actor
        // life/stun state and tick window before executing or changing simulation.
        public abstract bool ApplyClientIntent(byte actor, in OnlinePacket intent, out string rejection);
        // Replica-only APIs: apply canonical state without replaying damage/impulses.
        public abstract bool ApplyAuthorityState(in OnlinePacket state, out string failure);
    }
}
