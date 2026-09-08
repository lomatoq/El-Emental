using System.Collections.Generic;
using UnityEngine;

namespace Elemental.Input.Gestures
{
    [DisallowMultipleComponent]
    public sealed class EarthPreviewPresenter : MonoBehaviour
    {
        [SerializeField] private LineRenderer line;
        [SerializeField, Min(.01f)] private float minimumWorldWidth = .065f;
        [SerializeField, ColorUsage(true, true)] private Color previewTint = new Color(1.6f, 1.25f, .55f, 1f);
        private MaterialPropertyBlock _appearance;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public int PositionCount => line != null ? line.positionCount : 0;

        public void Configure(LineRenderer configuredLine)
        { line = configuredLine; ApplyAppearance(); }

        private void Awake() => ApplyAppearance();

        private void ApplyAppearance()
        {
            if (line == null) return;
            // The old 18 mm stroke became a subpixel orange line against orange
            // ground. Keep authored wider strokes and a readable cream highlight.
            line.widthMultiplier = Mathf.Max(line.widthMultiplier, minimumWorldWidth);
            if (_appearance == null) _appearance = new MaterialPropertyBlock();
            line.GetPropertyBlock(_appearance);
            _appearance.SetColor(BaseColorId, previewTint);
            _appearance.SetColor(ColorId, previewTint);
            line.SetPropertyBlock(_appearance);
        }

        public void Present(IReadOnlyList<Vector3> points)
        {
            if (line == null) return;
            int count = points?.Count ?? 0;
            // The production scene stores the idle contour disabled. Rendering is
            // owned by this presenter, not by that idle serialized flag.
            line.enabled = count > 1;
            line.positionCount = count;
            for (int index = 0; index < count; index++) line.SetPosition(index, points[index]);
        }

        public void Clear()
        {
            if (line != null) { line.positionCount = 0; line.enabled = false; }
        }
    }
}
