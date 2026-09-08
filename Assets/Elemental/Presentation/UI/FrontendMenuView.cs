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
        private readonly System.Collections.Generic.List<MenuLayoutBinding> _sidebarLayout=new();
        private UiGlowMotes _menuMotes;
        private MenuLayoutBinding _countdownLayout;
        private static readonly Unity.Profiling.ProfilerMarker MenuLayoutMarker=new Unity.Profiling.ProfilerMarker("Elemental.MenuLayout.Apply");
        private readonly System.Collections.Generic.List<MenuLayoutBinding>[] _pageLayouts=new System.Collections.Generic.List<MenuLayoutBinding>[5];
        public System.Collections.Generic.IReadOnlyList<MenuLayoutBinding> SidebarBindings=>_sidebarLayout;
        public System.Collections.Generic.IReadOnlyList<MenuLayoutBinding> PageBindings(int page)=>_pageLayouts[page];
        [SerializeField] private SidebarRevealSettings _revealSettings = new SidebarRevealSettings();
        private readonly System.Collections.Generic.List<SidebarRevealNode> _headerReveal = new();
        private readonly System.Collections.Generic.List<SidebarRevealNode>[] _pageReveal = new System.Collections.Generic.List<SidebarRevealNode>[5];
        private float _sidebarRevealAge;
        private bool _departing;
        private float _departureAge, _departureStartX, _departureStartAlpha;
        private Image _startVeil;
        public float StartIntroSeconds => _flow.Preferences.ReducedMotion ? .18f : .82f;
        public float StartCameraCueSeconds => _flow.Preferences.ReducedMotion ? .08f : .58f;
        public void BeginDeparture()
        {
            if(!_departing)_audio?.PlayPanelMove();
            _departureAge=0;_departureStartX=_renderedPanelX;_departureStartAlpha=_group.alpha;
            _departing=true;_group.interactable=false;_group.blocksRaycasts=false;
            SetCountdown(0);
        }
        public void SetStartVeil(float age)
        {
            if(_startVeil==null)return;
            bool reduced=_flow.Preferences.ReducedMotion;
            float cue=StartCameraCueSeconds;
            float enter=reduced?0:.44f;
            float a=age<cue?Mathf.SmoothStep(0,1,Mathf.InverseLerp(enter,cue,age)):
                1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(cue,StartIntroSeconds,age));
            _startVeil.color=new Color(.012f,.017f,.024f,a);
            _startVeil.gameObject.SetActive(a>0);
        }
        private float _pageRevealLead;
        private void LateUpdate()
        {
            using(MenuLayoutMarker.Auto())
            {
            if(!_built||_pages==null||_flow==null)return;
            foreach(var node in _headerReveal)node.RemoveDelta();
            for(int i=0;i<5;i++)if(_pageReveal[i]!=null)foreach(var node in _pageReveal[i])node.RemoveDelta();
            var library=_theme!=null?_theme.menuPresentation:null;
            foreach(var b in _sidebarLayout)b.Apply(library!=null?library.Get(MenuScreenId.Sidebar):null);
            if(_menuMotes!=null)_menuMotes.ReducedMotion=_flow.Preferences.ReducedMotion;
            _countdownLayout?.Apply(library!=null?library.Get(MenuScreenId.Countdown):null);
            for(int i=0;i<5;i++)if(_pages[i]!=null&&_pages[i].activeSelf&&_pageLayouts[i]!=null)
                foreach(var b in _pageLayouts[i])b.Apply(library!=null?library.Get((MenuScreenId)((int)MenuScreenId.Main+i)):null);
            if(ReferenceActive)
            {
                bool reduced=_flow.Preferences.ReducedMotion;
                bool pause=_pages[(int)FrontendPage.Pause].activeSelf;
                float lead=pause?_revealSettings.pausePanelLead:_revealSettings.panelLead;
                foreach(var node in _headerReveal)node.Apply(_sidebarRevealAge,lead*.5f,_revealSettings,reduced);
                for(int i=0;i<5;i++)if(_pages[i].activeSelf&&_pageReveal[i]!=null)
                    foreach(var node in _pageReveal[i])node.Apply(_pageAge,_pageRevealLead,_revealSettings,reduced);
            }
            }
        }
        private ElementalStoneReferenceProfile Reference => _theme?.stoneSkin?.referenceProfile;
        private bool ReferenceActive => Reference != null && Reference.enabled;
        private Vector2 _panelBasePosition;
        private Vector2 _curtainBasePosition;
        private TMP_Text _footerText;
        private readonly TMP_Text[] _footerLabels = new TMP_Text[4];
        private RectTransform _footerMarker;
        private Graphic _footerMarkerGlow;
        private float _footerMarkerFrom = 50, _footerMarkerTo = 50;
        private static readonly string[] FooterNames = { "EARTH", "FIRE", "WATER", "AIR" };
        private RectTransform _curtainRect;
        private string _panelTrack="panel_enter";
        private float _panelTrackAge;
        private float _panelStartAlpha, _panelStartX;
        private float _renderedPanelX;
        private bool _panelMotionInitialized;
        private CanvasGroup _pageGroup;
        private RectTransform _activePage;
        private float _pageAge, _statusAge, _visibleTarget, _visibleAlpha;
        private float _menuElementAge;
        private bool _connecting, _statusError, _statusLeaving;
        private Vector2 _statusBasePosition;
        private readonly Graphic[] _elementFrames = new Graphic[4];
        private readonly RectTransform[] _elementHolders = new RectTransform[4];
        private readonly Graphic[] _elementGlints = new Graphic[4];
        private readonly Graphic[] _elementGlows = new Graphic[4];
        private Image _activeElementGlyph;
        private readonly UiContourBloom[] _elementBlooms = new UiContourBloom[4];
        private UiContourBloom _cardBloom;
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
            if (ReferenceActive) _group.alpha = 0;
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
            if (ReferenceActive) Place(_panel,82,32,570,1020);
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
                RectTransform page = Rect(_panel, ((FrontendPage)i).ToString()); Place(page, ReferenceActive ? 32 : 0, ReferenceActive ? 572 : 345, ReferenceActive ? 560 : 480, ReferenceActive ? 500 : 470); _pages[i] = page.gameObject;
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
                footer.gameObject.SetActive(false);
                for (int i=0;i<FooterNames.Length;i++)
                {
                    var word=Label(footerBlock,FooterNames[i],16,_theme.muted);
                    Place(word.rectTransform,i*110,0,100,30);word.fontSize=16;word.characterSpacing=2;
                    word.alignment=TextAlignmentOptions.Center;_footerLabels[i]=word;
                    if(i<3){var slash=Label(footerBlock,"/",14,_theme.muted);Place(slash.rectTransform,100+i*110,0,10,30);slash.alignment=TextAlignmentOptions.Center;}
                }
                var footerRule=StoneReferenceGlyph.Create(footerBlock,"Element footer rule",StoneReferenceGlyph.Shape.Rule,_theme.accent);
                Place(footerRule.rectTransform,0,45,430,2);
                _footerMarker=Rect(footerBlock,"Selected element footer marker");Place(_footerMarker,50,46,0,0);
                var segmentGlow=StoneReferenceGlyph.Create(_footerMarker,"Selected footer segment glow",StoneReferenceGlyph.Shape.Rule,new Color(1,.9f,.48f,.22f));
                Place(segmentGlow.rectTransform,-48,-3,96,6);
                var segment=StoneReferenceGlyph.Create(_footerMarker,"Selected footer segment",StoneReferenceGlyph.Shape.Rule,new Color(1,.96f,.68f,1));
                Place(segment.rectTransform,-48,-1.5f,96,3);
                _footerMarkerGlow=StoneReferenceGlyph.Create(_footerMarker,"Element footer glow",StoneReferenceGlyph.Shape.DiamondGlow,new Color(1,.91f,.58f,.8f));
                Place(_footerMarkerGlow.rectTransform,-15,-15,30,30);
                var footerDiamond=StoneReferenceGlyph.Create(_footerMarker,"Element footer diamond",StoneReferenceGlyph.Shape.SingleDiamond,new Color(1,.96f,.72f,1));
                Place(footerDiamond.rectTransform,-10,-10,20,20); footerDiamond.transform.SetAsLastSibling();
            }
            _startVeil=Image(transform,"Match start transition",new Color(.012f,.017f,.024f,0));
            Stretch(_startVeil.rectTransform,Vector2.zero,Vector2.one);_startVeil.raycastTarget=false;
            _startVeil.gameObject.SetActive(false);
            _countdown = Label(transform, "4", 160, _theme.text);
            Stretch(_countdown.rectTransform, new Vector2(.35f, .30f), new Vector2(.65f, .70f));
            _countdown.alignment = TextAlignmentOptions.Center;
            _countdownGroup = _countdown.gameObject.AddComponent<CanvasGroup>();
            _countdownGroup.ignoreParentGroups = true; _countdownGroup.blocksRaycasts = false;
            _countdown.gameObject.SetActive(false);
            _countdownLayout=new MenuLayoutBinding(_countdown.rectTransform,"Countdown");
            _buttons = GetComponentsInChildren<FrontendButton>(true);
            if(theme.menuPresentation!=null)
            {
                var settings=theme.menuPresentation.Get(MenuScreenId.Sidebar);
                foreach(var art in _panel.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                    if(art.name=="Stone wordmark"||art.name=="Active element glyph"||art.name=="Element glyph")
                        art.gameObject.AddComponent<UiChromaticEdges>().Settings=settings;
                if(_activeElementGlyph!=null)
                {
                    var motes=Rect(_activeElementGlyph.transform,"Glow motes",typeof(CanvasRenderer),typeof(UiGlowMotes));
                    Stretch(motes,Vector2.zero,Vector2.one);_menuMotes=motes.GetComponent<UiGlowMotes>();
                    _menuMotes.Settings=settings;_menuMotes.color=new Color(.78f,1,.38f);_menuMotes.raycastTarget=false;
                }
            }
            MenuLayoutBinding.Collect(menuContents,_sidebarLayout,skipPages:true);
            for(int i=0;i<5;i++)
            {
                _pageLayouts[i]=new System.Collections.Generic.List<MenuLayoutBinding>();
                MenuLayoutBinding.Collect((RectTransform)_pages[i].transform,_pageLayouts[i]);
            }
            if (ReferenceActive) gameObject.AddComponent<FrontendMenuVignette>().Configure(flow);
            if (ReferenceActive)
            {
                for(int i=0;i<_panel.childCount;i++)
                {
                    var child=_panel.GetChild(i) as RectTransform;
                    if(child!=null&&!Enum.TryParse<FrontendPage>(child.name,out _))
                        _headerReveal.Add(new SidebarRevealNode(child,_headerReveal.Count*.65f));
                }
                for(int i=0;i<5;i++)
                {
                    _pageReveal[i]=new System.Collections.Generic.List<SidebarRevealNode>();
                    var page=_pages[i].transform;
                    for(int j=0;j<page.childCount;j++)
                    {
                        var child=page.GetChild(j) as RectTransform;
                        // Controls on the same visual row reveal together (slider caption/value/track).
                        if(child!=null)_pageReveal[i].Add(new SidebarRevealNode(child,Mathf.Max(0,-child.anchoredPosition.y)/90f));
                    }
                }
            }
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
            var label = Label(parent, "PAUSED", 34, _theme.accent); Place(label.rectTransform, 0, 0, ReferenceActive ? 560 : 470, 50); if(ReferenceActive)label.alignment=TextAlignmentOptions.Center;
            _resume = Button(parent, "RESUME", ReferenceActive ? 74 : 82, 0, () => _flow.Resume(), _theme.stoneSkin?.playIcon);
            Button(parent, "SETTINGS", ReferenceActive ? 164 : 178, 1, () => _flow.OpenSettings(), _theme.stoneSkin?.settingsIcon);
            Button(parent, "END MATCH - MAIN MENU", ReferenceActive ? 254 : 292, 2, () => _flow.EndMatch(), _theme.stoneSkin?.exitIcon);
        }
        private void BuildSettings(Transform parent)
        {
            float settingsOffset = 0;
            _master = Slider(parent, "MASTER VOLUME", settingsOffset, 0, 1, _theme.stoneSkin?.volumeIcon, out _masterValue);
            _ui = Slider(parent, "UI VOLUME", settingsOffset + 92, 0, 1, _theme.stoneSkin?.settingsIcon, out _uiValue);
            _sensitivity = Slider(parent, "CAMERA SENSITIVITY", settingsOffset + 184, .25f, 2f, _theme.stoneSkin?.sensitivityIcon, out _sensitivityValue);
            _reduced = Rect(parent, "Reduced motion", typeof(Toggle)).GetComponent<Toggle>();
            Place((RectTransform)_reduced.transform, 0, settingsOffset + 281, ReferenceActive ? 560 : 470, 48);
            var box = Image(_reduced.transform, "Box", ReferenceActive ? _theme.accent : _theme.ink); Place(box.rectTransform, 0, 4, 32, 32);
            if(ReferenceActive)
            {
                var inset=Image(box.transform,"Unchecked interior",_theme.ink);inset.raycastTarget=false;
                Stretch(inset.rectTransform,Vector2.zero,Vector2.one);inset.rectTransform.offsetMin=new Vector2(2,2);inset.rectTransform.offsetMax=new Vector2(-2,-2);
            }
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
            _hostReady = Button(parent, "READY", ReferenceActive ? 246 : 248, 0, () => _flow.SetNetworkReady(), _theme.stoneSkin?.checkIcon);
            Button(parent, "BACK", ReferenceActive ? 336 : 364, 2, () => _flow.Back(), _theme.stoneSkin?.backIcon);
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
            Button(parent, "BACK", ReferenceActive ? 266 : 284, 2, () => _flow.Back(), _theme.stoneSkin?.backIcon);
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
            bool samePage = _activePage == _pages[(int)page].transform && _pages[(int)page].activeSelf;
            for (int i = 0; i < _pages.Length; i++) _pages[i].SetActive(i == (int)page);
            if (ReferenceActive && !samePage)
            {
                _activePage = (RectTransform)_pages[(int)page].transform;
                _pageAge=0;
                _pageRevealLead=_sidebarRevealAge<.55f?(page==FrontendPage.Pause?_revealSettings.pausePanelLead:_revealSettings.panelLead):0;
                _pageGroup = _activePage.GetComponent<CanvasGroup>();
                if (_pageGroup == null) _pageGroup = _activePage.gameObject.AddComponent<CanvasGroup>();
                _pageGroup.alpha=1;
            }
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                GameObject first = page == FrontendPage.Settings ? _master.gameObject :
                    page == FrontendPage.Host ? _hostReady.gameObject :
                    page == FrontendPage.Join ? _codeInput.gameObject :
                    page == FrontendPage.Pause ? _resume.gameObject : _play.gameObject;
                EventSystem.current.SetSelectedGameObject(page == FrontendPage.Main || page == FrontendPage.Pause ? null : first);
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
            if((alpha>.01f)!=(_visibleTarget>.01f) && (!_departing || alpha>.01f))_audio?.PlayPanelMove();
            if (_group == null) return;
            if(alpha>0 && interactable){_departing=false;SetStartVeil(StartIntroSeconds);}
            if(ReferenceActive)
            {
                string next=alpha>_visibleTarget?"panel_enter":alpha<_visibleTarget?"panel_exit":_panelTrack;
                if(!_panelMotionInitialized || next!=_panelTrack)
                {
                    if(alpha>_visibleTarget&&_group.alpha<.02f)
                    {
                        _sidebarRevealAge=0;_pageAge=0;
                        _pageRevealLead=_pages[(int)FrontendPage.Pause].activeSelf?_revealSettings.pausePanelLead:_revealSettings.panelLead;
                    }
                    // Retarget from the rendered pose: reversing a half-finished fade
                    // must not replay an authored fully-visible/fully-hidden endpoint.
                    _panelStartAlpha=_group.alpha;
                    _panelStartX=_panelMotionInitialized?_renderedPanelX:-_revealSettings.panelTravel;
                    _panelTrack=next;_panelTrackAge=0;_panelMotionInitialized=true;
                }
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
            RectTransform r = Rect(parent, text, typeof(Image), typeof(Button), typeof(FrontendButton)); Place(r, ReferenceActive ? y / 3f : 0f, y, ReferenceActive ? 560 : 470, ReferenceActive ? 78 : 70);
            Color normal = tier == 0 ? _theme.accent : tier == 1 ? new Color(.11f, .19f, .23f, .9f) : new Color(.08f, .13f, .17f, .35f);
            var graphic = r.GetComponent<Image>(); graphic.color = normal;
            var skin = _theme.stoneSkin;
            bool skinned = skin != null && skin.normal != null;
            if (skinned) { graphic.sprite = ReferenceActive && skin.referenceNormal != null ? skin.referenceNormal : tier == 0 && skin.primary != null ? skin.primary : skin.normal; graphic.type = UnityEngine.UI.Image.Type.Sliced; graphic.color = Color.white;
                // Scale border pixels uniformly with the 136px source height; leave the hit rect unchanged.
                graphic.pixelsPerUnitMultiplier = graphic.sprite.rect.height / (ReferenceActive ? 78f : 70f);
            }
            var b = r.GetComponent<Button>(); b.targetGraphic = graphic;
            b.onClick.AddListener(()=>{if(_audio!=null)_audio.InvokeButtonAction(action);else action?.Invoke();});
            var caption = Label(r, text, ReferenceActive ? 25 : tier == 0 ? 30 : 27, tier == 0 && !skinned ? _theme.ink : _theme.text);
            Stretch(caption.rectTransform, Vector2.zero, Vector2.one); caption.margin = ReferenceActive ? new Vector4(90,12,90,12) : new Vector4(skinned ? 72 : 24,0,24,0); caption.alignment = ReferenceActive ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
            if (skinned && roleIcon != null) Ornament(r, "Button role icon", roleIcon, ReferenceActive ? 38 : 28, ReferenceActive ? 18 : 17, ReferenceActive ? 42 : 36, ReferenceActive ? 42 : 36);
            if (ReferenceActive && roleIcon != null)
            {
                StoneReferenceGlyph.Shape? shape=roleIcon==skin.hostIcon?StoneReferenceGlyph.Shape.People:roleIcon==skin.joinIcon?StoneReferenceGlyph.Shape.PersonPlus:roleIcon==skin.settingsIcon?StoneReferenceGlyph.Shape.Gear:roleIcon==skin.exitIcon?StoneReferenceGlyph.Shape.Door:roleIcon==skin.botIcon?StoneReferenceGlyph.Shape.Target:(StoneReferenceGlyph.Shape?)null;
                if(shape.HasValue){r.Find("Button role icon").GetComponent<Image>().enabled=false;var glyph=StoneReferenceGlyph.Create(r,"Reference role silhouette",shape.Value,_theme.text);Place(glyph.rectTransform,38,18,42,42);}
            }
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
            var label = r.GetComponent<TextMeshProUGUI>(); label.font = ReferenceActive && Reference.menuFont != null ? Reference.menuFont : display ? _theme.displayFont : readable ? _theme.readableFont : _theme.labelFont;
            label.text = text; label.fontSize = size * _theme.menuFontScale;
            if (ReferenceActive) { label.fontSize = Mathf.Max(18,label.fontSize); label.characterSpacing=1.5f; } label.color = color; label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }
        // Visible alpha bounds measured from the installed sprite rects, in a 40px image box.
        // These compensate source padding and asymmetric silhouettes without changing artwork.
        private static Vector2 ElementGlyphOffset(Elemental.Simulation.Magic.ElementId element)
        {
            switch(element)
            {
                case Elemental.Simulation.Magic.ElementId.Fire: return new Vector2(1.132f,.906f);
                case Elemental.Simulation.Magic.ElementId.Water: return new Vector2(1f,-.52f);
                case Elemental.Simulation.Magic.ElementId.Air: return new Vector2(-.037f,0f);
                default: return new Vector2(.043f,.638f);
            }
        }
        private void BuildElementStatus(Transform parent)
        {
            var skin=_theme.stoneSkin;
            Sprite[] sprites={skin.fire,skin.earth,skin.water,skin.air};
            Color[] colors={new Color(1,.40f,.04f),new Color(.77f,.97f,.41f),new Color(.2f,.75f,1),new Color(.76f,.75f,.71f)};
            for(int i=0;i<4;i++)
            {
                // One shared center/scaling transform; frame, rim light and symbol cannot drift apart.
                var holder=Rect(parent,"Element token "+ElementOrder[i]);Place(holder,53+i*128,0,70,70);
                holder.pivot=new Vector2(.5f,.5f);holder.anchoredPosition+=new Vector2(35,-35);_elementHolders[i]=holder;
                var glow=StoneReferenceGlyph.Create(holder,"Element glow "+ElementOrder[i],StoneReferenceGlyph.Shape.DiamondSoftRim,new Color(.84f,1,.48f,1));
                Place(glow.rectTransform,0,0,70,70);_elementGlows[i]=glow;glow.canvasRenderer.SetAlpha(0);
                var frame=StoneReferenceGlyph.Create(holder,"Element status "+ElementOrder[i],StoneReferenceGlyph.Shape.SingleDiamond,colors[i]);
                Place(frame.rectTransform,0,0,70,70);_elementFrames[i]=frame;
                if(sprites[i]!=null)
                {
                    float glyphSize=ElementOrder[i]==Elemental.Simulation.Magic.ElementId.Air?34:40;
                    float inset=(70-glyphSize)*.5f;var offset=ElementGlyphOffset(ElementOrder[i])*(glyphSize/40f);
                    Ornament(holder,"Element glyph",sprites[i],inset+offset.x,inset+offset.y,glyphSize,glyphSize);
                }
                _elementBlooms[i]=UiContourBloom.Create(holder.Find("Element glyph").GetComponent<Image>(),new Color(.84f,1,.45f),9);
                var glint=StoneReferenceGlyph.Create(holder,"Element rim glints",StoneReferenceGlyph.Shape.DiamondGlints,new Color(1,1,.78f,1));
                Place(glint.rectTransform,0,0,70,70);_elementGlints[i]=glint;glint.canvasRenderer.SetAlpha(0);
                var name=Label(parent,ElementOrder[i].ToString().ToUpperInvariant(),18,colors[i]);
                Place(name.rectTransform,37+i*128,84,102,30); name.alignment=TextAlignmentOptions.Center;
            }
            var ribbon=Image(parent,"Active element ribbon",Color.white); ribbon.sprite=skin.referenceCard!=null?skin.referenceCard:skin.ribbon; ribbon.type=UnityEngine.UI.Image.Type.Simple;
            ribbon.raycastTarget=false; Place(ribbon.rectTransform,-12,154,594,133);
            _activeElementGlyph=Image(ribbon.transform,"Active element glyph",Color.white); _activeElementGlyph.preserveAspect=true; _activeElementGlyph.raycastTarget=false; Place(_activeElementGlyph.rectTransform,65,18,68,68);
            _cardBloom=UiContourBloom.Create(_activeElementGlyph,new Color(.84f,1,.45f),11);
            _activeElementLabel=Label(ribbon.transform,"",21,_theme.accent); Place(_activeElementLabel.rectTransform,180,8,348,34);
            _activeElementLabel.alignment=TextAlignmentOptions.Center;
            _activeElementTraits=Label(ribbon.transform,"",13,_theme.text); Place(_activeElementTraits.rectTransform,180,48,348,26);
            _activeElementTraits.fontSize=13; _activeElementTraits.characterSpacing=2; _activeElementTraits.alignment=TextAlignmentOptions.Center;
            _lastStatusElement=(Elemental.Simulation.Magic.ElementId)byte.MaxValue;
        }
        private void UpdateReferenceMotion()
        {
            if ((_pages[0].activeSelf || _pages[4].activeSelf) && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            {
                var keys=UnityEngine.InputSystem.Keyboard.current;
                if(keys!=null&&(keys.tabKey.wasPressedThisFrame||keys.upArrowKey.wasPressedThisFrame||keys.downArrowKey.wasPressedThisFrame)) EventSystem.current.SetSelectedGameObject(_pages[4].activeSelf?_resume.gameObject:_play.gameObject);
            }
            bool reduced=_flow.Preferences.ReducedMotion; float dt=Time.unscaledDeltaTime;
            _sidebarRevealAge+=dt;
            _panelTrackAge+=dt;
            float panelDuration=Reference.Duration(_panelTrack,.28f,reduced);
            if(!reduced&&_panelTrack=="panel_enter"&&_pages[(int)FrontendPage.Pause].activeSelf)
                panelDuration=_revealSettings.pausePanelSeconds;
            float panelT=_panelTrackAge/Mathf.Max(.001f,panelDuration);
            float panelEase=Reference.Evaluate(_panelTrack,panelT);
            _visibleAlpha=Mathf.Lerp(_panelStartAlpha,_visibleTarget,panelEase);
            _group.alpha=_visibleAlpha;
            float panelX=reduced?0:Mathf.Lerp(_panelStartX,_panelTrack=="panel_exit"?-_revealSettings.panelTravel*.65f:0,panelEase);
            if(_departing)
            {
                _departureAge+=dt;
                float t=Mathf.Clamp01(_departureAge/(reduced?.08f:.54f));
                // Keep the art opaque until it is outside the viewport. Full-width
                // translation includes the oversized curtain, not just the buttons.
                float width=((RectTransform)transform).rect.width;
                float travel=Mathf.Max(1500,width+200);
                panelX=reduced?0:Mathf.Lerp(_departureStartX,-travel,t*t*(3-2*t));
                _group.alpha=reduced?Mathf.Lerp(_departureStartAlpha,0,t):t<1?_departureStartAlpha:0;
                _visibleAlpha=_group.alpha;
            }
            _renderedPanelX=panelX;
            _panel.anchoredPosition=_panelBasePosition+new Vector2(panelX,0);
            if(_curtainRect!=null)_curtainRect.anchoredPosition=_curtainBasePosition+new Vector2(panelX,0);
            _pageAge+=dt;
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
                    _footerMarkerFrom=_footerMarker!=null?_footerMarker.anchoredPosition.x:50;
                    for(int i=0;i<FooterNames.Length;i++)
                    {
                        bool active=FooterNames[i]==selected.ToString().ToUpperInvariant();
                        if(_footerLabels[i]!=null){_footerLabels[i].color=active?new Color(1,.95f,.78f):_theme.muted;_footerLabels[i].fontStyle=active?FontStyles.Bold:FontStyles.Normal;}
                        if(active)_footerMarkerTo=50+i*110;
                    }
                    var skin=_theme.stoneSkin;
                    _activeElementGlyph.sprite=selected==Elemental.Simulation.Magic.ElementId.Fire?skin.fire:selected==Elemental.Simulation.Magic.ElementId.Water?skin.water:selected==Elemental.Simulation.Magic.ElementId.Air?skin.air:skin.earth;
                    float cardGlyphSize=selected==Elemental.Simulation.Magic.ElementId.Air?58:68;
                    float cardInset=(68-cardGlyphSize)*.5f;var cardOffset=ElementGlyphOffset(selected)*(cardGlyphSize/40f);
                    Place(_activeElementGlyph.rectTransform,65+cardInset+cardOffset.x,18+cardInset+cardOffset.y,cardGlyphSize,cardGlyphSize); CenterAspectPivot(_activeElementGlyph.rectTransform);
                    _activeElementTraits.text=selected==Elemental.Simulation.Magic.ElementId.Earth?"STRENGTH  /  BALANCE  /  ENDURANCE":"";
                }
                if(_footerMarker!=null)
                {
                    float t=reduced?1:Mathf.SmoothStep(0,1,Mathf.Clamp01(_menuElementAge/.22f));
                    _footerMarker.anchoredPosition=new Vector2(Mathf.Lerp(_footerMarkerFrom,_footerMarkerTo,t),-46);
                    _footerMarkerGlow.canvasRenderer.SetAlpha(reduced?.65f:.48f+.12f*Mathf.Sin(Time.unscaledTime*1.6f));
                }
                for(int i=0;i<4;i++) if(_elementFrames[i]!=null)
                {
                    bool active=ElementOrder[i]==selected;
                    var menuFx=_theme.menuPresentation!=null?_theme.menuPresentation.Get(MenuScreenId.Sidebar):null;
                    float fxStrength=menuFx!=null?menuFx.glowStrength:.85f;
                    _elementBlooms[i].SetStrength(active?fxStrength:0);_cardBloom.SetStrength(fxStrength);
                    if(menuFx!=null){_elementBlooms[i].Radius=menuFx.glowRadius;_cardBloom.Radius=menuFx.glowRadius;}
                    _elementFrames[i].canvasRenderer.SetAlpha(1);
                    // A single outline survives selection; only its tint and shared token scale change.
                    var baseColor=ElementOrder[i]==Elemental.Simulation.Magic.ElementId.Fire?new Color(1,.40f,.04f):ElementOrder[i]==Elemental.Simulation.Magic.ElementId.Water?new Color(.2f,.75f,1):ElementOrder[i]==Elemental.Simulation.Magic.ElementId.Air?new Color(.76f,.75f,.71f):new Color(.77f,.97f,.41f);
                    _elementFrames[i].color=active?new Color(.94f,1,.66f):baseColor;
                    float selectionT=_menuElementAge/Reference.Duration("element_select",.22f,reduced);
                    float selectionScale=active&&!reduced?Reference.Sample("element_select","scale",selectionT,1):1;
                    _elementHolders[i].localScale=Vector3.one*(active?1.24f*selectionScale:1);
                    _elementGlows[i].canvasRenderer.SetAlpha(active?(reduced?.65f:.72f+.18f*Mathf.Sin(Time.unscaledTime*1.5f)):0);
                    _elementGlints[i].canvasRenderer.SetAlpha(active?(reduced?.5f:.55f+.35f*Mathf.Pow(.5f+.5f*Mathf.Sin(Time.unscaledTime*1.8f),4)):0);
                }
            }
        }
        private static void Ornament(Transform parent, string name, Sprite sprite, float x, float y, float width, float height)
        {
            var image = Image(parent, name, Color.white); image.sprite = sprite; image.preserveAspect = true;
            image.raycastTarget = false; Place(image.rectTransform, x, y, width, height); CenterAspectPivot(image.rectTransform);
        }
        private static void CenterAspectPivot(RectTransform rect)
        { rect.pivot=new Vector2(.5f,.5f); rect.anchoredPosition+=new Vector2(rect.sizeDelta.x*.5f,-rect.sizeDelta.y*.5f); }
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
