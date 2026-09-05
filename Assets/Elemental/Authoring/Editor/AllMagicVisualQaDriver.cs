// Staged outside Assets while Unity QA owns the compilation window.
// Copy to Assets/Elemental/Authoring/Editor, refresh, then invoke the MenuItem below.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Camera;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Camera = UnityEngine.Camera;
using Object = UnityEngine.Object;

namespace Elemental.Authoring.Editor
{
    /// <summary>
    /// Captures the production player's complete eleven-slot semantic magic matrix.
    /// This is visual evidence for direct presentation clips. Shipping quick-stone
    /// LMB/RMB grammar and dual-button overlap remain covered by runtime tests.
    /// </summary>
    [InitializeOnLoad]
    public static class AllMagicVisualQaDriver
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const string ArmedKey = "Elemental.AllMagicVisualQa.Armed";
        private const string MagicLayerName = "Earth Magic Upper Body";
        private const int ScreenshotWriteFrames = 4;
        private const int NormalRate = 60;
        private static readonly EarthTechniqueId[] Techniques =
        {
            EarthTechniqueId.RaiseWall,
            EarthTechniqueId.RaisePlatform,
            EarthTechniqueId.PullStone,
            EarthTechniqueId.ThrowStone,
            EarthTechniqueId.VectorPush,
            EarthTechniqueId.Repair,
            EarthTechniqueId.Resonance,
            EarthTechniqueId.PillarJump,
            EarthTechniqueId.Armor,
            EarthTechniqueId.ArmorBarrage,
            EarthTechniqueId.QuickStonePunch
        };

        private static readonly List<FrameRecord> Frames = new();
        private static readonly Dictionary<Behaviour, bool> DisabledBehaviours = new();
        private static readonly Dictionary<Behaviour, bool> HiddenUi = new();
        private static readonly List<EarthAnimationPoseProbe> AddedProbes = new();
        private static readonly Dictionary<EarthAnimationPoseProbe, string> ProbeScenarios = new();

        private static bool _running;
        private static bool _pendingScreenshot;
        private static bool _clockFrozen;
        private static bool _cameraAdjusted;
        private static bool _runtimeStateSaved;
        private static bool _finishedSuccessfully;
        private static int _slotIndex;
        private static int _repeatIndex;
        private static int _renderFrame;
        private static int _lastUnityFrame = -1;
        private static int _issuedRenderFrame;
        private static int _savedTargetFrameRate;
        private static uint _sequence;
        private static double _stateDeadline;
        private static double _runDeadline;
        private static float _savedTimeScale;
        private static float _savedFixedDeltaTime;
        private static float _savedTrackingHeight;
        private static float _savedNeutralPitch;
        private static string _outputFolder;
        private static string _pendingPath;
        private static string _pendingLabel;
        private static string _pendingStage;
        private static DateTime _screenshotRequestedUtc;
        private static Stage _stage;
        private static Scene _scene;
        private static Camera _camera;
        private static HumanoidCharacterPresentation _presentation;
        private static EarthCharacterPoseController _pose;
        private static EarthAnimationDriver _driver;
        private static EarthAnimationPoseProbe _probe;
        private static PlanetMotor _motor;
        private static Rigidbody _body;
        private static Animator _animator;
        private static int _magicLayer;
        private static QuietInput _quietInput;
        private static MonoBehaviour _savedInput;
        private static Vector3 _savedBodyPosition;
        private static Quaternion _savedBodyRotation;
        private static Vector3 _savedVelocity;
        private static Vector3 _savedAngularVelocity;
        private static EarthCinemachineCameraController _cameraController;
        private static FieldInfo _trackingHeightField;
        private static FieldInfo _neutralPitchField;
        private static FrameRecord _issuedFrame;
        private static Report _report;

        static AllMagicVisualQaDriver()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Elemental/QA/Capture All 11 Magic Animation Matrix")]
        public static void CaptureAll()
        {
            if (_running || SessionState.GetBool(ArmedKey, false))
                throw new InvalidOperationException("The all-magic capture is already running.");
            if (SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open EarthCoreSlice before starting the capture.");
            SessionState.SetBool(ArmedKey, true);
            if (Application.isPlaying) StartRun();
            else EditorApplication.isPlaying = true;
        }

        [MenuItem("Elemental/QA/Abort All 11 Magic Animation Matrix")]
        public static void Abort() => EndRun("Aborted", "Capture was aborted by the operator.");

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(ArmedKey, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode) StartRun();
            else if (change == PlayModeStateChange.ExitingPlayMode && _running)
                EndRun("Failed", "Play Mode exited before the capture completed.");
        }

