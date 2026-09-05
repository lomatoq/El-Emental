using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Elemental.Input.Gestures;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Camera;
using Elemental.Presentation.MotionMatching;
using Elemental.Runtime.Characters;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Elemental.Experimental.SonicPrototype
{
    internal static class SonicProductionActorPreview
    {
        private const string Menu =
            "Elemental/Experimental/SONIC/5 Preview SONIC On Production Actor";
        private const double TimeoutSeconds = 30.0;
        private static Session _session;

        [MenuItem(Menu, priority = 2204)]
        private static void Begin()
        {
            if (!EditorApplication.isPlaying || _session != null)
            {
                Debug.LogError("Start Play Mode and wait for any previous SONIC preview to finish.");
                return;
            }

            var inputs = UnityEngine.Object.FindObjectsByType<MagicInputController>(
                FindObjectsInactive.Exclude);
            HumanoidCharacterPresentation presentation = null;
            for (int index = 0; index < inputs.Length; index++)
            {
                if (!inputs[index].isActiveAndEnabled) continue;
                presentation = inputs[index].GetComponentInChildren<HumanoidCharacterPresentation>(true);
                if (presentation != null) break;
            }
            if (presentation == null)
            {
                Debug.LogError("No active production player with MagicInputController and Humanoid presentation was found.");
                return;
            }

            Animator animator = presentation.Animator != null
                ? presentation.Animator
                : presentation.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                Debug.LogError("The production player does not expose a valid Humanoid Animator.", presentation);
                return;
            }
            if (animator.GetComponent<SonicPlannerPreviewAdapter>() != null)
            {
                Debug.LogError("The production Animator already has a SONIC preview adapter; refusing to replace it.", animator);
                return;
            }

            ModelAsset model = AssetDatabase.LoadAssetAtPath<ModelAsset>(
                SonicPlannerImportAndBenchmark.ImportedModelAssetPath);
            if (model == null)
            {
                Debug.LogError("Import the pinned SONIC model with menu 1 before starting the production-actor preview.");
                return;
            }

            var profile = ScriptableObject.CreateInstance<SonicHumanoidRetargetProfile>();
            profile.hideFlags = HideFlags.DontSave;
            SonicPlannerPreviewAdapter adapter = null;
            PreviewVisualScope visualScope = null;
            try
            {
                profile.CaptureFromAvatarDefinition(animator);
                visualScope = new PreviewVisualScope(presentation, animator);
                var bridge = animator.GetComponent<EAMMBasePoseBridge>();
                adapter = animator.gameObject.AddComponent<SonicPlannerPreviewAdapter>();
                adapter.hideFlags = HideFlags.DontSave;
                bool bridgeWasEnabled = bridge != null && bridge.enabled;
                if (!adapter.ConfigureAndStartPreview(
                        model,
                        profile,
                        SonicPreviewMode.Walk,
                        BackendType.CPU,
                        followPlanetMotorDirection: false))
                {
                    throw new InvalidOperationException("Adapter start failed: " + adapter.Status);
                }

                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
                string directory = Path.GetFullPath(Path.Combine(
                    Application.dataPath, "..", "BuildReports", "SonicPrototype", "ProductionActorPreview", stamp));
                Directory.CreateDirectory(directory);
                _session = new Session(
                    presentation,
                    animator,
                    bridge,
                    bridgeWasEnabled,
                    adapter,
                    profile,
                    visualScope,
                    directory,
                    EditorApplication.timeSinceStartup);
                EditorApplication.update += Tick;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
                Debug.Log($"SONIC bounded production-actor preview started on {presentation.name}. " +
                          $"Walk and boxing captures will be written under {directory}.", presentation);
            }
            catch (Exception exception)
            {
                if (adapter != null)
                {
                    adapter.StopAndReleasePreview();
                    UnityEngine.Object.Destroy(adapter);
                }
                visualScope?.Dispose();
                UnityEngine.Object.Destroy(profile);
                Debug.LogError("SONIC production-actor preview could not start: " + exception, presentation);
            }
        }

        [MenuItem(Menu, validate = true)]
        private static bool CanBegin() => EditorApplication.isPlaying && _session == null;

        private static void Tick()
        {
            Session session = _session;
            if (session == null) return;
            try
            {
                double now = EditorApplication.timeSinceStartup;
                if (now - session.StartedAt > TimeoutSeconds)
                    throw new TimeoutException("SONIC preview did not complete within 30 seconds. Status: " + session.Adapter.Status);

                switch (session.Phase)
                {
                    case PreviewPhase.WaitingForWalk:
                        if (session.Adapter.AcceptedSequence <= 0 || session.Adapter.ActiveFrameCount <= 0 ||
                            session.Adapter.RetargetApplicationCount <= 0) return;
                        session.WalkSequence = session.Adapter.AcceptedSequence;
                        session.WalkStatus = session.Adapter.Status;
                        session.PhaseStartedAt = now;
                        session.Phase = PreviewPhase.WaitingForWalkSettle;
                        break;

                    case PreviewPhase.WaitingForWalkSettle:
                        if (now - session.PhaseStartedAt < .4) return;
                        session.WalkPose = SampleAndValidatePose(session, "walk", 0);
                        session.WalkCapturePath = Path.Combine(session.Directory, "walk.png");
                        ScreenCapture.CaptureScreenshot(session.WalkCapturePath, 1);
                        session.PhaseStartedAt = now;
                        session.Phase = PreviewPhase.WaitingForWalkCapture;
                        break;

                    case PreviewPhase.WaitingForWalkCapture:
                        if (!File.Exists(session.WalkCapturePath))
                        {
                            if (now - session.PhaseStartedAt < 2.0) return;
                            throw new IOException("Walk screenshot was not written within two seconds.");
                        }
                        session.Adapter.SetPreviewMode(SonicPreviewMode.RandomPunches);
                        session.Phase = PreviewPhase.WaitingForBoxing;
                        break;

                    case PreviewPhase.WaitingForBoxing:
                        if (session.Adapter.AcceptedSequence <= session.WalkSequence ||
                            session.Adapter.ActiveFrameCount <= 0 ||
                            session.Adapter.RetargetApplicationCount <= session.WalkPose.retargetApplicationCount) return;
                        session.BoxingSequence = session.Adapter.AcceptedSequence;
                        session.BoxingStatus = session.Adapter.Status;
                        session.PhaseStartedAt = now;
                        session.Phase = PreviewPhase.WaitingForBoxingSettle;
                        break;

                    case PreviewPhase.WaitingForBoxingSettle:
                        if (now - session.PhaseStartedAt < .4) return;
                        session.BoxingPose = SampleAndValidatePose(
                            session,
                            "boxing",
                            session.WalkPose.retargetApplicationCount);
                        session.BoxingCapturePath = Path.Combine(session.Directory, "boxing.png");
                        ScreenCapture.CaptureScreenshot(session.BoxingCapturePath, 1);
                        session.PhaseStartedAt = now;
                        session.Phase = PreviewPhase.WaitingForBoxingCapture;
                        break;

                    case PreviewPhase.WaitingForBoxingCapture:
                        if (!File.Exists(session.BoxingCapturePath))
                        {
                            if (now - session.PhaseStartedAt < 2.0) return;
                            throw new IOException("Boxing screenshot was not written within two seconds.");
                        }
                        session.Adapter.StopAndReleasePreview();
                        Complete("Passed", string.Empty);
                        break;
                }
            }
            catch (Exception exception)
            {
                Complete("Failed", exception.ToString());
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode || change == PlayModeStateChange.EnteredEditMode)
                Complete("Interrupted", "Play Mode ended before the bounded preview completed.");
        }

        private static void Complete(string status, string error)
        {
            Session session = _session;
            if (session == null) return;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            _session = null;

            if (session.Adapter != null)
            {
                session.Adapter.StopAndReleasePreview();
                session.Adapter.enabled = false;
            }
            bool finalBridgeRestored = session.Bridge == null || session.Bridge.enabled == session.BridgeWasEnabled;
            session.VisualScope.Dispose();
            if (!session.VisualScope.Restored && string.Equals(status, "Passed", StringComparison.Ordinal))
            {
                status = "Failed";
                error = "Production camera or UI state was not restored.";
            }
            var report = new PreviewReport
            {
                status = status,
                error = error,
                completedUtc = DateTime.UtcNow.ToString("O"),
                actor = session.Presentation != null ? session.Presentation.name : "destroyed",
                avatar = session.Animator != null && session.Animator.avatar != null
                    ? session.Animator.avatar.name
                    : "destroyed",
                backend = BackendType.CPU.ToString(),
                walkSequence = session.WalkSequence,
                boxingSequence = session.BoxingSequence,
                walkStatus = session.WalkStatus,
                boxingStatus = session.BoxingStatus,
                walkCapturePath = session.WalkCapturePath,
                boxingCapturePath = session.BoxingCapturePath,
                walkCaptureExists = !string.IsNullOrEmpty(session.WalkCapturePath) && File.Exists(session.WalkCapturePath),
                boxingCaptureExists = !string.IsNullOrEmpty(session.BoxingCapturePath) && File.Exists(session.BoxingCapturePath),
                walkPose = session.WalkPose,
                boxingPose = session.BoxingPose,
                bridgeWasEnabled = session.BridgeWasEnabled,
                bridgeRestored = finalBridgeRestored,
                cameraAndUiRestored = session.VisualScope.Restored,
                runtimeOnlyObjects = true,
            };
            File.WriteAllText(Path.Combine(session.Directory, "PreviewReport.json"), JsonUtility.ToJson(report, true));

            if (session.Adapter != null) UnityEngine.Object.Destroy(session.Adapter);
            if (session.Profile != null) UnityEngine.Object.Destroy(session.Profile);
            if (string.Equals(status, "Passed", StringComparison.Ordinal) &&
                report.bridgeRestored && report.cameraAndUiRestored)
                Debug.Log("SONIC bounded production-actor preview passed. Report: " + session.Directory);
            else
                Debug.LogError("SONIC bounded production-actor preview ended with " + status + ". Report: " + session.Directory + "\n" + error);
        }

        private static PoseEvidence SampleAndValidatePose(
            Session session,
            string label,
            int minimumPreviousApplications)
        {
            Animator animator = session.Animator;
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (head == null || leftFoot == null || rightFoot == null)
                throw new InvalidOperationException(label + " pose is missing Humanoid head/feet.");

            PlanetMotor motor = session.Presentation.GetComponentInParent<PlanetMotor>();
            Vector3 up = motor != null ? motor.LocalUp.normalized : animator.transform.up;
            Vector3 feet = (leftFoot.position + rightFoot.position) * .5f;
            float headHeight = Vector3.Dot(head.position - feet, up);
            UnityEngine.Camera camera = session.VisualScope.Camera;
            Vector3 headViewport = camera.WorldToViewportPoint(head.position);
            Vector3 leftViewport = camera.WorldToViewportPoint(leftFoot.position);
            Vector3 rightViewport = camera.WorldToViewportPoint(rightFoot.position);
            float footViewportY = Mathf.Min(leftViewport.y, rightViewport.y);
            float viewportHeight = headViewport.y - footViewportY;
            int applied = session.Adapter.RetargetApplicationCount;
            bool recentRetarget = session.Adapter.LastRetargetFrame >= Time.frameCount - 3;
            bool visible = Inside(headViewport) && Inside(leftViewport) && Inside(rightViewport) &&
                           viewportHeight >= .32f;
            float minimumAnatomicalHeight = Mathf.Max(.55f, session.VisualScope.BaselineHeadHeight * .80f);
            bool anatomyValid = float.IsFinite(headHeight) && headHeight >= minimumAnatomicalHeight &&
                                headHeight <= session.VisualScope.BaselineHeadHeight * 1.25f;
            if (applied <= minimumPreviousApplications || !recentRetarget)
                throw new InvalidOperationException(
                    $"{label} produced inference but no current-frame Humanoid retarget application " +
                    $"(count={applied}, lastFrame={session.Adapter.LastRetargetFrame}, frame={Time.frameCount}).");
            if (!anatomyValid)
                throw new InvalidOperationException(
                    $"{label} retarget anatomy collapsed or stretched: headHeight={headHeight:F3}m, " +
                    $"baseline={session.VisualScope.BaselineHeadHeight:F3}m.");
            if (!visible)
                throw new InvalidOperationException(
                    $"{label} full body is not reviewable: head={headViewport}, leftFoot={leftViewport}, " +
                    $"rightFoot={rightViewport}, viewportHeight={viewportHeight:F3}.");

            return new PoseEvidence
            {
                retargetApplicationCount = applied,
                lastRetargetFrame = session.Adapter.LastRetargetFrame,
                maximumAppliedBoneDeltaDegrees = session.Adapter.LastRetargetMaxBoneDeltaDegrees,
                headHeightMeters = headHeight,
                baselineHeadHeightMeters = session.VisualScope.BaselineHeadHeight,
                headHeightRatio = headHeight / Mathf.Max(.001f, session.VisualScope.BaselineHeadHeight),
                viewportHeight = viewportHeight,
                headViewport = headViewport,
                leftFootViewport = leftViewport,
                rightFootViewport = rightViewport,
                fullBodyVisible = visible,
                anatomyValid = anatomyValid,
            };
        }

        private static bool Inside(Vector3 point) =>
            point.z > 0f && point.x >= .04f && point.x <= .96f &&
            point.y >= .025f && point.y <= .975f;

        private sealed class PreviewVisualScope : IDisposable
        {
            private readonly Dictionary<Behaviour, bool> _ui = new Dictionary<Behaviour, bool>();
            private EarthCinemachineCameraController _controller;
            private FieldInfo _trackingHeightField;
            private FieldInfo _neutralPitchField;
            private object _thirdPersonFollow;
            private FieldInfo _cameraDistanceField;
            private float _savedTrackingHeight;
            private float _savedNeutralPitch;
            private float _savedCameraDistance;
            private bool _cameraAdjusted;
            private bool _disposed;

            public UnityEngine.Camera Camera { get; private set; }
            public float BaselineHeadHeight { get; }
            public bool Restored { get; private set; }

            public PreviewVisualScope(HumanoidCharacterPresentation presentation, Animator animator)
            {
                BaselineHeadHeight = MeasureHeadHeight(presentation, animator);
                try
                {
                    Camera = UnityEngine.Camera.main;
                    if (Camera == null)
                        throw new InvalidOperationException("Production Main Camera was not found.");

                    foreach (Behaviour behaviour in UnityEngine.Object.FindObjectsByType<Behaviour>(
                                 FindObjectsInactive.Include))
                    {
                        if (behaviour == null || behaviour.gameObject.scene != presentation.gameObject.scene)
                            continue;
                        if (behaviour is UIDocument || behaviour is Canvas || IsDebugOverlay(behaviour.GetType().FullName))
                        {
                            _ui[behaviour] = behaviour.enabled;
                            behaviour.enabled = false;
                        }
                    }

                    foreach (EarthCinemachineCameraController candidate in
                             UnityEngine.Object.FindObjectsByType<EarthCinemachineCameraController>(
                                 FindObjectsInactive.Include))
                    {
                        if (candidate.gameObject.scene != presentation.gameObject.scene) continue;
                        _controller = candidate;
                        break;
                    }
                    if (_controller == null)
                        throw new InvalidOperationException("Production Cinemachine controller was not found.");

                    const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
                    _trackingHeightField = typeof(EarthCinemachineCameraController).GetField("trackingHeight", instance);
                    _neutralPitchField = typeof(EarthCinemachineCameraController).GetField("neutralPitch", instance);
                    FieldInfo followField = typeof(EarthCinemachineCameraController).GetField("thirdPersonFollow", instance);
                    if (_trackingHeightField == null || _neutralPitchField == null || followField == null)
                        throw new InvalidOperationException("Production camera composition fields are unavailable.");
                    _thirdPersonFollow = followField.GetValue(_controller);
                    _cameraDistanceField = _thirdPersonFollow?.GetType().GetField(
                        "CameraDistance", BindingFlags.Instance | BindingFlags.Public);
                    if (_thirdPersonFollow == null || _cameraDistanceField == null)
                        throw new InvalidOperationException("Production third-person camera distance is unavailable.");

                    _savedTrackingHeight = (float)_trackingHeightField.GetValue(_controller);
                    _savedNeutralPitch = (float)_neutralPitchField.GetValue(_controller);
                    _savedCameraDistance = (float)_cameraDistanceField.GetValue(_thirdPersonFollow);
                    _cameraAdjusted = true;
                    _trackingHeightField.SetValue(_controller, -.55f);
                    _neutralPitchField.SetValue(_controller, 7f);
                    _cameraDistanceField.SetValue(_thirdPersonFollow, Mathf.Min(_savedCameraDistance, 4.2f));
                    _controller.SnapToTarget();
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                bool restored = true;
                try
                {
                    if (_cameraAdjusted && _controller != null)
                    {
                        _trackingHeightField?.SetValue(_controller, _savedTrackingHeight);
                        _neutralPitchField?.SetValue(_controller, _savedNeutralPitch);
                        _cameraDistanceField?.SetValue(_thirdPersonFollow, _savedCameraDistance);
                        _controller.SnapToTarget();
                    }
                }
                catch (Exception exception)
                {
                    restored = false;
                    Debug.LogException(exception);
                }
                finally
                {
                    foreach (KeyValuePair<Behaviour, bool> pair in _ui)
                    {
                        if (pair.Key == null) continue;
                        try { pair.Key.enabled = pair.Value; }
                        catch (Exception exception)
                        {
                            restored = false;
                            Debug.LogException(exception);
                        }
                    }
                    _ui.Clear();
                    Restored = restored;
                }
            }

            private static float MeasureHeadHeight(
                HumanoidCharacterPresentation presentation,
                Animator animator)
            {
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                if (head == null || leftFoot == null || rightFoot == null)
                    throw new InvalidOperationException("Humanoid head/feet are required for anatomy validation.");
                PlanetMotor motor = presentation.GetComponentInParent<PlanetMotor>();
                Vector3 up = motor != null ? motor.LocalUp.normalized : animator.transform.up;
                float height = Vector3.Dot(head.position - (leftFoot.position + rightFoot.position) * .5f, up);
                if (!float.IsFinite(height) || height < .55f)
                    throw new InvalidOperationException($"Production actor baseline anatomy is invalid ({height:F3}m). ");
                return height;
            }

            private static bool IsDebugOverlay(string typeName) =>
                typeName == "Elemental.Presentation.VFX.RumbleLookdevRuntime" ||
                typeName == "Elemental.Presentation.UI.EarthPolishLabController" ||
                typeName == "Elemental.Presentation.UI.BendingDebugOverlay";
        }

        [Serializable]
        private sealed class PoseEvidence
        {
            public int retargetApplicationCount;
            public int lastRetargetFrame;
            public float maximumAppliedBoneDeltaDegrees;
            public float headHeightMeters;
            public float baselineHeadHeightMeters;
            public float headHeightRatio;
            public float viewportHeight;
            public Vector3 headViewport;
            public Vector3 leftFootViewport;
            public Vector3 rightFootViewport;
            public bool fullBodyVisible;
            public bool anatomyValid;
        }

        private enum PreviewPhase
        {
            WaitingForWalk,
            WaitingForWalkSettle,
            WaitingForWalkCapture,
            WaitingForBoxing,
            WaitingForBoxingSettle,
            WaitingForBoxingCapture,
        }

        private sealed class Session
        {
            public readonly HumanoidCharacterPresentation Presentation;
            public readonly Animator Animator;
            public readonly EAMMBasePoseBridge Bridge;
            public readonly bool BridgeWasEnabled;
            public readonly SonicPlannerPreviewAdapter Adapter;
            public readonly SonicHumanoidRetargetProfile Profile;
            public readonly PreviewVisualScope VisualScope;
            public readonly string Directory;
            public readonly double StartedAt;
            public PreviewPhase Phase;
            public double PhaseStartedAt;
            public int WalkSequence;
            public int BoxingSequence;
            public string WalkStatus = string.Empty;
            public string BoxingStatus = string.Empty;
            public string WalkCapturePath = string.Empty;
            public string BoxingCapturePath = string.Empty;
            public PoseEvidence WalkPose;
            public PoseEvidence BoxingPose;

            public Session(
                HumanoidCharacterPresentation presentation,
                Animator animator,
                EAMMBasePoseBridge bridge,
                bool bridgeWasEnabled,
                SonicPlannerPreviewAdapter adapter,
                SonicHumanoidRetargetProfile profile,
                PreviewVisualScope visualScope,
                string directory,
                double startedAt)
            {
                Presentation = presentation;
                Animator = animator;
                Bridge = bridge;
                BridgeWasEnabled = bridgeWasEnabled;
                Adapter = adapter;
                Profile = profile;
                VisualScope = visualScope;
                Directory = directory;
                StartedAt = startedAt;
                Phase = PreviewPhase.WaitingForWalk;
            }
        }

        [Serializable]
        private sealed class PreviewReport
        {
            public string status;
            public string error;
            public string completedUtc;
            public string actor;
            public string avatar;
            public string backend;
            public int walkSequence;
            public int boxingSequence;
            public string walkStatus;
            public string boxingStatus;
            public string walkCapturePath;
            public string boxingCapturePath;
            public bool walkCaptureExists;
            public bool boxingCaptureExists;
            public PoseEvidence walkPose;
            public PoseEvidence boxingPose;
            public bool bridgeWasEnabled;
            public bool bridgeRestored;
            public bool cameraAndUiRestored;
            public bool runtimeOnlyObjects;
        }
    }
}
