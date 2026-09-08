using Elemental.Simulation.Networking;
using UnityEngine;

namespace Elemental.Runtime.World
{
    public sealed partial class MagicExecutor
    {
        private bool _onlineReplicaPresentation;
        private EarthOnlineAbilityView _onlineView;
        private Rigidbody _onlineHeldBody;
        public void ConfigureOnlineAuthority(bool authority)
        { _onlineReplicaPresentation = !authority; _onlineView = default; _onlineHeldBody = null; }
        public void ApplyOnlinePresentation(in EarthOnlineAbilityView view, Rigidbody heldBody)
        { if (_onlineReplicaPresentation) { _onlineView = view; _onlineHeldBody = heldBody; } }
    }
}