        private static void StartRun()
        {
            if (_running) return;
            _running = true;
            _finishedSuccessfully = false;
            _stage = Stage.Ready;
            _runDeadline = Now + 150d;
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += Abort;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private static void Tick()
        {
            if (!_running) return;
            try
            {
                if (Now > _runDeadline)
                    throw new TimeoutException("All-magic capture timed out in " + _stage + ".");
                if (_stage == Stage.Ready)
                {
                    EarthSceneReadinessGate gate = Object.FindAnyObjectByType<EarthSceneReadinessGate>(
                        FindObjectsInactive.Include);
                    if (gate == null || gate.Failed)
                        throw new InvalidOperationException("Earth scene readiness failed.");
                    if (!gate.IsReady) return;
                    Setup();
                    StartSlot(0, 0);
                    return;
                }
                if (_pendingScreenshot)
                {
                    PollScreenshot();
                    return;
                }
                if (Now > _stateDeadline)
                    throw new TimeoutException("Magic stage timed out: " + _stage + " for slot " + (_slotIndex + 1) + ".");
                DriveStage();
            }
            catch (Exception exception)
            {
                EndRun("Failed", exception.ToString());
            }
        }

        private static void Setup()
        {
            _scene = SceneManager.GetActiveScene();
            if (_scene.path != ScenePath) throw new InvalidOperationException("EarthCoreSlice is not active.");
            _camera = Camera.main;
            if (_camera == null || !_camera.isActiveAndEnabled || _camera.gameObject.scene != _scene ||
                _camera.targetTexture != null)
                throw new InvalidOperationException("A live production Main Camera rendering the Game view is required.");
            if (!HasLiveProductionCameraRig())
                throw new InvalidOperationException("The production camera rig must remain enabled.");

            foreach (HumanoidCharacterPresentation candidate in Object.FindObjectsByType<HumanoidCharacterPresentation>(
                         FindObjectsInactive.Include))
            {
                if (candidate.gameObject.scene == _scene && candidate.PoseController != null)
                {
                    if (_presentation != null)
                        throw new InvalidOperationException("Expected one production player presentation.");
                    _presentation = candidate;
                }
            }
            if (_presentation == null) throw new InvalidOperationException("Production player presentation was not found.");
            _pose = _presentation.PoseController;
            _driver = _presentation.GetComponent<EarthAnimationDriver>();
            _animator = _presentation.Animator;
            _motor = _presentation.GetComponentInParent<PlanetMotor>();
            _body = _motor != null ? _motor.GetComponent<Rigidbody>() : null;
            if (_pose == null || _driver == null || _animator == null || !_animator.isHuman ||
                _motor == null || _body == null)
                throw new InvalidOperationException("The production player animation stack is incomplete.");
            if (_pose.PresentationSuppressed || _pose.HasAuthoritativePresentation || _motor.IsMantling)
                throw new InvalidOperationException("Begin the visual audit with the production player idle and animation ownership free.");
            _magicLayer = _animator.GetLayerIndex(MagicLayerName);
            if (_magicLayer < 0) throw new InvalidOperationException("The production magic layer is missing.");

            _savedTargetFrameRate = Application.targetFrameRate;
            _savedFixedDeltaTime = Time.fixedDeltaTime;
            FieldInfo inputField = typeof(PlanetMotor).GetField(
                "inputSourceBehaviour", BindingFlags.Instance | BindingFlags.NonPublic);
            if (inputField == null)
                throw new InvalidOperationException("PlanetMotor input source field was not found.");
            _savedInput = inputField.GetValue(_motor) as MonoBehaviour;
            _savedBodyPosition = _body.position;
            _savedBodyRotation = _body.rotation;
            _savedVelocity = _body.linearVelocity;
            _savedAngularVelocity = _body.angularVelocity;
            _runtimeStateSaved = true;
            Application.targetFrameRate = NormalRate;
            Time.fixedDeltaTime = 1f / NormalRate;
            _quietInput = _motor.gameObject.AddComponent<QuietInput>();
            _motor.ConfigureInputSource(_quietInput);
            if (!_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }

            foreach (Behaviour behaviour in Object.FindObjectsByType<Behaviour>(
                         FindObjectsInactive.Include))
            {
                if (behaviour.gameObject.scene != _scene) continue;
                if (behaviour is EarthMvpBotController || behaviour is EarthMvpDuelController)
                {
                    DisabledBehaviours[behaviour] = behaviour.enabled;
                    behaviour.enabled = false;
                }
                else if (behaviour is UIDocument || IsDebugOverlay(behaviour.GetType().FullName))
                {
                    HiddenUi[behaviour] = behaviour.enabled;
                    behaviour.enabled = false;
                }
            }

            _probe = _presentation.GetComponent<EarthAnimationPoseProbe>();
            if (_probe == null)
            {
                _probe = _presentation.gameObject.AddComponent<EarthAnimationPoseProbe>();
                AddedProbes.Add(_probe);
            }
            else ProbeScenarios[_probe] = _probe.Scenario;
            AdjustCameraFraming();
            _cameraController.SnapToTarget();

            string stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
            _outputFolder = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "BuildReports", "EnvironmentAnimationRescue", "AllMagicVisualQA", stamp);
            Directory.CreateDirectory(_outputFolder);
            Frames.Clear();
            _report = new Report
            {
                schema = "all-magic-animation-visual-qa-v1",
                status = "Recording",
                startedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                scene = _scene.path,
                actor = _presentation.transform.root.name,
                targetFrameRate = NormalRate,
                productionCameraRigLeftEnabled = true,
                scope = "Direct semantic presentation of all eleven production magic slots at normal rate. " +
                        "Each slot records anticipation, rendered contact and recovery. Slot 11 repeats to prove " +
                        "same-slot A/B restart. Physical short-LMB quick stone and simultaneous LMB/RMB routing " +
                        "are covered separately by Animation Punch Continuity Runtime Audit. Human review of PNGs remains required."
            };
            SaveManifest();
        }

