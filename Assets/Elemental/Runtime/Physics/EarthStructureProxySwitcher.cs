using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    public sealed class EarthStructureProxySwitcher : MonoBehaviour
    {
        private static readonly ProfilerMarker ProxySwitchMarker =
            new ProfilerMarker("Elemental.Earth.Proxy.Switch");
        private Renderer _intactRenderer;
        private Collider _intactCollider;
        private Transform[] _pieces = System.Array.Empty<Transform>();

        public bool IsShowingIntact { get; private set; }

        public void Configure(
            Renderer intactRenderer,
            Collider intactCollider,
            Transform[] pieces)
        {
            _intactRenderer = intactRenderer;
            _intactCollider = intactCollider;
            _pieces = pieces ?? System.Array.Empty<Transform>();
            ShowIntact(false);
        }

        public void ShowIntact(bool colliderEnabled)
        {
            using var marker = ProxySwitchMarker.Auto();
            if (_intactRenderer != null) _intactRenderer.enabled = true;
            if (_intactCollider != null) _intactCollider.enabled = colliderEnabled;
            for (int index = 0; index < _pieces.Length; index++)
                if (_pieces[index] != null) _pieces[index].gameObject.SetActive(false);
            IsShowingIntact = true;
        }

        public void SetIntactColliderEnabled(bool enabled)
        {
            if (IsShowingIntact && _intactCollider != null) _intactCollider.enabled = enabled;
        }

        public void ShowFractured()
        {
            using var marker = ProxySwitchMarker.Auto();
            if (_intactRenderer != null) _intactRenderer.enabled = false;
            if (_intactCollider != null) _intactCollider.enabled = false;
            for (int index = 0; index < _pieces.Length; index++)
                if (_pieces[index] != null) _pieces[index].gameObject.SetActive(true);
            IsShowingIntact = false;
        }

        public void HideAll()
        {
            using var marker = ProxySwitchMarker.Auto();
            if (_intactRenderer != null) _intactRenderer.enabled = false;
            if (_intactCollider != null) _intactCollider.enabled = false;
            for (int index = 0; index < _pieces.Length; index++)
                if (_pieces[index] != null) _pieces[index].gameObject.SetActive(false);
            IsShowingIntact = false;
        }
    }
}
