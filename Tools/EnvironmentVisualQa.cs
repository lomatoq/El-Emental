// Stage into an Editor assembly explicitly. No automatic run or scene save.
// Invoke Elemental.Authoring.Editor.EnvironmentVisualQa.Run() in ready Play Mode.
using System;
using System.Collections.Generic;
using System.IO;
using Elemental.Presentation.Camera;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Camera = UnityEngine.Camera;
using Object = UnityEngine.Object;

namespace Elemental.Authoring.Editor
{
    public static class EnvironmentVisualQa
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const int SettleFrames = 10, WriteFrames = 5;
        private static readonly Dictionary<Behaviour, bool> SavedEnabled = new();
        private static readonly List<CaptureRecord> Captures = new();
        private static readonly Shot[] Shots = {
            new("Noon", .25f), new("Dawn", .01f), new("Dusk", .49f), new("Night", .75f),
            new("StarsUp", .75f), new("ColumnCurveDetail", .25f),
            new("DawnSunHorizon", .01f), new("DuskSunHorizon", .49f)
        };
        private static Camera _camera;
        private static CelestialSystemBehaviour _sky;
        private static Vector3 _oldPosition, _overviewPosition, _detailPosition, _detailTarget, _up, _forward;
        private static Quaternion _oldRotation, _overviewRotation;
        private static float _oldFov, _oldPhase;
        private static CameraClearFlags _oldClearFlags;
        private static bool _running, _captureRequested;
        private static int _shotIndex, _renderFrames, _poseFrame, _captureFrame, _lastRenderedFrame;
        private static double _startEditorTime;
        private static DateTime _captureRequestedUtc;
        private static string _folder;
        private static Report _report;

        private readonly struct Shot
        {
            public readonly string name;
            public readonly float phase;
            public Shot(string name, float phase) { this.name = name; this.phase = phase; }
        }

        [Serializable]
        public sealed class CaptureRecord
        {
            public string name, path, utc;
            public float requestedPhase, actualPhase, fov, sunIntensity, nightFraction;
            public Vector3 cameraPosition, cameraEuler, sunDirection;
            public Color sunColor, ambientSky;
            public int screenWidth, screenHeight, renderedSettleFrames, renderedWriteFrames;
            public long pngBytes;
        }

        [Serializable]
        public sealed class Report
        {
            public string utc, completedUtc, unityVersion, scene, status, error, scope;
            public bool restored, applicationPlayingAtStart;
            public float originalPhase, originalTimeScale;
            public Vector3 originalCameraPosition;
            public string[] temporarilyDisabled;
            public CaptureRecord[] captures;
        }

