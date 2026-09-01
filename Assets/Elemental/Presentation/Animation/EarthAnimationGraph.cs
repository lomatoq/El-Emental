using System;
using Elemental.Runtime.Characters;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

namespace Elemental.Presentation.Animation
{
    /// <summary>
    /// Owns the optional controller -> inertialization -> rig/IK graph. When the
    /// feature is disabled or graph construction fails, the Animator and RigBuilder
    /// return to their legacy ownership path.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class EarthAnimationGraph : MonoBehaviour
    {
        private static readonly ProfilerMarker UpdateMarker =
            new ProfilerMarker("Elemental.Character.AnimationGraph");
        private static readonly ProfilerMarker TransitionMarker =
            new ProfilerMarker("Elemental.Character.PoseInertialization.Begin");
        private static readonly ProfilerMarker CaptureMarker =
            new ProfilerMarker("Elemental.Character.AnimationGraph.Capture");
        public const int CaptureFrameCapacity = 720;

        [SerializeField] private Animator animator;
        [SerializeField] private EarthAnimationGraphProfile profile;
        [SerializeField] private EarthFootContactController footContactController;
        [SerializeField] private HumanoidRagdollRig visibleRagdoll;

        private PlayableGraph _graph;
        private AnimatorControllerPlayable _controllerPlayable;
        private AnimationScriptPlayable _inertializationPlayable;
        private AnimationPlayableOutput _baseOutput;
        private EarthPoseHistory _poseHistory;
        private RigBuilder _rigBuilder;
        private EarthAnimationGraphSettings _settings;
        private EarthAnimationGraphSettings _activeSettings;
        private bool _configured;
        private bool _rigLayersAppended;
        private int _rigOutputStartIndex;
        private int _rigOutputCount;
        private bool _legacyRigBuilderWasEnabled;
        private bool _leftHandContact;
        private bool _rightHandContact;
        private AnimatorControllerParameter[] _controllerParameters;
        private bool[] _externallyWritableControllerParameters = Array.Empty<bool>();
        private AnimatorStateInfo[] _handoffStates = Array.Empty<AnimatorStateInfo>();
        private float[] _handoffLayerWeights = Array.Empty<float>();
        private bool[] _handoffStateValid = Array.Empty<bool>();
        private bool _runtimeEnablePending;
        private bool _runtimeDisablePending;
        private bool _poseDisablePending;
        private uint _stateHandoffCount;
        private readonly EarthAnimationGraphCaptureSample[] _captureFrames =
            new EarthAnimationGraphCaptureSample[CaptureFrameCapacity];
        private int _captureWriteIndex;
        private int _captureCount;
        private uint _activeUpdateCount;
        private uint _rigSyncCount;
        private int _hotPathAllocationSampleCount;
        private int _hotPathAllocationFramesOverZero;
        private long _hotPathTotalManagedAllocationBytes;
        private long _hotPathMaximumManagedAllocationBytes;
        private EarthAnimationGraphFallbackReason _fallbackReason =
            EarthAnimationGraphFallbackReason.FeatureDisabled;

        public bool IsActive => _graph.IsValid() && _controllerPlayable.IsValid() &&
                                _inertializationPlayable.IsValid();
        public bool UsePoseInertialization => IsActive &&
                                              _settings.UsePlayablesAnimationGraph &&
                                              _settings.UsePoseInertialization &&
                                              !_runtimeDisablePending;
        public AnimatorControllerPlayable ControllerPlayable => _controllerPlayable;
        public EarthAnimationGraphProfile Profile => profile;
        public int CapturedFrameCount => _captureCount;
        public EarthAnimationGraphCaptureSample LatestCaptureSample => _captureCount > 0
            ? _captureFrames[(_captureWriteIndex - 1 + CaptureFrameCapacity) % CaptureFrameCapacity]
            : default;
        public EarthAnimationGraphHotPathEvidence HotPathEvidence
        {
            get
            {
                uint jobEvaluationCount = _poseHistory != null &&
                                          _poseHistory.Diagnostics.IsCreated
                    ? _poseHistory.Diagnostics[0].EvaluationCount
                    : 0u;
                return new EarthAnimationGraphHotPathEvidence(
                    _activeUpdateCount,
                    jobEvaluationCount,
                    _rigSyncCount,
                    _hotPathAllocationSampleCount,
                    _hotPathAllocationFramesOverZero,
                    _hotPathTotalManagedAllocationBytes,
                    _hotPathMaximumManagedAllocationBytes);
            }
        }

        public EarthAnimationGraphDiagnostics Diagnostics
        {
            get
            {
                EarthAnimationJobDiagnostics job = _poseHistory != null &&
                                                    _poseHistory.Diagnostics.IsCreated
                    ? _poseHistory.Diagnostics[0]
                    : default;
                return new EarthAnimationGraphDiagnostics(
                    _graph.IsValid(),
                    _controllerPlayable.IsValid(),
                    _inertializationPlayable.IsValid(),
                    ValidateTopology(),
                    _rigLayersAppended,
                    _rigOutputCount,
                    _rigLayersAppended && ValidateRigOutputs(),
                    !IsActive,
                    _fallbackReason,
                    _poseHistory?.BoneCount ?? 0,
                    job.TransitionRequestCount,
                    job.InterruptedTransitionCount,
                    job.InertiaActive != 0,
                    job.ElapsedSeconds,
                    job.MaximumPositionOffset,
                    job.MaximumRotationOffsetRadians,
                    _runtimeEnablePending,
                    _runtimeDisablePending,
                    _poseDisablePending,
                    _stateHandoffCount);
            }
        }

        public bool Configure(
            Animator configuredAnimator,
            EarthAnimationGraphProfile configuredProfile,
            EarthFootContactController configuredFootContacts = null,
            HumanoidRagdollRig configuredVisibleRagdoll = null)
        {
            animator = configuredAnimator;
            profile = configuredProfile;
            footContactController = configuredFootContacts;
            visibleRagdoll = configuredVisibleRagdoll;
            _settings = profile != null ? profile.Settings : EarthAnimationGraphSettings.Disabled;
            _configured = true;
            return ApplyRequestedSettings();
        }

        public bool Configure(
            Animator configuredAnimator,
            in EarthAnimationGraphSettings settings,
            EarthFootContactController configuredFootContacts = null,
            HumanoidRagdollRig configuredVisibleRagdoll = null)
        {
            animator = configuredAnimator;
            profile = null;
            footContactController = configuredFootContacts;
            visibleRagdoll = configuredVisibleRagdoll;
            _settings = settings;
            _configured = true;
            return ApplyRequestedSettings();
        }

        public void SetHandContactOwnership(bool leftActive, bool rightActive)
        {
            _leftHandContact = leftActive;
            _rightHandContact = rightActive;
            UpdateOwnershipMask();
        }

        public bool BeginInertialization(float requestedDurationSeconds)
        {
            if (!UsePoseInertialization || _poseHistory == null) return false;
            using (TransitionMarker.Auto())
            {
                var controls = _poseHistory.Control;
                EarthAnimationGraphControl control = controls[0];
                control.RequestSequence++;
                if (control.RequestSequence == 0) control.RequestSequence = 1;
                float requested = float.IsFinite(requestedDurationSeconds)
                    ? requestedDurationSeconds
                    : _settings.MaximumDurationSeconds;
                float decayWindow = Mathf.Max(
                    _settings.PositionHalfLifeSeconds * 6f,
                    _settings.RotationHalfLifeSeconds * 6f);
                control.MaximumDurationSeconds = Mathf.Clamp(
                    Mathf.Max(requested, decayWindow),
                    0.05f,
                    _settings.MaximumDurationSeconds);
                controls[0] = control;
                return true;
            }
        }

        public void CrossFade(int stateHash, float normalizedDuration, int layer, float normalizedTime)
        {
            if (IsActive)
                _controllerPlayable.CrossFade(stateHash, normalizedDuration, layer, normalizedTime);
            else
                animator?.CrossFade(stateHash, normalizedDuration, layer, normalizedTime);
        }

        public void CrossFadeInFixedTime(int stateHash, float duration, int layer, float startSeconds)
        {
            if (IsActive)
                _controllerPlayable.CrossFadeInFixedTime(stateHash, duration, layer, startSeconds);
            else
                animator?.CrossFadeInFixedTime(stateHash, duration, layer, startSeconds);
        }

        public void Play(int stateHash, int layer, float normalizedTime)
        {
            if (IsActive) _controllerPlayable.Play(stateHash, layer, normalizedTime);
            else animator?.Play(stateHash, layer, normalizedTime);
        }

        public void PlayInFixedTime(int stateHash, int layer, float fixedTime)
        {
            if (IsActive) _controllerPlayable.PlayInFixedTime(stateHash, layer, fixedTime);
            else animator?.PlayInFixedTime(stateHash, layer, fixedTime);
        }

        public void SetTrigger(int parameterHash)
        {
            if (IsActive) _controllerPlayable.SetTrigger(parameterHash);
            else animator?.SetTrigger(parameterHash);
        }

        public void ResetTrigger(int parameterHash)
        {
            if (IsActive) _controllerPlayable.ResetTrigger(parameterHash);
            else animator?.ResetTrigger(parameterHash);
        }

        public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layer) => IsActive
            ? _controllerPlayable.GetCurrentAnimatorStateInfo(layer)
            : animator != null ? animator.GetCurrentAnimatorStateInfo(layer) : default;

