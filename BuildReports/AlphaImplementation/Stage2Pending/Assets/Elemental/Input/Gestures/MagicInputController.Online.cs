using Elemental.Simulation.Networking;

namespace Elemental.Input.Gestures
{
    public sealed partial class MagicInputController
    {
        private bool _onlineReplicaPresentation;
        private EarthOnlineAbilityView _onlineView;
        public void ConfigureOnlinePresentation(bool replica)
        { _onlineReplicaPresentation = replica; _onlineView = default; }
        public void ApplyOnlinePresentation(in EarthOnlineAbilityView view)
        { if (_onlineReplicaPresentation) _onlineView = view; }
    }
}
