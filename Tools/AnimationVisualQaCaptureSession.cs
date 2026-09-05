// Stage into an Editor assembly explicitly. This recorder does not drive gameplay.
// The scenario owner must use production inputs/fixtures and restore them in finally.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Camera;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using Elemental.Simulation.Characters;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Camera = UnityEngine.Camera;
using Object = UnityEngine.Object;

namespace Elemental.Authoring.Editor
{
    public static class AnimationVisualQaCaptureSession
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const int WriteFrames = 5;
        private static readonly string[] RequiredLabels = {
            "idle", "walk", "stop", "turn", "uneven-pit", "uneven-slope",
            "mantle-reach", "mantle-raise", "mantle-transfer", "mantle-settle",
            "magic-start", "magic-sample", "magic-recovery"
        };
        private static readonly HashSet<string> HiddenUiTypes = new(StringComparer.Ordinal) {
            "Elemental.Presentation.VFX.RumbleLookdevRuntime",
            "Elemental.Presentation.UI.EarthPolishLabController",
            "Elemental.Presentation.UI.BendingDebugOverlay"
        };
        private static readonly Dictionary<Behaviour, bool> UiStates = new();
        private static readonly Dictionary<EarthAnimationPoseProbe, string> ProbeScenarios = new();
        private static readonly List<EarthAnimationPoseProbe> AddedProbes = new();
        private static readonly List<FrameRecord> Frames = new();
        private static Camera _camera;
        private static Scene _scene;
        private static string _folder, _pendingLabel, _pendingScenario, _pendingPath;
        private static bool _running, _screenshotIssued;
        private static int _renderFrame, _lastUnityFrame, _issueRenderFrame;
        private static DateTime _requestedUtc;
        private static Report _report;
        private static ActorRecord[] _issuedActors;
        private static Vector3 _issuedCameraPosition, _issuedCameraEuler;
        private static int _issuedWidth, _issuedHeight;

        [Serializable] public sealed class ActorRecord
        {
            public string actor, action, footPolicy, baseClip, mantlePhase, eammStatus;
            public Vector3 viewport, headViewport, leftFootViewport, rightFootViewport;
            public bool visible, fullBodyFramed, lineOfSight, grounded, authoredTurn,
                leftLocked, rightLocked;
            public float speed, filteredTurn, headHeight, headPitchDegrees, leftIkWeight,
                rightIkWeight, leftFootError, rightFootError, leftSlopeDegrees,
                rightSlopeDegrees, mantleProgress, magicSampleTime, handConstraintWeight;
            public float eammWeight;
            public int contactFrame, sampleFrame, finalGraphEvaluations, weightedContactPasses;
        }

        [Serializable] public sealed class FrameRecord
        {
            public string label, scenario, path, requestedUtc, completedUtc;
            public int screenWidth, screenHeight, requestRenderFrame, screenshotRenderFrame,
                completedRenderFrame;
            public long pngBytes;
            public Vector3 cameraPosition, cameraEuler;
            public ActorRecord[] actors;
        }

        [Serializable] public sealed class Report
        {
            public string schema = "animation-actual-game-capture-v1";
            public string startedUtc, completedUtc, unityVersion, scene, status, error, scope;
            public bool actualGameScreenCapture, productionCameraRigLeftEnabled,
                debugUiRestored, probesRestored, completeRequiredMatrix;
            public string[] missingRequiredLabels, temporarilyHiddenUi;
            public FrameRecord[] frames;
        }

        public static bool IsRunning => _running;
        public static bool IsReadyForNext => _running && string.IsNullOrEmpty(_pendingLabel);
        public static string OutputFolder => _folder;

        public static bool TryValidateFraming(out string reason)
        {
            reason = string.Empty;
            if (!_running || _camera == null)
            {
                reason = "The capture session or production camera is unavailable.";
                return false;
            }
            List<HumanoidCharacterPresentation> actors = FindInScene<HumanoidCharacterPresentation>();
            if (actors.Count != 2)
            {
                reason = "Expected two production actors; found " + actors.Count + ".";
                return false;
            }
            foreach (HumanoidCharacterPresentation actor in actors)
            {
                if (!TryActorFraming(actor, out _, out _, out _, out _, out _, out string actorReason))
                {
                    reason = actor.transform.root.name + ": " + actorReason;
                    return false;
                }
            }
            return true;
        }