        private static void StartSlot(int slotIndex, int repeatIndex)
        {
            _slotIndex = slotIndex;
            _repeatIndex = repeatIndex;
            EarthTechniqueId technique = Techniques[_slotIndex];
            _sequence = 0xac000000u + (uint)(_slotIndex * 4 + _repeatIndex + 1);
            Vector3 up = _motor.LocalUp.normalized;
            Vector3 forward = Vector3.ProjectOnPlane(_motor.FacingForward, up).normalized;
            Vector3 target = _presentation.transform.position + forward * 3f + up * .6f;
            _pose.RequestSemanticPresentation(
                MagicPresentationSemanticResolver.ResolveKind(technique),
                technique,
                _sequence,
                target,
                45f + _slotIndex * 9f,
                5f + _slotIndex);
            _stage = Stage.Anticipation;
            _stateDeadline = Now + 5d;
        }

        private static void DriveStage()
        {
            switch (_stage)
            {
                case Stage.Anticipation:
                {
                    bool current = _pose.LastAuthoritativeTick == _sequence &&
                                   _pose.CurrentRequest.Technique == Techniques[_slotIndex];
                    bool readable = _driver.GetLayerWeight(_magicLayer) >= .28f &&
                                    _presentation.MagicClipTime >= .055f &&
                                    _pose.LastRenderedSemanticWeight >= .70f &&
                                    !_pose.RenderedContactReached;
                    if (current && readable) CaptureCurrent("anticipation");
                    break;
                }
                case Stage.Contact:
                    if (_pose.LastAuthoritativeTick == _sequence && _pose.RenderedContactReached &&
                        _pose.LastRenderedSemanticWeight >= .70f &&
                        _pose.LastRenderedMagicLayerWeight >= .35f)
                        CaptureCurrent("contact");
                    break;
                case Stage.Recovery:
                    if (_pose.LastAuthoritativeTick == _sequence &&
                        _pose.AuthoritativePhase == EarthCastPhase.Recover &&
                        _driver.GetLayerWeight(_magicLayer) >= .18f)
                        CaptureCurrent("recovery");
                    break;
            }
        }

