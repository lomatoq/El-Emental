using System.Collections.Generic;
using Elemental.Input.Gestures;
using Elemental.Simulation.Magic;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    [DisallowMultipleComponent]
    public sealed class EarthFootprintPreview : MonoBehaviour
    {
        [SerializeField] private MagicInputController input;
        [SerializeField] private Transform[] pebbleMarkers;

        public void Configure(MagicInputController configuredInput, Transform[] configuredMarkers)
        {
            if (isActiveAndEnabled && input != null) Unsubscribe();
            input = configuredInput;
            pebbleMarkers = configuredMarkers;
            Hide();
            if (isActiveAndEnabled && input != null) Subscribe();
        }

        private void OnEnable()
        {
            if (input != null) Subscribe();
        }

        private void OnDisable()
        {
            if (input != null) Unsubscribe();
        }

        private void Subscribe()
        {
            input.PreviewChanged += Show;
            input.PreviewCleared += Hide;
        }

        private void Unsubscribe()
        {
            input.PreviewChanged -= Show;
            input.PreviewCleared -= Hide;
        }

        private void Show(IReadOnlyList<Vector3> points)
        {
            if (pebbleMarkers == null) return;
            bool wallPreview = input != null &&
                               (input.SelectedAbility == EarthAbilityIds.LineWall ||
                                input.SelectedAbility == EarthAbilityIds.RaisePlatform);
            int visibleCount = wallPreview ? Mathf.Min(points?.Count ?? 0, pebbleMarkers.Length) : 0;
            for (int index = 0; index < pebbleMarkers.Length; index++)
            {
                Transform marker = pebbleMarkers[index];
                if (marker == null) continue;
                bool visible = index < visibleCount;
                marker.gameObject.SetActive(visible);
                if (!visible) continue;
                Vector3 point = points[index];
                Vector3 up = point.sqrMagnitude > 0.01f ? point.normalized : Vector3.up;
                marker.position = point + (up * 0.035f);
                marker.rotation = Quaternion.FromToRotation(Vector3.up, up) *
                                  Quaternion.AngleAxis((index * 47f) % 180f, Vector3.up);
            }
        }

        private void Hide()
        {
            if (pebbleMarkers == null) return;
            for (int index = 0; index < pebbleMarkers.Length; index++)
                if (pebbleMarkers[index] != null) pebbleMarkers[index].gameObject.SetActive(false);
        }
    }
}