        public static void Begin()
        {
            if (_running) throw new InvalidOperationException("Animation capture session is already running.");
            if (!Application.isPlaying || EditorApplication.isPaused)
                throw new InvalidOperationException("Begin in unpaused Play Mode after Earth readiness.");
            _scene = SceneManager.GetActiveScene();
            if (_scene.path != ScenePath) throw new InvalidOperationException("EarthCoreSlice must be active.");
            var gate = FindUnique<EarthSceneReadinessGate>();
            if (!gate.IsReady || gate.Failed) throw new InvalidOperationException("Earth readiness gate has not passed.");
            _camera = Camera.main;
            if (_camera == null || !_camera.isActiveAndEnabled || _camera.gameObject.scene != _scene ||
                _camera.targetTexture != null)
                throw new InvalidOperationException("An active production Main Camera rendering the Game view is required.");
            bool liveRig = FindInScene<PlanetCameraRig>().Exists(rig => rig.enabled) ||
                           FindInScene<EarthCinemachineCameraController>().Exists(rig => rig.enabled);
            if (!liveRig)
                throw new InvalidOperationException("A live production Planet/Cinemachine camera rig is required; the recorder does not substitute a QA camera.");
            var presentations = FindInScene<HumanoidCharacterPresentation>();
            if (presentations.Count != 2)
                throw new InvalidOperationException("The final audit requires exactly two production humanoid presentations.");

            string stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
            _folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "BuildReports",
                "EnvironmentAnimationRescue", "AnimationVisualFinal", stamp);
            Directory.CreateDirectory(_folder);
            Frames.Clear(); UiStates.Clear(); ProbeScenarios.Clear(); AddedProbes.Clear();
            var hidden = new List<string>();
            try
            {
                foreach (Behaviour behaviour in Object.FindObjectsByType<Behaviour>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (behaviour.gameObject.scene != _scene ||
                        (!(behaviour is UIDocument) && !HiddenUiTypes.Contains(behaviour.GetType().FullName)))
                        continue;
                    UiStates.Add(behaviour, behaviour.enabled);
                    if (behaviour.enabled) hidden.Add(behaviour.GetType().FullName + " @ " + behaviour.name);
                    behaviour.enabled = false;
                }
                foreach (HumanoidCharacterPresentation presentation in presentations)
                {
                    EarthAnimationPoseProbe probe = presentation.GetComponent<EarthAnimationPoseProbe>();
                    if (probe == null) { probe = presentation.gameObject.AddComponent<EarthAnimationPoseProbe>(); AddedProbes.Add(probe); }
                    else ProbeScenarios.Add(probe, probe.Scenario);
                }
                _report = new Report {
                    startedUtc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                    scene = _scene.path, status = "Recording", actualGameScreenCapture = true,
                    productionCameraRigLeftEnabled = true, temporarilyHiddenUi = hidden.ToArray(),
                    scope = "Actual Game-view ScreenCapture using the live production Main Camera and camera rig. " +
                        "The recorder changes no camera pose, lens, actor input, body, animation state, fixture, clock, light, material or scene asset. " +
                        "Its caller owns deterministic production input/temporary fixtures and must restore them in finally. " +
                        "Every frame records both actors; images and manifest still require human visual inspection."
                };
                _running = true; _pendingLabel = null; _renderFrame = 0; _lastUnityFrame = -1;
                Type gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (gameView == null) throw new InvalidOperationException("Unity Game view was unavailable.");
                EditorWindow.GetWindow(gameView).Show(); EditorWindow.GetWindow(gameView).Focus();
                RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
                EditorApplication.update += Tick;
                EditorApplication.playModeStateChanged += OnPlayMode;
                AssemblyReloadEvents.beforeAssemblyReload += Abort;
                Save();
            }
            catch { Restore(); throw; }
        }

        public static void Capture(string label, string scenario)
        {
            if (!_running) throw new InvalidOperationException("Call Begin first.");
            if (!string.IsNullOrEmpty(_pendingLabel)) throw new InvalidOperationException("Wait until IsReadyForNext before requesting another frame.");
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("Capture label is required.");
            foreach (char character in label)
                if (!(char.IsLetterOrDigit(character) || character is '-' or '_'))
                    throw new ArgumentException("Capture labels may contain only letters, digits, hyphen and underscore.");
            if (Frames.Exists(frame => string.Equals(frame.label, label, StringComparison.Ordinal)))
                throw new InvalidOperationException("Capture labels must be unique within a session: " + label);
            _pendingLabel = label; _pendingScenario = string.IsNullOrWhiteSpace(scenario) ? label : scenario;
            _pendingPath = Path.Combine(_folder, label + ".png");
            _requestedUtc = DateTime.UtcNow; _screenshotIssued = false;
            foreach (EarthAnimationPoseProbe probe in AllProbes()) probe.Scenario = _pendingScenario;
        }

        public static void Finish()
        {
            if (!_running) return;
            if (!string.IsNullOrEmpty(_pendingLabel)) throw new InvalidOperationException("Wait for the pending PNG before Finish.");
            FinishInternal("Captured", null);
        }

        public static void Abort()
        { if (_running) FinishInternal("Aborted", "Capture session was interrupted."); }

        public static void AbortWithError(string error)
        { if (_running) FinishInternal("Failed", string.IsNullOrWhiteSpace(error) ? "Capture failed." : error); }

        private static void OnPlayMode(PlayModeStateChange state)
        { if (state == PlayModeStateChange.ExitingPlayMode) Abort(); }

        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!_running || camera != _camera) return;
            if (_lastUnityFrame == Time.frameCount) return;
            _lastUnityFrame = Time.frameCount; _renderFrame++;
        }

        private static void Tick()
        {
            if (!_running || string.IsNullOrEmpty(_pendingLabel)) return;
            try
            {
                if (!Application.isPlaying || _camera == null) throw new InvalidOperationException("Play scene or Main Camera disappeared.");
                if (!_screenshotIssued)
                {
                    // Snapshot telemetry when requesting the end-of-frame Game capture;
                    // animation can continue while Unity writes the PNG asynchronously.
                    _issuedActors = SampleActors();
                    _issuedCameraPosition = _camera.transform.position;
                    _issuedCameraEuler = _camera.transform.eulerAngles;
                    _issuedWidth = Screen.width; _issuedHeight = Screen.height;
                    ScreenCapture.CaptureScreenshot(_pendingPath, 1);
                    _issueRenderFrame = _renderFrame; _screenshotIssued = true; return;
                }
                if (_renderFrame - _issueRenderFrame < WriteFrames) return;
                var file = new FileInfo(_pendingPath);
                if (!file.Exists || file.Length == 0 || file.LastWriteTimeUtc < _requestedUtc) return;
                Frames.Add(new FrameRecord {
                    label = _pendingLabel, scenario = _pendingScenario,
                    path = file.FullName.Replace('\\', '/'), requestedUtc = _requestedUtc.ToString("O"),
                    completedUtc = DateTime.UtcNow.ToString("O"), screenWidth = _issuedWidth,
                    screenHeight = _issuedHeight, requestRenderFrame = _issueRenderFrame,
                    screenshotRenderFrame = _issueRenderFrame + 1, completedRenderFrame = _renderFrame,
                    pngBytes = file.Length, cameraPosition = _issuedCameraPosition,
                    cameraEuler = _issuedCameraEuler, actors = _issuedActors
                });
                _pendingLabel = null; _pendingScenario = null; _pendingPath = null; Save();
            }
            catch (Exception exception) { FinishInternal("Failed", exception.ToString()); }
        }

        private static ActorRecord[] SampleActors()
        {
            var rows = new List<ActorRecord>(2);
            foreach (HumanoidCharacterPresentation presentation in FindInScene<HumanoidCharacterPresentation>())
            {
                PlanetMotor motor = presentation.GetComponentInParent<PlanetMotor>();
                Animator animator = presentation.Animator;
                EarthAnimationPoseProbe probe = presentation.GetComponent<EarthAnimationPoseProbe>();
                EarthAnimationPoseSample sample = probe != null ? probe.Latest : default;
                Transform chest = animator != null && animator.isHuman
                    ? animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest)
                    : presentation.transform;
                TryActorFraming(presentation, out Vector3 viewport, out Vector3 headViewport,
                    out Vector3 leftFootViewport, out Vector3 rightFootViewport,
                    out bool lineOfSight, out _);
                bool fullBodyFramed = KeyPointInside(headViewport) && KeyPointInside(leftFootViewport) &&
                                      KeyPointInside(rightFootViewport);
                AnimatorClipInfo[] clips = animator != null ? animator.GetCurrentAnimatorClipInfo(0) : Array.Empty<AnimatorClipInfo>();
                string clip = "none"; float weight = -1f;
                foreach (AnimatorClipInfo info in clips) if (info.clip != null && info.weight > weight) { weight = info.weight; clip = info.clip.name; }
                rows.Add(new ActorRecord {
                    actor = presentation.transform.root.name, action = presentation.CurrentAuthoredAction.ToString(),
                    footPolicy = presentation.CurrentFootPolicy.ToString(), baseClip = clip,
                    mantlePhase = motor != null ? motor.MantlePhase.ToString() : "None",
                    mantleProgress = motor != null ? motor.MantleProgress : 0f, viewport = viewport,
                    headViewport = headViewport, leftFootViewport = leftFootViewport,
                    rightFootViewport = rightFootViewport, fullBodyFramed = fullBodyFramed,
                    lineOfSight = lineOfSight, visible = fullBodyFramed && lineOfSight,
                    grounded = motor != null && motor.HasStableSupport, speed = sample.speed,
                    filteredTurn = presentation.FilteredTurn, authoredTurn = sample.authoredTurn,
                    eammStatus = sample.eammStatus.ToString(), eammWeight = sample.eammWeight,
                    headHeight = sample.headHeight, headPitchDegrees = sample.headPitchDegrees,
                    leftIkWeight = sample.leftContactWeight, rightIkWeight = sample.rightContactWeight,
                    leftLocked = presentation.FootContactController != null && presentation.FootContactController.LeftFootLocked,
                    rightLocked = presentation.FootContactController != null && presentation.FootContactController.RightFootLocked,
                    leftFootError = sample.leftFootError, rightFootError = sample.rightFootError,
                    leftSlopeDegrees = sample.leftSurfaceSlopeDegrees, rightSlopeDegrees = sample.rightSurfaceSlopeDegrees,
                    magicSampleTime = sample.magicSampleTime, handConstraintWeight = sample.handConstraintWeight,
                    contactFrame = sample.contactFrame, sampleFrame = sample.frame,
                    finalGraphEvaluations = sample.finalGraphEvaluations,
                    weightedContactPasses = sample.weightedContactPasses
                });
            }
            rows.Sort((a, b) => string.CompareOrdinal(a.actor, b.actor));
            return rows.ToArray();
        }

        private static bool TryActorFraming(
            HumanoidCharacterPresentation presentation,
            out Vector3 chestViewport,
            out Vector3 headViewport,
            out Vector3 leftFootViewport,
            out Vector3 rightFootViewport,
            out bool lineOfSight,
            out string reason)
        {
            Animator animator = presentation != null ? presentation.Animator : null;
            PlanetMotor motor = presentation != null ? presentation.GetComponentInParent<PlanetMotor>() : null;
            Transform chest = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest)
                : presentation != null ? presentation.transform : null;
            Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : chest;
            Transform leftFoot = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftFoot) : null;
            Transform rightFoot = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightFoot) : null;
            Vector3 fallback = presentation != null ? presentation.transform.position : Vector3.zero;
            chestViewport = _camera != null ? _camera.WorldToViewportPoint(chest != null ? chest.position : fallback) : default;
            headViewport = _camera != null ? _camera.WorldToViewportPoint(head != null ? head.position : fallback) : default;
            leftFootViewport = _camera != null ? _camera.WorldToViewportPoint(leftFoot != null ? leftFoot.position : fallback) : default;
            rightFootViewport = _camera != null ? _camera.WorldToViewportPoint(rightFoot != null ? rightFoot.position : fallback) : default;
            lineOfSight = HasLineOfSight(chest != null ? chest.position : fallback, motor);
            if (!KeyPointInside(headViewport) || !KeyPointInside(leftFootViewport) || !KeyPointInside(rightFootViewport))
            {
                reason = $"full body is outside Game View (head={headViewport}, leftFoot={leftFootViewport}, rightFoot={rightFootViewport}).";
                return false;
            }
            if (!lineOfSight)
            {
                reason = "production camera line of sight to the chest is obstructed.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private static bool KeyPointInside(Vector3 viewport) =>
            viewport.z > 0f && viewport.x >= .05f && viewport.x <= .95f &&
            viewport.y >= .035f && viewport.y <= .965f;

        private static bool HasLineOfSight(Vector3 target, PlanetMotor motor)
        {
            if (_camera == null) return false;
            Vector3 delta = target - _camera.transform.position;
            float distance = delta.magnitude;
            if (distance <= .01f) return true;
            RaycastHit[] hits = Physics.RaycastAll(
                _camera.transform.position, delta / distance, distance - .01f, ~0,
                QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null) continue;
                Transform transform = hit.collider.transform;
                if (motor != null && (transform == motor.transform || transform.IsChildOf(motor.transform))) continue;
                return false;
            }
            return true;
        }

        private static void FinishInternal(string status, string error)
        {
            _running = false;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= OnPlayMode;
            AssemblyReloadEvents.beforeAssemblyReload -= Abort;
            var missing = new List<string>();
            foreach (string required in RequiredLabels)
                if (!Frames.Exists(frame => frame.label.StartsWith(required, StringComparison.Ordinal))) missing.Add(required);
            _report.status = status; _report.error = error; _report.completedUtc = DateTime.UtcNow.ToString("O");
            _report.missingRequiredLabels = missing.ToArray();
            _report.completeRequiredMatrix = status == "Captured" && missing.Count == 0;
            Restore(); Save();
            Debug.Log("[AnimationVisualQa] " + status + "; completeMatrix=" + _report.completeRequiredMatrix + "; " + _folder);
        }

        private static void Restore()
        {
            foreach (var pair in ProbeScenarios) if (pair.Key != null) pair.Key.Scenario = pair.Value;
            foreach (EarthAnimationPoseProbe probe in AddedProbes) if (probe != null) Object.Destroy(probe);
            if (_report != null) _report.probesRestored = true;
            foreach (var pair in UiStates) if (pair.Key != null) pair.Key.enabled = pair.Value;
            if (_report != null) _report.debugUiRestored = true;
            ProbeScenarios.Clear(); AddedProbes.Clear(); UiStates.Clear();
        }

        private static EarthAnimationPoseProbe[] AllProbes()
        {
            var probes = new List<EarthAnimationPoseProbe>();
            probes.AddRange(ProbeScenarios.Keys); probes.AddRange(AddedProbes); return probes.ToArray();
        }

        private static void Save()
        { if (_report != null && !string.IsNullOrEmpty(_folder)) { _report.frames = Frames.ToArray(); File.WriteAllText(Path.Combine(_folder, "CaptureManifest.json"), JsonUtility.ToJson(_report, true)); } }

        private static T FindUnique<T>() where T : Component
        {
            List<T> values = FindInScene<T>();
            if (values.Count != 1) throw new InvalidOperationException("Expected one " + typeof(T).Name + "; found " + values.Count + ".");
            return values[0];
        }

        private static List<T> FindInScene<T>() where T : Component
        {
            var values = new List<T>();
            foreach (T candidate in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (candidate.gameObject.scene == _scene) values.Add(candidate);
            return values;
        }
    }
}
