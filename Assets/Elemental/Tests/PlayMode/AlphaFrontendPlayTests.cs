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
            ExecuteEvents.Execute(play, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Assert.That(flow.State, Is.EqualTo(FrontendState.Starting), "Keyboard submit must start the same frontend transition.");
            Assert.That(flow.BeginBot(), Is.False);
            if (!flow.Preferences.ReducedMotion) Assert.That(menu.CountdownFocalLength, Is.EqualTo(150f).Within(.01f));
            float started = Time.unscaledTime;
            using var cpu = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 64);
            using var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 64);
            int frame = 0;
            var digits = new System.Collections.Generic.HashSet<string>();
            float roundBeforeCountdown = duel.RoundRemainingSeconds;
            float previousDollyDistance = float.PositiveInfinity;
            while (flow.State == FrontendState.Starting)
            {
                Assert.That(duel.CombatAllowed, Is.False);
                Assert.That(duel.RoundRemainingSeconds, Is.EqualTo(roundBeforeCountdown));
                if (digits.Add(view.CountdownText))
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
                    if (digit == "4" && !flow.Preferences.ReducedMotion) Assert.That(_camera.focalLength, Is.GreaterThan(130f));
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
                if (Time.unscaledTime - started > .15f && flow.State == FrontendState.Starting)
                {
                    float distance = Vector3.Distance(_camera.transform.position, menu.SubjectCenter);
                    Assert.That(distance, Is.LessThanOrEqualTo(previousDollyDistance + .10f), "Countdown camera moved away from the fighters.");
                    previousDollyDistance = distance;
                }
                frame++;
                yield return null;
            }
            Assert.That(Time.unscaledTime - started, Is.InRange(3.95f, 4.5f));
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
            yield return new WaitForSecondsRealtime(4.2f);
            Assert.That(flow.State, Is.EqualTo(FrontendState.Combat)); Assert.That(driver.PresentationClockMultiplier, Is.EqualTo(1));
            File.WriteAllText(Folder + "/metrics.txt", $"Main Thread recorder valid={cpu.Valid}; last ns={cpu.LastValue}\nGC recorder valid={gc.Valid}; last bytes={gc.LastValue}\nTimeScale={Time.timeScale}\nTransitionFrames={frame}\n");
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