        public static void Run()
        {
            if (_running) throw new InvalidOperationException("Environment visual capture is already running.");
            if (!Application.isPlaying || EditorApplication.isPaused)
                throw new InvalidOperationException("Run in unpaused Play Mode after Earth readiness; this helper never starts Play.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("EarthCoreSlice must be the active scene.");
            var gate = FindUnique<EarthSceneReadinessGate>(scene);
            if (!gate.IsReady || gate.Failed) throw new InvalidOperationException("Earth readiness gate has not passed.");
            _sky = FindUnique<CelestialSystemBehaviour>(scene);
            _camera = Camera.main;
            if (_camera == null || !_camera.isActiveAndEnabled || _camera.gameObject.scene != scene ||
                !_sky.HasRequiredBindings || _sky.TargetCamera != _camera)
                throw new InvalidOperationException("The active production Main Camera and bound celestial system are required.");
            if (_camera.targetTexture != null) throw new InvalidOperationException("Main Camera must render the Game view, not a target texture.");
            Transform ring = FindUniqueTransform(scene, "Outer Stone Ring");
            Transform arch = null;
            foreach (Transform candidate in ring.GetComponentsInChildren<Transform>(true))
                if (candidate.name == "FRAME_outer_arch_02") arch = candidate;
            if (arch == null) throw new InvalidOperationException("Expected authored FRAME_outer_arch_02 in Outer Stone Ring.");
            Bounds ringBounds = VisibleMeshBounds(ring), archBounds = VisibleMeshBounds(arch);
            Vector3 anchor = _sky.LightingAnchor.position;
            _up = _sky.LightingUp.normalized;
            if (_up.sqrMagnitude < .9f) throw new InvalidOperationException("Celestial lighting frame is not initialized.");
            Vector3 outward = Vector3.ProjectOnPlane(archBounds.center - anchor, _up).normalized;
            if (outward.sqrMagnitude < .9f) outward = Vector3.ProjectOnPlane(_camera.transform.forward, _up).normalized;
            Vector3 side = Vector3.Cross(_up, outward).normalized;
            _forward = -outward;
            float radius = Mathf.Max(ringBounds.extents.x, ringBounds.extents.y, ringBounds.extents.z);
            _overviewPosition = anchor + outward * (radius * 1.55f + 12f) + _up * (radius * .55f + 7f);
            _overviewRotation = Quaternion.LookRotation(anchor + _up * 3f - _overviewPosition, _up);
            _detailTarget = archBounds.center + _up * 2f;
            _detailPosition = _detailTarget - outward * 10f + side * 3f;

            _oldPosition = _camera.transform.position;
            _oldRotation = _camera.transform.rotation;
            _oldFov = _camera.fieldOfView;
            _oldClearFlags = _camera.clearFlags;
            _oldPhase = _sky.Snapshot.TimeOfDay01;
            _folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "BuildReports", "EnvironmentAnimationRescue", "VisualFinal");
            Directory.CreateDirectory(_folder);
            _report = new Report {
                utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion, scene = scene.path,
                status = "Capturing", applicationPlayingAtStart = true, originalPhase = _oldPhase,
                originalCameraPosition = _oldPosition, originalTimeScale = Time.timeScale,
                scope = "Actual production Main Camera through ScreenCapture/Game view; transient Play-only camera and clock poses. " +
                    "Bots and explicit camera motion owners paused. No light, material, shadow, exposure, profile or timeScale edits; no scene save. " +
                    "Camera lookdev owners may disable their transient cinematic depth of field while suspended. Images require human visual inspection."
            };
            Captures.Clear(); SavedEnabled.Clear();
            _shotIndex = 0; _renderFrames = 0; _poseFrame = 0; _lastRenderedFrame = -1;
            _captureRequested = false; _startEditorTime = EditorApplication.timeSinceStartup;
            _running = true;
            try
            {
                var disabled = new List<string>();
                foreach (Behaviour component in Object.FindObjectsByType<Behaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (component.gameObject.scene != scene) continue;
                    string type = component.GetType().FullName;
                    bool owner = component is PlanetCameraRig || component is EarthCinemachineCameraController ||
                        component is EarthCameraDirector || component is EarthChargeCameraLookdev ||
                        component is EarthChargeCameraLookdevV2 || component is EarthMvpBotController ||
                        component is VisualQaCaptureBehaviour || type == "Unity.Cinemachine.CinemachineBrain" ||
                        type == "Cinemachine.CinemachineBrain";
                    if (!owner) continue;
                    SavedEnabled.Add(component, component.enabled);
                    if (component.enabled) disabled.Add(type + " @ " + component.name);
                    component.enabled = false;
                }
                _report.temporarilyDisabled = disabled.ToArray();
                Type gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (gameView == null) throw new InvalidOperationException("Unity Game view type was not available.");
                EditorWindow.GetWindow(gameView).Show();
                EditorWindow.GetWindow(gameView).Focus();
                EditorApplication.update += Tick;
                EditorApplication.playModeStateChanged += OnPlayState;
                AssemblyReloadEvents.beforeAssemblyReload += Abort;
                RenderPipelineManager.beginCameraRendering += BeforeRender;
                HoldPoseAndClock();
                SaveReport();
            }
            catch (Exception exception) { Finish("Failed", exception.ToString()); throw; }
        }

        public static void Abort() { if (_running) Finish("Aborted", "Capture interrupted before all requested images completed."); }

        private static void OnPlayState(PlayModeStateChange state)
        { if (state == PlayModeStateChange.ExitingPlayMode) Abort(); }

        private static void BeforeRender(ScriptableRenderContext context, Camera camera)
        {
            if (!_running || camera != _camera) return;
            try
            {
                HoldPoseAndClock();
                if (Time.frameCount != _lastRenderedFrame) { _lastRenderedFrame = Time.frameCount; _renderFrames++; }
            }
            catch (Exception exception) { Finish("Failed", exception.ToString()); }
        }

        private static void Tick()
        {
            if (!_running) return;
            try
            {
                if (!Application.isPlaying || _camera == null || _sky == null)
                    throw new InvalidOperationException("Play scene or camera disappeared during capture.");
                if (EditorApplication.timeSinceStartup - _startEditorTime > 180)
                    throw new TimeoutException("Visual capture exceeded 180 seconds; ensure Game view is rendering and Play is not paused.");
                HoldPoseAndClock();
                if (!_captureRequested && _renderFrames - _poseFrame >= SettleFrames)
                {
                    _captureRequestedUtc = DateTime.UtcNow;
                    ScreenCapture.CaptureScreenshot(Path.Combine(_folder, Shots[_shotIndex].name + ".png"), 1);
                    _captureFrame = _renderFrames; _captureRequested = true;
                }
                if (!_captureRequested || _renderFrames - _captureFrame < WriteFrames) return;
                var file = new FileInfo(Path.Combine(_folder, Shots[_shotIndex].name + ".png"));
                if (!file.Exists || file.Length == 0 || file.LastWriteTimeUtc < _captureRequestedUtc) return;
                var direction = _sky.Snapshot.SunDirection;
                Captures.Add(new CaptureRecord {
                    name = Shots[_shotIndex].name, path = file.FullName, utc = DateTime.UtcNow.ToString("O"),
                    requestedPhase = Shots[_shotIndex].phase, actualPhase = _sky.Snapshot.TimeOfDay01,
                    fov = _camera.fieldOfView, cameraPosition = _camera.transform.position,
                    cameraEuler = _camera.transform.eulerAngles, sunDirection = new Vector3(direction.x, direction.y, direction.z),
                    sunIntensity = _sky.SunLight.intensity, sunColor = _sky.SunLight.color,
                    ambientSky = RenderSettings.ambientSkyColor, nightFraction = _sky.Snapshot.Night01,
                    screenWidth = Screen.width, screenHeight = Screen.height, pngBytes = file.Length,
                    renderedSettleFrames = _captureFrame - _poseFrame, renderedWriteFrames = _renderFrames - _captureFrame
                });
                _shotIndex++;
                if (_shotIndex == Shots.Length) { Finish("Captured", null); return; }
                _poseFrame = _renderFrames; _captureRequested = false;
                HoldPoseAndClock(); SaveReport();
            }
            catch (Exception exception) { Finish("Failed", exception.ToString()); }
        }

