using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Elemental.Presentation.UI;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using UnityEngine;
using Unity.Profiling;
using System.Text;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthDuelHudPlayTests
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private Scene _scene, _previous;
        private bool _opened;
        private float _oldTimeScale;
        private PanelSettings _panel;
        private RenderTexture _oldTarget, _target;
        private bool _oldClear;
        private Color _oldClearColor;

        [UnitySetUp]
        public IEnumerator Load()
        {
            _previous = SceneManager.GetActiveScene();
            _oldTimeScale = Time.timeScale;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(!_scene.IsValid() || !_scene.isLoaded, Is.True,
                "Run this destructive gameplay acceptance with EarthCoreSlice closed; it owns an additive copy and unloads it.");
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByPath(ScenePath);
            _opened = true;
            SceneManager.SetActiveScene(_scene);
            EarthSceneReadinessGate gate = All<EarthSceneReadinessGate>().Single();
            double deadline = Time.realtimeSinceStartupAsDouble + 130d;
            while (!gate.IsReady && !gate.Failed && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            Assert.That(gate.IsReady, Is.True, gate.Status);
            yield return ProductionCombatTestFlow.BeginBotAfterReadiness(_scene);
            yield return new WaitForSecondsRealtime(.7f);
            foreach (EarthMvpBotController bot in All<EarthMvpBotController>()) bot.enabled = false;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            if (_panel != null)
            {
                _panel.targetTexture = _oldTarget;
                _panel.clearColor = _oldClear;
                _panel.colorClearValue = _oldClearColor;
            }
            if (_target != null) { _target.Release(); Object.Destroy(_target); }
            if (_previous.IsValid() && _previous.isLoaded) SceneManager.SetActiveScene(_previous);
            if (_opened && _scene.IsValid() && _scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_scene);
            Time.timeScale = _oldTimeScale;
        }

        [UnityTest]
        public IEnumerator SavedHudRendersAndTracksHealthManaScoreAndRoundRestart()
        {
            EarthDuelHud hud = All<EarthDuelHud>().Single();
            EarthMvpDuelController duel = All<EarthMvpDuelController>().Single();
            EarthDualMouseAbilityController shots = All<EarthDualMouseAbilityController>().Single();
            UIDocument document = hud.GetComponent<UIDocument>();
            VisualElement root = document.rootVisualElement;
            Assert.That(duel.CombatAllowed, Is.True);
            Assert.That(duel.PlayerHealth, Is.EqualTo(100f));
            Assert.That(root.Q("duel-scoreboard"), Is.Not.Null);
            Assert.That(root.Q("planet-globe"), Is.Not.Null);
            Assert.That(hud.Globe.HasNavigation, Is.True);
            Assert.That(root.Q("duel-hud").resolvedStyle.opacity, Is.GreaterThan(.99f));
            float timerBefore = duel.RoundRemainingSeconds;
            yield return new WaitForSeconds(.08f);
            Assert.That(duel.RoundRemainingSeconds, Is.LessThan(timerBefore));

            bool shotCommitted = false;
            float manaAtCommit = 100f;
            void OnShot(float cost) { shotCommitted = true; manaAtCommit = hud.DisplayMana; }
            shots.StoneShotCommitted += OnShot;
            try
            {
                Assert.That(shots.CastStompStone(), Is.True, "The saved physical shot route must accept the cast.");
                double shotDeadline = Time.realtimeSinceStartupAsDouble + 5d;
                while (!shotCommitted && Time.realtimeSinceStartupAsDouble < shotDeadline) yield return null;
                Assert.That(shotCommitted, Is.True, "No contact-time shot event was committed.");
                Assert.That(manaAtCommit, Is.LessThan(100f), "Accepted shot did not spend display mana.");
                Assert.That(manaAtCommit, Is.GreaterThanOrEqualTo(15f));
            }
            finally { shots.StoneShotCommitted -= OnShot; }

            duel.ApplyDamage(EarthDuelFighterId.Player, 25f, RagdollHandoff.Uniform(Vector3.zero));
            yield return null;
            Assert.That(root.Q("health-panel").Q<Label>(className: "duel-vital-value").text, Is.EqualTo("75"));
            Assert.That(duel.BotScore, Is.Zero);

            // Render the production panel itself at an explicit resolution, independent of batch Game view size.
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                _panel = document.panelSettings;
                _oldTarget = _panel.targetTexture;
                _oldClear = _panel.clearColor;
                _oldClearColor = _panel.colorClearValue;
                _panel.clearColor = true;
                _panel.colorClearValue = new Color(.022f, .035f, .055f, 1f);
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../BuildReports/HudOriginalRestore"));
                Directory.CreateDirectory(folder);
                var evidence = new StringBuilder("Production UIDocument captures; health75; actual stone shot event observed.\n");
                var sizes = new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720),
                    new Vector2Int(2560, 1080), new Vector2Int(3840, 2160) };
                foreach (Vector2Int size in sizes)
                {
                    RenderTexture old = _target;
                    _target = new RenderTexture(size.x, size.y, 0, RenderTextureFormat.ARGB32);
                    _target.Create();
                    _panel.targetTexture = _target;
                    if (old != null) { old.Release(); Object.Destroy(old); }
                    for (int frame = 0; frame < 6; frame++) yield return null;
                    float width = root.worldBound.width, height = root.worldBound.height;
                    // The user requested the original slim HUD, with only about 12% larger text.
                    Assert.That(root.Q("health-panel").Q<Label>(className: "duel-vital-value").resolvedStyle.fontSize,
                        Is.InRange(13.2f, 13.6f), "Keep the original vital text scale with a small increase.");
                    Assert.That(root.Q("mana-panel").Q<Label>(className: "duel-vital-value").resolvedStyle.fontSize,
                        Is.InRange(13.2f, 13.6f), "Keep the original vital text scale with a small increase.");
                    Assert.That(root.Q("round-clock").Q<Label>(className: "duel-time").resolvedStyle.fontSize,
                        Is.InRange(37.9f, 38.3f), "Timer text must be only slightly larger than original 34px.");
                    Assert.That(root.Q("health-readout"), Is.Null, "The original HUD has no boxed vital readout.");
                    Assert.That(root.Q("health-panel").Q<Label>(className: "duel-symbol").text, Is.EqualTo("+"));
                    Assert.That(root.Q("mana-panel").Q<Label>(className: "duel-symbol").text, Is.EqualTo("✦"));
                    Assert.That(root.Q("health-gauge").resolvedStyle.width, Is.EqualTo(58f).Within(.1f));
                    Assert.That(root.Q("planet-panel").resolvedStyle.backgroundColor.a, Is.Zero);
                    foreach (string name in new[] { "duel-scoreboard", "health-panel", "mana-panel", "planet-globe" })
                    {
                        Rect bounds = root.Q(name).worldBound;
                        Assert.That(bounds.width, Is.GreaterThan(1f), name);
                        Assert.That(bounds.height, Is.GreaterThan(1f), name);
                        Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(-1f), name);
                        Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(-1f), name);
                        Assert.That(bounds.xMax, Is.LessThanOrEqualTo(width + 1f), name);
                        Assert.That(bounds.yMax, Is.LessThanOrEqualTo(height + 1f), name);
                        evidence.AppendLine($"{size.x}x{size.y} {name}: {bounds}");
                    }
                    Assert.That(root.Q("mana-panel").worldBound.yMax,
                        Is.LessThan(root.Q("planet-panel").worldBound.yMin), "Mana and navigation must not overlap.");
                    string filename = size.x == 1920 ? "hud.png" : $"hud-{size.x}x{size.y}.png";
                    SaveTarget(_target, Path.Combine(folder, filename));
                }
                using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Elemental.DuelHud.Tick", 120))
                {
                    Assert.That(recorder.Valid, Is.True, "HUD profiler marker missing.");
                    var samples = new long[60];
                    for (int index = 0; index < samples.Length; index++)
                    {
                        yield return null;
                        samples[index] = recorder.LastValue;
                    }
                    System.Array.Sort(samples);
                    double total = 0;
                    foreach (long sample in samples) total += sample;
                    Assert.That(samples[59], Is.GreaterThan(0), "HUD marker did not record any measured work.");
                    evidence.AppendLine($"Elemental.DuelHud.Tick 60frames CPU ms mean={total / 60 / 1000000:F5} p95={samples[56] / 1000000d:F5} max={samples[59] / 1000000d:F5}");
                }
                // Composite the same production HUD over the actual gameplay camera,
                // retaining its scene lighting and post processing for visual review.
                var gameplayCamera = (Camera)typeof(EarthDualMouseAbilityController)
                    .GetField("castCamera", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shots);
                Assert.That(gameplayCamera, Is.Not.Null);
                RenderTexture previousUiTarget = _target;
                _target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                _target.Create();
                RenderTexture previousCameraTarget = gameplayCamera.targetTexture;
                float previousAspect = gameplayCamera.aspect;
                try
                {
                    gameplayCamera.targetTexture = _target;
                    gameplayCamera.aspect = 1920f / 1080f;
                    gameplayCamera.Render();
                }
                finally
                {
                    gameplayCamera.targetTexture = previousCameraTarget;
                    gameplayCamera.aspect = previousAspect;
                }
                _panel.clearColor = false;
                _panel.targetTexture = _target;
                previousUiTarget.Release();
                Object.Destroy(previousUiTarget);
                yield return null;
                yield return null;
                SaveTarget(_target, Path.Combine(folder, "gameplay-hud.png"));
                evidence.AppendLine("gameplay-hud.png: actual gameplay camera + production HUD at 1920x1080; camera target/aspect restored.");
                File.WriteAllText(Path.Combine(folder, "evidence.txt"), evidence.ToString());
            }
            else Debug.Log("[Duel HUD] Graphics-null run verifies bindings only; visual PNG requires a graphics-enabled run.");

            duel.ApplyDamage(EarthDuelFighterId.Player, 100f, RagdollHandoff.Uniform(Vector3.zero));
            duel.RequestKnockout(EarthDuelFighterId.Player, RagdollHandoff.Uniform(Vector3.zero));
            yield return null;
            Assert.That(duel.BotScore, Is.EqualTo(1));
            Assert.That(root.Q("blue-team").Q<Label>(className: "duel-score").text, Is.EqualTo("1"));

            // Fast-forward only the pure match clock, leaving the runtime to execute the actual end transition.
            var match = (EarthDuelMatchState)typeof(EarthMvpDuelController)
                .GetField("_match", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(duel);
            match.Step(Mathf.Max(0f, match.RemainingSeconds - .03f));
            yield return new WaitForSeconds(.08f);
            Assert.That(duel.IsRoundOver, Is.True);
            Assert.That(duel.CombatAllowed, Is.False);
            Assert.That(root.Q("round-result").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(root.Q<Button>("restart-round"), Is.Not.Null);
            duel.ApplyDamage(EarthDuelFighterId.Bot, 100f, RagdollHandoff.Uniform(Vector3.zero));
            Assert.That(duel.PlayerScore, Is.Zero);
            duel.RestartRound();
            foreach (EarthMvpBotController bot in All<EarthMvpBotController>()) bot.enabled = false;
            yield return null;
            Assert.That(duel.CombatAllowed, Is.True);
            Assert.That(duel.PlayerHealth, Is.EqualTo(100f));
            Assert.That(duel.PlayerScore + duel.BotScore, Is.Zero);
            Assert.That(hud.DisplayMana, Is.EqualTo(100f));
            Assert.That(root.Q("round-result").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(duel.RoundRemainingSeconds, Is.GreaterThan(299f));
        }

        private T[] All<T>() where T : Component => _scene.GetRootGameObjects()
            .SelectMany(value => value.GetComponentsInChildren<T>(true)).ToArray();

        private static void SaveTarget(RenderTexture target, string path)
        {
            RenderTexture previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.Destroy(image); }
        }
    }
}
