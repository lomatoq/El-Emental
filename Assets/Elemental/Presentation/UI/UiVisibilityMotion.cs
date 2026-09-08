using UnityEngine;
using UnityEngine.UIElements;

namespace Elemental.Presentation.UI
{
    // Reversible, unscaled presentation envelope. Retargeting starts from the visible value.
    public struct UiVisibilityMotion
    {
        public float Value { get; private set; }
        public void Reset(float value = 0) => Value = Mathf.Clamp01(value);
        public float Step(bool visible, float deltaTime, bool reduced)
        {
            Value = Mathf.MoveTowards(Value, visible ? 1 : 0,
                Mathf.Max(0, deltaTime) / (reduced ? .07f : visible ? .24f : .18f));
            return Value * Value * (3 - 2 * Value);
        }
    }

    // Remove only our previous scale before applying authored layout; never rewrite coordinates.
    public sealed class ToolkitOpacityScaleMotion
    {
        private readonly VisualElement _element;
        private StyleScale _authoredScale;
        private Vector3 _output;
        private bool _applied;
        public ToolkitOpacityScaleMotion(VisualElement element) => _element = element;
        public void Restore()
        {
            if (_applied && _element.style.scale.value.value == _output)
                _element.style.scale = _authoredScale;
            _applied = false;
        }
        public void Apply(float opacity, float scale)
        {
            Restore();
            _authoredScale = _element.style.scale;
            var basis = _authoredScale.keyword == StyleKeyword.Undefined ? _authoredScale.value.value : Vector3.one;
            // Unset inline scale can expose a zero-initialized Scale on an unattached UI tree.
            if (basis == Vector3.zero) basis = Vector3.one;
            _output = new Vector3(basis.x * scale, basis.y * scale, basis.z);
            _element.style.scale = new Scale(_output);
            _element.style.opacity = opacity;
            _applied = true;
        }
    }
}
