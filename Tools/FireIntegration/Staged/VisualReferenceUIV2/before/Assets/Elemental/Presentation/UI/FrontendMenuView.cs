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
                var stoneCurtain = Image(menuContents, "Stone curtain", Color.white);
                stoneCurtain.sprite = _theme.stoneSkin.curtain; stoneCurtain.preserveAspect = true;
                stoneCurtain.raycastTarget = false;
                stoneCurtain.rectTransform.anchorMin = Vector2.zero;
                stoneCurtain.rectTransform.anchorMax = Vector2.up;
                stoneCurtain.rectTransform.pivot = new Vector2(0, .5f);
                stoneCurtain.rectTransform.sizeDelta = new Vector2(1080f * stoneCurtain.sprite.rect.width / stoneCurtain.sprite.rect.height, 0);
            }
            _panel = Rect(menuContents, "Menu column"); Stretch(_panel, new Vector2(.04f, .10f), new Vector2(.31f, .90f));
            var logo = Image(_panel, "Elemental logo", Color.white); logo.sprite = _theme.logo; logo.preserveAspect = true; logo.raycastTarget = false;
            Place(logo.rectTransform, 0, 0, 90, 90);
            var eyebrow = Label(_panel, "LOCAL ALPHA  /  01", 18, _theme.accent); Place(eyebrow.rectTransform, 112, 23, 390, 36);
            if (_theme.stoneSkin != null && _theme.stoneSkin.wordmark != null)
                Ornament(_panel, "Stone wordmark", _theme.stoneSkin.wordmark, 0, 110, 470, 110);
            else
            { var title = Label(_panel, "EL EMENTAL", 70, _theme.text, true); Place(title.rectTransform, 0, 110, 530, 110); }
            var subtitle = Label(_panel, "SHAPE THE WORLD.\nHOLD YOUR GROUND.", 19, _theme.muted); Place(subtitle.rectTransform, 0, 225, 480, 64);
            var divider = _theme.stoneSkin != null && _theme.stoneSkin.divider != null ? _theme.stoneSkin.divider : _theme.panelDetail;
            if (divider != null)
            {
                var detail = Image(_panel, "Menu separator", _theme.accent); detail.sprite = divider;
                detail.preserveAspect = true; detail.raycastTarget = false; Place(detail.rectTransform, 0, 308, 470, 12);
            }
            _pages = new GameObject[5];
            for (int i = 0; i < _pages.Length; i++)
            {
                RectTransform page = Rect(_panel, ((FrontendPage)i).ToString()); Place(page, 0, 345, 480, 470); _pages[i] = page.gameObject;
            }
            BuildMain(_pages[0].transform); BuildSettings(_pages[1].transform); BuildHost(_pages[2].transform); BuildJoin(_pages[3].transform); BuildPause(_pages[4].transform);
            _status = Label(menuContents, "", 20, _theme.muted, readable: true); Stretch(_status.rectTransform, new Vector2(.04f, .022f), new Vector2(.64f, .082f));
            _status.textWrappingMode = TextWrappingModes.Normal;
            var footer = Label(menuContents, "EARTH  /  FIRE  /  WATER  /  AIR", 16, _theme.muted); Stretch(footer.rectTransform, new Vector2(.70f, .025f), new Vector2(.97f, .065f));
            footer.alignment = TextAlignmentOptions.Right;
            _countdown = Label(transform, "4", 160, _theme.text);
            Stretch(_countdown.rectTransform, new Vector2(.35f, .30f), new Vector2(.65f, .70f));
            _countdown.alignment = TextAlignmentOptions.Center;
            _countdownGroup = _countdown.gameObject.AddComponent<CanvasGroup>();
            _countdownGroup.ignoreParentGroups = true; _countdownGroup.blocksRaycasts = false;
            _countdown.gameObject.SetActive(false);
            _buttons = GetComponentsInChildren<FrontendButton>(true);
            Show(FrontendPage.Main); RefreshSettings(); SetNetworkAvailable(flow.OnlineAvailable);
        }

        private void BuildMain(Transform parent)
        {
            _play = Button(parent, "PLAY VS BOT", 0, 0, () => _flow.BeginBot(), _theme.stoneSkin?.botIcon);
            _host = Button(parent, "HOST GAME", 92, 1, () => _flow.OpenHost(), _theme.stoneSkin?.hostIcon);
            _join = Button(parent, "JOIN GAME", 176, 1, () => _flow.OpenJoin(), _theme.stoneSkin?.joinIcon);
            Button(parent, "SETTINGS", 284, 2, () => _flow.OpenSettings(), _theme.stoneSkin?.settingsIcon);
            Button(parent, "QUIT", 364, 2, () => _flow.Quit(), _theme.stoneSkin?.exitIcon);
        }

        private void BuildPause(Transform parent)
        {
            var label = Label(parent, "PAUSED", 34, _theme.accent); Place(label.rectTransform, 0, 0, 470, 50);
            _resume = Button(parent, "RESUME", 82, 0, () => _flow.Resume(), _theme.stoneSkin?.playIcon);
            Button(parent, "SETTINGS", 178, 1, () => _flow.OpenSettings(), _theme.stoneSkin?.settingsIcon);
            Button(parent, "END MATCH - MAIN MENU", 292, 2, () => _flow.EndMatch(), _theme.stoneSkin?.exitIcon);
        }
        private void BuildSettings(Transform parent)
        {
            _master = Slider(parent, "MASTER VOLUME", 0, 0, 1, _theme.stoneSkin?.volumeIcon, out _masterValue);
            _ui = Slider(parent, "UI VOLUME", 92, 0, 1, _theme.stoneSkin?.settingsIcon, out _uiValue);
            _sensitivity = Slider(parent, "CAMERA SENSITIVITY", 184, .25f, 2f, _theme.stoneSkin?.sensitivityIcon, out _sensitivityValue);
            _reduced = Rect(parent, "Reduced motion", typeof(Toggle)).GetComponent<Toggle>();
            Place((RectTransform)_reduced.transform, 0, 281, 470, 48);
            var box = Image(_reduced.transform, "Box", _theme.ink); Place(box.rectTransform, 0, 4, 32, 32);
            var mark = Image(box.transform, "Mark", _theme.accent); Stretch(mark.rectTransform, Vector2.one * .2f, Vector2.one * .8f);
            if (_theme.stoneSkin != null && _theme.stoneSkin.checkIcon != null)
            { mark.sprite = _theme.stoneSkin.checkIcon; mark.preserveAspect = true; mark.raycastTarget = false; }
            _reduced.targetGraphic = box; _reduced.graphic = mark;
            var label = Label(_reduced.transform, "REDUCED MOTION", 24, _theme.text); Place(label.rectTransform, 52, 0, 410, 44);
            _master.onValueChanged.AddListener(_ => SaveSettings()); _ui.onValueChanged.AddListener(_ => SaveSettings());
            _sensitivity.onValueChanged.AddListener(_ => SaveSettings()); _reduced.onValueChanged.AddListener(_ => SaveSettings());
            Button(parent, "BACK", 376, 2, () => _flow.Back(), _theme.stoneSkin?.backIcon);
        }
        private void BuildHost(Transform parent)
        {
            var label = Label(parent, "ROOM CODE", 22, _theme.muted); Place(label.rectTransform, 0, 0, 470, 42);
            _roomCode = Label(parent, "—", 56, _theme.text, readable: true); Place(_roomCode.rectTransform, 0, 52, 470, 90);
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
        public void SetStatus(string text, bool error = false) { if (_status != null) { _status.text = text; _status.color = error ? _theme.damage : _theme.muted; } }
        public void SetRoomCode(string code) => _roomCode.text = string.IsNullOrEmpty(code) ? "—" : code;
        public void SetConnecting(bool connecting) { _joinSubmit.interactable = !connecting; _codeInput.interactable = !connecting; }
        public void Show(FrontendPage page)
        {
            for (int i = 0; i < _pages.Length; i++) _pages[i].SetActive(i == (int)page);
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
            _group.alpha = alpha; _group.interactable = interactable; _group.blocksRaycasts = alpha > .01f;
            _panel.anchoredPosition = new Vector2(_flow.Preferences.ReducedMotion ? 0 : (1 - alpha) * -80, 0);
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
            RectTransform r = Rect(parent, text, typeof(Image), typeof(Button), typeof(FrontendButton)); Place(r, 0, y, 470, 70);
            Color normal = tier == 0 ? _theme.accent : tier == 1 ? new Color(.11f, .19f, .23f, .9f) : new Color(.08f, .13f, .17f, .35f);
            var graphic = r.GetComponent<Image>(); graphic.color = normal;
            var skin = _theme.stoneSkin;
            bool skinned = skin != null && skin.normal != null;
            if (skinned) { graphic.sprite = tier == 0 && skin.primary != null ? skin.primary : skin.normal; graphic.type = UnityEngine.UI.Image.Type.Sliced; graphic.color = Color.white;
                // Scale border pixels uniformly with the 136px source height; leave the hit rect unchanged.
                graphic.pixelsPerUnitMultiplier = graphic.sprite.rect.height / 70f;
            }
            var b = r.GetComponent<Button>(); b.targetGraphic = graphic; b.onClick.AddListener(action);
            var caption = Label(r, text, tier == 0 ? 30 : 27, tier == 0 && !skinned ? _theme.ink : _theme.text);
            Stretch(caption.rectTransform, Vector2.zero, Vector2.one); caption.margin = new Vector4(skinned ? 72 : 24, 0, 16, 0); caption.alignment = TextAlignmentOptions.MidlineLeft;
            if (skinned && roleIcon != null) Ornament(r, "Button role icon", roleIcon, 28, 17, 36, 36);
            r.GetComponent<FrontendButton>().Configure(_theme, _audio, normal, Color.Lerp(normal, _theme.accent, tier == 0 ? .28f : .3f)); return b;
        }
        private Slider Slider(Transform parent, string name, float y, float min, float max, Sprite icon, out TMP_Text valueLabel)
        {
            var label = Label(parent, name, 22, _theme.text); Place(label.rectTransform, icon != null ? 44 : 0, y, icon != null ? 330 : 374, 40);
            if (icon != null) Ornament(parent, name + " icon", icon, 0, y + 4, 30, 30);
            valueLabel = Label(parent, "", 22, _theme.text, readable: true); valueLabel.name = name + " value";
            Place(valueLabel.rectTransform, 384, y, 86, 40); valueLabel.alignment = TextAlignmentOptions.Right;
            RectTransform r = Rect(parent, name + " slider", typeof(Slider)); Place(r, 0, y + 44, 470, 30);
            var track = Image(r, "Track", new Color(.18f, .25f, .29f)); Stretch(track.rectTransform, new Vector2(0, .4f), new Vector2(1, .6f));
            var fill = Image(track.transform, "Fill", _theme.accent); Stretch(fill.rectTransform, Vector2.zero, Vector2.one);
            var handleArea = Rect(r, "Handle area"); Stretch(handleArea, Vector2.zero, Vector2.one); handleArea.offsetMin = new Vector2(10, 0); handleArea.offsetMax = new Vector2(-10, 0);
            var handle = Image(handleArea, "Handle", _theme.text); handle.rectTransform.anchorMin = new Vector2(.5f, 0); handle.rectTransform.anchorMax = new Vector2(.5f, 1); handle.rectTransform.sizeDelta = new Vector2(18, -4);
            if (_theme.stoneSkin != null && _theme.stoneSkin.sliderDiamond != null)
            { handle.sprite = _theme.stoneSkin.sliderDiamond; handle.preserveAspect = true; handle.rectTransform.sizeDelta = new Vector2(26, -4); }
            var slider = r.GetComponent<Slider>(); slider.minValue = min; slider.maxValue = max; slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            return slider;
        }
        private TMP_Text Label(Transform parent, string text, float size, Color color, bool display = false, bool readable = false)
        {
            var r = Rect(parent, text.Length < 40 ? text : "Label", typeof(TextMeshProUGUI));
            var label = r.GetComponent<TextMeshProUGUI>(); label.font = display ? _theme.displayFont : readable ? _theme.readableFont : _theme.labelFont;
            label.text = text; label.fontSize = size * _theme.menuFontScale; label.color = color; label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
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
