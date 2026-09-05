// Stage into Assets/Elemental/Authoring/Editor only for explicit QA.
// Run Elemental.Authoring.Editor.SkyHorizonCelestialVisualQa.Run() from a ready,
// unpaused EarthCoreSlice Play session. It never saves or exits Play Mode.
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
using UnityEngine.UIElements;
using Camera = UnityEngine.Camera;
using Object = UnityEngine.Object;

namespace Elemental.Authoring.Editor
{
    public static class SkyHorizonCelestialVisualQa
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const int SettleFrames = 10;
        private const int WriteFrames = 5;
        private static readonly int VisibilityId = Shader.PropertyToID("_CelestialVisibility");
        private static readonly int IsMoonId = Shader.PropertyToID("_IsMoon");
        private static readonly int PhaseId = Shader.PropertyToID("_MoonPhase01");
        private static readonly Dictionary<Behaviour, bool> SavedEnabled = new();
        private static readonly List<CaptureRecord> Captures = new();
        private static readonly List<Shot> Shots = new();

        private enum ViewMode
        {
            HorizonA,
            HorizonB,
            MoonClose,
            MoonHorizon,
            PlanetSurface,
            PlanetHalfReveal,
            PlanetVisible
        }

        private sealed class Shot
        {
            public string Name;
            public float TimePhase;
            public float TargetMoonPhase;
            public ViewMode View;
            public float Fov;
        }

        [Serializable]
        public sealed class CaptureRecord
        {
            public string name, path, utc, viewMode, bodyName, bodyShader;
            public float requestedTimePhase, actualTimePhase, targetMoonPhase, actualMoonPhase,
                bodyVisibility, bodyIsMoon, bodyMoonPhaseProperty, bodyAltitude,
                cameraRadius, innerRadius, outerRadius, revealDistance, fov;
            public Vector3 cameraPosition, cameraEuler, bodyViewport;
            public bool bodyInFrame, numericContractPassed;
            public int screenWidth, screenHeight, renderedSettleFrames, renderedWriteFrames;
            public long pngBytes;
        }

        [Serializable]
        public sealed class Report
        {
            public string utc, completedUtc, unityVersion, scene, status, error, scope, folder;
            public bool restored, applicationPlayingAtStart, allNumericContractsPassed;
            public float originalPhase;
            public Vector3 originalCameraPosition;
            public string[] temporarilyDisabled;
            public CaptureRecord[] captures;
        }

        private static Camera _camera;
        private static CelestialSystemBehaviour _sky;
        private static Transform _moon, _planetBody;
        private static Renderer _moonRenderer, _planetRenderer;
        private static MaterialPropertyBlock _properties;
        private static Vector3 _oldPosition, _surfaceUp, _horizonForward, _planetCenter;
        private static Quaternion _oldRotation;
        private static float _oldFov, _oldPhase, _surfaceRadius, _innerRadius, _outerRadius, _revealDistance;
        private static CameraClearFlags _oldClearFlags;
        private static bool _running, _captureRequested;
        private static int _shotIndex, _renderFrames, _poseFrame, _captureFrame, _lastRenderedFrame;
        private static double _startEditorTime;
        private static DateTime _captureRequestedUtc;
        private static string _folder;
        private static Report _report;