        public AnimatorStateInfo GetNextAnimatorStateInfo(int layer) => IsActive
            ? _controllerPlayable.GetNextAnimatorStateInfo(layer)
            : animator != null ? animator.GetNextAnimatorStateInfo(layer) : default;

        public bool IsInTransition(int layer) => IsActive
            ? _controllerPlayable.IsInTransition(layer)
            : animator != null && animator.IsInTransition(layer);

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (footContactController == null)
                footContactController = GetComponent<EarthFootContactController>();
            if (visibleRagdoll == null) visibleRagdoll = GetComponent<HumanoidRagdollRig>();
            if (profile != null)
            {
                _settings = profile.Settings;
                _configured = true;
            }
        }

        private void OnEnable()
        {
            if (_configured) ApplyRequestedSettings();
        }

        private void Update()
        {
            bool sampleHotPath = IsActive;
            long allocationStart = sampleHotPath
                ? GC.GetAllocatedBytesForCurrentThread()
                : 0L;
            try
            {
                using (UpdateMarker.Auto())
                {
                    if (profile != null)
                    {
                        EarthAnimationGraphSettings current = profile.Settings;
                        if (!SettingsEqual(in current, in _settings))
                        {
                            _settings = current;
                            ApplyRequestedSettings();
                        }
                    }

                    if (_runtimeEnablePending)
                    {
                        if (!IsLegacyAnimatorTransitioning())
                        {
                            _runtimeEnablePending = false;
                            TryBuildGraph();
                        }
                        if (!IsActive) return;
                    }
                    if (!IsActive) return;
                    _activeUpdateCount++;
                    MirrorAnimatorControllerInputs();
                    UpdateOwnershipMask();
                    if (_rigLayersAppended && _rigBuilder != null)
                    {
                        _rigBuilder.SyncLayers();
                        _rigSyncCount++;
                    }
                    if (_poseDisablePending && !IsInertiaActive())
                    {
                        _poseDisablePending = false;
                        _activeSettings = _settings;
                    }
                    RefreshControlSettings();
                    if (_runtimeDisablePending && CanShutdownContinuously())
                    {
                        _runtimeDisablePending = false;
                        ShutdownGraph(
                            true,
                            EarthAnimationGraphFallbackReason.FeatureDisabled,
                            true);
                    }
                }
            }
            finally
            {
                if (sampleHotPath)
                {
                    long allocated = Math.Max(
                        0L,
                        GC.GetAllocatedBytesForCurrentThread() - allocationStart);
                    _hotPathAllocationSampleCount++;
                    if (allocated > 0L) _hotPathAllocationFramesOverZero++;
                    _hotPathTotalManagedAllocationBytes += allocated;
                    _hotPathMaximumManagedAllocationBytes = Math.Max(
                        _hotPathMaximumManagedAllocationBytes,
                        allocated);
                }
            }
        }

        private void OnDisable()
        {
            ShutdownGraph(true, EarthAnimationGraphFallbackReason.ComponentDisabled, true);
        }

        private void LateUpdate()
        {
            using (CaptureMarker.Auto())
            {
                EarthAnimationGraphDiagnostics diagnostics = Diagnostics;
                _captureFrames[_captureWriteIndex] = new EarthAnimationGraphCaptureSample(
                    Time.frameCount,
                    Time.unscaledTime,
                    in diagnostics);
                _captureWriteIndex = (_captureWriteIndex + 1) % CaptureFrameCapacity;
                if (_captureCount < CaptureFrameCapacity) _captureCount++;
            }
        }

        private void OnDestroy()
        {
            ShutdownGraph(true, EarthAnimationGraphFallbackReason.ComponentDisabled, false);
        }

        private bool ApplyRequestedSettings()
        {
            if (!_settings.UsePlayablesAnimationGraph)
            {
                _runtimeEnablePending = false;
                _poseDisablePending = false;
                if (IsActive && !CanShutdownContinuously())
                {
                    _runtimeDisablePending = true;
                    return false;
                }
                _runtimeDisablePending = false;
                ShutdownGraph(true, EarthAnimationGraphFallbackReason.FeatureDisabled, true);
                return false;
            }
            _runtimeDisablePending = false;
            if (IsActive)
            {
                if (_activeSettings.UsePoseInertialization &&
                    !_settings.UsePoseInertialization && IsInertiaActive())
                {
                    _poseDisablePending = true;
                }
                else
                {
                    _poseDisablePending = false;
                    _activeSettings = _settings;
                }
                RefreshControlSettings();
                return true;
            }
            if (IsLegacyAnimatorTransitioning())
            {
                _runtimeEnablePending = true;
                _fallbackReason = EarthAnimationGraphFallbackReason.EnableDeferredForTransition;
                return false;
            }
            _runtimeEnablePending = false;
            _activeSettings = _settings;
            return TryBuildGraph();
        }

        public int CopyRecentCaptureSamplesNonAlloc(
            EarthAnimationGraphCaptureSample[] destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            int copyCount = Math.Min(destination.Length, _captureCount);
            int sourceIndex = (_captureWriteIndex - copyCount + CaptureFrameCapacity) %
                              CaptureFrameCapacity;
            for (int index = 0; index < copyCount; index++)
                destination[index] = _captureFrames[(sourceIndex + index) % CaptureFrameCapacity];
            return copyCount;
        }

        public void ResetHotPathEvidence()
        {
            _activeUpdateCount = 0u;
            _rigSyncCount = 0u;
            _hotPathAllocationSampleCount = 0;
            _hotPathAllocationFramesOverZero = 0;
            _hotPathTotalManagedAllocationBytes = 0L;
            _hotPathMaximumManagedAllocationBytes = 0L;
            if (_poseHistory == null || !_poseHistory.Diagnostics.IsCreated) return;
            var diagnosticsArray = _poseHistory.Diagnostics;
            EarthAnimationJobDiagnostics diagnostics = diagnosticsArray[0];
            diagnostics.EvaluationCount = 0u;
            diagnosticsArray[0] = diagnostics;
        }

        public EarthAnimationGraphCaptureSummary GetCaptureSummary()
        {
            int graphActiveFrames = 0;
            int topologyFailureFrames = 0;
            int legacyFallbackFrames = 0;
            int inertiaActiveFrames = 0;
            int pendingHandoffFrames = 0;
            float maximumPositionOffset = 0f;
            float maximumRotationOffset = 0f;
            uint finalStateHandoffCount = 0;
            int first = (_captureWriteIndex - _captureCount + CaptureFrameCapacity) %
                        CaptureFrameCapacity;
            for (int index = 0; index < _captureCount; index++)
            {
                EarthAnimationGraphCaptureSample sample =
                    _captureFrames[(first + index) % CaptureFrameCapacity];
                if (sample.GraphValid) graphActiveFrames++;
                if (sample.GraphValid && !sample.TopologyValid) topologyFailureFrames++;
                if (sample.LegacyFallbackActive) legacyFallbackFrames++;
                if (sample.InertiaActive) inertiaActiveFrames++;
                if (sample.RuntimeEnablePending || sample.RuntimeDisablePending ||
                    sample.PoseDisablePending)
                    pendingHandoffFrames++;
                maximumPositionOffset = Mathf.Max(
                    maximumPositionOffset,
                    sample.MaximumPositionOffset);
                maximumRotationOffset = Mathf.Max(
                    maximumRotationOffset,
                    sample.MaximumRotationOffsetRadians);
                finalStateHandoffCount = sample.StateHandoffCount;
            }
            return new EarthAnimationGraphCaptureSummary(
                _captureCount,
                graphActiveFrames,
                topologyFailureFrames,
                legacyFallbackFrames,
                inertiaActiveFrames,
                pendingHandoffFrames,
                maximumPositionOffset,
                maximumRotationOffset,
                finalStateHandoffCount);
        }

        private bool TryBuildGraph()
        {
            if (animator == null)
                return FailToLegacy(EarthAnimationGraphFallbackReason.MissingAnimator, null);
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            if (controller == null)
                return FailToLegacy(EarthAnimationGraphFallbackReason.MissingController, null);

            CaptureAnimatorHandoffState();
            ShutdownGraph(true, EarthAnimationGraphFallbackReason.None, false);
            try
            {
                _rigBuilder = animator.GetComponent<RigBuilder>();
                if (_rigBuilder != null)
                {
                    _legacyRigBuilderWasEnabled = _rigBuilder.enabled;
                    _rigBuilder.enabled = false;
                    _rigBuilder.Clear();
                }

                int boneCount = CountBoundBones(animator);
                _poseHistory = new EarthPoseHistory(boneCount);
                BindBones(animator, _poseHistory);
                _activeSettings = _settings;
                RefreshControlSettings();

                _graph = PlayableGraph.Create($"{animator.gameObject.name}_EarthAnimationGraph");
                _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                _controllerPlayable = AnimatorControllerPlayable.Create(_graph, controller);
                CacheControllerParameters();
                ApplyAnimatorStateToControllerPlayable();
                _inertializationPlayable = AnimationScriptPlayable.Create(
                    _graph,
                    _poseHistory.CreateJob(),
                    1);
                _inertializationPlayable.SetProcessInputs(true);
                _graph.Connect(_controllerPlayable, 0, _inertializationPlayable, 0);
                _inertializationPlayable.SetInputWeight(0, 1f);
                _baseOutput = AnimationPlayableOutput.Create(
                    _graph,
                    "Earth Controller + Pose Inertialization",
                    animator);
                _baseOutput.SetSourcePlayable(_inertializationPlayable);
                _baseOutput.SetWeight(1f);
                _baseOutput.SetSortingOrder(0);

                _rigOutputStartIndex = _graph.GetOutputCount();
                bool rigLayersRequested = _rigBuilder != null && _rigBuilder.layers.Count > 0;
                bool rigBuildSucceeded = rigLayersRequested && _rigBuilder.Build(_graph);
                _rigOutputCount = _graph.GetOutputCount() - _rigOutputStartIndex;
                _rigLayersAppended = rigBuildSucceeded && _rigOutputCount > 0;
                RequestInitialPoseContinuity();
                _graph.Play();
                _fallbackReason = EarthAnimationGraphFallbackReason.None;
                if (!ValidateTopology())
                    return FailToLegacy(
                        EarthAnimationGraphFallbackReason.InvalidTopology,
                        null);
                _stateHandoffCount++;
                return true;
            }
            catch (Exception exception)
            {
                return FailToLegacy(EarthAnimationGraphFallbackReason.GraphBuildFailed, exception);
            }
        }

        private bool FailToLegacy(EarthAnimationGraphFallbackReason reason, Exception exception)
        {
            ShutdownGraph(true, reason, false);
            string detail = exception != null ? $": {exception.Message}" : string.Empty;
            Debug.LogWarning(
                $"Earth animation graph fell back to legacy Animator ({reason}){detail}",
                this);
            return false;
        }

        private void ShutdownGraph(
            bool restoreLegacyRig,
            EarthAnimationGraphFallbackReason reason,
            bool preserveControllerState)
        {
            bool stateCaptured = preserveControllerState && CapturePlayableHandoffState();
            bool hadExternalRig = _rigLayersAppended;
            _rigLayersAppended = false;
            if (hadExternalRig && _rigBuilder != null) _rigBuilder.Clear();
            if (_graph.IsValid())
            {
                _graph.Stop();
                _graph.Destroy();
            }
            _controllerPlayable = default;
            _inertializationPlayable = default;
            _baseOutput = default;
            _rigOutputStartIndex = 0;
            _rigOutputCount = 0;
            _poseHistory?.Dispose();
            _poseHistory = null;
            _fallbackReason = reason;
            _runtimeDisablePending = false;
            _poseDisablePending = false;
            if (reason != EarthAnimationGraphFallbackReason.None)
                _runtimeEnablePending = false;

            if (!restoreLegacyRig) return;
            if (_rigBuilder != null)
            {
                _rigBuilder.enabled = _legacyRigBuilderWasEnabled;
                if (_legacyRigBuilderWasEnabled && Application.isPlaying &&
                    _rigBuilder.isActiveAndEnabled && !_rigBuilder.graph.IsValid())
                    _rigBuilder.Build();
            }
            if (stateCaptured) ApplyHandoffStateToAnimator();
        }

        private void RefreshControlSettings()
        {
            if (_poseHistory == null || !_poseHistory.Control.IsCreated) return;
            var controls = _poseHistory.Control;
            EarthAnimationGraphControl control = controls[0];
            control.UsePoseInertialization = _activeSettings.UsePoseInertialization ? (byte)1 : (byte)0;
            control.PositionHalfLifeSeconds = _activeSettings.PositionHalfLifeSeconds;
            control.RotationHalfLifeSeconds = _activeSettings.RotationHalfLifeSeconds;
            control.MaximumDurationSeconds = _activeSettings.MaximumDurationSeconds;
            control.MaximumPositionOffset = _activeSettings.MaximumPositionOffsetMeters;
            control.MaximumRotationOffsetRadians = _activeSettings.MaximumRotationOffsetRadians;
            control.MaximumLinearVelocity = _activeSettings.MaximumLinearVelocity;
            control.MaximumAngularVelocity = _activeSettings.MaximumAngularVelocityRadians;
            controls[0] = control;
        }

        private void UpdateOwnershipMask()
        {
            if (_poseHistory == null || !_poseHistory.Control.IsCreated) return;
            EarthAnimationBoneOwnership active = EarthAnimationBoneOwnership.None;
            if (footContactController != null)
            {
                if (footContactController.LeftFootLocked)
                    active |= EarthAnimationBoneOwnership.LeftFootPlant;
                if (footContactController.RightFootLocked)
                    active |= EarthAnimationBoneOwnership.RightFootPlant;
            }
            if (_leftHandContact) active |= EarthAnimationBoneOwnership.LeftHandContact;
            if (_rightHandContact) active |= EarthAnimationBoneOwnership.RightHandContact;
            if (visibleRagdoll != null && visibleRagdoll.IsRagdollActive)
                active |= EarthAnimationBoneOwnership.FullRagdoll;
            var controls = _poseHistory.Control;
            EarthAnimationGraphControl control = controls[0];
            control.ActiveOwnership = active;
            controls[0] = control;
        }

        private void MirrorAnimatorControllerInputs()
        {
            if (!IsActive || animator == null || _controllerParameters == null) return;
            for (int index = 0; index < _controllerParameters.Length; index++)
            {
                if (!_externallyWritableControllerParameters[index]) continue;
                AnimatorControllerParameter parameter = _controllerParameters[index];
                int hash = parameter.nameHash;
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float:
                        _controllerPlayable.SetFloat(hash, animator.GetFloat(hash));
                        break;
                    case AnimatorControllerParameterType.Int:
                        _controllerPlayable.SetInteger(hash, animator.GetInteger(hash));
                        break;
                    case AnimatorControllerParameterType.Bool:
                        _controllerPlayable.SetBool(hash, animator.GetBool(hash));
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        break;
                }
            }
            int layerCount = animator.layerCount;
            for (int layer = 0; layer < layerCount; layer++)
                _controllerPlayable.SetLayerWeight(layer, animator.GetLayerWeight(layer));
            _controllerPlayable.SetSpeed(animator.speed);
        }

        private bool ValidateTopology()
        {
            if (!_graph.IsValid() || !_controllerPlayable.IsValid() ||
                !_inertializationPlayable.IsValid() || !_baseOutput.IsOutputValid())
                return false;
            Playable input = _inertializationPlayable.GetInput(0);
            return input.IsValid() && input.Equals((Playable)_controllerPlayable) &&
                   _baseOutput.GetSourcePlayable().Equals((Playable)_inertializationPlayable) &&
                   _baseOutput.GetSortingOrder() == 0 &&
                   ValidateRigOutputs();
        }

        private bool ValidateRigOutputs()
        {
            bool rigLayersExpected = _rigBuilder != null && _rigBuilder.layers.Count > 0;
            if (!rigLayersExpected) return !_rigLayersAppended && _rigOutputCount == 0;
            if (!_rigLayersAppended || _rigOutputCount <= 0 || !_graph.IsValid()) return false;
            int outputCount = _graph.GetOutputCount();
            if (_rigOutputStartIndex < 1 ||
                _rigOutputStartIndex + _rigOutputCount > outputCount)
                return false;
            for (int index = 0; index < _rigOutputCount; index++)
            {
                AnimationPlayableOutput output =
                    (AnimationPlayableOutput)_graph.GetOutput(_rigOutputStartIndex + index);
                if (!output.IsOutputValid() || output.GetTarget() != animator ||
                    output.GetSortingOrder() <= _baseOutput.GetSortingOrder() ||
                    output.GetAnimationStreamSource() != AnimationStreamSource.PreviousInputs ||
                    !output.GetSourcePlayable().IsValid())
                    return false;
            }
            return true;
        }

        private bool CanShutdownContinuously() =>
            !IsInertiaActive() && !IsAnyControllerLayerTransitioning();

        private bool IsLegacyAnimatorTransitioning()
        {
            if (animator == null || !animator.isActiveAndEnabled) return false;
            for (int layer = 0; layer < animator.layerCount; layer++)
                if (animator.IsInTransition(layer)) return true;
            return false;
        }

        private bool IsInertiaActive() => _poseHistory != null &&
                                          _poseHistory.Diagnostics.IsCreated &&
                                          _poseHistory.Diagnostics[0].InertiaActive != 0;

        private bool IsAnyControllerLayerTransitioning()
        {
            if (!IsActive) return false;
            for (int layer = 0; layer < _controllerPlayable.GetLayerCount(); layer++)
                if (_controllerPlayable.IsInTransition(layer)) return true;
            return false;
        }

        private void CacheControllerParameters()
        {
            if (_controllerParameters != null || animator == null) return;

            _controllerParameters = animator.parameters;
            _externallyWritableControllerParameters =
                new bool[_controllerParameters.Length];
            // This query must run while the legacy Animator still owns its graph.
            // Once the external PlayableGraph is active Unity no longer reports
            // the controller's curve ownership reliably.
            for (int index = 0; index < _controllerParameters.Length; index++)
            {
                AnimatorControllerParameter parameter = _controllerParameters[index];
                bool supported = parameter.type == AnimatorControllerParameterType.Float ||
                                 parameter.type == AnimatorControllerParameterType.Int ||
                                 parameter.type == AnimatorControllerParameterType.Bool;
                _externallyWritableControllerParameters[index] = supported &&
                    !animator.IsParameterControlledByCurve(parameter.nameHash);
            }
        }

        private void EnsureHandoffBuffers()
        {
            int layerCount = animator != null ? animator.layerCount : 0;
            if (_handoffStates.Length == layerCount) return;
            _handoffStates = new AnimatorStateInfo[layerCount];
            _handoffLayerWeights = new float[layerCount];
            _handoffStateValid = new bool[layerCount];
        }

        private void CaptureAnimatorHandoffState()
        {
            if (animator == null) return;
            CacheControllerParameters();
            EnsureHandoffBuffers();
            for (int layer = 0; layer < _handoffStates.Length; layer++)
            {
                AnimatorStateInfo state = animator.IsInTransition(layer)
                    ? animator.GetNextAnimatorStateInfo(layer)
                    : animator.GetCurrentAnimatorStateInfo(layer);
                _handoffStates[layer] = state;
                _handoffLayerWeights[layer] = animator.GetLayerWeight(layer);
                _handoffStateValid[layer] = state.fullPathHash != 0;
            }
        }

        private void ApplyAnimatorStateToControllerPlayable()
        {
            if (!_controllerPlayable.IsValid() || animator == null) return;
            for (int index = 0; index < _controllerParameters.Length; index++)
            {
                if (!_externallyWritableControllerParameters[index]) continue;
                AnimatorControllerParameter parameter = _controllerParameters[index];
                int hash = parameter.nameHash;
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float:
                        _controllerPlayable.SetFloat(hash, animator.GetFloat(hash));
                        break;
                    case AnimatorControllerParameterType.Int:
                        _controllerPlayable.SetInteger(hash, animator.GetInteger(hash));
                        break;
                    case AnimatorControllerParameterType.Bool:
                        _controllerPlayable.SetBool(hash, animator.GetBool(hash));
                        break;
                }
            }
            for (int layer = 0; layer < _handoffStates.Length; layer++)
            {
                _controllerPlayable.SetLayerWeight(layer, _handoffLayerWeights[layer]);
                if (_handoffStateValid[layer])
                    _controllerPlayable.Play(
                        _handoffStates[layer].fullPathHash,
                        layer,
                        _handoffStates[layer].normalizedTime);
            }
            _controllerPlayable.SetSpeed(animator.speed);
        }

        private bool CapturePlayableHandoffState()
        {
            if (!IsActive || animator == null) return false;
            CacheControllerParameters();
            EnsureHandoffBuffers();
            for (int index = 0; index < _controllerParameters.Length; index++)
            {
                if (!_externallyWritableControllerParameters[index]) continue;
                AnimatorControllerParameter parameter = _controllerParameters[index];
                int hash = parameter.nameHash;
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float:
                        animator.SetFloat(hash, _controllerPlayable.GetFloat(hash));
                        break;
                    case AnimatorControllerParameterType.Int:
                        animator.SetInteger(hash, _controllerPlayable.GetInteger(hash));
                        break;
                    case AnimatorControllerParameterType.Bool:
                        animator.SetBool(hash, _controllerPlayable.GetBool(hash));
                        break;
                }
            }
            for (int layer = 0; layer < _handoffStates.Length; layer++)
            {
                AnimatorStateInfo state = _controllerPlayable.GetCurrentAnimatorStateInfo(layer);
                _handoffStates[layer] = state;
                _handoffLayerWeights[layer] = _controllerPlayable.GetLayerWeight(layer);
                _handoffStateValid[layer] = state.fullPathHash != 0;
            }
            animator.speed = (float)_controllerPlayable.GetSpeed();
            _stateHandoffCount++;
            return true;
        }

        private void ApplyHandoffStateToAnimator()
        {
            if (animator == null || !animator.enabled) return;
            for (int layer = 0; layer < _handoffStates.Length; layer++)
            {
                animator.SetLayerWeight(layer, _handoffLayerWeights[layer]);
                if (_handoffStateValid[layer])
                    animator.Play(
                        _handoffStates[layer].fullPathHash,
                        layer,
                        _handoffStates[layer].normalizedTime);
            }
            animator.Update(0f);
        }

        private void RequestInitialPoseContinuity()
        {
            if (!_activeSettings.UsePoseInertialization || _poseHistory == null) return;
            var controls = _poseHistory.Control;
            EarthAnimationGraphControl control = controls[0];
            control.RequestSequence = control.RequestSequence == uint.MaxValue
                ? 1u
                : control.RequestSequence + 1u;
            controls[0] = control;
        }

        private static bool SettingsEqual(
            in EarthAnimationGraphSettings left,
            in EarthAnimationGraphSettings right) =>
            left.UsePlayablesAnimationGraph == right.UsePlayablesAnimationGraph &&
            left.UsePoseInertialization == right.UsePoseInertialization &&
            Mathf.Approximately(left.PositionHalfLifeSeconds, right.PositionHalfLifeSeconds) &&
            Mathf.Approximately(left.RotationHalfLifeSeconds, right.RotationHalfLifeSeconds) &&
            Mathf.Approximately(left.MaximumDurationSeconds, right.MaximumDurationSeconds) &&
            Mathf.Approximately(left.MaximumPositionOffsetMeters, right.MaximumPositionOffsetMeters) &&
            Mathf.Approximately(left.MaximumRotationOffsetRadians, right.MaximumRotationOffsetRadians) &&
            Mathf.Approximately(left.MaximumLinearVelocity, right.MaximumLinearVelocity) &&
            Mathf.Approximately(left.MaximumAngularVelocityRadians, right.MaximumAngularVelocityRadians);

        private static int CountBoundBones(Animator targetAnimator)
        {
            if (!targetAnimator.isHuman) return 0;
            int count = 0;
            for (int index = 0; index < EarthAnimationBoneMask.TrackedBoneCount; index++)
            {
                if (targetAnimator.GetBoneTransform(EarthAnimationBoneMask.BoneAt(index)) != null)
                    count++;
            }
            return count;
        }

        private static void BindBones(Animator targetAnimator, EarthPoseHistory history)
        {
            if (!targetAnimator.isHuman) return;
            var boneHandles = history.BoneHandles;
            var boneOwnership = history.BoneOwnership;
            var initialized = history.Initialized;
            var previousTargetPositions = history.PreviousTargetPositions;
            var previousTargetRotations = history.PreviousTargetRotations;
            var previousOutputPositions = history.PreviousOutputPositions;
            var previousOutputRotations = history.PreviousOutputRotations;
            int writeIndex = 0;
            for (int index = 0; index < EarthAnimationBoneMask.TrackedBoneCount; index++)
            {
                HumanBodyBones bone = EarthAnimationBoneMask.BoneAt(index);
                Transform transform = targetAnimator.GetBoneTransform(bone);
                if (transform == null) continue;
                boneHandles[writeIndex] = targetAnimator.BindStreamTransform(transform);
                boneOwnership[writeIndex] = EarthAnimationBoneMask.OwnershipFor(bone);
                initialized[writeIndex] = 1;
                float3 localPosition = new float3(
                    transform.localPosition.x,
                    transform.localPosition.y,
                    transform.localPosition.z);
                quaternion localRotation = new quaternion(
                    transform.localRotation.x,
                    transform.localRotation.y,
                    transform.localRotation.z,
                    transform.localRotation.w);
                previousTargetPositions[writeIndex] = localPosition;
                previousTargetRotations[writeIndex] = localRotation;
                previousOutputPositions[writeIndex] = localPosition;
                previousOutputRotations[writeIndex] = localRotation;
                writeIndex++;
            }
        }
    }
}
