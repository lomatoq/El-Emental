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
        private string _scaleTrack = "button_release";
        private bool _pointerHeld;
        private float _submitReleaseAt;
        private Image _halo, _shine, _focusMark;
        private float _focusProgress;
        private float _shineAge;
        private bool _focused, _pointerInside;
        private TMPro.TMP_Text[] _captions;
        private Image _roleIcon;
        private StoneReferenceGlyph _roleGlyph, _rimGlow, _edgeGlints;
        private ElementalStoneReferenceProfile Reference => _theme?.stoneSkin?.referenceProfile;
        private bool ReferenceActive => Reference != null && Reference.enabled;
        private float Duration(string track,float fallback)=>ReferenceActive?Reference.Duration(track,fallback,ReducedMotion):fallback;
        public bool ReducedMotion { get; set; }
        public void Configure(ElementalUITheme theme, UIAudioFeedback audio, Color normal, Color highlight)
        {
            _button = GetComponent<Button>(); _theme = theme; _audio = audio;
            _graphic = _button.targetGraphic; _normal = normal; _highlight = highlight;
            EnsureVisualRoot();
            _captions=_visual.GetComponentsInChildren<TMPro.TMP_Text>(true);
            var role=_visual.Find("Button role icon"); if(role!=null)_roleIcon=role.GetComponent<Image>();
            _restingSprite = (_graphic as Image)?.sprite;
            _roleGlyph=_visual.GetComponentInChildren<StoneReferenceGlyph>(true);
            _button.transition = Selectable.Transition.None;
            if(ReferenceActive && _halo==null)
            {
                _halo=CreateFx("Stone hover halo",_theme.stoneSkin.halo);_halo.transform.SetAsFirstSibling();
                _rimGlow=StoneReferenceGlyph.Create(_visual,"Reference button edge light",StoneReferenceGlyph.Shape.ButtonGlow,new Color(.73f,.95f,.46f,1));
                var rim=_rimGlow.rectTransform;rim.anchorMin=Vector2.zero;rim.anchorMax=Vector2.one;rim.offsetMin=Vector2.zero;rim.offsetMax=Vector2.zero;_rimGlow.canvasRenderer.SetAlpha(0);
                _edgeGlints=StoneReferenceGlyph.Create(_visual,"Reference button edge glints",StoneReferenceGlyph.Shape.ButtonGlints,new Color(1,1,.76f,1));
                var glintRect=_edgeGlints.rectTransform;glintRect.anchorMin=Vector2.zero;glintRect.anchorMax=Vector2.one;glintRect.offsetMin=glintRect.offsetMax=Vector2.zero;_edgeGlints.canvasRenderer.SetAlpha(0);
                _shine=CreateFx("Stone shine",_theme.stoneSkin.shine);_shine.transform.SetAsLastSibling();
                var clip=new GameObject("Stone shine clip",typeof(RectTransform),typeof(RectMask2D));var clipRect=(RectTransform)clip.transform;
                clipRect.SetParent(_visual,false);clipRect.anchorMin=Vector2.zero;clipRect.anchorMax=Vector2.one;clipRect.offsetMin=clipRect.offsetMax=Vector2.zero;
                _shine.rectTransform.SetParent(clipRect,false);_shine.rectTransform.anchorMin=Vector2.zero;_shine.rectTransform.anchorMax=Vector2.one;_shine.rectTransform.offsetMin=_shine.rectTransform.offsetMax=Vector2.zero;
                _focusMark=CreateFx("Stone keyboard focus",_theme.stoneSkin.diamond);
                _focusMark.rectTransform.anchorMin=_focusMark.rectTransform.anchorMax=new Vector2(.025f,.5f);
                _focusMark.rectTransform.sizeDelta=new Vector2(14,14);
            }
        }
        private void EnsureVisualRoot()
        {
            if (_visual != null) return;
            if (!(_graphic is Image source)) throw new System.InvalidOperationException("FrontendButton needs an Image target graphic.");
            // Keep the authored anchor, scale and hit rectangle still. Only its visual content
            // shrinks around its center, so a press near an edge cannot chase its own hit area.
            var go = new GameObject("Press Visual", typeof(RectTransform));
            _visual = go.GetComponent<RectTransform>(); _visual.SetParent(transform, false);
            _visual.anchorMin = Vector2.zero; _visual.anchorMax = Vector2.one;
            _visual.pivot = new Vector2(.5f, .5f); _visual.offsetMin = _visual.offsetMax = Vector2.zero;
            var art = new GameObject("Button artwork",typeof(RectTransform),typeof(Image));
            var artRect=(RectTransform)art.transform;artRect.SetParent(_visual,false);artRect.anchorMin=Vector2.zero;artRect.anchorMax=Vector2.one;artRect.offsetMin=artRect.offsetMax=Vector2.zero;
            var image = art.GetComponent<Image>();
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
            art.transform.SetAsFirstSibling();
            // Keep a rectangular transparent hit surface, never the old resting artwork.
            source.overrideSprite = null;
            source.sprite = null;
            source.type = Image.Type.Simple;
            source.material = null;
            source.color = Color.clear;
            _graphic = image; _button.targetGraphic = image;
        }
        private void Update()
        {
            if (_theme == null) return;
            if (_submitReleaseAt > 0 && Time.unscaledTime >= _submitReleaseAt)
            { _pressed = _pointerHeld; _submitReleaseAt = 0; }
            bool active = _button.IsInteractable();
            if (!active) { _pressed = _pointerHeld = false; _submitReleaseAt = 0; }
            float duration = _pressed ? Duration("button_press",_theme.pressSeconds) : _hot ? Duration(_focused?"focus_enter":"button_hover",_theme.hoverSeconds) : Duration("button_release",_theme.releaseSeconds);
            _blend = Mathf.MoveTowards(_blend, active && (_hot || _pressed) ? 1 : 0, Time.unscaledDeltaTime / Mathf.Max(.001f, duration));
            if (_theme.stoneSkin != null && _restingSprite != null && _graphic is Image skinned)
            {
                var sprite = _theme.stoneSkin.Button(active, _pressed, _hot, _restingSprite);
                skinned.overrideSprite = null; // The cloned normal override must not mask live state sprites.
                if (skinned.sprite != sprite) skinned.sprite = sprite;
                skinned.color = active?Color.white:new Color(.6f,.6f,.6f,.65f);
                var insets=ReferenceActive&&sprite==_theme.stoneSkin.referenceSelected?_theme.stoneSkin.referenceSelectedInsets:Vector4.zero;
                float faceHeight=sprite.rect.height-insets.y-insets.w;
                float density=faceHeight/Mathf.Max(1,_visual.rect.height);
                skinned.pixelsPerUnitMultiplier=density;
                skinned.rectTransform.offsetMin=new Vector2(-insets.x,-insets.y)/density;
                skinned.rectTransform.offsetMax=new Vector2(insets.z,insets.w)/density;
            }
            else _graphic.color = Color.Lerp(_normal, _highlight, _blend) * (active ? Color.white : new Color(.6f, .6f, .6f, .65f));
            bool selected=active&&(_hot||_focused||_pressed);
            bool cream=ReferenceActive&&_theme.stoneSkin.referenceSelected!=null&&selected;
            var ink=cream?new Color(.04f,.055f,.055f):_theme.text;
            if(_captions!=null)foreach(var caption in _captions){caption.color=caption.name=="Button chevron"&&!cream?new Color(.67f,.84f,.38f):ink;if(ReferenceActive&&caption.name!="Button chevron")caption.fontStyle=selected?TMPro.FontStyles.Bold:TMPro.FontStyles.Normal;}
            if(_roleIcon!=null)_roleIcon.color=ink;
            if(_roleGlyph!=null)_roleGlyph.color=ink;
            // Latest reference requests a subtle 4% visual enlargement; authored easing/duration remain.
            float target = !ReducedMotion && active ? _pressed ? (ReferenceActive?Reference.Sample("button_press","scale",1,_theme.pressedScale):_theme.pressedScale) : ReferenceActive&&(_hot||_focused)?1.04f:ReferenceActive?Reference.Sample("button_release","scale",1,1):1f : 1f;
            if(_halo!=null)
            {
                _halo.color=Color.clear; // No radial halo behind the plate.
                _rimGlow.canvasRenderer.SetAlpha(cream?0:_blend*.95f);
                _edgeGlints.canvasRenderer.SetAlpha((cream?0:_blend)*(ReducedMotion?.5f:.65f+.2f*Mathf.Sin(_shineAge*4)));
                _focusProgress=Mathf.MoveTowards(_focusProgress,_focused&&active?1:0,Time.unscaledDeltaTime/Duration("focus_enter",.1f));
                _focusMark.color=_theme.stoneSkin.referenceSelectedInsets==Vector4.zero?new Color(1,.94f,.74f,Reference.Sample("focus_enter","focus",_focusProgress)):Color.clear;
                _shineAge+=Time.unscaledDeltaTime;
                float phase=Reference.Sample("shine_pass","phase",_shineAge/Duration("shine_pass",.6f));
                _shine.rectTransform.anchoredPosition=new Vector2(Mathf.Lerp(-_visual.rect.width,_visual.rect.width,phase),0);
                _shine.color=new Color(1,.94f,.74f,!ReducedMotion&&selected?Mathf.Sin(phase*Mathf.PI)*.28f:0);
            }
            string scaleTrack=_pressed?"button_press":_hot?"button_hover":"button_release";
            if (!Mathf.Approximately(target, _scaleTarget) || scaleTrack != _scaleTrack)
            { _scaleStart = _visual.localScale.x; _scaleTarget = target; _scaleElapsed = 0; _scaleTrack = scaleTrack; }
            _scaleElapsed += Time.unscaledDeltaTime;
            float scaleDuration = Duration(scaleTrack,_pressed?_theme.pressSeconds:_hot?_theme.hoverSeconds:_theme.releaseSeconds);
            float scaleT = Mathf.Clamp01(_scaleElapsed / Mathf.Max(.001f, scaleDuration));
            _visual.localScale = Vector3.one * (ReducedMotion ? 1f : Mathf.Lerp(_scaleStart, _scaleTarget, ReferenceActive?Reference.Evaluate(scaleTrack,scaleT):scaleT * scaleT * (3 - 2 * scaleT)));
        }
        public void OnPointerEnter(PointerEventData e) { if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null); _pointerInside=true; _shineAge=0; _hot = true; if (_button.IsInteractable()) _audio?.Play(UIAudioCue.Hover); }
        public void OnPointerExit(PointerEventData e) { _pointerInside=false; _hot = _focused; }
        public void OnPointerDown(PointerEventData e) { if (e.button != PointerEventData.InputButton.Left || !_button.IsInteractable()) return; _submitReleaseAt = 0; _pressed = _pointerHeld = true; _audio?.Play(UIAudioCue.Press); }
        public void OnPointerUp(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) _pressed = _pointerHeld = false; }
        public void OnSelect(BaseEventData e) { _focused=true; _shineAge=0; _hot = true; if (_button.IsInteractable()) _audio?.Play(UIAudioCue.Hover); }
        public void OnDeselect(BaseEventData e) { _focused=false; _hot = _pointerInside; _pressed = _pointerHeld; if (!_pointerHeld) _submitReleaseAt = 0; }
        public void OnSubmit(BaseEventData e)
        {
            if (!_button.IsInteractable()) return;
            _audio?.Play(UIAudioCue.Press); _pressed = true;
            _submitReleaseAt = Time.unscaledTime + Mathf.Max(.001f, Duration("button_press", _theme.pressSeconds));
        }
        private Image CreateFx(string name,Sprite sprite)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));var rect=(RectTransform)go.transform;
            rect.SetParent(_visual,false);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var image=go.GetComponent<Image>();image.sprite=sprite;image.raycastTarget=false;image.color=Color.clear;return image;
        }
        private void OnDisable()
        { _focused=_pointerInside=_pointerHeld=false;_focusProgress=_blend=0;_hot = _pressed = false; _submitReleaseAt = 0; _scaleStart = _scaleTarget = 1; _scaleElapsed=0; if (_visual != null) _visual.localScale = Vector3.one; }
        private void OnApplicationFocus(bool focused) { if (!focused) { _pointerInside=_pointerHeld=_pressed=false; _hot=_focused; _submitReleaseAt = 0; } }
    }
}