        [MenuItem("Elemental/QA/Capture Sky Horizon and Celestials (Ready Play Mode)")]
        public static void Run()
        {
            if (_running) throw new InvalidOperationException("Sky/celestial capture is already running.");
            if (!Application.isPlaying || EditorApplication.isPaused)
                throw new InvalidOperationException("Run in unpaused Play Mode after Earth readiness.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("EarthCoreSlice must be active.");
            EarthSceneReadinessGate gate = FindUnique<EarthSceneReadinessGate>(scene);
            if (!gate.IsReady || gate.Failed) throw new InvalidOperationException("Earth readiness gate has not passed.");

            _sky = FindUnique<CelestialSystemBehaviour>(scene);
            _camera = Camera.main;
            _moon = FindUniqueTransform(scene, "Distant Moon");
            _planetBody = FindUniqueTransform(scene, "Ringed Ember Planet");
            _moonRenderer = _moon.GetComponent<Renderer>();
            _planetRenderer = _planetBody.GetComponent<Renderer>();
            if (_camera == null || !_camera.isActiveAndEnabled || _camera.gameObject.scene != scene ||
                _camera.targetTexture != null || !_sky.HasRequiredBindings || _sky.TargetCamera != _camera ||
                _moonRenderer == null || _planetRenderer == null)
                throw new InvalidOperationException("Production Main Camera and both bound celestial renderers are required.");

            _sky.EvaluatePresentationForQa();
            Vector4 planetData = Shader.GetGlobalVector("_ElementalPlanetCenterRadius");
            Vector4 atmosphereData = Shader.GetGlobalVector("_ElementalAtmosphereParams");
            _planetCenter = new Vector3(planetData.x, planetData.y, planetData.z);
            _innerRadius = planetData.w;
            _outerRadius = _innerRadius * atmosphereData.x;
            _revealDistance = Mathf.Max(.25f, (_outerRadius - _innerRadius) * .12f);
            if (_innerRadius <= 1f || atmosphereData.x <= 1f)
                throw new InvalidOperationException("Runtime planet/atmosphere globals are not initialized.");

            _oldPosition = _camera.transform.position;
            _oldRotation = _camera.transform.rotation;
            _oldFov = _camera.fieldOfView;
            _oldClearFlags = _camera.clearFlags;
            _oldPhase = _sky.Snapshot.TimeOfDay01;
            _surfaceRadius = Vector3.Distance(_oldPosition, _planetCenter);
            _surfaceUp = (_oldPosition - _planetCenter).normalized;
            _horizonForward = Vector3.ProjectOnPlane(_oldRotation * Vector3.forward, _surfaceUp).normalized;
            if (_surfaceUp.sqrMagnitude < .9f || _horizonForward.sqrMagnitude < .9f)
                throw new InvalidOperationException("Production camera has no stable local-up/horizon frame.");

            float crescent = FindMoonTimePhase(.22f);
            float quarter = FindMoonTimePhase(.50f);
            float full = FindMoonTimePhase(1f);
            _sky.SetTimeOfDayForQa(_oldPhase);
            _sky.EvaluatePresentationForQa();
            BuildShots(crescent, quarter, full);

            string stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
            _folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "BuildReports", "EnvironmentAnimationRescue", "SkyHorizonCelestial", stamp);
            Directory.CreateDirectory(_folder);
            _report = new Report {
                utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                scene = scene.path, status = "Capturing", folder = _folder,
                applicationPlayingAtStart = true, originalPhase = _oldPhase,
                originalCameraPosition = _oldPosition,
                scope = "Actual Game-view ScreenCapture through the production Main Camera. Camera pose and celestial QA time are transient. " +
                    "Camera motion owners are suspended and restored; no scene/profile/material/light save. Numeric MPB and viewport checks accompany images, but appearance still requires human review."
            };
            Captures.Clear();
            SavedEnabled.Clear();
            _properties = new MaterialPropertyBlock();
            _shotIndex = 0;
            _renderFrames = 0;
            _poseFrame = 0;
            _lastRenderedFrame = -1;
            _captureRequested = false;
            _startEditorTime = EditorApplication.timeSinceStartup;
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
                        component is EarthChargeCameraLookdevV2 || component is VisualQaCaptureBehaviour ||
                        component is UIDocument || type == "Unity.Cinemachine.CinemachineBrain" ||
                        type == "Cinemachine.CinemachineBrain";
                    if (!owner) continue;
                    SavedEnabled.Add(component, component.enabled);
                    if (component.enabled) disabled.Add(type + " @ " + component.name);
                    component.enabled = false;
                }
                _report.temporarilyDisabled = disabled.ToArray();
                Type gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (gameView == null) throw new InvalidOperationException("Unity Game view type unavailable.");
                EditorWindow.GetWindow(gameView).Show();
                EditorWindow.GetWindow(gameView).Focus();
                EditorApplication.update += Tick;
                EditorApplication.playModeStateChanged += OnPlayState;
                AssemblyReloadEvents.beforeAssemblyReload += Abort;
                RenderPipelineManager.beginCameraRendering += BeforeRender;
                HoldShot();
                SaveReport();
            }
            catch (Exception exception)
            {
                Finish("Failed", exception.ToString());
                throw;
            }
        }

        public static void Abort()
        {
            if (_running) Finish("Aborted", "Capture interrupted before all images completed.");
        }

        private static void BuildShots(float crescent, float quarter, float full)
        {
            Shots.Clear();
            Shots.Add(new Shot { Name = "NoonHorizonYaw0", TimePhase = .25f, View = ViewMode.HorizonA, Fov = 62f });
            Shots.Add(new Shot { Name = "NoonHorizonYaw90", TimePhase = .25f, View = ViewMode.HorizonB, Fov = 62f });
            Shots.Add(new Shot { Name = "MoonCrescentClose", TimePhase = crescent, TargetMoonPhase = .22f, View = ViewMode.MoonClose, Fov = 8f });
            Shots.Add(new Shot { Name = "MoonQuarterClose", TimePhase = quarter, TargetMoonPhase = .50f, View = ViewMode.MoonClose, Fov = 8f });
            Shots.Add(new Shot { Name = "MoonFullClose", TimePhase = full, TargetMoonPhase = 1f, View = ViewMode.MoonClose, Fov = 8f });
            Shots.Add(new Shot { Name = "MoonFullWarmHorizon", TimePhase = full, TargetMoonPhase = 1f, View = ViewMode.MoonHorizon, Fov = 34f });
            Shots.Add(new Shot { Name = "SystemPlanetSurfaceHidden", TimePhase = .25f, View = ViewMode.PlanetSurface, Fov = 12f });
            Shots.Add(new Shot { Name = "SystemPlanetHalfReveal", TimePhase = .25f, View = ViewMode.PlanetHalfReveal, Fov = 12f });
            Shots.Add(new Shot { Name = "SystemPlanetSpaceVisible", TimePhase = .25f, View = ViewMode.PlanetVisible, Fov = 12f });
        }

        private static float FindMoonTimePhase(float targetMoonPhase)
        {
            float bestTime = 0f;
            float bestScore = float.MaxValue;
            for (int index = 0; index <= 720; index++)
            {
                float time = index / 720f;
                _sky.SetTimeOfDayForQa(time);
                _sky.EvaluatePresentationForQa();
                float score = Mathf.Abs(_sky.Snapshot.MoonPhase01 - targetMoonPhase);
                if (score >= bestScore) continue;
                bestScore = score;
                bestTime = time;
            }
            return bestTime;
        }

        private static void OnPlayState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) Abort();
        }

        private static void BeforeRender(ScriptableRenderContext context, Camera camera)
        {
            if (!_running || camera != _camera) return;
            try
            {
                HoldShot();
                if (Time.frameCount != _lastRenderedFrame)
                {
                    _lastRenderedFrame = Time.frameCount;
                    _renderFrames++;
                }
            }
            catch (Exception exception) { Finish("Failed", exception.ToString()); }
        }

        private static void Tick()
        {
            if (!_running) return;
            try
            {
                if (!Application.isPlaying || _camera == null || _sky == null)
                    throw new InvalidOperationException("Play scene or sky bindings disappeared.");
                if (EditorApplication.timeSinceStartup - _startEditorTime > 180)
                    throw new TimeoutException("Capture exceeded 180 seconds; keep Game view rendering and Play unpaused.");
                HoldShot();
                if (!_captureRequested && _renderFrames - _poseFrame >= SettleFrames)
                {
                    _captureRequestedUtc = DateTime.UtcNow;
                    ScreenCapture.CaptureScreenshot(Path.Combine(_folder, Shots[_shotIndex].Name + ".png"), 1);
                    _captureFrame = _renderFrames;
                    _captureRequested = true;
                }
                if (!_captureRequested || _renderFrames - _captureFrame < WriteFrames) return;
                string path = Path.Combine(_folder, Shots[_shotIndex].Name + ".png");
                var file = new FileInfo(path);
                if (!file.Exists || file.Length == 0 || file.LastWriteTimeUtc < _captureRequestedUtc) return;
                CaptureRecord record = MakeRecord(Shots[_shotIndex], file);
                Captures.Add(record);
                SaveReport();
                if (!record.numericContractPassed)
                    throw new InvalidOperationException(
                        "Numeric/viewport capture contract failed for " + record.name +
                        $": visibility={record.bodyVisibility:F4}, isMoon={record.bodyIsMoon:F4}, " +
                        $"phase={record.actualMoonPhase:F4}/{record.targetMoonPhase:F4}, " +
                        $"phaseProperty={record.bodyMoonPhaseProperty:F4}, viewport={record.bodyViewport}.");
                _shotIndex++;
                if (_shotIndex == Shots.Count)
                {
                    Finish("Captured", null);
                    return;
                }
                _poseFrame = _renderFrames;
                _captureRequested = false;
                HoldShot();
                SaveReport();
            }
            catch (Exception exception) { Finish("Failed", exception.ToString()); }
        }

        private static void HoldShot()
        {
            Shot shot = Shots[_shotIndex];
            Vector3 position = _oldPosition;
            if (shot.View == ViewMode.PlanetHalfReveal)
                position = _planetCenter + _surfaceUp * (_outerRadius + _revealDistance * .5f);
            else if (shot.View == ViewMode.PlanetVisible)
                position = _planetCenter + _surfaceUp * (_outerRadius + _revealDistance * 1.05f);
            _camera.transform.position = position;
            _camera.fieldOfView = shot.Fov;
            _sky.SetTimeOfDayForQa(shot.TimePhase);
            _sky.EvaluatePresentationForQa();

            // Phase and horizon visibility are independent. For close phase studies,
            // move the production camera around the same physical-radius shell so the
            // requested ephemeris phase is above that observer's horizon. This avoids
            // weakening the phase tolerance or photographing through the planet.
            if (shot.View == ViewMode.MoonClose)
            {
                Vector3 moonDirection = ToVector3(_sky.Snapshot.MoonDirection).normalized;
                Vector3 perpendicular = Vector3.ProjectOnPlane(_surfaceUp, moonDirection).normalized;
                if (perpendicular.sqrMagnitude < .9f)
                    perpendicular = Vector3.Cross(moonDirection,
                        Mathf.Abs(moonDirection.x) < .8f ? Vector3.right : Vector3.forward).normalized;
                const float desiredAltitude = .32f;
                Vector3 observationUp = (perpendicular * Mathf.Sqrt(1f - desiredAltitude * desiredAltitude) +
                                         moonDirection * desiredAltitude).normalized;
                position = _planetCenter + observationUp * _surfaceRadius;
                _camera.transform.position = position;
                _sky.EvaluatePresentationForQa();
            }

            Vector3 look;
            if (shot.View == ViewMode.HorizonA)
                look = (_horizonForward + _surfaceUp * .035f).normalized;
            else if (shot.View == ViewMode.HorizonB)
                look = (Quaternion.AngleAxis(90f, _surfaceUp) * _horizonForward + _surfaceUp * .035f).normalized;
            else if (shot.View == ViewMode.MoonClose || shot.View == ViewMode.MoonHorizon)
                look = (_moon.position - position).normalized;
            else
                look = (_planetBody.position - position).normalized;
            Vector3 cameraUp = shot.View == ViewMode.PlanetHalfReveal || shot.View == ViewMode.PlanetVisible
                ? (position - _planetCenter).normalized : _surfaceUp;
            if (Mathf.Abs(Vector3.Dot(look, cameraUp)) > .985f)
                cameraUp = Vector3.Cross(look, Mathf.Abs(look.x) < .8f ? Vector3.right : Vector3.forward).normalized;
            _camera.transform.rotation = Quaternion.LookRotation(look, cameraUp);
            _sky.EvaluatePresentationForQa();
            if (Mathf.Abs(Mathf.DeltaAngle(shot.TimePhase * 360f, _sky.Snapshot.TimeOfDay01 * 360f)) > .01f)
                throw new InvalidOperationException("Celestial QA time did not remain pinned.");
        }

        private static CaptureRecord MakeRecord(Shot shot, FileInfo file)
        {
            bool moonShot = shot.View == ViewMode.MoonClose || shot.View == ViewMode.MoonHorizon;
            bool planetShot = shot.View == ViewMode.PlanetSurface || shot.View == ViewMode.PlanetHalfReveal || shot.View == ViewMode.PlanetVisible;
            Transform body = moonShot ? _moon : planetShot ? _planetBody : null;
            Renderer renderer = moonShot ? _moonRenderer : planetShot ? _planetRenderer : null;
            float visibility = 0f, isMoon = 0f, phaseProperty = 0f, altitude = 0f;
            Vector3 viewport = Vector3.zero;
            bool inFrame = true;
            if (body != null)
            {
                renderer.GetPropertyBlock(_properties);
                visibility = _properties.GetFloat(VisibilityId);
                isMoon = _properties.GetFloat(IsMoonId);
                phaseProperty = _properties.GetFloat(PhaseId);
                Vector3 direction = (body.position - _camera.transform.position).normalized;
                altitude = Vector3.Dot(direction, _sky.ObserverUp.normalized);
                viewport = _camera.WorldToViewportPoint(body.position);
                inFrame = viewport.z > 0f && viewport.x >= .1f && viewport.x <= .9f && viewport.y >= .1f && viewport.y <= .9f;
            }

            bool numericPass = inFrame;
            if (moonShot)
                numericPass &= visibility >= .999f && isMoon >= .999f &&
                    Mathf.Abs(phaseProperty - _sky.Snapshot.MoonPhase01) <= .002f &&
                    Mathf.Abs(_sky.Snapshot.MoonPhase01 - shot.TargetMoonPhase) <= .075f;
            else if (shot.View == ViewMode.PlanetSurface)
                numericPass &= visibility <= .001f && isMoon <= .001f;
            else if (shot.View == ViewMode.PlanetHalfReveal)
                numericPass &= visibility >= .42f && visibility <= .58f && isMoon <= .001f;
            else if (shot.View == ViewMode.PlanetVisible)
                numericPass &= visibility >= .999f && isMoon <= .001f;

            return new CaptureRecord {
                name = shot.Name, path = file.FullName, utc = DateTime.UtcNow.ToString("O"),
                viewMode = shot.View.ToString(), bodyName = body != null ? body.name : "",
                bodyShader = renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null
                    ? renderer.sharedMaterial.shader.name : "",
                requestedTimePhase = shot.TimePhase, actualTimePhase = _sky.Snapshot.TimeOfDay01,
                targetMoonPhase = shot.TargetMoonPhase, actualMoonPhase = _sky.Snapshot.MoonPhase01,
                bodyVisibility = visibility, bodyIsMoon = isMoon, bodyMoonPhaseProperty = phaseProperty,
                bodyAltitude = altitude, cameraRadius = Vector3.Distance(_camera.transform.position, _planetCenter),
                innerRadius = _innerRadius, outerRadius = _outerRadius, revealDistance = _revealDistance,
                fov = _camera.fieldOfView, cameraPosition = _camera.transform.position,
                cameraEuler = _camera.transform.eulerAngles, bodyViewport = viewport,
                bodyInFrame = inFrame, numericContractPassed = numericPass,
                screenWidth = Screen.width, screenHeight = Screen.height, pngBytes = file.Length,
                renderedSettleFrames = _captureFrame - _poseFrame,
                renderedWriteFrames = _renderFrames - _captureFrame
            };
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
                foreach (var saved in SavedEnabled)
                    if (saved.Key != null) saved.Key.enabled = saved.Value;
                if (_camera != null)
                {
                    _camera.transform.SetPositionAndRotation(_oldPosition, _oldRotation);
                    _camera.fieldOfView = _oldFov;
                    _camera.clearFlags = _oldClearFlags;
                }
                if (_sky != null)
                {
                    _sky.SetTimeOfDayForQa(_oldPhase);
                    _sky.EvaluatePresentationForQa();
                }
                _report.restored = true;
            }
            catch (Exception restoreError)
            {
                _report.restored = false;
                error = (error ?? "") + "\nRestore: " + restoreError;
                status = "Failed";
            }
            finally
            {
                SavedEnabled.Clear();
                _report.status = status;
                _report.error = error;
                _report.completedUtc = DateTime.UtcNow.ToString("O");
                _report.allNumericContractsPassed = Captures.Count == Shots.Count && Captures.TrueForAll(item => item.numericContractPassed);
                SaveReport();
            }
            Debug.Log("[SkyHorizonCelestialVisualQa] " + status + "; " + Path.Combine(_folder, "CaptureReport.json"));
        }

        private static void SaveReport()
        {
            _report.captures = Captures.ToArray();
            File.WriteAllText(Path.Combine(_folder, "CaptureReport.json"), JsonUtility.ToJson(_report, true));
        }

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

        private static Vector3 ToVector3(Unity.Mathematics.float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}
