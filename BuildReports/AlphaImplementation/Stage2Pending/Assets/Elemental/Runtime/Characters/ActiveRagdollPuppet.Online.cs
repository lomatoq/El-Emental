using System;

namespace Elemental.Runtime.Characters
{
    public sealed partial class ActiveRagdollPuppet
    {
        public uint StableActorId => actorId;
        public void ConfigureActorIdentity(uint id)
        {
            if (id == 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (actorId == id) return;
            actorId = id; _controller = null; EnsureController();
        }
    }
}
