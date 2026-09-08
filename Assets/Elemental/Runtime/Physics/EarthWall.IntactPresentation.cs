using UnityEngine;

namespace Elemental.Runtime.Physics
{
    public sealed partial class EarthWall
    {
        private MeshFilter _intactPresentationFilter;
        private Mesh _freshShellMesh, _crackedShellMesh;
        public bool HasRevealedCracks { get; private set; }

        public void ConfigureIntactPresentation(MeshFilter filter, Mesh freshShell, Mesh crackedShell)
        {
            _intactPresentationFilter = filter;
            _freshShellMesh = freshShell;
            _crackedShellMesh = crackedShell;
            ResetIntactPresentation();
        }

        // Presentation only: the shell collider, graph bonds and standing state
        // are untouched. Existing interaction code still decides actual damage.
        public void RevealCracks()
        {
            if (HasRevealedCracks || _fractured || _crackedShellMesh == null) return;
            HasRevealedCracks = true;
            BeginCrackFeedback();
            if (_intactPresentationFilter != null) _intactPresentationFilter.sharedMesh = _crackedShellMesh;
        }

        internal void ResetIntactPresentation()
        {
            RestoreDomainFoundationContact();
            HasRevealedCracks = false;
            ResetCrackFeedback();
            ResetHeavyContactChips();
            if (_intactPresentationFilter != null && _freshShellMesh != null)
                _intactPresentationFilter.sharedMesh = _freshShellMesh;
        }
    }
}
