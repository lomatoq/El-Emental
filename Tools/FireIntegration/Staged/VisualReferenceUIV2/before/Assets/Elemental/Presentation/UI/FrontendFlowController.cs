using System;
using Elemental.Presentation.Camera;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Elemental.Presentation.UI
{
    public enum FrontendState { Loading, Main, Settings, Starting, Combat, Host, Join, Paused, Ending }

    [DefaultExecutionOrder(11000), DisallowMultipleComponent]
    public sealed class FrontendFlowController : MonoBehaviour
    {
        [SerializeField] private ElementalUITheme theme;
        [SerializeField] private FrontendMenuView view;
        [SerializeField] private UIAudioFeedback audioFeedback;
        [SerializeField] private CinematicMenuCamera menuCamera;
        [SerializeField] private EarthDuelHud hud;
        [SerializeField] private EarthMvpDuelController duel;
        [SerializeField] private EarthSceneReadinessGate readiness;
        [SerializeField] private EarthCameraDirector cameraDirector;
        [SerializeField] private Behaviour[] debugOverlays = Array.Empty<Behaviour>();
        public float PresentationTransitionSeconds => theme.transitionSeconds;
        public float CountdownDurationSeconds => Mathf.Max(1f, theme.countdownSeconds);
        private Func<float> _networkCountdownRemaining;
        public void SetPresentationCameraDirector(EarthCameraDirector director)
        {
            cameraDirector = director;
            cameraDirector?.ApplyUserPreferences(Preferences.Sensitivity, Preferences.ReducedMotion);
        }
        public void ConfigureDebugOverlays(Behaviour[] overlays) => debugOverlays = overlays ?? Array.Empty<Behaviour>();
        private void HideDebugOverlays() { foreach (var overlay in debugOverlays) if (overlay != null) overlay.enabled = false; }
        private InputAction _back;
        private float _transition;
        private bool _countdownCameraReleased;
        private bool _built;
        private bool _pausedLocal, _settingsFromPause, _cursorBeforePause;
        private float _timeScaleBeforePause = 1f;
        private CursorLockMode _lockBeforePause;
        public event Action<bool> NetworkPauseRequested;
        private bool _networkRound;
        public FrontendPreferences Preferences { get; } = new FrontendPreferences();
        public FrontendState State { get; private set; } = FrontendState.Loading;
        public bool HasEnteredCombat { get; private set; }
        public bool IsWorldReady => (readiness == null || readiness.IsReady) &&
            (duel == null || !duel.ArenaResetInProgress && duel.ArenaResetError == null);
        public event Action HostRequested, NetworkReadyRequested, NetworkCancelRequested;
        public event Action<string> JoinRequested;
        public bool OnlineAvailable { get; private set; }
        public string RoomCode { get; private set; }
        public void Configure(ElementalUITheme palette, FrontendMenuView menu, UIAudioFeedback audio,
            CinematicMenuCamera camera, EarthDuelHud combatHud, EarthMvpDuelController match,
            EarthSceneReadinessGate gate, EarthCameraDirector director)
        { theme = palette; view = menu; audioFeedback = audio; menuCamera = camera; hud = combatHud; duel = match; readiness = gate; cameraDirector = director; }
        private void Start()
        {
            if (theme == null || view == null || duel == null) { Debug.LogError("Frontend needs theme, view and duel references.", this); enabled = false; return; }
            Preferences.Load(); audioFeedback.Configure(theme); view.Build(theme, audioFeedback, this); _built = true;
            hud?.ConfigurePause(Pause);
            ApplyPreferences(); HideDebugOverlays(); hud?.SetFrontendPresentation(theme, false); duel.SetRoundReady(false);
            view.SetPlayAvailable(false); view.SetStatus("PREPARING ARENA");
        }
        private void OnEnable()
        { _back = new InputAction("Frontend back", InputActionType.Button, "<Keyboard>/escape"); _back.performed += OnBack; _back.Enable(); }
        private void OnBack(InputAction.CallbackContext _) => Back();
        private void Update()
        {
            if (!_built) return;
            if (State == FrontendState.Loading)
            {
                if (readiness != null && readiness.Failed) { view.SetStatus("ARENA COULD NOT LOAD. CHECK THE GAME LOG.", true); return; }
                if (IsWorldReady) ShowMain();
            }
            if (State == FrontendState.Main) view.SetPlayAvailable(IsWorldReady);
            if (State == FrontendState.Starting)
            {
                if (!IsWorldReady) return;
                _transition += Time.unscaledDeltaTime;
                float duration = Mathf.Max(1f, theme.countdownSeconds);
                float blendDuration = Mathf.Clamp(theme.countdownCameraBlendSeconds, .1f, duration);
                float remaining = _networkRound && _networkCountdownRemaining != null
                    ? _networkCountdownRemaining() : Mathf.Max(0f, duration - _transition);
                view.SetCountdown(Mathf.CeilToInt(remaining));
                float menuFade = Mathf.Clamp01(_transition / theme.transitionSeconds);
                view.SetVisibility(1f - menuFade * menuFade * (3f - 2f * menuFade), false);
                menuCamera.SetCountdownDollyProgress((duration - remaining) / duration,
                    Mathf.Clamp01(1f - remaining / blendDuration), Preferences.ReducedMotion);
                if (!_countdownCameraReleased && remaining <= blendDuration)
                { _countdownCameraReleased = true; menuCamera.BeginCombatTransition(); }
                float t = Mathf.Clamp01(1f - remaining / blendDuration);
                menuCamera.SetTransitionProgress(t * t * (3f - 2f * t));
                if (remaining <= 0f)
                {
                    view.SetCountdown(0);
                    menuCamera.FinishCombatTransition(); State = FrontendState.Combat;
                    HasEnteredCombat = true;
                    if (!_networkRound) { duel.SetRoundReady(true); }
                    hud?.SetFrontendPresentation(theme, true);
                    view.SetVisibility(0, false);
                }
            }
            else if (menuCamera != null && menuCamera.NeedsReframe) menuCamera.Reframe(Preferences.ReducedMotion);
        }
        public bool BeginBot()
        {
            if (!_built || !IsWorldReady || State != FrontendState.Main) return false;
            _networkRound = false;
            audioFeedback.Play(UIAudioCue.Confirm); State = FrontendState.Starting; _transition = 0;
            duel.SetRoundReady(false); duel.RestartRound();
            StartCountdownPresentation(); return true;
        }
        private void StartCountdownPresentation()
        {
            _countdownCameraReleased = false; view.SetVisibility(1, false);
            view.SetCountdown(Mathf.CeilToInt(theme.countdownSeconds));
            menuCamera.BeginCountdown(Preferences.ReducedMotion, theme.countdownCameraBlendSeconds);
        }
        public void ShowMain(string message = null)
        {
            if (!_built) return;
            RestorePauseState();
            duel.CaptureArenaBaselineIfReady(); duel.RestoreArenaForMatchBoundary();
            _networkCountdownRemaining = null;
            _networkRound = false;
            duel.SetRoundReady(false); hud?.SetFrontendPresentation(theme, false);
            view.SetCountdown(0);
            State = FrontendState.Main; menuCamera.Enter(Preferences.ReducedMotion, theme.transitionSeconds);
            view.Show(FrontendPage.Main); view.SetVisibility(1, true); view.SetPlayAvailable(IsWorldReady);
            view.SetStatus(message ?? (OnlineAvailable ? "" : "ONLINE MULTIPLAYER — NOT AVAILABLE IN LOCAL ALPHA"));
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        }
        public void OpenSettings()
        {
            if (State != FrontendState.Main && State != FrontendState.Paused) return;
            _settingsFromPause = State == FrontendState.Paused;
            State = FrontendState.Settings; view.Show(FrontendPage.Settings);
            view.SetStatus("Settings are saved automatically.");
        }
        public void Pause()
        {
            if (!_built || State != FrontendState.Combat) return;
            _cursorBeforePause = Cursor.visible; _lockBeforePause = Cursor.lockState;
            if (_networkRound) NetworkPauseRequested?.Invoke(true);
            else
            {
                _timeScaleBeforePause = Time.timeScale; _pausedLocal = true;
                duel.SetRoundReady(false); Time.timeScale = 0f;
            }
            hud?.SetFrontendPresentation(theme, false);
            ShowPausePage();
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        }
        private void ShowPausePage()
        {
            State = FrontendState.Paused; view.Show(FrontendPage.Pause); view.SetVisibility(1, true);
            view.SetStatus(_networkRound ? "ONLINE MATCH CONTINUES WHILE THIS MENU IS OPEN" : "GAME PAUSED");
        }
        private void RestorePauseState()
        {
            if (_pausedLocal) { Time.timeScale = _timeScaleBeforePause; _pausedLocal = false; }
            _settingsFromPause = false;
        }
        public void Resume()
        {
            if (State != FrontendState.Paused && !(State == FrontendState.Settings && _settingsFromPause)) return;
            RestorePauseState();
            if (_networkRound) NetworkPauseRequested?.Invoke(false); else duel.SetRoundReady(true);
            State = FrontendState.Combat; view.SetVisibility(0, false);
            hud?.SetFrontendPresentation(theme, true);
            Cursor.visible = _cursorBeforePause; Cursor.lockState = _lockBeforePause;
            audioFeedback.Play(UIAudioCue.Confirm);
        }
        public void EndMatch()
        {
            if (State != FrontendState.Paused && State != FrontendState.Combat &&
                !(State == FrontendState.Settings && _settingsFromPause)) return;
            RestorePauseState();
            if (_networkRound)
            {
                State = FrontendState.Ending; view.SetVisibility(1, false); view.SetStatus("ENDING MATCH...");
                NetworkCancelRequested?.Invoke(); return;
            }
            ShowMain();
        }
        public void Back()
        {
            if (!_built || State is FrontendState.Starting or FrontendState.Loading or FrontendState.Ending) return;
            audioFeedback.Play(UIAudioCue.Back);
            if (State == FrontendState.Combat) { Pause(); return; }
            if (State == FrontendState.Paused) { Resume(); return; }
            if (State == FrontendState.Settings && _settingsFromPause) { ShowPausePage(); return; }
            if (State == FrontendState.Host || State == FrontendState.Join) NetworkCancelRequested?.Invoke();
            if (State != FrontendState.Main) ShowMain();
        }
        public void ApplyPreferences()
        {
            AudioListener.volume = Preferences.MasterVolume;
            if (audioFeedback != null) audioFeedback.Volume = Preferences.UIVolume;
            cameraDirector?.ApplyUserPreferences(Preferences.Sensitivity, Preferences.ReducedMotion);
            menuCamera?.Reframe(Preferences.ReducedMotion);
        }
        public void SetOnlineAvailable(bool available) { OnlineAvailable = available; if (_built) view.SetNetworkAvailable(available); }
        public void OpenHost()
        { if (!OnlineAvailable || State != FrontendState.Main) return; State = FrontendState.Host; view.Show(FrontendPage.Host); view.SetStatus("CREATING ROOM…"); HostRequested?.Invoke(); }
        public void OpenJoin()
        { if (!OnlineAvailable || State != FrontendState.Main) return; State = FrontendState.Join; view.Show(FrontendPage.Join); view.SetStatus("Paste the code shared by the host."); }
        public void JoinRoom(string code)
        { if (!OnlineAvailable || State != FrontendState.Join) return; JoinRequested?.Invoke(code.Trim().ToUpperInvariant()); }
        public void SetNetworkReady() => NetworkReadyRequested?.Invoke();
        public bool BeginOnlineMatch(Func<float> countdownRemaining)
        {
            if (!_built || !IsWorldReady || State is not (FrontendState.Host or FrontendState.Join)) return false;
            _networkCountdownRemaining = countdownRemaining ?? throw new ArgumentNullException(nameof(countdownRemaining));
            _networkRound = true; State = FrontendState.Starting; _transition = 0;
            audioFeedback.Play(UIAudioCue.Connect); view.SetVisibility(1, false);
            StartCountdownPresentation(); return true;
        }
        public void ShowConnectedRoom(string code, string status)
        {
            if (State is not (FrontendState.Host or FrontendState.Join)) return;
            State = FrontendState.Host; view.Show(FrontendPage.Host); UpdateRoom(code, status);
        }
        public void UpdateRoom(string code, string status)
        { RoomCode = code; view.SetRoomCode(code); view.SetStatus(status); }
        public void SetNetworkStatus(string status, bool error = false)
        { view.SetStatus(status, error); if (error) audioFeedback.Play(UIAudioCue.Error); }
        public void SetConnecting(bool value) => view.SetConnecting(value);
        public void CopyRoomCode()
        { if (string.IsNullOrEmpty(RoomCode)) return; GUIUtility.systemCopyBuffer = RoomCode; audioFeedback.Play(UIAudioCue.Copy); view.SetStatus("ROOM CODE COPIED"); }
        public void Quit()
        {
            if (State != FrontendState.Main) return;
            Preferences.Save();
            if (Application.isEditor) view.SetStatus("Stop Play mode to exit the editor preview."); else Application.Quit();
        }
        private void OnDisable()
        {
            RestorePauseState();
            if (_back != null) { _back.performed -= OnBack; _back.Dispose(); _back = null; }
            menuCamera?.FinishCombatTransition();
        }
    }
}