        private static void CaptureCurrent(string stage)
        {
            if (!TrySample(out FrameRecord frame, out string reason))
                throw new InvalidOperationException("Invalid " + stage + " frame for slot " +
                                                    (_slotIndex + 1) + ": " + reason);
            string technique = ToKebab(Techniques[_slotIndex].ToString());
            string repeat = _repeatIndex > 0 ? "-repeat" + _repeatIndex : string.Empty;
            string label = $"slot-{_slotIndex + 1:00}-{technique}{repeat}-{stage}";
            _pendingLabel = label;
            _pendingStage = stage;
            _pendingPath = Path.Combine(_outputFolder, label + ".png");
            _screenshotRequestedUtc = DateTime.UtcNow;
            frame.label = label;
            frame.stage = stage;
            frame.path = _pendingPath.Replace('\\', '/');
            frame.requestedUtc = _screenshotRequestedUtc.ToString("O");
            _issuedFrame = frame;
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _clockFrozen = true;
            ScreenCapture.CaptureScreenshot(_pendingPath, 1);
            _issuedRenderFrame = _renderFrame;
            _pendingScreenshot = true;
        }

        private static void PollScreenshot()
        {
            if (_renderFrame - _issuedRenderFrame < ScreenshotWriteFrames) return;
            var file = new FileInfo(_pendingPath);
            if (!file.Exists || file.Length <= 0 || file.LastWriteTimeUtc < _screenshotRequestedUtc) return;
            _issuedFrame.pngBytes = file.Length;
            _issuedFrame.completedUtc = DateTime.UtcNow.ToString("O");
            _issuedFrame.completedRenderFrame = _renderFrame;
            Frames.Add(_issuedFrame);
            SaveManifest();
            _pendingScreenshot = false;
            _pendingPath = null;
            _pendingLabel = null;
            if (_clockFrozen)
            {
                Time.timeScale = _savedTimeScale;
                _clockFrozen = false;
            }
            AdvanceAfterCapture(_pendingStage);
            _pendingStage = null;
        }

        private static void AdvanceAfterCapture(string capturedStage)
        {
            if (capturedStage == "anticipation")
            {
                _stage = Stage.Contact;
                _stateDeadline = Now + 5d;
                return;
            }
            if (capturedStage == "contact")
            {
                _stage = Stage.Recovery;
                _stateDeadline = Now + 7d;
                return;
            }

            if (_slotIndex == Techniques.Length - 1 && _repeatIndex == 0)
            {
                StartSlot(_slotIndex, 1);
                return;
            }
            if (_slotIndex + 1 < Techniques.Length)
            {
                StartSlot(_slotIndex + 1, 0);
                return;
            }
            ValidateCompletedMatrix();
            EndRun("Captured", null);
        }

