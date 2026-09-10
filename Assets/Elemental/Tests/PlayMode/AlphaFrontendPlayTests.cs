using System.Collections;
using System.IO;
using Elemental.Presentation.Animation;
using Elemental.Presentation.UI;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Unity.Profiling;

namespace Elemental.Tests.PlayMode
{
    public sealed class AlphaFrontendPlayTests
    {
        private Scene _scene, _previous;
        private RenderTexture _target;
        private int _oldGameSize = -1;
        private Canvas _canvas;
        private UnityEngine.InputSystem.Mouse _pauseTestMouse;
        private UnityEngine.Camera _camera;
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const string Folder = "BuildReports/AlphaFrontend";
        [UnitySetUp]
        public IEnumerator Load()
        {
            _previous = SceneManager.GetActiveScene();
            Assert.That(SceneManager.GetSceneByPath(ScenePath).isLoaded, Is.False);
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByPath(ScenePath); SceneManager.SetActiveScene(_scene);
            var gate = Find<EarthSceneReadinessGate>();
            double deadline = Time.realtimeSinceStartupAsDouble + 130;
            while (!gate.IsReady && !gate.Failed && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(gate.IsReady, Is.True, gate.Status);
            while (Find<FrontendFlowController>().State == FrontendState.Loading && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            yield return new WaitForSecondsRealtime(1);
            Directory.CreateDirectory(Folder);
        }
        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (_pauseTestMouse != null) UnityEngine.InputSystem.InputSystem.RemoveDevice(_pauseTestMouse);
            RestoreGameView();
            if (_camera != null) _camera.targetTexture = null;
            if (_target != null) { _target.Release(); Object.Destroy(_target); }
            if (_previous.IsValid() && _previous.isLoaded) SceneManager.SetActiveScene(_previous);
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        }
        [UnityTest]
        public IEnumerator MainSettingsCombatKeepRealCameraAndSingleRoundOwner()
        {
            var flow = Find<FrontendFlowController>(); var duel = Find<EarthMvpDuelController>();
            var menu = Find<CinematicMenuCamera>(); var view = Find<FrontendMenuView>();
            _canvas = view.GetComponent<Canvas>();
            _camera = Find<Unity.Cinemachine.CinemachineBrain>().GetComponent<UnityEngine.Camera>();
            var driver = duel.PlayerTransform.GetComponentInChildren<EarthAnimationDriver>();
            Assert.That(flow.State, Is.EqualTo(FrontendState.Main)); Assert.That(duel.CombatAllowed, Is.False);
            Assert.That(driver.PresentationClockMultiplier, Is.EqualTo(.45f).Within(.001f));
            Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(flow.OnlineAvailable, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("PLAY VS BOT"), "Keyboard navigation needs an initial selectable.");
            foreach (var button in view.GetComponentsInChildren<Button>())
                if (button.name == "HOST GAME" || button.name == "JOIN GAME") Assert.That(button.interactable, Is.False);

            foreach (Vector2Int size in new[] { new Vector2Int(1920,1080), new Vector2Int(1920,1200), new Vector2Int(2520,1080) })
            {
                BindCapture(size); menu.Reframe(flow.Preferences.ReducedMotion);
                yield return new WaitForSecondsRealtime(.2f);
                yield return Capture($"Main-{size.x}x{size.y}.png");
                var head = driver.Animator.GetBoneTransform(HumanBodyBones.Head);
                var foot = driver.Animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                float visibleHeight = Mathf.Abs(_camera.WorldToViewportPoint(head.position).y - _camera.WorldToViewportPoint(foot.position).y);
                File.AppendAllText(Folder + "/camera-framing.txt", $"{size}: camera={_camera.transform.position} FOV={_camera.fieldOfView} actualHeadFootHeight={visibleHeight} head={head.position} foot={foot.position} center={menu.SubjectCenter}\n");
                Vector3 actor = _camera.WorldToViewportPoint(menu.SubjectCenter);
                Assert.That(actor.x, Is.InRange(.54f, .88f), "Actual actor must occupy the right side.");
                flow.OpenSettings(); yield return null; yield return Capture($"Settings-{size.x}x{size.y}.png"); flow.Back();
            }
            BindCapture(new Vector2Int(1920,1080)); menu.Reframe(flow.Preferences.ReducedMotion);
            float master = flow.Preferences.MasterVolume, ui = flow.Preferences.UIVolume, sensitivity = flow.Preferences.Sensitivity;
            bool reduced = flow.Preferences.ReducedMotion;
            flow.Preferences.Set(master, ui, 1.3f, true); flow.ApplyPreferences();
            Assert.That(menu.OwnsPresentation, Is.True);
            flow.Preferences.Set(master, ui, sensitivity, reduced); flow.ApplyPreferences();
            var play = EventSystem.current.currentSelectedGameObject;
            Assert.That(play.name, Is.EqualTo("PLAY VS BOT"));
            Vector3 renderedClickPosition = _camera.transform.position;
            float renderedClickFov = _camera.fieldOfView;
            ExecuteEvents.Execute(play, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Assert.That(flow.State, Is.EqualTo(FrontendState.Starting), "Keyboard submit must start the same frontend transition.");
            Assert.That(flow.BeginBot(), Is.False);
            Assert.That(_camera.transform.position, Is.EqualTo(renderedClickPosition));
            Assert.That(_camera.fieldOfView, Is.EqualTo(renderedClickFov));
            float started = Time.unscaledTime;
            using var cpu = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 64);
            using var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 64);
            int frame = 0;
            var digits = new System.Collections.Generic.HashSet<string>();
            float roundBeforeCountdown = duel.RoundRemainingSeconds;
            while (flow.State == FrontendState.Starting)
            {
                Assert.That(duel.CombatAllowed, Is.False);
                Assert.That(duel.RoundRemainingSeconds, Is.EqualTo(roundBeforeCountdown));
                if (!string.IsNullOrEmpty(view.CountdownText) && digits.Add(view.CountdownText))
                {
                    string digit = view.CountdownText;
                    yield return new WaitForSecondsRealtime(.12f);
                    Assert.That(view.CountdownRenderedAlpha, Is.GreaterThan(.7f), digit + ": countdown was hidden by fading menu ancestry.");
                    File.AppendAllText(Folder + "/countdown-camera.txt", $"digit={digit} actualFov={_camera.fieldOfView} physical={_camera.usePhysicalProperties} focal={_camera.focalLength} distance={Vector3.Distance(_camera.transform.position,menu.SubjectCenter)} pos={_camera.transform.position} center={menu.SubjectCenter}\n");
                    if (digit == "4")
                    {
                        File.AppendAllText(Folder + "/countdown-camera.txt", $"virtualFocal={menu.CountdownFocalLength}\n");
                        foreach (var component in _camera.GetComponents<MonoBehaviour>())
                            File.AppendAllText(Folder + "/countdown-camera.txt", $"{component.GetType().Name}: enabled={component.enabled}\n");
                    }
                    foreach (var subject in new[] { duel.PlayerTransform, duel.BotTransform })
                    {
                        Vector3 screen = _camera.WorldToViewportPoint(subject.position);
                        Assert.That(screen.x, Is.InRange(.03f, .97f), digit + ": fighter left countdown frame.");
                        Assert.That(screen.y, Is.InRange(.03f, .97f), digit + ": fighter left countdown frame.");
                    }
                    yield return Capture($"Countdown-{digit}.png");
                    if (digit == "3")
                    {
                        Vector3 centerViewport = _camera.WorldToViewportPoint(menu.SubjectCenter);
                        Assert.That(centerViewport.x, Is.EqualTo(.5f).Within(.025f));
                        Assert.That(centerViewport.y, Is.EqualTo(.5f).Within(.025f));
                    }
                }
                Assert.That(view.StartVeilAlpha, Is.Zero, "The camera departure must stay visible.");
                frame++;
                yield return null;
            }
            Assert.That(Time.unscaledTime - started, Is.InRange(flow.CountdownDurationSeconds + view.StartIntroSeconds - .05f, flow.CountdownDurationSeconds + view.StartIntroSeconds + 1f));
            CollectionAssert.AreEquivalent(new[] { "4", "3", "2", "1" }, digits);
            Assert.That(flow.State, Is.EqualTo(FrontendState.Combat)); Assert.That(duel.CombatAllowed, Is.True);
            Assert.That(driver.PresentationClockMultiplier, Is.EqualTo(1)); Assert.That(menu.OwnsPresentation, Is.False);
            yield return new WaitForSecondsRealtime(.7f);
            var hudRoot = Find<EarthDuelHud>().GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement;
            var timer = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Label>(hudRoot, className: "duel-time");
            Assert.That(timer.resolvedStyle.fontSize, Is.GreaterThan(35f), "Menu startup must not replace the enlarged HUD timer with Toolkit's unresolved 14 px default.");
            var theme = UnityEditor.AssetDatabase.LoadAssetAtPath<ElementalUITheme>("Assets/Elemental/Content/UI/Frontend/ElementalUITheme.asset");
            Assert.That(theme.hudFont.name, Does.Contain("Varose"));
            var duelRoot = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.VisualElement>(hudRoot, className: "duel-hud");
            UnityEngine.UIElements.UQueryExtensions.Query<UnityEngine.UIElements.TextElement>(duelRoot).ForEach(label =>
                Assert.That(label.style.unityFont.value, Is.SameAs(theme.hudFont), label.name));
            yield return Capture("Combat.png");
            var pauseButton = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(hudRoot, "pause-match");
            Assert.That(pauseButton, Is.Not.Null);
            Assert.That(pauseButton.resolvedStyle.backgroundColor.a, Is.InRange(.2f, .5f));
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            _pauseTestMouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            Vector2 pausePosition = pauseButton.worldBound.center;
            Vector2 screenPoint = new Vector2(pausePosition.x, Screen.height - pausePosition.y);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_pauseTestMouse,
                new UnityEngine.InputSystem.LowLevel.MouseState { position = screenPoint });
            yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_pauseTestMouse,
                new UnityEngine.InputSystem.LowLevel.MouseState { position = screenPoint }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
            yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_pauseTestMouse,
                new UnityEngine.InputSystem.LowLevel.MouseState { position = screenPoint });
            yield return null;
            Assert.That(flow.State, Is.EqualTo(FrontendState.Paused)); Assert.That(Time.timeScale, Is.EqualTo(0));
            float health = duel.PlayerHealth, remaining = duel.RoundRemainingSeconds;
            Vector3 pausedPosition = duel.PlayerTransform.position;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(duel.RoundRemainingSeconds, Is.EqualTo(remaining));
            Assert.That(duel.PlayerTransform.position, Is.EqualTo(pausedPosition));
            foreach (Vector2Int size in new[] { new Vector2Int(1920,1080), new Vector2Int(1920,1200), new Vector2Int(2520,1080) })
            { BindCapture(size); yield return null; yield return Capture($"Pause-{size.x}x{size.y}.png"); }
            flow.OpenSettings(); Assert.That(flow.State, Is.EqualTo(FrontendState.Settings));
            flow.Back(); Assert.That(flow.State, Is.EqualTo(FrontendState.Paused));
            flow.Resume(); Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(duel.PlayerHealth, Is.EqualTo(health)); Assert.That(duel.RoundRemainingSeconds, Is.EqualTo(remaining));
            Assert.That(flow.State, Is.EqualTo(FrontendState.Combat));
            flow.Back(); flow.EndMatch(); yield return new WaitForSecondsRealtime(1);
            Assert.That(flow.State, Is.EqualTo(FrontendState.Main)); Assert.That(duel.CombatAllowed, Is.False);
            Assert.That(flow.BeginBot(), Is.True);
            yield return new WaitForSecondsRealtime(flow.CountdownDurationSeconds + view.StartIntroSeconds + .3f);
            Assert.That(flow.State, Is.EqualTo(FrontendState.Combat)); Assert.That(driver.PresentationClockMultiplier, Is.EqualTo(1));
            File.WriteAllText(Folder + "/metrics.txt", $"Main Thread recorder valid={cpu.Valid}; last ns={cpu.LastValue}\nGC recorder valid={gc.Valid}; last bytes={gc.LastValue}\nTimeScale={Time.timeScale}\nTransitionFrames={frame}\n");
        }
        [System.Serializable]
        private sealed class DepartureSample
        {
            public float time, progress, fov;
            public Vector3 position;
            public Quaternion rotation;
        }
        [System.Serializable]
        private sealed class DepartureTrace { public DepartureSample[] samples; public long peakCpuNanoseconds; }
        [UnityTest]
        public IEnumerator BotDepartureKeepsRenderedContinuityAndWaitsForWorldAtBothAspects()
        {
            var flow = Find<FrontendFlowController>(); var duel = Find<EarthMvpDuelController>();
            var menu = Find<CinematicMenuCamera>(); var view = Find<FrontendMenuView>(); var gate = Find<EarthSceneReadinessGate>();
            _camera = Find<Unity.Cinemachine.CinemachineBrain>().GetComponent<UnityEngine.Camera>();
            bool oldReduced = flow.Preferences.ReducedMotion, gateEnabled = gate.enabled;
            var ready = typeof(EarthSceneReadinessGate).GetProperty("IsReady");
            int restarts = 0; System.Action countRestart = () => restarts++; duel.RoundRestarted += countRestart;
            try
            {
                foreach (bool reduced in new[] { false, true })
                foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1920, 1200) })
                {
                    flow.Preferences.Set(flow.Preferences.MasterVolume, flow.Preferences.UIVolume, flow.Preferences.Sensitivity, reduced);
                    flow.ShowMain(); double readyDeadline = Time.realtimeSinceStartupAsDouble + 30;
                    while (!flow.IsWorldReady && Time.realtimeSinceStartupAsDouble < readyDeadline) yield return null;
                    Assert.That(flow.IsWorldReady, Is.True);
                    BindCapture(size); menu.Reframe(reduced); yield return new WaitForSecondsRealtime(.7f);
                    yield return new WaitForEndOfFrame();
                    Vector3 clickPosition = _camera.transform.position; Quaternion clickRotation = _camera.transform.rotation;
                    float clickFov = _camera.fieldOfView, timer = duel.RoundRemainingSeconds;
                    string prefix = $"Departure-{size.x}x{size.y}-{(reduced ? "reduced" : "normal")}";
                    SaveRenderedCapture(prefix + "-00-main.png");
                    var play = view.transform.Find("Menu contents/Menu column/Main/PLAY VS BOT").gameObject;
                    int beforeRestarts = restarts;
                    ExecuteEvents.Execute(play, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                    Assert.That(flow.State, Is.EqualTo(FrontendState.Starting)); Assert.That(restarts, Is.EqualTo(beforeRestarts + 1));
                    Assert.That(flow.BeginBot(), Is.False); Assert.That(restarts, Is.EqualTo(beforeRestarts + 1));
                    // Hold only the readiness dependency; disabling it also prevents its initial-loading overlay.
                    gate.enabled = false; ready.SetValue(gate, false);
                    float holdUntil = Time.unscaledTime + .3f;
                    while (Time.unscaledTime < holdUntil)
                    {
                        yield return new WaitForEndOfFrame();
                        Assert.That(Vector3.Distance(_camera.transform.position, clickPosition), Is.LessThan(.003f));
                        Assert.That(Quaternion.Angle(_camera.transform.rotation, clickRotation), Is.LessThan(.03f));
                        Assert.That(_camera.fieldOfView, Is.EqualTo(clickFov).Within(.01f));
                        Assert.That(view.CountdownText, Is.Empty); Assert.That(view.StartVeilAlpha, Is.Zero);
                        Assert.That(menu.DepartureProgress, Is.Zero); Assert.That(duel.CombatAllowed, Is.False);
                    }
                    SaveRenderedCapture(prefix + "-01-preparing.png");
                    ready.SetValue(gate, true); gate.enabled = gateEnabled;
                    var samples = new System.Collections.Generic.List<DepartureSample>();
                    samples.Add(new DepartureSample { time = Time.unscaledTime, position = _camera.transform.position, rotation = _camera.transform.rotation, fov = _camera.fieldOfView });
                    bool middle = false; long peak = 0;
                    using var cpu = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Elemental.MenuCamera.Departure", 64);
                    double deadline = Time.realtimeSinceStartupAsDouble + 5;
                    while (!menu.DepartureComplete && Time.realtimeSinceStartupAsDouble < deadline)
                    {
                        yield return new WaitForEndOfFrame();
                        samples.Add(new DepartureSample { time = Time.unscaledTime, progress = menu.DepartureProgress, position = _camera.transform.position, rotation = _camera.transform.rotation, fov = _camera.fieldOfView });
                        peak = System.Math.Max(peak, cpu.LastValue);
                        Assert.That(view.StartVeilAlpha, Is.Zero); Assert.That(duel.CombatAllowed, Is.False);
                        Assert.That(duel.RoundRemainingSeconds, Is.EqualTo(timer));
                        if (!menu.DepartureComplete) Assert.That(view.CountdownText, Is.Empty);
                        if (!middle && menu.DepartureProgress >= .45f) { middle = true; SaveRenderedCapture(prefix + "-02-moving.png"); }
                    }
                    Assert.That(menu.DepartureComplete, Is.True);
                    SaveRenderedCapture(prefix + "-03-countdown.png");
                    File.WriteAllText(Folder + "/" + prefix + ".json", JsonUtility.ToJson(new DepartureTrace { samples = samples.ToArray(), peakCpuNanoseconds = peak }, true));
                    Assert.That(samples.Count, Is.GreaterThan(reduced ? 1 : 4));
                    var first = samples[0]; var last = samples[samples.Count - 1];
                    float travel = Vector3.Distance(first.position, last.position), turn = Quaternion.Angle(first.rotation, last.rotation);
                    Assert.That(travel, Is.GreaterThan(.1f), "The visible departure must move the camera.");
                    for (int index = 1; index < samples.Count; index++)
                    {
                        var previous = samples[index - 1]; var current = samples[index];
                        float fraction = (current.time - previous.time) / CinematicMenuCamera.DepartureSeconds(reduced);
                        Assert.That(Vector3.Distance(previous.position, current.position), Is.LessThanOrEqualTo(travel * 2f * fraction + .03f), "Rendered camera position jumped.");
                        Assert.That(Quaternion.Angle(previous.rotation, current.rotation), Is.LessThanOrEqualTo(turn * 2f * fraction + .4f), "Rendered camera orientation jumped.");
                        Assert.That(Mathf.Abs(previous.fov - current.fov), Is.LessThanOrEqualTo(Mathf.Abs(last.fov - first.fov) * 2f * fraction + .15f), "Rendered camera lens jumped.");
                    }
                    // Cancel from the completed departure while still in countdown; the same lease must reopen cleanly.
                    flow.Back(); yield return new WaitForSecondsRealtime(.3f);
                    Assert.That(flow.State, Is.EqualTo(FrontendState.Main)); Assert.That(menu.OwnsPresentation, Is.True);
                }
            }
            finally
            {
                ready.SetValue(gate, true); gate.enabled = gateEnabled; duel.RoundRestarted -= countRestart;
                flow.Preferences.Set(flow.Preferences.MasterVolume, flow.Preferences.UIVolume, flow.Preferences.Sensitivity, oldReduced);
            }
        }
        [UnityTest]
        public IEnumerator BotDepartureEscapeCancelsBeforeReadyAndDuringMotion()
        {
            var flow = Find<FrontendFlowController>(); var duel = Find<EarthMvpDuelController>();
            var menu = Find<CinematicMenuCamera>(); var view = Find<FrontendMenuView>(); var gate = Find<EarthSceneReadinessGate>();
            var brain = Find<Unity.Cinemachine.CinemachineBrain>(); _camera = brain.GetComponent<UnityEngine.Camera>();
            var virtualCamera = (Unity.Cinemachine.CinemachineCamera)typeof(CinematicMenuCamera)
                .GetField("menuCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(menu);
            var driver = duel.PlayerTransform.GetComponentInChildren<EarthAnimationDriver>();
            var ready = typeof(EarthSceneReadinessGate).GetProperty("IsReady");
            bool gateEnabled = gate.enabled, oldReduced = flow.Preferences.ReducedMotion;
            var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            int restarts = 0, networkCancels = 0;
            System.Action restart = () => restarts++, cancel = () => networkCancels++;
            duel.RoundRestarted += restart; flow.NetworkCancelRequested += cancel;
            try
            {
                flow.Preferences.Set(flow.Preferences.MasterVolume, flow.Preferences.UIVolume, flow.Preferences.Sensitivity, false);
                foreach (bool waitForReady in new[] { true, false })
                {
                    flow.ShowMain(); double deadline = Time.realtimeSinceStartupAsDouble + 30;
                    while (!flow.IsWorldReady && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    Assert.That(flow.IsWorldReady, Is.True); yield return new WaitForSecondsRealtime(.7f);
                    yield return new WaitForEndOfFrame();
                    float fov = _camera.fieldOfView, clock = driver.PresentationClockMultiplier;
                    var priority = virtualCamera.Priority; bool ignoreTime = brain.IgnoreTimeScale;
                    var update = brain.UpdateMethod; var blendUpdate = brain.BlendUpdateMethod;
                    var play = view.transform.Find("Menu contents/Menu column/Main/PLAY VS BOT").gameObject;
                    int before = restarts;
                    ExecuteEvents.Execute(play, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                    ExecuteEvents.Execute(play, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                    Assert.That(restarts, Is.EqualTo(before + 1)); Assert.That(flow.BeginBot(), Is.False);
                    if (waitForReady)
                    {
                        gate.enabled = false; ready.SetValue(gate, false);
                        yield return new WaitForSecondsRealtime(.2f);
                        Assert.That(menu.DepartureProgress, Is.Zero);
                    }
                    else
                    {
                        deadline = Time.realtimeSinceStartupAsDouble + 5;
                        while (menu.DepartureProgress < .25f && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                        Assert.That(menu.DepartureProgress, Is.InRange(.25f, .9f));
                    }
                    // Exercise the same raw Escape action that invokes FrontendFlowController.Back.
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,
                        new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Escape));
                    yield return null;
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                    yield return null;
                    Assert.That(flow.State, Is.EqualTo(FrontendState.Main)); Assert.That(networkCancels, Is.Zero);
                    Assert.That(duel.CombatAllowed, Is.False); Assert.That(menu.OwnsPresentation, Is.True);
                    Assert.That(view.CountdownText, Is.Empty); Assert.That(view.StartVeilAlpha, Is.Zero);
                    Assert.That(menu.DepartureComplete, Is.False);
                    ready.SetValue(gate, true); gate.enabled = gateEnabled;
                    yield return new WaitForSecondsRealtime(.5f); yield return new WaitForEndOfFrame();
                    Assert.That(Time.timeScale, Is.EqualTo(1f));
                    Assert.That((int)virtualCamera.Priority, Is.EqualTo((int)priority));
                    Assert.That(driver.PresentationClockMultiplier, Is.EqualTo(clock));
                    Assert.That(brain.IgnoreTimeScale, Is.EqualTo(ignoreTime)); Assert.That(brain.UpdateMethod, Is.EqualTo(update));
                    Assert.That(brain.BlendUpdateMethod, Is.EqualTo(blendUpdate));
                    Assert.That(_camera.fieldOfView, Is.EqualTo(fov).Within(.01f));
                    Assert.That(Cursor.visible, Is.True); Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
                    // Main deliberately enters pointer mode. Keyboard navigation
                    // acquires the first button on its next navigation action.
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
                    SaveRenderedCapture(waitForReady ? "Departure-cancel-readiness.png" : "Departure-cancel-motion.png");
                }
            }
            finally
            {
                ready.SetValue(gate, true); gate.enabled = gateEnabled;
                UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
                duel.RoundRestarted -= restart; flow.NetworkCancelRequested -= cancel;
                flow.Preferences.Set(flow.Preferences.MasterVolume, flow.Preferences.UIVolume, flow.Preferences.Sensitivity, oldReduced);
            }
        }
        private void SaveRenderedCapture(string name)
        {
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(Folder + "/" + name, texture.EncodeToPNG()); }
            finally { Object.Destroy(texture); }
        }
        private void BindCapture(Vector2Int size)
        {
#if UNITY_EDITOR
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance").GetValue(null);
            var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { System.Enum.Parse(groupType, "Standalone") });
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var kindType = assembly.GetType("UnityEditor.GameViewSizeType");
            var constructor = sizeType.GetConstructor(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null,
                new[] {kindType, typeof(int), typeof(int), typeof(string)}, null);
            object item = constructor.Invoke(new[] {System.Enum.ToObject(kindType, 1), (object)size.x, size.y, "Alpha QA " + size.x + "x" + size.y});
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] {item});
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
            var viewType = assembly.GetType("UnityEditor.GameView");
            var window = UnityEditor.EditorWindow.GetWindow(viewType);
            var selected = viewType.GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (_oldGameSize < 0) _oldGameSize = (int)selected.GetValue(window);
            selected.SetValue(window, index); window.Focus(); window.Repaint();
#else
            Screen.SetResolution(size.x, size.y, false);
#endif
        }
        private void RestoreGameView()
        {
#if UNITY_EDITOR
            if (_oldGameSize < 0) return;
            var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            var window = UnityEditor.EditorWindow.GetWindow(type);
            type.GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(window, _oldGameSize);
#endif
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(Folder + "/" + name, texture.EncodeToPNG()); }
            finally { Object.Destroy(texture); }
        }
        private T Find<T>() where T : Component
        { foreach (var root in _scene.GetRootGameObjects()) { var value = root.GetComponentInChildren<T>(true); if (value != null) return value; } return null; }
    }
}
