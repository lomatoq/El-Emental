using System;
using System.Collections;
using System.IO;
using Elemental.Runtime.World;
using UnityEngine;
using UnityEngine.Rendering;
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
        [SerializeField] private Color coverColor = new Color(.20f, .24f, .30f, 1f);
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
        private CameraCaptureEvidence _bootstrapCapture;
        private CameraCaptureEvidence _playableCapture;
        private Camera _coverCamera;
        private GameObject _coverRoot;
        private Mesh _coverMesh;
        private Material _coverMaterial;
        private TextMesh _coverText;
        private bool _cameraCoverReady;

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
            {
                SetBootstrapSimulationEnabled(false);
                CreateCameraCover();
            }
        }

        private void Update()
        {
            if (!_finished)
            {
                _maximumFrameMilliseconds = Math.Max(
                    _maximumFrameMilliseconds, Time.unscaledDeltaTime * 1000.0);
                UpdateCameraCoverText();
            }
        }

        private void OnDestroy()
        {
            if (_coverRoot != null) Destroy(_coverRoot);
            if (_coverMaterial != null) Destroy(_coverMaterial);
            if (_coverMesh != null) Destroy(_coverMesh);
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
            _bootstrapCapture = CaptureCamera("BootstrapCover.png", gameObject.scene);

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
                _playableCapture = CaptureCamera("PlayableReady.png", playable);
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
            UpdateCameraCoverText();
            Debug.LogError("[Elemental] " + message, this);
            CaptureCamera("BootstrapFailure.png", gameObject.scene);
            WriteEvidence("Failed", message);
        }

        private void CreateCameraCover()
        {
            _coverCamera = FindEnabledCamera(gameObject.scene);
            if (_coverCamera == null)
            {
                Debug.LogError("[Elemental] Bootstrap camera cover requires an enabled scene camera.", this);
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (shader == null || font == null)
            {
                Debug.LogError(
                    "[Elemental] Bootstrap camera cover requires the URP Unlit shader and LegacyRuntime font.",
                    this);
                return;
            }

            _coverRoot = new GameObject("__Runtime Bootstrap Camera Cover")
            {
                hideFlags = HideFlags.DontSave
            };
            _coverRoot.transform.SetParent(_coverCamera.transform, false);

            GameObject background = new GameObject("Background")
            {
                hideFlags = HideFlags.DontSave
            };
            background.transform.SetParent(_coverRoot.transform, false);
            var filter = background.AddComponent<MeshFilter>();
            var renderer = background.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            const float backgroundDistance = .80f;
            float halfHeight = _coverCamera.orthographic
                ? _coverCamera.orthographicSize
                : backgroundDistance * Mathf.Tan(_coverCamera.fieldOfView * .5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * Mathf.Max(.1f, _coverCamera.aspect);
            // Two percent overscan prevents a one-pixel border from projection or
            // render-target rounding without changing the visible composition.
            halfHeight *= 1.02f;
            halfWidth *= 1.02f;
            _coverMesh = new Mesh
            {
                name = "Runtime Bootstrap Cover Quad",
                hideFlags = HideFlags.DontSave,
                vertices = new[]
                {
                    new Vector3(-halfWidth, -halfHeight, backgroundDistance),
                    new Vector3(-halfWidth,  halfHeight, backgroundDistance),
                    new Vector3( halfWidth,  halfHeight, backgroundDistance),
                    new Vector3( halfWidth, -halfHeight, backgroundDistance)
                },
                normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back },
                uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            _coverMesh.RecalculateBounds();
            filter.sharedMesh = _coverMesh;

            _coverMaterial = new Material(shader)
            {
                name = "Runtime Bootstrap Cover Material",
                hideFlags = HideFlags.DontSave,
                renderQueue = 1000
            };
            if (_coverMaterial.HasProperty("_BaseColor"))
                _coverMaterial.SetColor("_BaseColor", coverColor);
            if (_coverMaterial.HasProperty("_Color"))
                _coverMaterial.SetColor("_Color", coverColor);
            renderer.sharedMaterial = _coverMaterial;

            GameObject textObject = new GameObject("Status")
            {
                hideFlags = HideFlags.DontSave
            };
            textObject.transform.SetParent(_coverRoot.transform, false);
            textObject.transform.localPosition = new Vector3(0f, 0f, .60f);
            _coverText = textObject.AddComponent<TextMesh>();
            _coverText.font = font;
            _coverText.fontSize = 64;
            _coverText.characterSize = .006f;
            _coverText.anchor = TextAnchor.MiddleCenter;
            _coverText.alignment = TextAlignment.Center;
            _coverText.color = new Color(1f, .94f, .82f, 1f);
            MeshRenderer textRenderer = textObject.GetComponent<MeshRenderer>();
            textRenderer.sharedMaterial = font.material;
            textRenderer.shadowCastingMode = ShadowCastingMode.Off;
            textRenderer.receiveShadows = false;
            UpdateCameraCoverText();
            _cameraCoverReady = true;
        }

        private void UpdateCameraCoverText()
        {
            if (_coverText == null) return;
            string text = _failed ? "Loading failed" : _status;
            if (!string.Equals(_coverText.text, text, StringComparison.Ordinal))
                _coverText.text = text;
        }

        private CameraCaptureEvidence CaptureCamera(string fileName, Scene scene)
        {
            var evidence = new CameraCaptureEvidence { file = fileName, scene = scene.name };
            if (string.IsNullOrEmpty(_evidenceFolder))
            {
                evidence.error = "evidence-not-requested";
                return evidence;
            }

            Camera camera = FindEnabledCamera(scene);
            if (camera == null)
            {
                evidence.error = "no-enabled-camera";
                return evidence;
            }

            evidence.camera = camera.name;
            int width = Mathf.Clamp(Screen.width, 64, 1280);
            int height = Mathf.Clamp(Screen.height, 64, 720);
            evidence.width = width;
            evidence.height = height;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture target = null;
            Texture2D pixels = null;
            try
            {
                // ScreenCapture reads the hidden Player swapchain as black on some
                // Windows/D3D11 drivers. Render the actual scene camera into an
                // offscreen target so evidence does not depend on window visibility.
                target = RenderTexture.GetTemporary(
                    width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                    throw new InvalidOperationException(
                        $"Active render pipeline does not support a standard request for camera '{camera.name}'.");
                // URP renders the complete base-camera stack synchronously into
                // this target, independent of the hidden window swapchain.
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                pixels.Apply(false, false);
                Color32[] colors = pixels.GetPixels32();
                double sum = 0.0;
                float minimum = 1f;
                float maximum = 0f;
                int visible = 0;
                for (int index = 0; index < colors.Length; index++)
                {
                    Color32 color = colors[index];
                    float luminance = (0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b) / 255f;
                    sum += luminance;
                    minimum = Mathf.Min(minimum, luminance);
                    maximum = Mathf.Max(maximum, luminance);
                    if (luminance > 1f / 255f) visible++;
                }
                evidence.meanLuminance = colors.Length > 0 ? sum / colors.Length : 0.0;
                evidence.minimumLuminance = minimum;
                evidence.maximumLuminance = maximum;
                evidence.nonBlackFraction = colors.Length > 0 ? (double)visible / colors.Length : 0.0;
                evidence.success = colors.Length > 0;
                File.WriteAllBytes(Path.Combine(_evidenceFolder, fileName), pixels.EncodeToPNG());
            }
            catch (Exception exception)
            {
                evidence.error = exception.GetType().Name + ":" + exception.Message;
                Debug.LogWarning($"[StartupBootstrap] Offscreen capture failed for {scene.name}: {evidence.error}", this);
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (pixels != null) Destroy(pixels);
                if (target != null) RenderTexture.ReleaseTemporary(target);
            }
            return evidence;
        }

        private static Camera FindEnabledCamera(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                    if (camera != null && camera.enabled && camera.gameObject.activeInHierarchy)
                        return camera;
            return null;
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
                bootstrapCapture = _bootstrapCapture,
                playableCapture = _playableCapture,
                playableScene = playableSceneName,
                unityVersion = Application.unityVersion,
                bootstrapCoverRenderPath = _cameraCoverReady
                    ? "camera-geometry-and-text"
                    : "immediate-mode-fallback"
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
            public string bootstrapCoverRenderPath;
            public double bootstrapAwakeUptimeMilliseconds;
            public double coverPresentedUptimeMilliseconds;
            public double targetActivatedUptimeMilliseconds;
            public double readyUptimeMilliseconds;
            public double maximumObservedFrameMilliseconds;
            public CameraCaptureEvidence bootstrapCapture;
            public CameraCaptureEvidence playableCapture;
        }

        [Serializable]
        private sealed class CameraCaptureEvidence
        {
            public bool success;
            public string error;
            public string file;
            public string scene;
            public string camera;
            public int width;
            public int height;
            public double meanLuminance;
            public float minimumLuminance;
            public float maximumLuminance;
            public double nonBlackFraction;
        }

        private void OnGUI()
        {
            // The normal path is actual camera geometry so URP render requests,
            // hidden-player captures and the visible swapchain contain the same
            // cover. IMGUI remains only an actionable fallback if required built-in
            // render resources are unavailable; the strict evidence runner rejects
            // that fallback rather than treating a synthetic capture as proof.
            if (_finished || _cameraCoverReady) return;
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