        private static bool TrySample(out FrameRecord frame, out string reason)
        {
            frame = null;
            reason = string.Empty;
            Transform head = _animator.GetBoneTransform(HumanBodyBones.Head);
            Transform chest = _animator.GetBoneTransform(HumanBodyBones.UpperChest) ??
                              _animator.GetBoneTransform(HumanBodyBones.Chest);
            Transform leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (head == null || chest == null || leftFoot == null || rightFoot == null)
            {
                reason = "Humanoid head/foot bones are unavailable.";
                return false;
            }
            Vector3 headViewport = _camera.WorldToViewportPoint(head.position);
            Vector3 leftViewport = _camera.WorldToViewportPoint(leftFoot.position);
            Vector3 rightViewport = _camera.WorldToViewportPoint(rightFoot.position);
            if (!Finite(head.position) || !Finite(leftFoot.position) || !Finite(rightFoot.position) ||
                !Finite(headViewport) || !Finite(leftViewport) || !Finite(rightViewport))
            {
                reason = "The final skeleton produced non-finite head or foot data.";
                return false;
            }
            if (!Inside(headViewport) || !Inside(leftViewport) || !Inside(rightViewport))
            {
                reason = $"Full body is not visible (head={headViewport}, left={leftViewport}, right={rightViewport}).";
                return false;
            }
            if (!HasLineOfSight(chest.position))
            {
                reason = "The production camera line of sight to the player's chest is obstructed.";
                return false;
            }
            EarthAnimationPoseSample sample = _probe != null ? _probe.Latest : default;
            if (sample.headPitchDegrees < EarthHeadPitchStabilizer.MinimumPitchDegrees - .5f ||
                sample.headPitchDegrees > EarthHeadPitchStabilizer.MaximumPitchDegrees + .5f)
            {
                reason = $"Rest-calibrated head pitch escaped its expressive envelope: {sample.headPitchDegrees:F2} degrees.";
                return false;
            }
            if (!float.IsFinite(sample.neckLength) || sample.neckLength <= .01f)
            {
                reason = $"Head/neck chain is invalid: neckLength={sample.neckLength}.";
                return false;
            }
            AnimatorStateInfo current = _driver.GetCurrentAnimatorStateInfo(_magicLayer);
            AnimatorStateInfo next = _driver.GetNextAnimatorStateInfo(_magicLayer);
            // EAMM owns the output through AnimatorControllerPlayable. The
            // component Animator does not expose that playable's resident clip
            // info, so querying it produced `none` despite a weighted magic
            // layer. Use the same animation driver that owns states/parameters.
            AnimatorClipInfo[] clips = _driver.GetCurrentAnimatorClipInfo(_magicLayer);
            string dominantClip = "none";
            float dominantClipWeight = -1f;
            for (int index = 0; index < clips.Length; index++)
            {
                if (clips[index].clip == null || clips[index].weight <= dominantClipWeight) continue;
                dominantClip = clips[index].clip.name;
                dominantClipWeight = clips[index].weight;
            }
            frame = new FrameRecord
            {
                slot = _slotIndex + 1,
                repeat = _repeatIndex,
                technique = Techniques[_slotIndex].ToString(),
                sequence = _sequence,
                frame = Time.frameCount,
                renderFrame = _renderFrame,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                targetFrameRate = Application.targetFrameRate,
                deltaTime = Time.deltaTime,
                magicClock = _presentation.MagicClipTime,
                renderedClock = _pose.LastRenderedMagicTime,
                semanticWeight = _pose.LastRenderedSemanticWeight,
                layerWeight = _driver.GetLayerWeight(_magicLayer),
                handConstraintWeight = _presentation.HandConstraintWeight,
                leftFootIkWeight = _pose.LeftFootIkWeight,
                rightFootIkWeight = _pose.RightFootIkWeight,
                headHeight = sample.headHeight,
                headPitchDegrees = sample.headPitchDegrees,
                neckLength = sample.neckLength,
                phase = _pose.AuthoritativePhase.ToString(),
                action = _presentation.CurrentAuthoredAction.ToString(),
                footPolicy = _presentation.CurrentFootPolicy.ToString(),
                dominantClip = dominantClip,
                dominantClipWeight = Mathf.Max(0f, dominantClipWeight),
                inTransition = _driver.IsInTransition(_magicLayer),
                currentStateHash = current.fullPathHash,
                nextStateHash = next.fullPathHash,
                headViewport = headViewport,
                leftFootViewport = leftViewport,
                rightFootViewport = rightViewport,
                finalPoseFinite = true,
                fullBodyVisible = true,
                lineOfSight = true,
                cameraPosition = _camera.transform.position,
                cameraEuler = _camera.transform.eulerAngles
            };
            return true;
        }

        private static void ValidateCompletedMatrix()
        {
            int expected = Techniques.Length * 3 + 3;
            if (Frames.Count != expected)
                throw new InvalidOperationException($"Expected {expected} magic frames; captured {Frames.Count}.");
            for (int slot = 1; slot <= Techniques.Length; slot++)
            {
                Require(slot, 0, "anticipation");
                Require(slot, 0, "contact");
                Require(slot, 0, "recovery");
            }
            Require(11, 1, "anticipation");
            Require(11, 1, "contact");
            Require(11, 1, "recovery");
        }

        private static void Require(int slot, int repeat, string stage)
        {
            if (!Frames.Exists(frame => frame.slot == slot && frame.repeat == repeat && frame.stage == stage &&
                                       frame.pngBytes > 0 && frame.finalPoseFinite && frame.fullBodyVisible))
                throw new InvalidOperationException($"Missing valid slot {slot} repeat {repeat} {stage} capture.");
        }

