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
        private const double TimeoutSeconds = 35.0;
        private const double BoxingEvidenceSeconds = 2.55;
        private static readonly double[] BoxingCaptureOffsets = { .55, 1.35, 2.35 };
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
                var ownershipBaseline = new ProductionOwnershipBaseline(presentation, animator);
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
                    ownershipBaseline,
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
                        session.VisualScope.ComposeForCapture();
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
                        session.BeginBoxingEvidence();
                        session.Phase = PreviewPhase.SamplingBoxing;
                        break;

                    case PreviewPhase.SamplingBoxing:
                        SampleBoxingMotion(session, now);
                        if (session.BoxingCaptureIndex >= BoxingCaptureOffsets.Length)
                        {
                            if (now - session.PhaseStartedAt < BoxingEvidenceSeconds) return;
                            FinishBoxingAndComplete(session, now);
                            break;
                        }
                        if (now - session.PhaseStartedAt < BoxingCaptureOffsets[session.BoxingCaptureIndex]) return;
                        session.VisualScope.ComposeForCapture();
                        PoseEvidence boxingPose = SampleAndValidatePose(
                            session,
                            $"boxing-phase-{session.BoxingCaptureIndex + 1}",
                            session.WalkPose.retargetApplicationCount);
                        string boxingPath = Path.Combine(
                            session.Directory,
                            $"boxing-phase-{session.BoxingCaptureIndex + 1}.png");
                        session.PendingBoxingCapturePath = boxingPath;
                        session.BoxingPhases.Add(new BoxingPhaseEvidence
                        {
                            phase = session.BoxingCaptureIndex + 1,
                            elapsedSeconds = (float)(now - session.PhaseStartedAt),
                            sampledFrame = Time.frameCount,
                            leftHandExcursionMeters = session.CurrentLeftExcursion,
                            rightHandExcursionMeters = session.CurrentRightExcursion,
                            capturePath = boxingPath,
                            pose = boxingPose,
                        });
                        ScreenCapture.CaptureScreenshot(boxingPath, 1);
                        session.CaptureRequestedAt = now;
                        session.Phase = PreviewPhase.WaitingForBoxingCapture;
                        break;

                    case PreviewPhase.WaitingForBoxingCapture:
                        SampleBoxingMotion(session, now);
                        if (!File.Exists(session.PendingBoxingCapturePath))
                        {
                            if (now - session.CaptureRequestedAt < 2.0) return;
                            throw new IOException("A boxing phase screenshot was not written within two seconds.");
                        }
                        session.BoxingPhases[session.BoxingPhases.Count - 1].captureExists = true;
                        session.BoxingCaptureIndex++;
                        if (session.BoxingCaptureIndex < BoxingCaptureOffsets.Length)
                        {
                            session.Phase = PreviewPhase.SamplingBoxing;
                            break;
                        }

                        if (now - session.PhaseStartedAt < BoxingEvidenceSeconds)
                        {
                            session.Phase = PreviewPhase.SamplingBoxing;
                            break;
                        }
                        FinishBoxingAndComplete(session, now);
                        break;
                }
            }
            catch (Exception exception)
            {
                Complete("Failed", exception.ToString());
            }
        }

        private static void FinishBoxingAndComplete(Session session, double now)
        {
            session.FinishBoxingEvidence(now);
            session.Adapter.StopAndReleasePreview();
            Complete("Passed", string.Empty);
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
                boxingPhases = session.BoxingPhases.ToArray(),
                boxingMotion = session.BoxingMotion,
                bridgeWasEnabled = session.BridgeWasEnabled,
                bridgeRestored = finalBridgeRestored,
                cameraAndUiRestored = session.VisualScope.Restored,
                runtimeOnlyObjects = true,
            };
            File.WriteAllText(Path.Combine(session.Directory, "PreviewReport.json"), JsonUtility.ToJson(report, true));

            session.DisposeDynamicFrame();
            if (session.Adapter != null) UnityEngine.Object.Destroy(session.Adapter);
            if (session.Profile != null) UnityEngine.Object.Destroy(session.Profile);
            if (string.Equals(status, "Passed", StringComparison.Ordinal) &&
                report.bridgeRestored && report.cameraAndUiRestored)
                Debug.Log("SONIC bounded production-actor preview passed. Report: " + session.Directory);
            else
                Debug.LogError("SONIC bounded production-actor preview ended with " + status + ". Report: " + session.Directory + "\n" + error);
        }

        private static void SampleBoxingMotion(Session session, double now)
        {
            if (session.LastBoxingSampleFrame == Time.frameCount) return;
            session.LastBoxingSampleFrame = Time.frameCount;

            Vector3 left = session.Chest.InverseTransformPoint(session.LeftHand.position);
            Vector3 right = session.Chest.InverseTransformPoint(session.RightHand.position);
            session.BoxingSampleCount++;
            session.LastBoxingSampleAt = now;
            session.LeftRelativeMin = Vector3.Min(session.LeftRelativeMin, left);
            session.LeftRelativeMax = Vector3.Max(session.LeftRelativeMax, left);
            session.RightRelativeMin = Vector3.Min(session.RightRelativeMin, right);
            session.RightRelativeMax = Vector3.Max(session.RightRelativeMax, right);
            session.LeftDistanceMin = Mathf.Min(session.LeftDistanceMin, left.magnitude);
            session.LeftDistanceMax = Mathf.Max(session.LeftDistanceMax, left.magnitude);
            session.RightDistanceMin = Mathf.Min(session.RightDistanceMin, right.magnitude);
            session.RightDistanceMax = Mathf.Max(session.RightDistanceMax, right.magnitude);
            session.CurrentLeftExcursion = Vector3.Distance(left, session.LeftRelativeStart);
            session.CurrentRightExcursion = Vector3.Distance(right, session.RightRelativeStart);
            session.MaximumLeftExcursion = Mathf.Max(session.MaximumLeftExcursion, session.CurrentLeftExcursion);
            session.MaximumRightExcursion = Mathf.Max(session.MaximumRightExcursion, session.CurrentRightExcursion);
            Vector3 currentUp = session.Motor != null
                ? session.Motor.LocalUp.normalized
                : session.Animator.transform.up;
            Vector3 feet = (session.LeftFoot.position + session.RightFoot.position) * .5f;
            float headHeight = Vector3.Dot(session.Head.position - feet, currentUp);
            session.MinimumHeadHeight = Mathf.Min(session.MinimumHeadHeight, headHeight);
            session.MaximumHeadHeight = Mathf.Max(session.MaximumHeadHeight, headHeight);
            session.MaximumRootPositionDrift = Mathf.Max(
                session.MaximumRootPositionDrift,
                Vector3.Distance(session.Animator.transform.localPosition, session.InitialAnimatorLocalPosition));
            session.MaximumRootRotationDrift = Mathf.Max(
                session.MaximumRootRotationDrift,
                Quaternion.Angle(session.Animator.transform.localRotation, session.InitialAnimatorLocalRotation));
            session.MaximumLocalUpDrift = Mathf.Max(
                session.MaximumLocalUpDrift,
                Vector3.Angle(session.InitialLocalUp, currentUp));
            session.FootOwnershipPreservedThroughout &=
                session.Presentation.FootContactController == session.InitialFootOwner &&
                (session.InitialFootOwner == null ||
                 session.InitialFootOwner.enabled == session.InitialFootOwnerEnabled);

            float dynamicScore = Mathf.Max(session.CurrentLeftExcursion, session.CurrentRightExcursion);
            if (dynamicScore <= session.DynamicScore + .005f || now - session.LastDynamicCaptureAt < .08) return;
            session.VisualScope.ComposeForCapture();
            Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
            if (frame == null) return;
            if (session.DynamicFrame != null) UnityEngine.Object.Destroy(session.DynamicFrame);
            session.DynamicFrame = frame;
            session.DynamicScore = dynamicScore;
            session.DynamicFrameNumber = Time.frameCount;
            session.DynamicElapsedSeconds = (float)(now - session.PhaseStartedAt);
            session.LastDynamicCaptureAt = now;
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
            Bounds skinnedBounds = MeasureSkinnedBounds(animator);
            MeasureViewportBounds(camera, skinnedBounds, out Vector3 boundsViewportMin, out Vector3 boundsViewportMax);
            bool skinnedBoundsVisible = boundsViewportMin.z > 0f &&
                                        boundsViewportMin.x >= .01f && boundsViewportMax.x <= .99f &&
                                        boundsViewportMin.y >= .01f && boundsViewportMax.y <= .99f;
            int applied = session.Adapter.RetargetApplicationCount;
            bool recentRetarget = session.Adapter.LastRetargetFrame >= Time.frameCount - 3;
            bool visible = Inside(headViewport) && Inside(leftViewport) && Inside(rightViewport) &&
                           viewportHeight >= .32f && skinnedBoundsVisible;
            float minimumAnatomicalHeight = Mathf.Max(.55f, session.VisualScope.BaselineHeadHeight * .80f);
            bool anatomyValid = float.IsFinite(headHeight) && headHeight >= minimumAnatomicalHeight &&
                                headHeight <= session.VisualScope.BaselineHeadHeight * 1.25f;
            float rootPositionDrift = Vector3.Distance(
                animator.transform.localPosition,
                session.InitialAnimatorLocalPosition);
            float rootRotationDrift = Quaternion.Angle(
                animator.transform.localRotation,
                session.InitialAnimatorLocalRotation);
            Vector3 currentUp = motor != null ? motor.LocalUp.normalized : animator.transform.up;
            float localUpDrift = Vector3.Angle(session.InitialLocalUp, currentUp);
            bool footOwnerPreserved = session.Presentation.FootContactController == session.InitialFootOwner &&
                                      (session.InitialFootOwner == null ||
                                       session.InitialFootOwner.enabled == session.InitialFootOwnerEnabled);
            bool ownershipValid = rootPositionDrift <= .001f && rootRotationDrift <= .1f &&
                                  localUpDrift <= .1f && footOwnerPreserved;
            if (applied <= minimumPreviousApplications || !recentRetarget)
                throw new InvalidOperationException(
                    $"{label} produced inference but no current-frame Humanoid retarget application " +
                    $"(count={applied}, lastFrame={session.Adapter.LastRetargetFrame}, frame={Time.frameCount}).");
            if (!anatomyValid)
            {
                WriteAnatomyDiagnostic(session, label);
                throw new InvalidOperationException(
                    $"{label} retarget anatomy collapsed or stretched: headHeight={headHeight:F3}m, " +
                    $"baseline={session.VisualScope.BaselineHeadHeight:F3}m.");
            }
            if (!visible)
                throw new InvalidOperationException(
                    $"{label} full body is not reviewable: head={headViewport}, leftFoot={leftViewport}, " +
                    $"rightFoot={rightViewport}, viewportHeight={viewportHeight:F3}, " +
                    $"skinnedBounds={boundsViewportMin}..{boundsViewportMax}.");
            if (!ownershipValid)
                throw new InvalidOperationException(
                    $"{label} changed production root or foot ownership: localPosition={rootPositionDrift:F5}m, " +
                    $"localRotation={rootRotationDrift:F3}deg, localUp={localUpDrift:F3}deg, " +
                    $"footOwnerPreserved={footOwnerPreserved}.");

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
                skinnedBoundsViewportMin = boundsViewportMin,
                skinnedBoundsViewportMax = boundsViewportMax,
                fullSkinnedBoundsVisible = skinnedBoundsVisible,
                rootLocalPositionDriftMeters = rootPositionDrift,
                rootLocalRotationDriftDegrees = rootRotationDrift,
                localUpDriftDegrees = localUpDrift,
                footOwnerPreserved = footOwnerPreserved,
                ownershipValid = ownershipValid,
                fullBodyVisible = visible,
                anatomyValid = anatomyValid,
            };
        }

        private static Bounds MeasureSkinnedBounds(Animator animator)
        {
            SkinnedMeshRenderer[] renderers = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            bool found = false;
            Bounds combined = default;
            for (int index = 0; index < renderers.Length; index++)
            {
                SkinnedMeshRenderer renderer = renderers[index];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!found)
                {
                    combined = renderer.bounds;
                    found = true;
                }
                else combined.Encapsulate(renderer.bounds);
            }
            if (!found)
                throw new InvalidOperationException("Production actor has no active SkinnedMeshRenderer bounds.");
            return combined;
        }

        private static void MeasureViewportBounds(
            UnityEngine.Camera camera,
            Bounds bounds,
            out Vector3 viewportMin,
            out Vector3 viewportMax)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            viewportMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            viewportMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                Vector3 viewport = camera.WorldToViewportPoint(corner);
                viewportMin = Vector3.Min(viewportMin, viewport);
                viewportMax = Vector3.Max(viewportMax, viewport);
            }
        }

        private static bool Inside(Vector3 point) =>
            point.z > 0f && point.x >= .04f && point.x <= .96f &&
            point.y >= .025f && point.y <= .975f;

        private static void WriteAnatomyDiagnostic(Session session, string label)
        {
            var output = new System.Text.StringBuilder();
            var adapter = session.Adapter;
            output.AppendLine($"applications={adapter.RetargetApplicationCount} maxDelta={adapter.LastRetargetMaxBoneDeltaDegrees} referenceCaptured={adapter.RetargetReferenceCaptured} restVsReference={adapter.MaximumAvatarRestToRuntimeReferenceDegrees}");
            output.AppendLine($"bodyPosition={session.Animator.bodyPosition} bodyRotation={session.Animator.bodyRotation.eulerAngles} pelvisIK={session.Presentation.FootContactController?.PelvisCorrectionMeters}");
            var field = typeof(SonicPlannerPreviewAdapter).GetField("_samplePose", BindingFlags.NonPublic | BindingFlags.Instance);
            output.AppendLine("qpos=" + string.Join(",", (float[])field.GetValue(adapter)));
            foreach (HumanBodyBones id in new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Head, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot })
            {
                Transform bone = session.Animator.GetBoneTransform(id);
                output.AppendLine($"{id}: local={bone.localRotation.eulerAngles} modelPosition={session.Animator.transform.InverseTransformPoint(bone.position)}");
            }
            File.WriteAllText(Path.Combine(session.Directory, label + "FailedAnatomy.txt"), output.ToString());
            Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
            if (frame != null)
            {
                File.WriteAllBytes(Path.Combine(session.Directory, label + "FailedAnatomy.png"), frame.EncodeToPNG());
                UnityEngine.Object.Destroy(frame);
            }
        }

        private sealed class PreviewVisualScope : IDisposable
        {
            private readonly Dictionary<Behaviour, bool> _ui = new Dictionary<Behaviour, bool>();
            private readonly HumanoidCharacterPresentation _presentation;
            private readonly Animator _animator;
            private EarthCinemachineCameraController _controller;
            private bool _controllerWasEnabled;
            private FieldInfo _trackingHeightField;
            private FieldInfo _neutralPitchField;
            private object _thirdPersonFollow;
            private FieldInfo _cameraDistanceField;
            private float _savedTrackingHeight;
            private float _savedNeutralPitch;
            private float _savedCameraDistance;
            private bool _cameraAdjusted;
            private bool _disposed;
            private Vector3 _savedCameraPosition;
            private Quaternion _savedCameraRotation;
            private float _savedCameraFov;

            public UnityEngine.Camera Camera { get; private set; }
            public float BaselineHeadHeight { get; }
            public bool Restored { get; private set; }

            public PreviewVisualScope(HumanoidCharacterPresentation presentation, Animator animator)
            {
                _presentation = presentation;
                _animator = animator;
                BaselineHeadHeight = MeasureHeadHeight(presentation, animator);
                try
                {
                    Camera = UnityEngine.Camera.main;
                    if (Camera == null)
                        throw new InvalidOperationException("Production Main Camera was not found.");
                    _savedCameraPosition = Camera.transform.position;
                    _savedCameraRotation = Camera.transform.rotation;
                    _savedCameraFov = Camera.fieldOfView;

                    foreach (Behaviour behaviour in UnityEngine.Object.FindObjectsByType<Behaviour>(
                                 FindObjectsInactive.Include))
                    {
                        if (behaviour == null || behaviour.gameObject.scene != presentation.gameObject.scene)
                            continue;
                        if (behaviour is UIDocument || behaviour is Canvas || IsDebugOverlay(behaviour.GetType().FullName) ||
                            IsCameraOwner(behaviour.GetType().Name))
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
                    _cameraDistanceField.SetValue(_thirdPersonFollow, 4.5f);
                    _controller.SnapToTarget();
                    // Keep this diagnostic composition stable: the ordinary camera
                    // owner restores its authored follow distance each Update.
                    _controllerWasEnabled = _controller.enabled;
                    _controller.enabled = false;
                    _cameraDistanceField.SetValue(_thirdPersonFollow, 4.5f);
                    ComposeForCapture();
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public void ComposeForCapture()
            {
                if (_disposed || Camera == null || _animator == null || _presentation == null) return;
                Transform head = _animator.GetBoneTransform(HumanBodyBones.Head);
                Transform leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
                if (head == null || leftFoot == null || rightFoot == null)
                    throw new InvalidOperationException("Production camera composition requires Humanoid head and feet.");
                Vector3 feet = (leftFoot.position + rightFoot.position) * .5f;
                Vector3 focus = (head.position + feet) * .5f;
                PlanetMotor motor = _presentation.GetComponentInParent<PlanetMotor>();
                Vector3 up = motor != null ? motor.LocalUp.normalized : _animator.transform.up;
                Vector3 forward = motor != null ? motor.FacingForward : _animator.transform.forward;
                Vector3 viewSide = Vector3.Cross(up, forward).normalized;
                Vector3 position = focus - forward * 4.5f + viewSide * .60f + up * .12f;
                Camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(focus - position, up));
                Camera.fieldOfView = 58f;
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
                        _controller.enabled = _controllerWasEnabled;
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
                    if (Camera != null)
                    {
                        Camera.transform.SetPositionAndRotation(_savedCameraPosition, _savedCameraRotation);
                        Camera.fieldOfView = _savedCameraFov;
                    }
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

            private static bool IsCameraOwner(string name) =>
                name == "CinemachineBrain" || name == "EarthCinemachineCameraController" ||
                name == "PlanetCameraRig" || name == "EarthChargeCameraLookdevV2" ||
                name == "EarthCameraDirector" || name == "VisualQaCaptureBehaviour";
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
            public Vector3 skinnedBoundsViewportMin;
            public Vector3 skinnedBoundsViewportMax;
            public bool fullSkinnedBoundsVisible;
            public float rootLocalPositionDriftMeters;
            public float rootLocalRotationDriftDegrees;
            public float localUpDriftDegrees;
            public bool footOwnerPreserved;
            public bool ownershipValid;
            public bool fullBodyVisible;
            public bool anatomyValid;
        }

        [Serializable]
        private sealed class BoxingPhaseEvidence
        {
            public int phase;
            public float elapsedSeconds;
            public int sampledFrame;
            public float leftHandExcursionMeters;
            public float rightHandExcursionMeters;
            public string capturePath;
            public bool captureExists;
            public PoseEvidence pose;
        }

        [Serializable]
        private sealed class BoxingMotionEvidence
        {
            public int sampledFrames;
            public float sampledSeconds;
            public Vector3 leftHandRelativeChestMin;
            public Vector3 leftHandRelativeChestMax;
            public Vector3 rightHandRelativeChestMin;
            public Vector3 rightHandRelativeChestMax;
            public float leftHandSpatialRangeMeters;
            public float rightHandSpatialRangeMeters;
            public float leftHandMinimumChestDistanceMeters;
            public float leftHandMaximumChestDistanceMeters;
            public float rightHandMinimumChestDistanceMeters;
            public float rightHandMaximumChestDistanceMeters;
            public float leftHandMaximumExcursionMeters;
            public float rightHandMaximumExcursionMeters;
            public int selectedDynamicFrame;
            public float selectedDynamicElapsedSeconds;
            public float selectedDynamicScoreMeters;
            public string selectedDynamicCapturePath;
            public bool durationValid;
            public bool bilateralMotionValid;
            public int acceptedSequences;
            public bool rollingReplanningObserved;
            public float minimumHeadHeightMeters;
            public float maximumHeadHeightMeters;
            public float maximumRootLocalPositionDriftMeters;
            public float maximumRootLocalRotationDriftDegrees;
            public float maximumLocalUpDriftDegrees;
            public bool footOwnershipPreservedThroughout;
            public bool anatomyAndOwnershipValidThroughout;
            public bool valid;
        }

        private sealed class ProductionOwnershipBaseline
        {
            public readonly Vector3 AnimatorLocalPosition;
            public readonly Quaternion AnimatorLocalRotation;
            public readonly Vector3 LocalUp;
            public readonly EarthFootContactController FootOwner;
            public readonly bool FootOwnerEnabled;

            public ProductionOwnershipBaseline(HumanoidCharacterPresentation presentation, Animator animator)
            {
                AnimatorLocalPosition = animator.transform.localPosition;
                AnimatorLocalRotation = animator.transform.localRotation;
                PlanetMotor motor = presentation.GetComponentInParent<PlanetMotor>();
                LocalUp = motor != null ? motor.LocalUp.normalized : animator.transform.up;
                FootOwner = presentation.FootContactController;
                FootOwnerEnabled = FootOwner != null && FootOwner.enabled;
            }
        }

        private enum PreviewPhase
        {
            WaitingForWalk,
            WaitingForWalkSettle,
            WaitingForWalkCapture,
            WaitingForBoxing,
            SamplingBoxing,
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
            public string PendingBoxingCapturePath = string.Empty;
            public PoseEvidence WalkPose;
            public PoseEvidence BoxingPose;
            public readonly List<BoxingPhaseEvidence> BoxingPhases = new List<BoxingPhaseEvidence>(3);
            public BoxingMotionEvidence BoxingMotion;
            public int BoxingCaptureIndex;
            public double CaptureRequestedAt;
            public Transform Chest;
            public Transform LeftHand;
            public Transform RightHand;
            public Transform Head;
            public Transform LeftFoot;
            public Transform RightFoot;
            public PlanetMotor Motor;
            public Vector3 LeftRelativeStart;
            public Vector3 RightRelativeStart;
            public Vector3 LeftRelativeMin;
            public Vector3 LeftRelativeMax;
            public Vector3 RightRelativeMin;
            public Vector3 RightRelativeMax;
            public float LeftDistanceMin;
            public float LeftDistanceMax;
            public float RightDistanceMin;
            public float RightDistanceMax;
            public float CurrentLeftExcursion;
            public float CurrentRightExcursion;
            public float MaximumLeftExcursion;
            public float MaximumRightExcursion;
            public float DynamicScore;
            public int DynamicFrameNumber;
            public float DynamicElapsedSeconds;
            public Texture2D DynamicFrame;
            public double LastDynamicCaptureAt;
            public int LastBoxingSampleFrame = -1;
            public int BoxingSampleCount;
            public double LastBoxingSampleAt;
            public float MinimumHeadHeight = float.PositiveInfinity;
            public float MaximumHeadHeight = float.NegativeInfinity;
            public float MaximumRootPositionDrift;
            public float MaximumRootRotationDrift;
            public float MaximumLocalUpDrift;
            public bool FootOwnershipPreservedThroughout = true;
            public readonly Vector3 InitialAnimatorLocalPosition;
            public readonly Quaternion InitialAnimatorLocalRotation;
            public readonly Vector3 InitialLocalUp;
            public readonly EarthFootContactController InitialFootOwner;
            public readonly bool InitialFootOwnerEnabled;

            public Session(
                HumanoidCharacterPresentation presentation,
                Animator animator,
                EAMMBasePoseBridge bridge,
                bool bridgeWasEnabled,
                SonicPlannerPreviewAdapter adapter,
                SonicHumanoidRetargetProfile profile,
                PreviewVisualScope visualScope,
                ProductionOwnershipBaseline ownershipBaseline,
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
                InitialAnimatorLocalPosition = ownershipBaseline.AnimatorLocalPosition;
                InitialAnimatorLocalRotation = ownershipBaseline.AnimatorLocalRotation;
                InitialLocalUp = ownershipBaseline.LocalUp;
                InitialFootOwner = ownershipBaseline.FootOwner;
                InitialFootOwnerEnabled = ownershipBaseline.FootOwnerEnabled;
            }

            public void BeginBoxingEvidence()
            {
                Chest = Animator.GetBoneTransform(HumanBodyBones.UpperChest) ??
                        Animator.GetBoneTransform(HumanBodyBones.Chest);
                LeftHand = Animator.GetBoneTransform(HumanBodyBones.LeftHand);
                RightHand = Animator.GetBoneTransform(HumanBodyBones.RightHand);
                Head = Animator.GetBoneTransform(HumanBodyBones.Head);
                LeftFoot = Animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                RightFoot = Animator.GetBoneTransform(HumanBodyBones.RightFoot);
                Motor = Presentation.GetComponentInParent<PlanetMotor>();
                if (Chest == null || LeftHand == null || RightHand == null ||
                    Head == null || LeftFoot == null || RightFoot == null)
                    throw new InvalidOperationException(
                        "Boxing evidence requires Humanoid chest, head, both hands, and both feet.");

                LeftRelativeStart = Chest.InverseTransformPoint(LeftHand.position);
                RightRelativeStart = Chest.InverseTransformPoint(RightHand.position);
                LeftRelativeMin = LeftRelativeMax = LeftRelativeStart;
                RightRelativeMin = RightRelativeMax = RightRelativeStart;
                LeftDistanceMin = LeftDistanceMax = LeftRelativeStart.magnitude;
                RightDistanceMin = RightDistanceMax = RightRelativeStart.magnitude;
                LastDynamicCaptureAt = double.NegativeInfinity;
            }

            public void FinishBoxingEvidence(double now)
            {
                if (BoxingSampleCount < 30)
                    throw new InvalidOperationException(
                        $"Boxing trajectory produced only {BoxingSampleCount} distinct rendered samples.");
                float sampledSeconds = (float)(LastBoxingSampleAt - PhaseStartedAt);
                float leftRange = (LeftRelativeMax - LeftRelativeMin).magnitude;
                float rightRange = (RightRelativeMax - RightRelativeMin).magnitude;
                bool durationValid = sampledSeconds >= 2.25f && now - PhaseStartedAt >= BoxingEvidenceSeconds;
                bool bilateralMotion = leftRange >= .08f && rightRange >= .08f &&
                                       Mathf.Max(MaximumLeftExcursion, MaximumRightExcursion) >= .14f;
                int acceptedSequences = Adapter.AcceptedSequence - BoxingSequence + 1;
                bool rollingReplanning = acceptedSequences >= 2;
                float minimumAnatomicalHeight = Mathf.Max(.55f, VisualScope.BaselineHeadHeight * .80f);
                bool trajectoryOwnershipValid =
                    MinimumHeadHeight >= minimumAnatomicalHeight &&
                    MaximumHeadHeight <= VisualScope.BaselineHeadHeight * 1.25f &&
                    MaximumRootPositionDrift <= .001f &&
                    MaximumRootRotationDrift <= .1f &&
                    MaximumLocalUpDrift <= .1f &&
                    FootOwnershipPreservedThroughout;
                if (DynamicFrame == null)
                    throw new InvalidOperationException("Boxing trajectory did not produce a dynamic screen frame.");

                BoxingCapturePath = Path.Combine(Directory, "boxing-dynamic.png");
                File.WriteAllBytes(BoxingCapturePath, DynamicFrame.EncodeToPNG());
                BoxingPose = BoxingPhases.Count > 0
                    ? BoxingPhases[BoxingPhases.Count - 1].pose
                    : null;
                BoxingMotion = new BoxingMotionEvidence
                {
                    sampledFrames = BoxingSampleCount,
                    sampledSeconds = sampledSeconds,
                    leftHandRelativeChestMin = LeftRelativeMin,
                    leftHandRelativeChestMax = LeftRelativeMax,
                    rightHandRelativeChestMin = RightRelativeMin,
                    rightHandRelativeChestMax = RightRelativeMax,
                    leftHandSpatialRangeMeters = leftRange,
                    rightHandSpatialRangeMeters = rightRange,
                    leftHandMinimumChestDistanceMeters = LeftDistanceMin,
                    leftHandMaximumChestDistanceMeters = LeftDistanceMax,
                    rightHandMinimumChestDistanceMeters = RightDistanceMin,
                    rightHandMaximumChestDistanceMeters = RightDistanceMax,
                    leftHandMaximumExcursionMeters = MaximumLeftExcursion,
                    rightHandMaximumExcursionMeters = MaximumRightExcursion,
                    selectedDynamicFrame = DynamicFrameNumber,
                    selectedDynamicElapsedSeconds = DynamicElapsedSeconds,
                    selectedDynamicScoreMeters = DynamicScore,
                    selectedDynamicCapturePath = BoxingCapturePath,
                    durationValid = durationValid,
                    bilateralMotionValid = bilateralMotion,
                    acceptedSequences = acceptedSequences,
                    rollingReplanningObserved = rollingReplanning,
                    minimumHeadHeightMeters = MinimumHeadHeight,
                    maximumHeadHeightMeters = MaximumHeadHeight,
                    maximumRootLocalPositionDriftMeters = MaximumRootPositionDrift,
                    maximumRootLocalRotationDriftDegrees = MaximumRootRotationDrift,
                    maximumLocalUpDriftDegrees = MaximumLocalUpDrift,
                    footOwnershipPreservedThroughout = FootOwnershipPreservedThroughout,
                    anatomyAndOwnershipValidThroughout = trajectoryOwnershipValid,
                    valid = durationValid && bilateralMotion && rollingReplanning && trajectoryOwnershipValid,
                };
                if (!BoxingMotion.valid)
                    throw new InvalidOperationException(
                        $"Boxing trajectory was not visibly bilateral over {sampledSeconds:F2}s: " +
                        $"leftRange={leftRange:F3}m, rightRange={rightRange:F3}m, " +
                        $"leftExcursion={MaximumLeftExcursion:F3}m, rightExcursion={MaximumRightExcursion:F3}m, " +
                        $"acceptedSequences={acceptedSequences}, minimumHead={MinimumHeadHeight:F3}m, " +
                        $"rootDrift={MaximumRootPositionDrift:F5}m, footOwner={FootOwnershipPreservedThroughout}.");
            }

            public void DisposeDynamicFrame()
            {
                if (DynamicFrame == null) return;
                UnityEngine.Object.Destroy(DynamicFrame);
                DynamicFrame = null;
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
            public BoxingPhaseEvidence[] boxingPhases;
            public BoxingMotionEvidence boxingMotion;
            public bool bridgeWasEnabled;
            public bool bridgeRestored;
            public bool cameraAndUiRestored;
            public bool runtimeOnlyObjects;
        }
    }
}
