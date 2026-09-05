using System;
using Elemental.Presentation.Animation;
using Elemental.Presentation.MotionMatching;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Characters;
using Unity.InferenceEngine;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Experimental.SonicPrototype
{
    public enum SonicPreviewMode
    {
        Idle = 0,
        Walk = 2,
        Run = 3,
        IdleBoxing = 9,
        WalkBoxing = 10,
        LeftJab = 11,
        RightJab = 12,
        RandomPunches = 13,
        LeftHook = 15,
        RightHook = 16,
    }

    /// <summary>
    /// Opt-in SONIC locomotion/boxing preview. Inference stays in this component,
    /// PlanetMotor retains root authority, and EarthFootContactController runs its
    /// later final IK pass. This is a diagnostic adapter, not a shipping source.
    /// </summary>
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class SonicPlannerPreviewAdapter : MonoBehaviour
    {
        private static readonly ProfilerMarker PollMarker =
            new ProfilerMarker("Elemental.Experimental.SONIC.Poll");
        private static readonly ProfilerMarker RetargetMarker =
            new ProfilerMarker("Elemental.Experimental.SONIC.Retarget");

        [Header("Explicit experimental ownership")]
        [SerializeField] private bool takeBasePoseOwnership = false;
        [SerializeField] private ModelAsset planner = null;
        [SerializeField] private SonicHumanoidRetargetProfile retargetProfile = null;
        [SerializeField] private BackendType backend = BackendType.CPU;

        [Header("Preview request")]
        [SerializeField] private SonicPreviewMode mode = SonicPreviewMode.Walk;
        [SerializeField, Range(0f, 1f)] private float poseWeight = 1f;
        [SerializeField, Range(.1f, 1f)] private float directionMagnitude = 1f;
        [SerializeField, Min(.1f)] private float planIntervalSeconds = .1f;
        [SerializeField, Min(1)] private int randomSeed = 20260905;
        [SerializeField] private bool usePlanetMotorDirection = true;
        [Tooltip("When no PlanetMotor input is available, advance along G1 +X for an isolated preview.")]
        [SerializeField] private bool useFallbackForwardWithoutInput = true;
        [SerializeField] private bool drawHiddenG1 = false;

        [Header("Existing authorities")]
        [SerializeField] private Animator animator;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private HumanoidCharacterPresentation presentation;
        [SerializeField] private HumanoidRagdollRig ragdoll;
        [SerializeField] private EAMMBasePoseBridge eammBridge;

        private readonly Quaternion[] _sourceWorldRotations = new Quaternion[SonicG1Skeleton.JointCount];
        private readonly Vector3[] _sourceWorldPositions = new Vector3[SonicG1Skeleton.JointCount];
        private readonly Quaternion[] _nextSourceWorldRotations = new Quaternion[SonicG1Skeleton.JointCount];
        private readonly Vector3[] _nextSourceWorldPositions = new Vector3[SonicG1Skeleton.JointCount];
        private readonly float[] _samplePose = new float[SonicG1Skeleton.PoseSize];
        private readonly float[] _nextSamplePose = new float[SonicG1Skeleton.PoseSize];
        private readonly float[] _blendFromPose = new float[SonicG1Skeleton.PoseSize];
        private readonly float[] _contextScratch = new float[4 * SonicG1Skeleton.PoseSize];

        private Worker _worker;
        private SonicPlannerInputs _pendingInputs;
        private Tensor<float> _pendingQpos;
        private Tensor<int> _pendingFrameCount;
        private float[] _activeTrajectory;
        private int _activeFrameCount;
        private float _playbackFrame;
        private float _blendElapsed;
        private float _nextPlanAt;
        private int _pendingGeneration;
        private int _generation;
        private int _sequence;
        private bool _planning;
        private bool _running;
        private bool _protectedLane;
        private bool _disabledEamm;

        public string Status { get; private set; } = "disabled";
        public int AcceptedSequence => _sequence;
        public int ActiveFrameCount => _activeFrameCount;
        public bool OwnsGameplayRoot => false;
        public bool OwnsFinalFootIk => false;
        public bool IsPlanning => _planning;
        public int RetargetApplicationCount { get; private set; }
        public int LastRetargetFrame { get; private set; } = -1;
        public float LastRetargetMaxBoneDeltaDegrees { get; private set; }

        private bool IsBoxing => (int)mode >= 9 && (int)mode <= 16;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (motor == null) motor = GetComponentInParent<PlanetMotor>();
            if (presentation == null) presentation = GetComponent<HumanoidCharacterPresentation>();
            if (ragdoll == null) ragdoll = GetComponent<HumanoidRagdollRig>();
            if (eammBridge == null) eammBridge = GetComponent<EAMMBasePoseBridge>();
        }

        private void OnEnable()
        {
            if (takeBasePoseOwnership)
                TryStartPreview();
        }

        private void OnDisable() => StopPreview(restoreEamm: true);
        private void OnDestroy() => StopPreview(restoreEamm: true);

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                _generation++;
                ClearActiveTrajectory();
                presentation?.FootContactController?.ClearBasePoseContactMetadata();
                Status = "paused";
            }
            else if (_running)
            {
                _nextPlanAt = Time.unscaledTime;
                Status = "awaiting-fresh-plan";
            }
        }

        private void Update()
        {
            if (takeBasePoseOwnership && !_running)
                TryStartPreview();
            else if (!takeBasePoseOwnership && _running)
            {
                StopPreview(restoreEamm: true);
                return;
            }
            if (!_running) return;

            bool protectedNow = IsProtectedByAuthoredAuthority();
            if (protectedNow != _protectedLane)
            {
                _protectedLane = protectedNow;
                _generation++;
                ClearActiveTrajectory();
                presentation?.FootContactController?.ClearBasePoseContactMetadata();
                Status = protectedNow ? "authored-authority" : "awaiting-fresh-plan";
                _nextPlanAt = Time.unscaledTime;
            }

            using (PollMarker.Auto())
            {
                if (_planning)
                    PollPlan();
            }

            if (_protectedLane) return;
            _playbackFrame = Mathf.Min(
                Mathf.Max(0f, _activeFrameCount - 1f),
                _playbackFrame + Mathf.Max(0f, Time.unscaledDeltaTime) * 30f);
            _blendElapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
            if (!_planning && Time.unscaledTime >= _nextPlanAt)
                SchedulePlan();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || !_running || _protectedLane ||
                _activeTrajectory == null || _activeFrameCount <= 0 ||
                animator == null || !animator.isHuman || retargetProfile == null)
                return;
            // Authority can change after Update but before this IK callback.
            if (IsProtectedByAuthoredAuthority())
            {
                _generation++;
                _protectedLane = true;
                ClearActiveTrajectory();
                presentation?.FootContactController?.ClearBasePoseContactMetadata();
                Status = "authored-authority";
                return;
            }

            using (RetargetMarker.Auto())
            {
                SampleTrajectory(_playbackFrame, _samplePose);
                if (_blendElapsed < 8f / 30f)
                    BlendQpos(_blendFromPose, _samplePose, Mathf.Clamp01(_blendElapsed * 30f / 8f), _samplePose);
                if (!SonicG1Skeleton.TryEvaluate(_samplePose, _sourceWorldRotations, _sourceWorldPositions))
                {
                    FailRuntime("non-finite-g1-pose", null);
                    return;
                }

                int appliedBones = ApplyRetargetedPose();
                if (appliedBones <= 0)
                {
                    FailRuntime("no-humanoid-bones-applied", null);
                    return;
                }
                RetargetApplicationCount++;
                LastRetargetFrame = Time.frameCount;
                PublishFootContacts();
            }
        }

        public void SetPreviewMode(SonicPreviewMode requestedMode)
        {
            if (mode == requestedMode) return;
            mode = requestedMode;
            _generation++;
            ClearActiveTrajectory();
            _nextPlanAt = Time.unscaledTime;
            Status = "mode-changed-awaiting-fresh-plan";
        }

        public bool ConfigureAndStartPreview(
            ModelAsset configuredPlanner,
            SonicHumanoidRetargetProfile configuredProfile,
            SonicPreviewMode requestedMode,
            BackendType configuredBackend = BackendType.CPU,
            bool followPlanetMotorDirection = true)
        {
            if (_running)
                StopPreview(restoreEamm: true);
            planner = configuredPlanner;
            retargetProfile = configuredProfile;
            mode = requestedMode;
            backend = configuredBackend;
            usePlanetMotorDirection = followPlanetMotorDirection;
            takeBasePoseOwnership = true;
            TryStartPreview();
            return _running;
        }

        public void StopAndReleasePreview()
        {
            takeBasePoseOwnership = false;
            StopPreview(restoreEamm: true);
        }

        [ContextMenu("SONIC Preview/Use Walk")]
        private void UseWalk() => SetPreviewMode(SonicPreviewMode.Walk);

        [ContextMenu("SONIC Preview/Use Random Punches")]
        private void UseRandomPunches() => SetPreviewMode(SonicPreviewMode.RandomPunches);

        private void TryStartPreview()
        {
            if (animator == null || planner == null || retargetProfile == null)
            {
                Status = "missing-animator-model-or-profile";
                return;
            }
            if (!retargetProfile.Validate(animator, out string reason))
            {
                Status = "invalid-retarget:" + reason;
                return;
            }
            try
            {
                if (eammBridge != null && eammBridge.enabled)
                {
                    eammBridge.enabled = false;
                    _disabledEamm = true;
                }
                Model model = ModelLoader.Load(planner);
                _worker = new Worker(model, backend);
                _running = true;
                _generation++;
                _nextPlanAt = Time.unscaledTime;
                _protectedLane = IsProtectedByAuthoredAuthority();
                Status = _protectedLane ? "authored-authority" : "awaiting-first-plan";
            }
            catch (Exception exception)
            {
                Status = "startup-failed:" + exception.GetType().Name + ":" + exception.Message;
                StopPreview(restoreEamm: true);
                Debug.LogError($"SONIC preview failed to start on {name}: {exception}", this);
            }
        }

        private void StopPreview(bool restoreEamm)
        {
            _generation++;
            _running = false;
            _planning = false;
            _pendingQpos = null;
            _pendingFrameCount = null;
            _pendingInputs?.Dispose();
            _pendingInputs = null;
            _worker?.Dispose();
            _worker = null;
            ClearActiveTrajectory();
            presentation?.FootContactController?.ClearBasePoseContactMetadata();
            if (restoreEamm && _disabledEamm && eammBridge != null)
                eammBridge.enabled = true;
            _disabledEamm = false;
            Status = "disabled";
        }

        private bool IsProtectedByAuthoredAuthority()
        {
            if (motor != null && motor.IsMantling) return true;
            if (ragdoll != null && (ragdoll.IsRagdollActive || ragdoll.IsRecoveringToAnimation)) return true;
            if (presentation == null) return false;
            EarthAuthoredActionId action = presentation.CurrentAuthoredAction;
            return action != EarthAuthoredActionId.None && action != EarthAuthoredActionId.Locomotion;
        }

        private void SchedulePlan()
        {
            if (_worker == null) return;
            try
            {
                BuildContext(_contextScratch);
                Vector3 direction = ResolveSourceDirection();
                _pendingInputs = new SonicPlannerInputs(
                    _contextScratch,
                    (int)mode,
                    direction,
                    randomSeed + _sequence);
                _pendingInputs.Bind(_worker);
                _worker.Schedule();
                _pendingQpos = _worker.PeekOutput("mujoco_qpos") as Tensor<float>;
                _pendingFrameCount = _worker.PeekOutput("num_pred_frames") as Tensor<int>;
                if (_pendingQpos == null || _pendingFrameCount == null)
                {
                    FailRuntime("output-type-mismatch", null);
                    return;
                }
                _pendingQpos.ReadbackRequest();
                _pendingFrameCount.ReadbackRequest();
                _planning = true;
                _pendingGeneration = _generation;
                _nextPlanAt = Time.unscaledTime + Mathf.Max(.1f, planIntervalSeconds);
                Status = "planning";
            }
            catch (Exception exception)
            {
                FailRuntime("schedule-failed", exception);
            }
        }

        private void PollPlan()
        {
            if (_pendingQpos == null || _pendingFrameCount == null)
            {
                FailRuntime("pending-output-missing", null);
                return;
            }
            if (!_pendingQpos.IsReadbackRequestDone() || !_pendingFrameCount.IsReadbackRequestDone())
                return;

            float[] qpos;
            int[] counts;
            try
            {
                qpos = _pendingQpos.DownloadToArray();
                counts = _pendingFrameCount.DownloadToArray();
            }
            catch (Exception exception)
            {
                FailRuntime("readback-failed", exception);
                return;
            }
            _pendingInputs?.Dispose();
            _pendingInputs = null;
            _pendingQpos = null;
            _pendingFrameCount = null;
            _planning = false;

            if (_pendingGeneration != _generation || _protectedLane)
            {
                Status = _protectedLane ? "authored-authority" : "discarded-stale-plan";
                return;
            }
            int count = counts != null && counts.Length > 0 ? counts[0] : 0;
            if (qpos == null || qpos.Length != 64 * SonicG1Skeleton.PoseSize || count < 24 || count > 64 ||
                !ValidateFinite(qpos, count * SonicG1Skeleton.PoseSize))
            {
                InvalidateOutput("invalid-output-contract");
                return;
            }

            if (_activeTrajectory != null && _activeFrameCount > 0)
                SampleTrajectory(_playbackFrame, _blendFromPose);
            else
                BuildNeutralPose(_blendFromPose);
            _activeTrajectory = qpos;
            _activeFrameCount = count;
            _playbackFrame = 0f;
            _blendElapsed = 0f;
            _sequence++;
            Status = $"active:{mode}:sequence-{_sequence}";
        }

        private int ApplyRetargetedPose()
        {
            Quaternion sourceRoot = SonicG1Skeleton.SourceRootRotation(_samplePose);
            int applied = 0;
            float maximumDelta = 0f;
            for (int index = 0; index < retargetProfile.Bindings.Count; index++)
            {
                SonicHumanoidBinding binding = retargetProfile.Bindings[index];
                Transform bone = animator.GetBoneTransform(binding.TargetBone);
                if (bone == null) continue;

                Quaternion sourceCurrent = binding.SourceJointIndex < 0
                    ? sourceRoot
                    : _sourceWorldRotations[binding.SourceJointIndex];
                Quaternion sourceRest = binding.SourceJointIndex < 0
                    ? Quaternion.identity
                    : SonicG1Skeleton.GetRestWorldRotation(binding.SourceJointIndex);
                Quaternion sourceParentCurrent = binding.SourceParentJointIndex < 0
                    ? sourceRoot
                    : _sourceWorldRotations[binding.SourceParentJointIndex];
                Quaternion sourceParentRest = binding.SourceParentJointIndex < 0
                    ? Quaternion.identity
                    : SonicG1Skeleton.GetRestWorldRotation(binding.SourceParentJointIndex);
                if (binding.SourceJointIndex < 0)
                    sourceParentCurrent = sourceParentRest = Quaternion.identity;
                Quaternion sourceLocalCurrent = Quaternion.Inverse(sourceParentCurrent) * sourceCurrent;
                Quaternion sourceLocalRest = Quaternion.Inverse(sourceParentRest) * sourceRest;
                Quaternion sourceLocalDelta = sourceLocalCurrent * Quaternion.Inverse(sourceLocalRest);
                Quaternion mappedDelta = SonicG1Skeleton.MapRotationToUnity(sourceLocalDelta);
                if (binding.SourceJointIndex < 0)
                {
                    // PlanetMotor owns visible tangent facing. Keep only the root tilt in
                    // the Humanoid hips; waist joints still carry authored SONIC twist.
                    mappedDelta = Quaternion.FromToRotation(Vector3.up, mappedDelta * Vector3.up);
                }
                Quaternion basis = Normalize(binding.DeltaBasis);
                Quaternion targetDelta = basis * mappedDelta * Quaternion.Inverse(basis);
                Quaternion desiredLocal = targetDelta * binding.TargetRestLocal;
                float bindingWeight = IsBoxing ? binding.BoxingWeight : binding.LocomotionWeight;
                float weightedPose = Mathf.Clamp01(poseWeight * bindingWeight);
                if (weightedPose <= 0f) continue;
                maximumDelta = Mathf.Max(maximumDelta, Quaternion.Angle(bone.localRotation, Normalize(desiredLocal)));
                animator.SetBoneLocalRotation(
                    binding.TargetBone,
                    Quaternion.Slerp(bone.localRotation, Normalize(desiredLocal), weightedPose));
                applied++;
            }
            LastRetargetMaxBoneDeltaDegrees = maximumDelta;
            return applied;
        }

        private void PublishFootContacts()
        {
            EarthFootContactController feet = presentation != null ? presentation.FootContactController : null;
            if (feet == null) return;
            if (IsBoxing)
            {
                feet.SetBasePoseContactMetadata(0f, .5f, true, true);
                return;
            }

            SampleTrajectory(Mathf.Min(_playbackFrame + 1f, _activeFrameCount - 1f), _nextSamplePose);
            if (!SonicG1Skeleton.TryEvaluate(
                    _nextSamplePose,
                    _nextSourceWorldRotations,
                    _nextSourceWorldPositions))
            {
                feet.ClearBasePoseContactMetadata();
                return;
            }

            float leftHeight = _sourceWorldPositions[5].z;
            float rightHeight = _sourceWorldPositions[11].z;
            float floor = Mathf.Min(leftHeight, rightHeight);
            float leftVelocity = (_nextSourceWorldPositions[5].z - leftHeight) * 30f;
            float rightVelocity = (_nextSourceWorldPositions[11].z - rightHeight) * 30f;
            bool left = leftHeight <= floor + .035f && Mathf.Abs(leftVelocity) <= .18f;
            bool right = rightHeight <= floor + .035f && Mathf.Abs(rightVelocity) <= .18f;
            if (!left && !right)
            {
                left = leftHeight <= rightHeight;
                right = !left;
            }
            float phase = Mathf.Repeat(_playbackFrame / 30f, 1f);
            feet.SetBasePoseContactMetadata(phase, Mathf.Repeat(phase + .5f, 1f), left, right);
        }

        private void BuildContext(float[] context)
        {
            if (_activeTrajectory == null || _activeFrameCount <= 0)
            {
                for (int frame = 0; frame < 4; frame++)
                    BuildNeutralPose(context, frame * SonicG1Skeleton.PoseSize);
                return;
            }
            for (int history = 0; history < 4; history++)
            {
                SampleTrajectory(_playbackFrame - (3 - history), _samplePose);
                Array.Copy(
                    _samplePose,
                    0,
                    context,
                    history * SonicG1Skeleton.PoseSize,
                    SonicG1Skeleton.PoseSize);
            }
        }

        private Vector3 ResolveSourceDirection()
        {
            if (usePlanetMotorDirection && motor != null)
            {
                float right = motor.LastCommand.Move.x;
                float forward = motor.LastCommand.Move.y;
                float magnitude = Mathf.Sqrt(right * right + forward * forward);
                if (magnitude > .001f)
                    return new Vector3(forward / magnitude, -right / magnitude, 0f) * directionMagnitude;
                return Vector3.zero;
            }
            return useFallbackForwardWithoutInput && (!IsBoxing || mode == SonicPreviewMode.WalkBoxing)
                ? new Vector3(directionMagnitude, 0f, 0f)
                : Vector3.zero;
        }

        private void SampleTrajectory(float frame, float[] destination)
        {
            if (_activeTrajectory == null || _activeFrameCount <= 0)
            {
                BuildNeutralPose(destination);
                return;
            }
            float clamped = Mathf.Clamp(frame, 0f, _activeFrameCount - 1f);
            int first = Mathf.FloorToInt(clamped);
            int second = Mathf.Min(first + 1, _activeFrameCount - 1);
            float t = clamped - first;
            int firstOffset = first * SonicG1Skeleton.PoseSize;
            int secondOffset = second * SonicG1Skeleton.PoseSize;
            for (int index = 0; index < SonicG1Skeleton.PoseSize; index++)
                destination[index] = Mathf.LerpUnclamped(_activeTrajectory[firstOffset + index], _activeTrajectory[secondOffset + index], t);
            Quaternion a = Normalize(new Quaternion(
                _activeTrajectory[firstOffset + 4], _activeTrajectory[firstOffset + 5],
                _activeTrajectory[firstOffset + 6], _activeTrajectory[firstOffset + 3]));
            Quaternion b = Normalize(new Quaternion(
                _activeTrajectory[secondOffset + 4], _activeTrajectory[secondOffset + 5],
                _activeTrajectory[secondOffset + 6], _activeTrajectory[secondOffset + 3]));
            Quaternion root = Quaternion.Slerp(a, b, t);
            destination[3] = root.w;
            destination[4] = root.x;
            destination[5] = root.y;
            destination[6] = root.z;
        }

        private static void BlendQpos(float[] from, float[] to, float weight, float[] destination)
        {
            Quaternion a = Normalize(new Quaternion(from[4], from[5], from[6], from[3]));
            Quaternion b = Normalize(new Quaternion(to[4], to[5], to[6], to[3]));
            for (int index = 0; index < SonicG1Skeleton.PoseSize; index++)
                destination[index] = Mathf.LerpUnclamped(from[index], to[index], weight);
            Quaternion root = Quaternion.Slerp(a, b, weight);
            destination[3] = root.w;
            destination[4] = root.x;
            destination[5] = root.y;
            destination[6] = root.z;
        }

        private static void BuildNeutralPose(float[] destination, int offset = 0)
        {
            Array.Clear(destination, offset, SonicG1Skeleton.PoseSize);
            destination[offset + 2] = .78f;
            destination[offset + 3] = 1f;
        }

        private void ClearActiveTrajectory()
        {
            _activeTrajectory = null;
            _activeFrameCount = 0;
            _playbackFrame = 0f;
            _blendElapsed = 0f;
            BuildNeutralPose(_blendFromPose);
        }

        private void InvalidateOutput(string reason)
        {
            _generation++;
            _planning = false;
            _pendingQpos = null;
            _pendingFrameCount = null;
            _pendingInputs?.Dispose();
            _pendingInputs = null;
            ClearActiveTrajectory();
            presentation?.FootContactController?.ClearBasePoseContactMetadata();
            Status = "rejected:" + reason;
            Debug.LogError($"SONIC preview rejected output on {name}: {reason}", this);
        }

        private void FailRuntime(string reason, Exception exception)
        {
            string details = exception == null
                ? reason
                : reason + ":" + exception.GetType().Name + ":" + exception.Message;
            StopPreview(restoreEamm: true);
            Status = "failed:" + details;
            Debug.LogError($"SONIC preview stopped on {name}: {details}", this);
        }

        private static bool ValidateFinite(float[] values, int count)
        {
            int end = Mathf.Min(values.Length, count);
            for (int index = 0; index < end; index++)
            {
                if (!float.IsFinite(values[index])) return false;
            }
            return true;
        }

        private static Quaternion Normalize(Quaternion value)
        {
            float length = Mathf.Sqrt(
                value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w);
            return length > .000001f
                ? new Quaternion(value.x / length, value.y / length, value.z / length, value.w / length)
                : Quaternion.identity;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawHiddenG1 || _activeTrajectory == null || _activeFrameCount <= 0) return;
            Gizmos.color = Color.cyan;
            for (int index = 0; index < SonicG1Skeleton.JointCount; index++)
            {
                int parent = SonicG1Skeleton.GetParent(index);
                Vector3 point = transform.TransformPoint(
                    SonicG1Skeleton.MapPositionToUnity(_sourceWorldPositions[index] -
                                                       new Vector3(_samplePose[0], _samplePose[1], _samplePose[2])));
                Gizmos.DrawSphere(point, .012f);
                if (parent >= 0)
                {
                    Vector3 parentPoint = transform.TransformPoint(
                        SonicG1Skeleton.MapPositionToUnity(_sourceWorldPositions[parent] -
                                                           new Vector3(_samplePose[0], _samplePose[1], _samplePose[2])));
                    Gizmos.DrawLine(parentPoint, point);
                }
            }
        }

        private sealed class SonicPlannerInputs : IDisposable
        {
            private readonly Tensor<float>[] _floatInputs;
            private readonly Tensor<int>[] _intInputs;

            public SonicPlannerInputs(float[] context, int mode, Vector3 direction, int seed)
            {
                var contextCopy = new float[context.Length];
                Array.Copy(context, contextCopy, context.Length);
                var allowed = new int[11];
                allowed[0] = 1;
                _floatInputs = new[]
                {
                    new Tensor<float>(new TensorShape(1, 4, 36), contextCopy),
                    new Tensor<float>(new TensorShape(1), new[] { -1f }),
                    new Tensor<float>(new TensorShape(1, 3), new[] { direction.x, direction.y, direction.z }),
                    new Tensor<float>(new TensorShape(1, 3), new[] { 1f, 0f, 0f }),
                    new Tensor<float>(new TensorShape(1, 4, 3), new float[12]),
                    new Tensor<float>(new TensorShape(1, 4), new float[4]),
                    new Tensor<float>(new TensorShape(1), new[] { -1f }),
                };
                _intInputs = new[]
                {
                    new Tensor<int>(new TensorShape(1), new[] { mode }),
                    new Tensor<int>(new TensorShape(1), new[] { seed }),
                    new Tensor<int>(new TensorShape(1, 1), new[] { 0 }),
                    new Tensor<int>(new TensorShape(1, 11), allowed),
                };
            }

            public void Bind(Worker worker)
            {
                worker.SetInput("context_mujoco_qpos", _floatInputs[0]);
                worker.SetInput("target_vel", _floatInputs[1]);
                worker.SetInput("mode", _intInputs[0]);
                worker.SetInput("movement_direction", _floatInputs[2]);
                worker.SetInput("facing_direction", _floatInputs[3]);
                worker.SetInput("random_seed", _intInputs[1]);
                worker.SetInput("has_specific_target", _intInputs[2]);
                worker.SetInput("specific_target_positions", _floatInputs[4]);
                worker.SetInput("specific_target_headings", _floatInputs[5]);
                worker.SetInput("allowed_pred_num_tokens", _intInputs[3]);
                worker.SetInput("height", _floatInputs[6]);
            }

            public void Dispose()
            {
                for (int index = 0; index < _floatInputs.Length; index++)
                    _floatInputs[index].Dispose();
                for (int index = 0; index < _intInputs.Length; index++)
                    _intInputs[index].Dispose();
            }
        }
    }
}