        private static void EndRun(string status, string error)
        {
            if (!_running && !SessionState.GetBool(ArmedKey, false)) return;
            _running = false;
            _finishedSuccessfully = status == "Captured" && error == null;
            EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= Abort;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            SessionState.EraseBool(ArmedKey);
            if (_clockFrozen)
            {
                Time.timeScale = _savedTimeScale;
                _clockFrozen = false;
            }
            try
            {
                if (_runtimeStateSaved && _pose != null)
                    _pose.CancelPresentationForAnimationOwnership();
                if (_runtimeStateSaved && _motor != null)
                    _motor.ConfigureInputSource(_savedInput);
                if (_quietInput != null) Object.Destroy(_quietInput);
                if (_runtimeStateSaved && _body != null)
                {
                    _body.position = _savedBodyPosition;
                    _body.rotation = _savedBodyRotation;
                    if (!_body.isKinematic)
                    {
                        _body.linearVelocity = _savedVelocity;
                        _body.angularVelocity = _savedAngularVelocity;
                    }
                }
                foreach (KeyValuePair<Behaviour, bool> pair in DisabledBehaviours)
                    if (pair.Key != null) pair.Key.enabled = pair.Value;
                foreach (KeyValuePair<Behaviour, bool> pair in HiddenUi)
                    if (pair.Key != null) pair.Key.enabled = pair.Value;
                foreach (KeyValuePair<EarthAnimationPoseProbe, string> pair in ProbeScenarios)
                    if (pair.Key != null) pair.Key.Scenario = pair.Value;
                foreach (EarthAnimationPoseProbe probe in AddedProbes)
                    if (probe != null) Object.Destroy(probe);
                RestoreCameraFraming();
                if (_runtimeStateSaved)
                {
                    Application.targetFrameRate = _savedTargetFrameRate;
                    Time.fixedDeltaTime = _savedFixedDeltaTime;
                }
                Physics.SyncTransforms();
            }
            finally
            {
                if (_report != null)
                {
                    _report.status = status;
                    _report.error = error;
                    _report.completedUtc = DateTime.UtcNow.ToString("O");
                    _report.completeMatrix = _finishedSuccessfully;
                    _report.restoredTimeAndInput = true;
                    _report.restoredCameraAndUi = true;
                    SaveManifest();
                }
                DisabledBehaviours.Clear();
                HiddenUi.Clear();
                ProbeScenarios.Clear();
                AddedProbes.Clear();
                Debug.Log("[AllMagicVisualQa] " + status + "; " + (error ?? _outputFolder));
                ResetReferences();
            }
            if (Application.isPlaying && EditorApplication.isPlaying)
                EditorApplication.delayCall += StopPlayMode;
        }

        private static void SaveManifest()
        {
            if (_report == null || string.IsNullOrEmpty(_outputFolder)) return;
            _report.frames = Frames.ToArray();
            File.WriteAllText(Path.Combine(_outputFolder, "AllMagicVisualManifest.json"),
                JsonUtility.ToJson(_report, true));
        }

        private static void AdjustCameraFraming()
        {
            _cameraController = Object.FindAnyObjectByType<EarthCinemachineCameraController>(
                FindObjectsInactive.Include);
            if (_cameraController == null)
                throw new InvalidOperationException("Production Cinemachine camera controller was not found.");
            _trackingHeightField = typeof(EarthCinemachineCameraController).GetField(
                "trackingHeight", BindingFlags.Instance | BindingFlags.NonPublic);
            _neutralPitchField = typeof(EarthCinemachineCameraController).GetField(
                "neutralPitch", BindingFlags.Instance | BindingFlags.NonPublic);
            if (_trackingHeightField == null || _neutralPitchField == null)
                throw new InvalidOperationException("Production camera composition fields are unavailable.");
            _savedTrackingHeight = (float)_trackingHeightField.GetValue(_cameraController);
            _savedNeutralPitch = (float)_neutralPitchField.GetValue(_cameraController);
            _cameraAdjusted = true;
            _trackingHeightField.SetValue(_cameraController, -.55f);
            _neutralPitchField.SetValue(_cameraController, 7f);
        }

        private static void RestoreCameraFraming()
        {
            if (_cameraAdjusted && _cameraController != null)
            {
                _trackingHeightField?.SetValue(_cameraController, _savedTrackingHeight);
                _neutralPitchField?.SetValue(_cameraController, _savedNeutralPitch);
                _cameraController.SnapToTarget();
            }
            _cameraAdjusted = false;
        }