        private static void HoldPoseAndClock()
        {
            Shot shot = Shots[_shotIndex];
            _sky.SetTimeOfDayForQa(shot.phase);
            _sky.EvaluatePresentationForQa();
            Vector3 position = _overviewPosition;
            Quaternion rotation = _overviewRotation;
            float fov = 65f;
            if (shot.name == "ColumnCurveDetail")
            { position = _detailPosition; rotation = Quaternion.LookRotation(_detailTarget - position, _up); fov = 45f; }
            else if (shot.name == "StarsUp")
            { rotation = Quaternion.LookRotation((_up + _forward * .12f).normalized, _forward); fov = 70f; }
            else if (shot.name.EndsWith("SunHorizon", StringComparison.Ordinal))
            {
                var sun = _sky.Snapshot.SunDirection;
                rotation = Quaternion.LookRotation(new Vector3(sun.x, sun.y, sun.z), _up); fov = 65f;
            }
            _camera.transform.SetPositionAndRotation(position, rotation);
            _camera.fieldOfView = fov;
            _sky.EvaluatePresentationForQa(); // Observer local-up follows the actual capture camera.
            float error = Mathf.Abs(Mathf.DeltaAngle(shot.phase * 360f, _sky.Snapshot.TimeOfDay01 * 360f));
            if (error > .01f) throw new InvalidOperationException("Celestial QA phase failed to remain pinned.");
        }

        private static void Finish(string status, string error)
        {
            _running = false;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayState;
            AssemblyReloadEvents.beforeAssemblyReload -= Abort;
            RenderPipelineManager.beginCameraRendering -= BeforeRender;
            try
            {
                foreach (var state in SavedEnabled) if (state.Key != null) state.Key.enabled = state.Value;
                if (_camera != null)
                {
                    _camera.transform.SetPositionAndRotation(_oldPosition, _oldRotation);
                    _camera.fieldOfView = _oldFov; _camera.clearFlags = _oldClearFlags;
                }
                if (_sky != null) { _sky.SetTimeOfDayForQa(_oldPhase); _sky.EvaluatePresentationForQa(); }
                if (_camera != null) _camera.clearFlags = _oldClearFlags;
                _report.restored = true;
            }
            catch (Exception restoreError)
            { _report.restored = false; error = (error ?? "") + "\nRestore: " + restoreError; status = "Failed"; }
            finally
            {
                SavedEnabled.Clear(); _report.status = status; _report.error = error;
                _report.completedUtc = DateTime.UtcNow.ToString("O"); SaveReport();
            }
            Debug.Log("[EnvironmentVisualQa] " + status + "; " + Path.Combine(_folder, "CaptureReport.json"));
        }

        private static void SaveReport()
        { _report.captures = Captures.ToArray(); File.WriteAllText(Path.Combine(_folder, "CaptureReport.json"), JsonUtility.ToJson(_report, true)); }

        private static T FindUnique<T>(Scene scene) where T : Component
        {
            T found = null;
            foreach (T candidate in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene != scene) continue;
                if (found != null) throw new InvalidOperationException("Expected one " + typeof(T).Name + " in Earth scene.");
                found = candidate;
            }
            return found != null ? found : throw new InvalidOperationException("Missing " + typeof(T).Name + " in Earth scene.");
        }

        private static Transform FindUniqueTransform(Scene scene, string name)
        {
            Transform found = null;
            foreach (Transform candidate in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene != scene || candidate.name != name) continue;
                if (found != null) throw new InvalidOperationException("Ambiguous scene object " + name);
                found = candidate;
            }
            return found != null ? found : throw new InvalidOperationException("Missing scene object " + name);
        }

        private static Bounds VisibleMeshBounds(Transform root)
        {
            bool found = false; Bounds bounds = default;
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!renderer.enabled || renderer.GetComponent<MeshFilter>()?.sharedMesh == null) continue;
                if (found) bounds.Encapsulate(renderer.bounds); else { bounds = renderer.bounds; found = true; }
            }
            return found ? bounds : throw new InvalidOperationException("No visible authored mesh under " + root.name);
        }
    }
}
