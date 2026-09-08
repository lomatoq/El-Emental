using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Elemental.Presentation.UI
{
    [RequireComponent(typeof(Button))]
    public sealed class FrontendButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        private Button _button;
        private ElementalUITheme _theme;
        private UIAudioFeedback _audio;
        private Graphic _graphic;
        private RectTransform _visual;
        private Sprite _restingSprite;
        private Color _normal, _highlight;
        private bool _hot, _pressed;
        private float _blend;
        private float _scaleStart = 1f, _scaleTarget = 1f, _scaleElapsed;
        private float _submitReleaseAt;
        public bool ReducedMotion { get; set; }
        public void Configure(ElementalUITheme theme, UIAudioFeedback audio, Color normal, Color highlight)
        {
            _button = GetComponent<Button>(); _theme = theme; _audio = audio;
            _graphic = _button.targetGraphic; _normal = normal; _highlight = highlight;
            EnsureVisualRoot();
            _restingSprite = (_graphic as Image)?.sprite;
            _button.transition = Selectable.Transition.None;
        }
        private void EnsureVisualRoot()
        {
            if (_visual != null) return;
            if (!(_graphic is Image source)) throw new System.InvalidOperationException("FrontendButton needs an Image target graphic.");
            // Keep the authored anchor, scale and hit rectangle still. Only its visual content
            // shrinks around its center, so a press near an edge cannot chase its own hit area.
            var go = new GameObject("Press Visual", typeof(RectTransform), typeof(Image));
            _visual = go.GetComponent<RectTransform>(); _visual.SetParent(transform, false);
            _visual.anchorMin = Vector2.zero; _visual.anchorMax = Vector2.one;
            _visual.pivot = new Vector2(.5f, .5f); _visual.offsetMin = _visual.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.sprite = source.sprite; image.overrideSprite = source.overrideSprite;
            image.type = source.type; image.preserveAspect = source.preserveAspect;
            image.fillCenter = source.fillCenter; image.fillAmount = source.fillAmount;
            image.fillMethod = source.fillMethod; image.fillOrigin = source.fillOrigin;
            image.fillClockwise = source.fillClockwise; image.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            image.material = source.material; image.color = source.color; image.raycastTarget = false;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child != _visual) { child.SetParent(_visual, true); child.SetAsFirstSibling(); }
            }
            source.color = Color.clear;
            _graphic = image; _button.targetGraphic = image;
        }
        private void Update()
        {
            if (_theme == null) return;
            if (_submitReleaseAt > 0 && Time.unscaledTime >= _submitReleaseAt)
            { _pressed = false; _submitReleaseAt = 0; }
            bool active = _button.IsInteractable();
            float duration = _pressed ? _theme.pressSeconds : _hot ? _theme.hoverSeconds : _theme.releaseSeconds;
            _blend = Mathf.MoveTowards(_blend, active && (_hot || _pressed) ? 1 : 0, Time.unscaledDeltaTime / duration);
            if (_theme.stoneSkin != null && _restingSprite != null && _graphic is Image skinned)
            {
                var sprite = _theme.stoneSkin.Button(active, _pressed, _hot, _restingSprite);
                skinned.overrideSprite = null; // The cloned normal override must not mask live state sprites.
                if (skinned.sprite != sprite) skinned.sprite = sprite;
                skinned.color = Color.white;
            }
            else _graphic.color = Color.Lerp(_normal, _highlight, _blend) * (active ? Color.white : new Color(.6f, .6f, .6f, .65f));
            float target = !ReducedMotion && _pressed && active ? _theme.pressedScale : 1f;
            if (!Mathf.Approximately(target, _scaleTarget))
            { _scaleStart = _visual.localScale.x; _scaleTarget = target; _scaleElapsed = 0; }
            _scaleElapsed += Time.unscaledDeltaTime;
            float scaleDuration = _pressed ? _theme.pressSeconds : _theme.releaseSeconds;
            float scaleT = Mathf.Clamp01(_scaleElapsed / Mathf.Max(.001f, scaleDuration));
            _visual.localScale = Vector3.one * Mathf.Lerp(_scaleStart, _scaleTarget, scaleT * scaleT * (3 - 2 * scaleT));
        }
        public void OnPointerEnter(PointerEventData e) { _hot = true; if (_button.IsInteractable()) _audio?.Play(UIAudioCue.Hover); }
        public void OnPointerExit(PointerEventData e) { _hot = false; }
        public void OnPointerDown(PointerEventData e) { if (e.button != PointerEventData.InputButton.Left || !_button.IsInteractable()) return; _submitReleaseAt = 0; _pressed = true; _audio?.Play(UIAudioCue.Press); }
        public void OnPointerUp(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) _pressed = false; }
        public void OnSelect(BaseEventData e) { _hot = true; if (_button.IsInteractable()) _audio?.Play(UIAudioCue.Hover); }
        public void OnDeselect(BaseEventData e) { _hot = false; _pressed = false; }
        public void OnSubmit(BaseEventData e)
        {
            if (!_button.IsInteractable()) return;
            _audio?.Play(UIAudioCue.Press); _pressed = true;
            _submitReleaseAt = Time.unscaledTime + _theme.pressSeconds;
        }
        private void OnDisable()
        { _hot = _pressed = false; _submitReleaseAt = 0; _scaleStart = _scaleTarget = 1; if (_visual != null) _visual.localScale = Vector3.one; }
        private void OnApplicationFocus(bool focused) { if (!focused) { _pressed = false; _submitReleaseAt = 0; } }
    }
}
