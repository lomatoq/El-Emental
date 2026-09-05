using System;
using System.Collections;
using System.IO;
using Elemental.Runtime.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Runtime.Bootstrap
{
    /// <summary>
    /// Keeps a lightweight scene rendered while the production scene loads and
    /// crosses its exact geometry/physics readiness boundary.
    /// </summary>
    [DefaultExecutionOrder(-32600), DisallowMultipleComponent]
    public sealed class EarthBootstrapSceneLoader : MonoBehaviour
    {
        private const string BootstrapSceneName = "Bootstrap";
        private const string DefaultPlayableSceneName = "EarthCoreSlice";

        [SerializeField] private string playableSceneName = DefaultPlayableSceneName;
        [SerializeField] private Color coverColor = new Color(.055f, .065f, .085f, 1f);
        [SerializeField, Min(5f)] private float loadTimeoutSeconds = 180f;

        private string _status = "Loading…";
        private bool _finished;
        private bool _failed;
        private string _evidenceFolder;
        private double _bootstrapAwakeMilliseconds;
        private double _coverPresentedMilliseconds;
        private double _targetActivatedMilliseconds;
        private double _readyMilliseconds;
        private double _maximumFrameMilliseconds;

        public string PlayableSceneName => playableSceneName;
        public string Status => _status;
        public bool Failed => _failed;

        public void Configure(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                throw new System.ArgumentException("A playable scene name is required.", nameof(sceneName));
            playableSceneName = sceneName;
        }

        private void Awake()
        {
            _bootstrapAwakeMilliseconds = UptimeMilliseconds();
            _evidenceFolder = ReadArgumentValue("-startupBootstrapEvidenceFolder");
            if (!string.IsNullOrEmpty(_evidenceFolder))
            {
                _evidenceFolder = Path.GetFullPath(_evidenceFolder);
                Directory.CreateDirectory(_evidenceFolder);
            }
            if (gameObject.scene.name == BootstrapSceneName)
                SetBootstrapSimulationEnabled(false);
        }

        private void Update()
        {
            if (!_finished)
                _maximumFrameMilliseconds = Math.Max(
                    _maximumFrameMilliseconds, Time.unscaledDeltaTime * 1000.0);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrapLoader()
        {
            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid() || active.name != BootstrapSceneName) return;

            foreach (GameObject root in active.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<EarthBootstrapSceneLoader>(true) != null)
                    return;
            }

            GameObject host = new GameObject("Production Scene Loader");
            SceneManager.MoveGameObjectToScene(host, active);
            host.AddComponent<EarthBootstrapSceneLoader>();
        }

        private IEnumerator Start()
        {
            if (gameObject.scene.name != BootstrapSceneName)
            {
                enabled = false;
                yield break;
            }

            // Guarantee that the lightweight cover reaches the display before
            // scene loading or activation can occupy the main thread.
            yield return new WaitForEndOfFrame();
            _coverPresentedMilliseconds = UptimeMilliseconds();
            Debug.Log($"[StartupBootstrap] Cover presented at app uptime {_coverPresentedMilliseconds:F2}ms.", this);
            Capture("BootstrapCover.png");

            Scene existing = SceneManager.GetSceneByName(playableSceneName);
            if (existing.IsValid() && existing.isLoaded)
            {
                yield return WaitForReadiness(existing);
                yield break;
            }

            _status = "Loading world…";
            AsyncOperation load = SceneManager.LoadSceneAsync(playableSceneName, LoadSceneMode.Additive);
            if (load == null)
            {
                Fail($"Unity could not begin loading scene '{playableSceneName}'.");
                yield break;
            }

            load.allowSceneActivation = false;
            double loadStarted = Time.realtimeSinceStartupAsDouble;
            while (load.progress < .9f)
            {
                if (Time.realtimeSinceStartupAsDouble - loadStarted > loadTimeoutSeconds)
                {
                    load.allowSceneActivation = true;
                    Fail($"Scene '{playableSceneName}' loading exceeded {loadTimeoutSeconds:F1}s.");
                    yield break;
                }
                yield return null;
            }

            // Keep one fully rendered cover frame immediately before activation.
            yield return new WaitForEndOfFrame();
            _status = "Preparing world…";
            load.allowSceneActivation = true;
            while (!load.isDone)
            {
                if (Time.realtimeSinceStartupAsDouble - loadStarted > loadTimeoutSeconds)
                {
                    Fail($"Scene '{playableSceneName}' activation exceeded {loadTimeoutSeconds:F1}s.");
                    yield break;
                }
                yield return null;
            }
            _targetActivatedMilliseconds = UptimeMilliseconds();
            Debug.Log($"[StartupBootstrap] Target activated at app uptime {_targetActivatedMilliseconds:F2}ms.", this);

            Scene playable = SceneManager.GetSceneByName(playableSceneName);
            if (!playable.IsValid() || !playable.isLoaded)
            {
                Fail($"Playable scene '{playableSceneName}' did not finish loading.");
                yield break;
            }

            yield return WaitForReadiness(playable);
        }

        private IEnumerator WaitForReadiness(Scene playable)
        {
            EarthSceneReadinessGate gate = FindReadinessGate(playable);
            if (gate == null)
            {
                Fail($"Scene '{playable.name}' has no EarthSceneReadinessGate.");
                yield break;
            }

            while (!gate.IsReady && !gate.Failed)
            {
                _status = gate.Status;
                yield return null;
            }

            if (gate.Failed)
            {
                Fail($"Scene '{playable.name}' readiness failed: {gate.Status}.");
                yield break;
            }

            if (!SceneManager.SetActiveScene(playable))
            {
                Fail($"Unity could not activate scene '{playable.name}'.");
                yield break;
            }

            _readyMilliseconds = UptimeMilliseconds();
            Debug.Log($"[StartupBootstrap] Target ready at app uptime {_readyMilliseconds:F2}ms.", this);

            // The playable scene owns simulation from this point forward. Avoid
            // one overlapping FixedUpdate while bootstrap unload completes.
            SetBootstrapSimulationEnabled(false);
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                    camera.enabled = false;
                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
                    listener.enabled = false;
            }

            _finished = true;
            if (!string.IsNullOrEmpty(_evidenceFolder))
            {
                // With the cover and bootstrap camera disabled, this captures the
                // first accepted production view rather than another loading frame.
                yield return new WaitForEndOfFrame();
                Capture("PlayableReady.png");
                yield return null;
                WriteEvidence("Ready", string.Empty);
            }
            AsyncOperation unload = SceneManager.UnloadSceneAsync(gameObject.scene);
            if (unload == null)
                Debug.LogWarning("[Elemental] Bootstrap scene could not begin unloading after readiness.", this);
        }

        private void SetBootstrapSimulationEnabled(bool value)
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                foreach (WorldBootstrap bootstrapClock in root.GetComponentsInChildren<WorldBootstrap>(true))
                    bootstrapClock.enabled = value;
        }

        private static EarthSceneReadinessGate FindReadinessGate(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                EarthSceneReadinessGate gate = root.GetComponentInChildren<EarthSceneReadinessGate>(true);
                if (gate != null) return gate;
            }
            return null;
        }

        private void Fail(string message)
        {
            _failed = true;
            _status = "Loading failed";
            Debug.LogError("[Elemental] " + message, this);
            Capture("BootstrapFailure.png");
            WriteEvidence("Failed", message);
        }

        private void Capture(string fileName)
        {
            if (!string.IsNullOrEmpty(_evidenceFolder))
                ScreenCapture.CaptureScreenshot(Path.Combine(_evidenceFolder, fileName));
        }

        private void WriteEvidence(string result, string error)
        {
            if (string.IsNullOrEmpty(_evidenceFolder)) return;
            var report = new StartupEvidence
            {
                status = result,
                error = error,
                bootstrapAwakeUptimeMilliseconds = _bootstrapAwakeMilliseconds,
                coverPresentedUptimeMilliseconds = _coverPresentedMilliseconds,
                targetActivatedUptimeMilliseconds = _targetActivatedMilliseconds,
                readyUptimeMilliseconds = _readyMilliseconds,
                maximumObservedFrameMilliseconds = _maximumFrameMilliseconds,
                playableScene = playableSceneName,
                unityVersion = Application.unityVersion
            };
            File.WriteAllText(Path.Combine(_evidenceFolder, "BootstrapStartup.json"),
                JsonUtility.ToJson(report, true));
        }

        private static string ReadArgumentValue(string key)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < arguments.Length; index++)
                if (string.Equals(arguments[index], key, StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            return string.Empty;
        }

        private static double UptimeMilliseconds() => Time.realtimeSinceStartupAsDouble * 1000.0;

        [Serializable]
        private sealed class StartupEvidence
        {
            public string status;
            public string error;
            public string playableScene;
            public string unityVersion;
            public double bootstrapAwakeUptimeMilliseconds;
            public double coverPresentedUptimeMilliseconds;
            public double targetActivatedUptimeMilliseconds;
            public double readyUptimeMilliseconds;
            public double maximumObservedFrameMilliseconds;
        }

        private void OnGUI()
        {
            if (_finished) return;
            int depth = GUI.depth;
            Color color = GUI.color;
            GUI.depth = -32767;
            GUI.color = coverColor;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            string text = _failed ? "Loading failed" : _status;
            GUI.Label(new Rect(Screen.width * .5f - 100f, Screen.height * .5f - 12f, 240f, 40f), text);
            GUI.color = color;
            GUI.depth = depth;
        }
    }
}
