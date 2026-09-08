using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace ElEmental.StoneUI
{
    [Serializable] public struct StoneHudSnapshot
    {
        public float health, maxHealth, mana, maxMana, secondsRemaining;
        public int redScore, blueScore;
        public string modeLabel, status;
        public static StoneHudSnapshot Preview => new StoneHudSnapshot { health=78, maxHealth=100, mana=100, maxMana=100, secondsRemaining=296, modeLabel="EARTH DUEL" };
    }
    [Serializable] public struct StoneUISettings
    {
        [Range(0, 1)] public float masterVolume, uiVolume, sensitivity;
        public bool reducedMotion;
    }
    [Serializable] public sealed class StoneActionEvent : UnityEvent<string, string> { }
    [Serializable] public sealed class StoneBlockEvent : UnityEvent<bool> { }

    /// <summary>Presentation only. It never changes physics, timeScale, camera poses or network state.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(UIDocument))]
    public sealed class StoneUIController : MonoBehaviour
    {
        public StoneUIAssets assets;
        [Tooltip("Only the demo prefab enables this. No simulated action is represented as a real game/network action.")]
        public bool demoMode;
        public string initialScreen = "MainMenu";
        public StoneActionEvent onAction = new StoneActionEvent();
        public StoneBlockEvent onInputBlock = new StoneBlockEvent();
        public event Action<string, string> ActionRequested;
        public event Action<bool> InputBlockChanged;
        public event Action<StoneUISettings> SettingsChanged;
        public string CurrentScreen { get; private set; }
        public StoneUISettings Settings => _settings;
        public bool BlocksGameplayInput => CurrentScreen != "HUD";

        private UIDocument _document;
        private VisualElement _root, _view;
        private readonly Stack<string> _history = new Stack<string>();
        private readonly List<Button> _focusButtons = new List<Button>();
        private StoneUISettings _settings;
        private StoneHudSnapshot _hud = StoneHudSnapshot.Preview;
        private float _shownHp = .78f, _shownMp = 1f;
        private string _roomCode = "", _lastJoinCode = "";
        private int _occupancy = 1, _generation;
        private float _saveAfter = -1f;
        private Rect _lastSafeArea;
        private Vector2Int _lastResolution;
        private StoneUIMotion _motion;
        private StoneUISounds _sounds;
        private const string Pref = "EE.StoneUI.";

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            if (assets == null) assets = Resources.Load<StoneUIAssets>("ElEmentalStoneUI/StoneUIAssets");
            if (assets == null) { Debug.LogError("StoneUI: run Tools > EL EMENTAL > Stone UI > Build library first.", this); enabled=false; return; }
            _root = _document.rootVisualElement;
            if (_root == null) { Debug.LogError("StoneUI: UIDocument has no root. Assign PanelSettings.", this); enabled=false; return; }
            _motion = GetComponent<StoneUIMotion>();
            if (_motion == null) _motion = gameObject.AddComponent<StoneUIMotion>();
            _sounds = GetComponent<StoneUISounds>();
            _motion.Configure(assets.animationPresets);
            LoadSettings();
            _root.RegisterCallback<GeometryChangedEvent>(OnGeometry);
            ShowScreen(initialScreen, false);
        }
        private void OnDisable()
        {
            ++_generation;
            if (_root != null) _root.UnregisterCallback<GeometryChangedEvent>(OnGeometry);
            SaveNow();
            _motion?.CancelAll();
            _history.Clear();
        }
        private void OnApplicationPause(bool paused) { if (paused) SaveNow(); }
        private void OnApplicationFocus(bool focus) { if (!focus) SaveNow(); }
        private void OnGeometry(GeometryChangedEvent e) => UpdateSafeArea();

        public void ShowScreen(string id, bool remember = true)
        {
            if (_root == null || assets == null) return;
            VisualTreeAsset tree = assets.Screen(id);
            if (tree == null) { Debug.LogError("StoneUI: missing screen " + id, this); return; }
            if (remember && !string.IsNullOrEmpty(CurrentScreen) && CurrentScreen != id) _history.Push(CurrentScreen);
            ++_generation;
            _motion?.CancelAll();
            _root.Clear();
            tree.CloneTree(_root);
            if (assets.theme != null && !_root.styleSheets.Contains(assets.theme)) _root.styleSheets.Add(assets.theme);
            _view = _root.Q<VisualElement>("screen-root");
            if (_view == null) { Debug.LogError("StoneUI: screen lacks screen-root: " + id, this); return; }
            CurrentScreen = id;
            if (assets.bodyFont != null) _view.style.unityFont = assets.bodyFont;
            if (assets.displayFont != null)
            {
                _view.Query<Label>().ForEach(l => { if (l.ClassListContains("button-label") || l.ClassListContains("screen-title") || l.ClassListContains("result-title")) l.style.unityFont = assets.displayFont; });
            }
            _view.EnableInClassList("reduced-motion", _settings.reducedMotion);
            var background = _view.Q<VisualElement>("demo-background");
            if (background != null)
            {
                background.style.display = demoMode ? DisplayStyle.Flex : DisplayStyle.None;
                if (demoMode && assets.demoBackground != null) background.style.backgroundImage = assets.demoBackground;
            }
            var badge = _view.Q<Label>("demo-badge");
            if (badge != null) badge.style.display = demoMode ? DisplayStyle.Flex : DisplayStyle.None;
            ConfigurePicking(id == "HUD");
            BindScreen();
            UpdateSafeArea();
            if (id == "HUD") RefreshHudLabels();
            if (id == "Host") RefreshRoom();
            _view.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            int generation = _generation;
            _view.schedule.Execute(() => { if (generation == _generation && id != "HUD" && _focusButtons.Count > 0) _focusButtons[0].Focus(); });
            if (id != "HUD") _motion?.Reveal(_view, _settings.reducedMotion, id == "Victory" || id == "Defeat" || id == "Draw");
            InputBlockChanged?.Invoke(BlocksGameplayInput);
            onInputBlock?.Invoke(BlocksGameplayInput);
        }
        private void ConfigurePicking(bool hud)
        {
            _root.pickingMode = PickingMode.Ignore;
            _view.Query<VisualElement>().ForEach(v =>
            {
                if (hud || v.ClassListContains("art") || v is Label || v.name == "safe-root") v.pickingMode = PickingMode.Ignore;
                if (v is Button || v is TextField || v is Slider || v is Toggle) v.pickingMode = PickingMode.Position;
            });
            if (hud) _view.pickingMode = PickingMode.Ignore;
        }
        private void BindScreen()
        {
            _focusButtons.Clear();
            _view.Query<Button>().ForEach(b =>
            {
                _focusButtons.Add(b);
                b.RegisterCallback<PointerEnterEvent>(_ => { if (b.enabledInHierarchy) _sounds?.Play("hover"); });
                b.RegisterCallback<PointerDownEvent>(_ => { if (b.enabledInHierarchy) _motion?.Press(b, _settings.reducedMotion); });
                b.RegisterCallback<PointerUpEvent>(_ => _motion?.Release(b, _settings.reducedMotion));
                b.RegisterCallback<PointerLeaveEvent>(_ => _motion?.Release(b, _settings.reducedMotion));
            });
            Click("back", Back); Click("settings", () => ShowScreen("Settings"));
            Click("multiplayer", () => ShowScreen("Multiplayer"));
            Click("pause", () => ShowScreen("Pause")); Click("resume", () => ShowScreen("HUD", false));
            Click("controls", () => ShowScreen("Controls"));
            Click("exit", () => ShowScreen("ConfirmExit"));
            Click("confirm-exit", () => Emit("exit", ""));
            Click("practice", () => StartMode("practice")); Click("bot", () => StartMode("bot"));
            Click("host", () => { _roomCode=""; _occupancy=1; ShowScreen("Host"); if (demoMode) SetRoom("DEMO42",1,"Preview only — no room was created."); else Emit("host", ""); });
            Click("guest", () => ShowScreen("Guest"));
            Click("copy", () => { if (string.IsNullOrWhiteSpace(_roomCode)) { Status("A room code is not available yet."); return; } GUIUtility.systemCopyBuffer=_roomCode; _sounds?.Play("copy"); Status("Code copied."); });
            Click("join", Join);
            Click("cancel", Back);
            Click("retry-connect", () => { if (demoMode) ShowScreen("HUD"); else { ShowScreen("Connecting", false); Emit("retry-connect", _lastJoinCode); } });
            Click("menu", () => { if (demoMode) { _history.Clear(); ShowScreen("MainMenu",false); } else Emit("return-to-menu", ""); });
            Click("rematch", () => { if (demoMode) ShowScreen("HUD",false); else Emit("rematch", ""); });
            if (CurrentScreen == "Settings") BindSettings();
            var codeInput = _view.Q<TextField>("room-input");
            if (codeInput != null) { codeInput.SetValueWithoutNotify(_lastJoinCode); codeInput.RegisterValueChangedCallback(_ => SetLabel("validation", "")); }
        }
        private void Click(string id, Action action)
        {
            Button b = _view.Q<Button>(id);
            if (b != null) b.clicked += () => { _sounds?.Play("confirm"); action(); };
        }
        private void StartMode(string mode)
        {
            if (demoMode) { _history.Clear(); ApplyHud(StoneHudSnapshot.Preview); ShowScreen("HUD",false); }
            else Emit("start-" + mode, "");
        }
        private void Join()
        {
            TextField field = _view.Q<TextField>("room-input");
            string value = field != null ? (field.value ?? "").Trim() : "";
            if (value.Length == 0) { SetLabel("validation", "Enter the room code."); field?.Focus(); _sounds?.Play("error"); return; }
            // Preserve case and the service's alphabet. Actual protocol validation belongs to the backend.
            _lastJoinCode = value;
            if (demoMode) { ShowScreen("HUD",false); return; }
            ShowScreen("Connecting");
            Emit("join", value);
        }
        private void Emit(string action, string payload)
        {
            ActionRequested?.Invoke(action, payload);
            onAction?.Invoke(action, payload);
            if (!demoMode && ActionRequested == null && (onAction == null || onAction.GetPersistentEventCount() == 0))
                Status("Backend not bound. Connect StoneUIController.ActionRequested.");
        }
        public void Back()
        {
            if (CurrentScreen == "Host" && !demoMode) Emit("cancel-host", "");
            if (CurrentScreen == "Connecting" && !demoMode) Emit("cancel-connect", "");
            ShowScreen(_history.Count > 0 ? _history.Pop() : "MainMenu", false);
        }
        public void TogglePause()
        {
            if (CurrentScreen == "HUD") ShowScreen("Pause");
            else if (CurrentScreen == "Pause") ShowScreen("HUD", false);
        }
        private void OnKeyDown(KeyDownEvent e)
        {
            if (e.keyCode == KeyCode.Escape)
            {
                if (CurrentScreen == "HUD" || CurrentScreen == "Pause") TogglePause(); else Back();
                e.StopPropagation(); return;
            }
            if (e.keyCode == KeyCode.Return && CurrentScreen == "Guest" && e.target is TextField) { Join(); e.StopPropagation(); }
            // UI Toolkit provides native keyboard focus navigation. The game bridge must not process
            // these same keys while BlocksGameplayInput is true. No Input.GetKey calls are used.
        }
        public void ApplyHud(StoneHudSnapshot data)
        {
            data.health = Finite(data.health); data.maxHealth=Mathf.Max(1,Finite(data.maxHealth));
            data.mana=Finite(data.mana); data.maxMana=Mathf.Max(1,Finite(data.maxMana));
            data.secondsRemaining=Mathf.Max(0,Finite(data.secondsRemaining)); _hud=data;
            if (CurrentScreen == "HUD") RefreshHudLabels();
        }
        private void RefreshHudLabels()
        {
            SetLabel("hp-value", Mathf.CeilToInt(Mathf.Clamp(_hud.health,0,_hud.maxHealth)).ToString());
            SetLabel("mp-value", Mathf.CeilToInt(Mathf.Clamp(_hud.mana,0,_hud.maxMana)).ToString());
            SetLabel("red-score",_hud.redScore.ToString()); SetLabel("blue-score",_hud.blueScore.ToString());
            int seconds=Mathf.CeilToInt(_hud.secondsRemaining);
            SetLabel("timer-value", (seconds/60).ToString("00")+":"+(seconds%60).ToString("00"));
            SetLabel("mode-label",_hud.modeLabel ?? "EARTH DUEL"); SetLabel("game-status",_hud.status ?? "");
        }
        public void SetMinimap(Texture texture)
        {
            var map = _view?.Q<VisualElement>("minimap-data");
            if (map == null) return;
            if (texture is RenderTexture rt) map.style.backgroundImage = Background.FromRenderTexture(rt);
            else if (texture is Texture2D tex) map.style.backgroundImage=tex;
            else map.style.backgroundImage=StyleKeyword.None;
        }
        public void SetRoom(string code, int players, string status = null)
        {
            _roomCode=code ?? ""; _occupancy=Mathf.Clamp(players,0,2); RefreshRoom();
            if (status != null) SetLabel("connection-status", status);
        }
        private void RefreshRoom()
        {
            SetLabel("room-code",string.IsNullOrWhiteSpace(_roomCode)?"—":_roomCode);
            SetLabel("occupancy",_occupancy+" / 2");
            SetLabel("connection-status", string.IsNullOrWhiteSpace(_roomCode)?"Creating the room…":"Waiting for the other player…");
            var b=_view?.Q<Button>("copy"); b?.SetEnabled(!string.IsNullOrWhiteSpace(_roomCode));
        }
        public void SetLoading(float progress, string text)
        {
            var fill=_view?.Q<VisualElement>("loading-fill"); float p=Mathf.Clamp01(Finite(progress));
            if (fill!=null) fill.style.width=Length.Percent(p*100);
            SetLabel("loading-label",text??""); SetLabel("loading-progress",Mathf.RoundToInt(p*100)+"%");
        }
        public void ShowResult(string outcome, int localScore, int opponentScore)
        {
            string screen=outcome=="win"?"Victory":outcome=="draw"?"Draw":"Defeat";
            _history.Clear(); ShowScreen(screen,false); SetLabel("result-score",localScore+" — "+opponentScore);
            _sounds?.Play(screen=="Victory"?"success":"error");
        }
        public void ShowConnectionError(string message)
        {
            ShowScreen("ConnectionError",false); SetLabel("error-message", message ?? "Connection interrupted."); _sounds?.Play("error");
        }
        public void Status(string text)
        {
            if (_view == null) return;
            var label=_view.Q<Label>("status-label")??_view.Q<Label>("game-status")??_view.Q<Label>("connection-status");
            if (label!=null) label.text=text??"";
        }
        public void SetControlsText(string bindings) => SetLabel("controls-text",bindings??"");
        private void SetLabel(string name,string text)
        { var label=_view?.Q<Label>(name); if (label!=null && label.text!=text) label.text=text; }
        private static float Finite(float v) => float.IsNaN(v)||float.IsInfinity(v)?0:v;
        private void Update()
        {
            if (_saveAfter>=0 && Time.unscaledTime>=_saveAfter) SaveNow();
            if (_lastResolution.x!=Screen.width || _lastResolution.y!=Screen.height || _lastSafeArea!=Screen.safeArea) UpdateSafeArea();
            if (CurrentScreen!="HUD" || _view==null) return;
            float k=_settings.reducedMotion?1:1-Mathf.Exp(-Time.unscaledDeltaTime/.055f);
            _shownHp=Mathf.Lerp(_shownHp,Mathf.Clamp01(_hud.health/_hud.maxHealth),k);
            _shownMp=Mathf.Lerp(_shownMp,Mathf.Clamp01(_hud.mana/_hud.maxMana),k);
            var hp=_view.Q<VisualElement>("hp-clip"); if(hp!=null) hp.style.height=318*_shownHp;
            var mp=_view.Q<VisualElement>("mp-clip"); if(mp!=null) mp.style.height=318*_shownMp;
        }
        private void UpdateSafeArea()
        {
            _lastResolution=new Vector2Int(Screen.width,Screen.height); _lastSafeArea=Screen.safeArea;
            if (_view==null || Screen.width<=0 || Screen.height<=0) return;
            var safe=_view.Q<VisualElement>("safe-root"); if (safe==null) return;
            safe.style.left=Length.Percent(_lastSafeArea.xMin/Screen.width*100);
            safe.style.right=Length.Percent((Screen.width-_lastSafeArea.xMax)/Screen.width*100);
            safe.style.top=Length.Percent((Screen.height-_lastSafeArea.yMax)/Screen.height*100);
            safe.style.bottom=Length.Percent(_lastSafeArea.yMin/Screen.height*100);
        }
        private void LoadSettings()
        {
            _settings=new StoneUISettings { masterVolume=PlayerPrefs.GetFloat(Pref+"Master",.8f),uiVolume=PlayerPrefs.GetFloat(Pref+"UI",.5f),sensitivity=PlayerPrefs.GetFloat(Pref+"Sensitivity",.45f),reducedMotion=PlayerPrefs.GetInt(Pref+"ReducedMotion",0)!=0 };
            if (_sounds!=null) { _sounds.assets=assets; _sounds.gain=_settings.masterVolume*_settings.uiVolume; }
        }
        private void BindSettings()
        {
            BindSlider("master",_settings.masterVolume,v=>_settings.masterVolume=v);
            BindSlider("ui",_settings.uiVolume,v=>_settings.uiVolume=v);
            BindSlider("sensitivity",_settings.sensitivity,v=>_settings.sensitivity=v);
            var t=_view.Q<Toggle>("reduced-motion");
            if(t!=null) { t.SetValueWithoutNotify(_settings.reducedMotion); t.RegisterValueChangedCallback(e=> { _settings.reducedMotion=e.newValue; _view.EnableInClassList("reduced-motion",e.newValue); if(e.newValue)_motion?.CancelAll(); ChangedSettings(); }); }
            foreach(string element in new[]{"fire","earth","water","air"})
            {
                var b=_view.Q<Button>("element-"+element);
                // This is an element-status display, not an implementation of unavailable magic.
                if(b!=null) { b.SetEnabled(element=="earth"); b.tooltip=element=="earth"?"Earth is available":"Connect availability to the real capability model"; }
            }
        }
        private void BindSlider(string id,float v,Action<float> set)
        {
            var s=_view.Q<Slider>(id); if(s==null)return; s.SetValueWithoutNotify(v*100); SetLabel(id+"-value",Mathf.RoundToInt(v*100).ToString());
            s.RegisterValueChangedCallback(e=>{float f=Mathf.Clamp01(e.newValue/100);set(f);SetLabel(id+"-value",Mathf.RoundToInt(f*100).ToString());ChangedSettings();});
        }
        private void ChangedSettings()
        {
            if(_sounds!=null)_sounds.gain=_settings.masterVolume*_settings.uiVolume;
            _saveAfter=Time.unscaledTime+.35f; SettingsChanged?.Invoke(_settings);
        }
        private void SaveNow()
        {
            if(_saveAfter<0)return;
            PlayerPrefs.SetFloat(Pref+"Master",_settings.masterVolume);PlayerPrefs.SetFloat(Pref+"UI",_settings.uiVolume);PlayerPrefs.SetFloat(Pref+"Sensitivity",_settings.sensitivity);PlayerPrefs.SetInt(Pref+"ReducedMotion",_settings.reducedMotion?1:0);PlayerPrefs.Save();_saveAfter=-1;
        }
    }
}