        private static bool HasLiveProductionCameraRig()
        {
            foreach (PlanetCameraRig rig in Object.FindObjectsByType<PlanetCameraRig>(
                         FindObjectsInactive.Include))
                if (rig.gameObject.scene == _scene && rig.enabled) return true;
            foreach (EarthCinemachineCameraController rig in Object.FindObjectsByType<EarthCinemachineCameraController>(
                         FindObjectsInactive.Include))
                if (rig.gameObject.scene == _scene && rig.enabled) return true;
            return false;
        }

        private static void OnBeginCameraRendering(ScriptableRenderContext _, Camera camera)
        {
            if (!_running || camera != _camera || _lastUnityFrame == Time.frameCount) return;
            _lastUnityFrame = Time.frameCount;
            _renderFrame++;
        }

        private static bool IsDebugOverlay(string typeName) =>
            typeName == "Elemental.Presentation.VFX.RumbleLookdevRuntime" ||
            typeName == "Elemental.Presentation.UI.EarthPolishLabController" ||
            typeName == "Elemental.Presentation.UI.BendingDebugOverlay";

        private static bool Inside(Vector3 point) => point.z > 0f && point.x >= .04f && point.x <= .96f &&
                                                      point.y >= .025f && point.y <= .975f;
        private static bool HasLineOfSight(Vector3 target)
        {
            Vector3 delta = target - _camera.transform.position;
            float distance = delta.magnitude;
            if (distance <= .01f) return true;
            RaycastHit[] hits = Physics.RaycastAll(
                _camera.transform.position,
                delta / distance,
                distance - .01f,
                ~0,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < hits.Length; index++)
            {
                Collider collider = hits[index].collider;
                if (collider == null) continue;
                Transform hit = collider.transform;
                if (hit == _motor.transform || hit.IsChildOf(_motor.transform)) continue;
                return false;
            }
            return true;
        }
        private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) &&
                                                     float.IsFinite(value.z);
        private static string ToKebab(string value)
        {
            var chars = new List<char>(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (i > 0 && char.IsUpper(current)) chars.Add('-');
                chars.Add(char.ToLowerInvariant(current));
            }
            return new string(chars.ToArray());
        }
        private static double Now => Time.realtimeSinceStartupAsDouble;

        private static void StopPlayMode()
        {
            EditorApplication.delayCall -= StopPlayMode;
            if (Application.isPlaying) EditorApplication.isPlaying = false;
        }

        private static void ResetReferences()
        {
            _presentation = null;
            _pose = null;
            _driver = null;
            _probe = null;
            _motor = null;
            _body = null;
            _animator = null;
            _quietInput = null;
            _savedInput = null;
            _camera = null;
            _cameraController = null;
            _trackingHeightField = null;
            _neutralPitchField = null;
            _report = null;
            _runtimeStateSaved = false;
            _pendingScreenshot = false;
            _outputFolder = null;
        }

        private sealed class QuietInput : MonoBehaviour, IPlanetMotorInputSource
        {
            public PlanetMotorCommand SampleCommand(uint tick) => new(tick, float2.zero, false);
        }

        private enum Stage : byte
        {
            Ready,
            Anticipation,
            Contact,
            Recovery
        }

        [Serializable]
        private sealed class FrameRecord
        {
            public string label, technique, stage, phase, action, footPolicy, dominantClip,
                path, requestedUtc, completedUtc;
            public int slot, repeat, frame, renderFrame, completedRenderFrame, screenWidth, screenHeight,
                targetFrameRate, currentStateHash, nextStateHash;
            public uint sequence;
            public long pngBytes;
            public bool inTransition, finalPoseFinite, fullBodyVisible, lineOfSight;
            public float deltaTime, magicClock, renderedClock, semanticWeight, layerWeight, dominantClipWeight,
                handConstraintWeight, leftFootIkWeight, rightFootIkWeight, headHeight, headPitchDegrees;
            public float neckLength;
            public Vector3 headViewport, leftFootViewport, rightFootViewport, cameraPosition, cameraEuler;
        }

        [Serializable]
        private sealed class Report
        {
            public string schema, status, error, startedUtc, completedUtc, unityVersion, scene, actor, scope;
            public int targetFrameRate;
            public bool productionCameraRigLeftEnabled, completeMatrix, restoredTimeAndInput, restoredCameraAndUi;
            public FrameRecord[] frames;
        }
    }
}
