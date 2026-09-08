using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Elemental.Presentation.UI
{
    public enum FrontendPage { Main, Settings, Host, Join, Pause }

    [DisallowMultipleComponent]
    public sealed class FrontendMenuView : MonoBehaviour
    {
        private ElementalUITheme _theme;
        private UIAudioFeedback _audio;
        private FrontendFlowController _flow;
        private RectTransform _panel;
        private CanvasGroup _group;
        private GameObject[] _pages;
        private Button _play, _resume, _host, _join, _hostReady, _joinSubmit;
        private TMP_Text _status, _roomCode, _countdown;
        private CanvasGroup _countdownGroup;
        private int _countdownNumber;
        private float _countdownChangedAt;
        private static readonly string[] CountdownDigits = { "", "1", "2", "3", "4" };
        public float CountdownRenderedAlpha => _countdown != null && _countdown.gameObject.activeInHierarchy ? _countdown.canvasRenderer.GetInheritedAlpha() : 0f;
        public string CountdownText => _countdown != null && _countdown.gameObject.activeSelf ? _countdown.text : "";
        private TMP_InputField _codeInput;
        private Slider _master, _ui, _sensitivity;
        private TMP_Text _masterValue, _uiValue, _sensitivityValue;
        private Toggle _reduced;
        private FrontendButton[] _buttons;
        private bool _built;
        private ElementalStoneReferenceProfile Reference => _theme?.stoneSkin?.referenceProfile;
        private bool ReferenceActive => Reference != null && Reference.enabled;
        private Vector2 _panelBasePosition;
        private Vector2 _curtainBasePosition;
        private TMP_Text _footerText;
        private RectTransform _curtainRect;
        private string _panelTrack="panel_enter";
        private float _panelTrackAge;
        private CanvasGroup _pageGroup;
        private RectTransform _activePage;
        private Vector2 _pageBasePosition;
        private float _pageAge, _statusAge, _visibleTarget, _visibleAlpha;
        private float _menuElementAge;
        private bool _connecting, _statusError, _statusLeaving;
        private Vector2 _statusBasePosition;
        private readonly Image[] _elementFrames = new Image[4];
        private readonly Image[] _elementGlows = new Image[4];
        private Image _activeElementGlyph;
        private TMP_Text _activeElementTraits;
        private TMP_Text _activeElementLabel;
        private Elemental.Simulation.Magic.ElementId _lastStatusElement;
        private static readonly Elemental.Simulation.Magic.ElementId[] ElementOrder = { Elemental.Simulation.Magic.ElementId.Fire, Elemental.Simulation.Magic.ElementId.Earth, Elemental.Simulation.Magic.ElementId.Water, Elemental.Simulation.Magic.ElementId.Air };
        public TMP_InputField CodeInput => _codeInput;
        public bool IsVisible => _group != null && _group.alpha > .01f;

        public void Build(ElementalUITheme theme, UIAudioFeedback audio, FrontendFlowController flow)
        {
            if (_built) return;
            _built = true; _theme = theme; _audio = audio; _flow = flow;
            Canvas canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 150;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 1;
            gameObject.AddComponent<GraphicRaycaster>();
            var menuContents = Rect(transform, "Menu contents"); Stretch(menuContents, Vector2.zero, Vector2.one);
            _group = menuContents.gameObject.AddComponent<CanvasGroup>();
            if (_theme.stoneSkin != null && _theme.stoneSkin.curtain != null)
            {
                // Full-height art owns its own alpha edge; no obsolete rectangular veil underneath.
                var curtain = Image(menuContents, "Stone curtain", Color.white);
                _curtainRect=curtain.rectTransform; curtain.sprite = ReferenceActive && _theme.stoneSkin.referenceCurtain != null ? _theme.stoneSkin.referenceCurtain : _theme.stoneSkin.curtain; curtain.preserveAspect = !ReferenceActive; curtain.raycastTarget = false;
                curtain.rectTransform.anchorMin = Vector2.zero; curtain.rectTransform.anchorMax = Vector2.up;
                curtain.rectTransform.pivot = new Vector2(0,.5f);
                curtain.rectTransform.sizeDelta = new Vector2(ReferenceActive && _theme.stoneSkin.referenceCurtain != null ? 900 : 1080f * curtain.sprite.rect.width / curtain.sprite.rect.height,0);
                if(ReferenceActive)
                {
                    _curtainBasePosition=new Vector2(-130,0);_curtainRect.anchoredPosition=_curtainBasePosition;
                    _curtainRect.localScale=Vector3.one*1.3f;
                }
            }
            _panel = Rect(menuContents,"Menu column");
            if (ReferenceActive) Place(_panel,54,32,570,1020);
            else Stretch(_panel,new Vector2(.04f,.10f),new Vector2(.31f,.90f));
            _panelBasePosition = _panel.anchoredPosition;
            if (_theme.logo != null)
            {
                var logo = Image(_panel, "Elemental logo", Color.white); logo.sprite = _theme.logo; logo.preserveAspect = true; logo.raycastTarget = false;
                Place(logo.rectTransform, ReferenceActive ? 70 : 0, 0, ReferenceActive ? 64 : 90, ReferenceActive ? 64 : 90);
            }
            var eyebrow = Label(_panel, "LOCAL ALPHA  /  01", 18, _theme.accent); Place(eyebrow.rectTransform, ReferenceActive ? 148 : 112, ReferenceActive ? 14 : 23, ReferenceActive ? 350 : 390, 36);
            if (ReferenceActive) { eyebrow.alignment=TextAlignmentOptions.Center; eyebrow.characterSpacing=4; }
            if (_theme.stoneSkin != null && _theme.stoneSkin.wordmark != null)
                Ornament(_panel, "Stone wordmark", _theme.stoneSkin.wordmark, ReferenceActive ? 30 : 0, ReferenceActive ? 74 : 110, ReferenceActive ? 500 : 470, ReferenceActive ? 110 : 130);
            else
            { var title = Label(_panel, "EL EMENTAL", 70, _theme.text, true); Place(title.rectTransform, 0, 110, 530, 110); }
            var subtitle = Label(_panel, "SHAPE THE WORLD.\nHOLD YOUR GROUND.", 19, _theme.muted); Place(subtitle.rectTransform, ReferenceActive ? 28 : 0, ReferenceActive ? 184 : 245, ReferenceActive ? 514 : 480, 64);
            if (ReferenceActive) { subtitle.alignment=TextAlignmentOptions.Center; subtitle.characterSpacing=3; }
            var divider = _theme.stoneSkin != null && _theme.stoneSkin.divider != null ? _theme.stoneSkin.divider : _theme.panelDetail;
            if (divider != null && !ReferenceActive)
            {
                var detail = Image(_panel, "Menu separator", _theme.accent); detail.sprite = divider;
                detail.preserveAspect = true; detail.raycastTarget = false; Place(detail.rectTransform, 0, ReferenceActive ? 174 : 320, ReferenceActive ? 570 : 470, 12);
            }
            if (ReferenceActive) { var elements=Rect(_panel,"Element status block"); Place(elements,0,270,570,200); BuildElementStatus(elements); }
            _pages = new GameObject[5];
            for (int i = 0; i < _pages.Length; i++)
            {
                RectTransform page = Rect(_panel, ((FrontendPage)i).ToString()); Place(page, 0, ReferenceActive ? 484 : 345, ReferenceActive ? 560 : 480, ReferenceActive ? 500 : 470); _pages[i] = page.gameObject;
            }
            BuildMain(_pages[0].transform); BuildSettings(_pages[1].transform); BuildHost(_pages[2].transform); BuildJoin(_pages[3].transform); BuildPause(_pages[4].transform);
            _status = Label(menuContents, "", 20, _theme.muted, readable: true); Stretch(_status.rectTransform, new Vector2(.044f, .006f), new Vector2(.35f, .04f));
            _status.textWrappingMode = TextWrappingModes.Normal; _statusBasePosition=_status.rectTransform.anchoredPosition;
            var footer = Label(menuContents, "EARTH  /  FIRE  /  WATER  /  AIR", 16, _theme.muted); Stretch(footer.rectTransform, new Vector2(.70f, .025f), new Vector2(.97f, .065f));
            footer.alignment = TextAlignmentOptions.Right;
            _footerText=footer;
            if (ReferenceActive && divider != null)
            {
                var footerBlock=Rect(menuContents,"Element footer");footerBlock.anchorMin=footerBlock.anchorMax=new Vector2(1,0);
                footerBlock.pivot=new Vector2(1,0);footerBlock.anchoredPosition=new Vector2(-42,24);footerBlock.sizeDelta=new Vector2(430,68);
                footer.rectTransform.SetParent(footerBlock,false);Place(footer.rectTransform,0,0,430,30);footer.characterSpacing=2;
                var footerRule=Image(footerBlock,"Element footer rule",_theme.accent); footerRule.raycastTarget=false;Place(footerRule.rectTransform,0,46,430,1);
                var halo=Image(footerBlock,"Element footer glow",new Color(1,.91f,.58f,.5f));halo.sprite=_theme.stoneSkin.halo;halo.raycastTarget=false;Place(halo.rectTransform,9,23,46,46);
                var footerDiamond=Image(footerBlock,"Element footer diamond",_theme.accent); footerDiamond.raycastTarget=false;
                footerDiamond.sprite=_theme.stoneSkin.diamond; footerDiamond.preserveAspect=true;Place(footerDiamond.rectTransform,21,35,22,22);
                var core=Image(footerDiamond.transform,"Light core",_theme.accent);core.raycastTarget=false;core.rectTransform.anchorMin=core.rectTransform.anchorMax=new Vector2(.5f,.5f);core.rectTransform.sizeDelta=new Vector2(6,6);core.rectTransform.localRotation=Quaternion.Euler(0,0,45);
            }
            _countdown = Label(transform, "4", 160, _theme.text);
            Stretch(_countdown.rectTransform, new Vector2(.35f, .30f), new Vector2(.65f, .70f));
            _countdown.alignment = TextAlignmentOptions.Center;
            _countdownGroup = _countdown.gameObject.AddComponent<CanvasGroup>();
            _countdownGroup.ignoreParentGroups = true; _countdownGroup.blocksRaycasts = false;
            _countdown.gameObject.SetActive(false);
            _buttons = GetComponentsInChildren<FrontendButton>(true);
            if (ReferenceActive) gameObject.AddComponent<FrontendMenuVignette>().Configure(flow);
            Show(FrontendPage.Main); RefreshSettings(); SetNetworkAvailable(flow.OnlineAvailable);
        }

        private void BuildMain(Transform parent)
        {
            _play = Button(parent, "PLAY VS BOT", 0, 0, () => _flow.BeginBot(), _theme.stoneSkin?.botIcon);
            _host = Button(parent, "HOST GAME", ReferenceActive ? 90 : 92, 1, () => _flow.OpenHost(), _theme.stoneSkin?.hostIcon);
            _join = Button(parent, "JOIN GAME", ReferenceActive ? 180 : 176, 1, () => _flow.OpenJoin(), _theme.stoneSkin?.joinIcon);
            Button(parent, "SETTINGS", ReferenceActive ? 270 : 284, 2, () => _flow.OpenSettings(), _theme.stoneSkin?.settingsIcon);
            Button(parent, "QUIT", ReferenceActive ? 360 : 364, 2, () => _flow.Quit(), _theme.stoneSkin?.exitIcon);
        }

        private void BuildPause(Transform parent)
        {
            var label = Label(parent, "PAUSED", 34, _theme.accent); Place(label.rectTransform, 0, 0, 470, 50);
            _resume = Button(parent, "RESUME", ReferenceActive ? 92 : 82, 0, () => _flow.Resume(), _theme.stoneSkin?.playIcon);
            Button(parent, "SETTINGS", ReferenceActive ? 218 : 178, 1, () => _flow.OpenSettings(), _theme.stoneSkin?.settingsIcon);
            Button(parent, "END MATCH - MAIN MENU", ReferenceActive ? 354 : 292, 2, () => _flow.EndMatch(), _theme.stoneSkin?.exitIcon);
        }
        private void BuildSettings(Transform parent)
        {
            float settingsOffset = 0;
            _master = Slider(parent, "MASTER VOLUME", settingsOffset, 0, 1, _theme.stoneSkin?.volumeIcon, out _masterValue);
            _ui = Slider(parent, "UI VOLUME", settingsOffset + 92, 0, 1, _theme.stoneSkin?.settingsIcon, out _uiValue);
            _sensitivity = Slider(parent, "CAMERA SENSITIVITY", settingsOffset + 184, .25f, 2f, _theme.stoneSkin?.sensitivityIcon, out _sensitivityValue);
            _reduced = Rect(parent, "Reduced motion", typeof(Toggle)).GetComponent<Toggle>();
            Place((RectTransform)_reduced.transform, 0, settingsOffset + 281, ReferenceActive ? 560 : 470, 48);
            var box = Image(_reduced.transform, "Box", _theme.ink); Place(box.rectTransform, 0, 4, 32, 32);
            var mark = Image(box.transform, "Mark", _theme.accent); Stretch(mark.rectTransform, Vector2.one * .2f, Vector2.one * .8f);
            if (_theme.stoneSkin != null && _theme.stoneSkin.checkIcon != null)
            { mark.sprite = _theme.stoneSkin.checkIcon; mark.preserveAspect = true; mark.raycastTarget = false; }
            _reduced.targetGraphic = box; _reduced.graphic = mark;
            var label = Label(_reduced.transform, "REDUCED MOTION", 24, _theme.text); Place(label.rectTransform, 52, 0, 410, 44);
            _master.onValueChanged.AddListener(_ => SaveSettings()); _ui.onValueChanged.AddListener(_ => SaveSettings());
            _sensitivity.onValueChanged.AddListener(_ => SaveSettings()); _reduced.onValueChanged.AddListener(_ => SaveSettings());
            Button(parent, "BACK", settingsOffset + 348, 2, () => _flow.Back(), _theme.stoneSkin?.backIcon);
        }
        private void BuildHost(Transform parent)
        {
            var label = Label(parent, "ROOM CODE", 22, _theme.muted); Place(label.rectTransform, 0, 0, 470, 42);
            _roomCode = Label(parent, "â€”", 56, _theme.text, readable: true); Place(_roomCode.rectTransform, 0, 52, 470, 90);
            Button(parent, "COPY CODE", 156, 1, () => _flow.CopyRoomCode(), _theme.stoneSkin?.copyIcon);
            _hostReady = Button(parent, "READY", 248, 0, () => _flow.SetNetworkReady(), _theme.stoneSkin?.checkIcon);
            Button(parent, "BACK", 364, 2, () => _flow.Back(), _theme.stoneSkin?.backIcon);
        }
        private void BuildJoin(Transform parent)
        {
            var label = Label(parent, "ENTER ROOM CODE", 24, _theme.text); Place(label.rectTransform, 0, 0, 470, 48);
            var rect = Rect(parent, "Room code input", typeof(Image), typeof(TMP_InputField)); Place(rect, 0, 64, 470, 76);
            rect.GetComponent<Image>().color = _theme.ink; _codeInput = rect.GetComponent<TMP_InputField>();
            if (_theme.stoneSkin != null && _theme.stoneSkin.codeField != null)
            { var field = rect.GetComponent<Image>(); field.sprite = _theme.stoneSkin.codeField; field.type = UnityEngine.UI.Image.Type.Sliced; field.color = Color.white; }
            var viewport = Rect(rect, "Text viewport", typeof(RectMask2D)); Stretch(viewport, Vector2.zero, Vector2.one); viewport.offsetMin = new Vector2(18, 8); viewport.offsetMax = new Vector2(-18, -8);
            var text = Label(viewport, "", 36, _theme.text, readable: true); Stretch(text.rectTransform, Vector2.zero, Vector2.one);
            var hint = Label(viewport, "PASTE CODE", 25, _theme.muted, readable: true); Stretch(hint.rectTransform, Vector2.zero, Vector2.one);
            _codeInput.textViewport = viewport; _codeInput.textComponent = text; _codeInput.placeholder = hint;
            _codeInput.characterLimit = 16; _codeInput.lineType = TMP_InputField.LineType.SingleLine;
            _codeInput.onSubmit.AddListener(_ => _flow.JoinRoom(_codeInput.text));
            _joinSubmit = Button(parent, "CONNECT", 176, 0, () => _flow.JoinRoom(_codeInput.text), _theme.stoneSkin?.joinIcon);
            Button(parent, "BACK", 284, 2, () => _flow.Back(), _theme.stoneSkin?.backIcon);
        }
        public void SetNetworkAvailable(bool available) { _host.interactable = _join.interactable = available; }
        public void SetPlayAvailable(bool available) => _play.interactable = available;
        public void SetStatus(string text,bool error=false)
        {
            if(_status==null)return;
            bool changed=_status.text!=text;
            _statusLeaving=ReferenceActive&&string.IsNullOrEmpty(text)&&!string.IsNullOrEmpty(_status.text);
            if(changed)_statusAge=0;
            _statusError=error; if(!_statusLeaving)_status.text=text;
            _status.color=error?_theme.damage:_theme.muted;
        }
        public void SetRoomCode(string code) => _roomCode.text = string.IsNullOrEmpty(code) ? "â€”" : code;
        public void SetConnecting(bool connecting) { _connecting = connecting; _joinSubmit.interactable = !connecting; _codeInput.interactable = !connecting; }
        public void Show(FrontendPage page)
        {
            for (int i = 0; i < _pages.Length; i++) _pages[i].SetActive(i == (int)page);
            if (ReferenceActive)
            {
                _activePage = (RectTransform)_pages[(int)page].transform;
                _pageBasePosition = new Vector2(0,-484); _activePage.anchoredPosition=_pageBasePosition; _pageAge=0;
                _pageGroup = _activePage.GetComponent<CanvasGroup>();
                if (_pageGroup == null) _pageGroup = _activePage.gameObject.AddComponent<CanvasGroup>();
                _pageGroup.alpha=0;
            }
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                GameObject first = page == FrontendPage.Settings ? _master.gameObject :
                    page == FrontendPage.Host ? _hostReady.gameObject :
                    page == FrontendPage.Join ? _codeInput.gameObject :
                    page == FrontendPage.Pause ? _resume.gameObject : _play.gameObject;
                EventSystem.current.SetSelectedGameObject(first);
            }
            if (page == FrontendPage.Join) _codeInput.ActivateInputField();
            if (page == FrontendPage.Settings) RefreshSettings();
        }
        public void SetCountdown(int number)
        {
            if (_countdown == null) return;
            if (number != _countdownNumber) { _countdownNumber = number; _countdownChangedAt = Time.unscaledTime; }
            _countdown.gameObject.SetActive(number > 0);
            if (number > 0) _countdown.text = number < CountdownDigits.Length ? CountdownDigits[number] : number.ToString();
        }
        private void Update()
        {
            if (ReferenceActive) UpdateReferenceMotion();
            if (_countdownNumber <= 0 || _countdownGroup == null) return;
            float age = Time.unscaledTime - _countdownChangedAt;
            float pop = Mathf.Clamp01(age / .22f);
            float scale = _flow.Preferences.ReducedMotion ? 1f : Mathf.Lerp(1.3f, 1f, 1f - Mathf.Pow(1f - pop, 3f));
            _countdown.transform.localScale = Vector3.one * scale;
            _countdownGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / .07f)) *
                (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - .72f) / .28f)));
        }
        public void SetVisibility(float alpha, bool interactable)
        {
            if (_group == null) return;
            if(ReferenceActive)
            {
                string next=alpha>_visibleTarget?"panel_enter":alpha<_visibleTarget?"panel_exit":_panelTrack;
                if(next!=_panelTrack){_panelTrack=next;_panelTrackAge=0;}
            }
            _visibleTarget = alpha;
            if (!ReferenceActive) _group.alpha = alpha;
            _group.interactable = interactable; _group.blocksRaycasts = alpha > .01f;
            if (!ReferenceActive) _panel.anchoredPosition = _panelBasePosition + new Vector2(_flow.Preferences.ReducedMotion ? 0 : (1-alpha)*-80,0);
        }
        public void RefreshSettings()
        {
            var p = _flow.Preferences;
            _master.SetValueWithoutNotify(p.MasterVolume); _ui.SetValueWithoutNotify(p.UIVolume);
            _sensitivity.SetValueWithoutNotify(p.Sensitivity); _reduced.SetIsOnWithoutNotify(p.ReducedMotion);
            _masterValue.text = Mathf.RoundToInt(p.MasterVolume * 100f).ToString() + "%";
            _uiValue.text = Mathf.RoundToInt(p.UIVolume * 100f).ToString() + "%";
            _sensitivityValue.text = p.Sensitivity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "x";
            if (_buttons != null) foreach (var button in _buttons) button.ReducedMotion = p.ReducedMotion;
        }
        private void SaveSettings()
        {
            _flow.Preferences.Set(_master.value, _ui.value, _sensitivity.value, _reduced.isOn);
            _flow.ApplyPreferences(); _flow.Preferences.Save(); RefreshSettings();
        }
        private Button Button(Transform parent, string text, float y, int tier, UnityAction action, Sprite roleIcon = null)
        {
            RectTransform r = Rect(parent, text, typeof(Image), typeof(Button), typeof(FrontendButton)); Place(r, 0, y, ReferenceActive ? 560 : 470, ReferenceActive ? 78 : 70);
            Color normal = tier == 0 ? _theme.accent : tier == 1 ? new Color(.11f, .19f, .23f, .9f) : new Color(.08f, .13f, .17f, .35f);
            var graphic = r.GetComponent<Image>(); graphic.color = normal;
            var skin = _theme.stoneSkin;
            bool skinned = skin != null && skin.normal != null;
            if (skinned) { graphic.sprite = tier == 0 && skin.primary != null ? skin.primary : skin.normal; graphic.type = UnityEngine.UI.Image.Type.Sliced; graphic.color = Color.white;
                // Scale border pixels uniformly with the 136px source height; leave the hit rect unchanged.
                graphic.pixelsPerUnitMultiplier = graphic.sprite.rect.height / (ReferenceActive ? 78f : 70f);
            }
            var b = r.GetComponent<Button>(); b.targetGraphic = graphic; b.onClick.AddListener(action);
            var caption = Label(r, text, ReferenceActive ? 25 : tier == 0 ? 30 : 27, tier == 0 && !skinned ? _theme.ink : _theme.text);
            Stretch(caption.rectTransform, Vector2.zero, Vector2.one); caption.margin = new Vector4(ReferenceActive ? 114 : skinned ? 72 : 24, 0, 24, 0); caption.alignment = TextAlignmentOptions.MidlineLeft;
            if (skinned && roleIcon != null) Ornament(r, "Button role icon", roleIcon, ReferenceActive ? 38 : 28, ReferenceActive ? 18 : 17, ReferenceActive ? 42 : 36, ReferenceActive ? 42 : 36);
            if (ReferenceActive) { var arrow=Label(r,"›",36,_theme.accent,readable:true); arrow.name="Button chevron"; Place(arrow.rectTransform,507,8,32,62); arrow.alignment=TextAlignmentOptions.Center; }
            r.GetComponent<FrontendButton>().Configure(_theme, _audio, normal, Color.Lerp(normal, _theme.accent, tier == 0 ? .28f : .3f)); return b;
        }
        private Slider Slider(Transform parent, string name, float y, float min, float max, Sprite icon, out TMP_Text valueLabel)
        {
            var label = Label(parent, name, 22, _theme.text); Place(label.rectTransform, icon != null ? 54 : 0, y, icon != null ? 330 : 374, 40);
            if (icon != null) Ornament(parent, name + " icon", icon, 0, y, ReferenceActive ? 42 : 30, ReferenceActive ? 42 : 30);
            valueLabel = Label(parent, "", 22, _theme.text, readable: true); valueLabel.name = name + " value";
            Place(valueLabel.rectTransform, ReferenceActive ? 474 : 384, y, 86, 40); valueLabel.alignment = TextAlignmentOptions.Right;
            RectTransform r = Rect(parent, name + " slider", typeof(Slider)); Place(r, ReferenceActive ? 46 : 0, y + 44, ReferenceActive ? 426 : 470, 30);
            var track = Image(r, "Track", new Color(.18f, .25f, .29f)); Stretch(track.rectTransform, new Vector2(0, .4f), new Vector2(1, .6f));
            var fill = Image(track.transform, "Fill", _theme.accent); Stretch(fill.rectTransform, Vector2.zero, Vector2.one);
            var handleArea = Rect(r, "Handle area"); Stretch(handleArea, Vector2.zero, Vector2.one); handleArea.offsetMin = new Vector2(10, 0); handleArea.offsetMax = new Vector2(-10, 0);
            var handle = Image(handleArea, "Handle", _theme.text); handle.rectTransform.anchorMin = new Vector2(.5f, 0); handle.rectTransform.anchorMax = new Vector2(.5f, 1); handle.rectTransform.sizeDelta = new Vector2(18, -4);
            if (_theme.stoneSkin != null && _theme.stoneSkin.sliderDiamond != null)
            { handle.sprite = _theme.stoneSkin.sliderDiamond; handle.preserveAspect = true; handle.rectTransform.sizeDelta = new Vector2(26, -4); }
            if (ReferenceActive)
            {
                var core=Image(handle.transform,"Handle light core",_theme.accent); core.raycastTarget=false;
                core.rectTransform.anchorMin=core.rectTransform.anchorMax=new Vector2(.5f,.5f);
                core.rectTransform.sizeDelta=new Vector2(10,10);core.rectTransform.localRotation=Quaternion.Euler(0,0,45);
            }
            var slider = r.GetComponent<Slider>(); slider.minValue = min; slider.maxValue = max; slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            return slider;
        }
        private TMP_Text Label(Transform parent, string text, float size, Color color, bool display = false, bool readable = false)
        {
            var r = Rect(parent, text.Length < 40 ? text : "Label", typeof(TextMeshProUGUI));
            var label = r.GetComponent<TextMeshProUGUI>(); label.font = ReferenceActive && Reference.menuFont != null ? (readable ? _theme.readableFont : Reference.menuFont) : display ? _theme.displayFont : readable ? _theme.readableFont : _theme.labelFont;
            label.text = text; label.fontSize = size * _theme.menuFontScale;
            if (ReferenceActive) { label.fontSize = Mathf.Max(18,label.fontSize); label.characterSpacing=1.5f; } label.color = color; label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }
        private void BuildElementStatus(Transform parent)
        {
            var skin=_theme.stoneSkin;
            Sprite[] sprites={skin.fire,skin.earth,skin.water,skin.air};
            Color[] colors={new Color(1,.40f,.04f),new Color(.77f,.97f,.41f),new Color(.2f,.75f,1),new Color(.76f,.75f,.71f)};
            for(int i=0;i<4;i++)
            {
                var glow=Image(parent,"Element glow "+ElementOrder[i],Color.clear); glow.sprite=skin.halo;
                glow.raycastTarget=false; Place(glow.rectTransform,26+i*128,-27,124,124); _elementGlows[i]=glow;
                var frame=Image(parent,"Element status "+ElementOrder[i],colors[i]); frame.sprite=skin.diamond;
                frame.preserveAspect=true; frame.raycastTarget=false; Place(frame.rectTransform,53+i*128,0,70,70);
                frame.rectTransform.pivot=new Vector2(.5f,.5f);frame.rectTransform.anchoredPosition+=new Vector2(35,-35);_elementFrames[i]=frame;
                if(sprites[i]!=null) { Ornament(frame.transform,"Element glyph",sprites[i],15,15,40,40); frame.transform.Find("Element glyph").GetComponent<Image>().color=colors[i]; }
                var name=Label(parent,ElementOrder[i].ToString().ToUpperInvariant(),18,colors[i]);
                Place(name.rectTransform,38+i*128,84,102,30); name.alignment=TextAlignmentOptions.Center;
            }
            var ribbon=Image(parent,"Active element ribbon",Color.white); ribbon.sprite=skin.ribbon; ribbon.type=UnityEngine.UI.Image.Type.Sliced;
            ribbon.raycastTarget=false; Place(ribbon.rectTransform,-12,112,594,82);
            _activeElementGlyph=Image(ribbon.transform,"Active element glyph",Color.white); _activeElementGlyph.preserveAspect=true; _activeElementGlyph.raycastTarget=false; Place(_activeElementGlyph.rectTransform,29,9,65,65);
            _activeElementLabel=Label(ribbon.transform,"",21,_theme.accent); Place(_activeElementLabel.rectTransform,110,8,470,34);
            _activeElementLabel.alignment=TextAlignmentOptions.Center;
            _activeElementTraits=Label(ribbon.transform,"",13,_theme.text); Place(_activeElementTraits.rectTransform,110,44,470,26);
            _activeElementTraits.fontSize=13; _activeElementTraits.characterSpacing=2; _activeElementTraits.alignment=TextAlignmentOptions.Center;
            _lastStatusElement=(Elemental.Simulation.Magic.ElementId)byte.MaxValue;
        }
        private void UpdateReferenceMotion()
        {
            bool reduced=_flow.Preferences.ReducedMotion; float dt=Time.unscaledDeltaTime;
            _panelTrackAge+=dt;
            float panelT=_panelTrackAge/Reference.Duration(_panelTrack,.28f,reduced);
            _visibleAlpha=Reference.Sample(_panelTrack,"alpha",panelT,_visibleTarget);
            _group.alpha=_visibleAlpha;
            float panelX=reduced?0:Reference.Sample(_panelTrack,"x",panelT);
            _panel.anchoredPosition=_panelBasePosition+new Vector2(panelX,0);
            if(_curtainRect!=null)_curtainRect.anchoredPosition=_curtainBasePosition+new Vector2(panelX,0);
            _pageAge+=dt;
            if(_pageGroup!=null)
            {
                float t=_pageAge/Reference.Duration("content_enter",.2f,reduced);
                _pageGroup.alpha=Reference.Sample("content_enter","alpha",t,1);
                _activePage.anchoredPosition=_pageBasePosition+new Vector2(0,reduced?0:-Reference.Sample("content_enter","y",t));
            }
            _statusAge+=dt;
            if(_status!=null)
            {
                string statusTrack=_statusLeaving?"toast_out":"toast_in";
                float statusT=_statusAge/Reference.Duration(statusTrack,.16f,reduced);
                float alpha=Reference.Sample(statusTrack,"alpha",statusT,1);
                if(_statusLeaving&&alpha<=0){_status.text="";_statusLeaving=false;}
                if(_connecting&&!reduced) alpha*=Reference.LoopSample("connect_pulse","opacity",_statusAge,1);
                _status.alpha=alpha;
                float x=_statusError&&!reduced?Reference.Sample("error_feedback","x",_statusAge/Reference.Duration("error_feedback",.16f)):0;
                float y=!reduced&&!_statusLeaving?-Reference.Sample("toast_in","y",statusT):0;
                _status.rectTransform.anchoredPosition=_statusBasePosition+new Vector2(x,y);
            }
            if(_activeElementLabel!=null)
            {
                _menuElementAge+=dt;
                var selected=_flow.SelectedElement;
                if(selected!=_lastStatusElement)
                {
                    _menuElementAge=0;
                    _lastStatusElement=selected;_activeElementLabel.text=selected.ToString().ToUpperInvariant()+" ELEMENT ACTIVE";
                    string[] footerElements={"EARTH","FIRE","WATER","AIR"};
                    for(int i=0;i<footerElements.Length;i++)if(footerElements[i]==selected.ToString().ToUpperInvariant())footerElements[i]="<color=#FFF2C7><b>"+footerElements[i]+"</b></color>";
                    _footerText.text=string.Join("  /  ",footerElements);
                    var skin=_theme.stoneSkin;
                    _activeElementGlyph.sprite=selected==Elemental.Simulation.Magic.ElementId.Fire?skin.fire:selected==Elemental.Simulation.Magic.ElementId.Water?skin.water:selected==Elemental.Simulation.Magic.ElementId.Air?skin.air:skin.earth;
                    _activeElementTraits.text=selected==Elemental.Simulation.Magic.ElementId.Earth?"STRENGTH  /  BALANCE  /  ENDURANCE":"";
                }
                for(int i=0;i<4;i++) if(_elementFrames[i]!=null)
                {
                    bool active=ElementOrder[i]==selected;
                    float selectionT=_menuElementAge/Reference.Duration("element_select",.22f,reduced);
                    float selectionScale=active&&!reduced?Reference.Sample("element_select","scale",selectionT,1):1;
                    _elementFrames[i].transform.localScale=Vector3.one*(active?1.24f*selectionScale:1);
                    _elementGlows[i].color=new Color(.75f,.98f,.38f,active?(reduced?.65f:.3f+Reference.LoopSample("selected_breath","opacity",Time.unscaledTime,.35f)):0);
                }
            }
        }
        private static void Ornament(Transform parent, string name, Sprite sprite, float x, float y, float width, float height)
        {
            var image = Image(parent, name, Color.white); image.sprite = sprite; image.preserveAspect = true;
            image.raycastTarget = false; Place(image.rectTransform, x, y, width, height);
        }
        private static Image Image(Transform parent, string name, Color color)
        { var r = Rect(parent, name, typeof(Image)); var image = r.GetComponent<Image>(); image.color = color; return image; }
        private static RectTransform Rect(Transform parent, string name, params Type[] components)
        {
            var go = new GameObject(name, typeof(RectTransform)); var r = (RectTransform)go.transform; r.SetParent(parent, false);
            foreach (var type in components) go.AddComponent(type); return r;
        }
        private static void Place(RectTransform r, float x, float y, float width, float height)
        { r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(width, height); }
        private static void Stretch(RectTransform r, Vector2 min, Vector2 max)
        { r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero; }
    }
}
