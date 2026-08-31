using System;
using Elemental.Runtime.Characters;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
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
        private bool _configured;
        private bool _rigLayersAppended;
        private bool _legacyRigBuilderWasEnabled;
        private bool _leftHandContact;
        private bool _rightHandContact;
        private AnimatorControllerParameter[] _controllerParameters;
        private EarthAnimationGraphFallbackReason _fallbackReason =
            EarthAnimationGraphFallbackReason.FeatureDisabled;

        public bool IsActive => _graph.IsValid() && _controllerPlayable.IsValid() &&
                                _inertializationPlayable.IsValid();
        public bool UsePoseInertialization => IsActive && _settings.UsePoseInertialization;
        public AnimatorControllerPlayable ControllerPlayable => _controllerPlayable;
        public EarthAnimationGraphProfile Profile => profile;

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
                    !IsActive,
                    _fallbackReason,
                    _poseHistory?.BoneCount ?? 0,
                    job.TransitionRequestCount,
                    job.InterruptedTransitionCount,
                    job.InertiaActive != 0,
                    job.ElapsedSeconds,
                    job.MaximumPositionOffset,
                    job.MaximumRotationOffsetRadians);
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
            return ApplyFeatureState();
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
            return ApplyFeatureState();
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
                EarthAnimationGraphControl control = _poseHistory.Control[0];
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
                _poseHistory.Control[0] = control;
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
            if (_configured) ApplyFeatureState();
        }

        private void Update()
        {
            using (UpdateMarker.Auto())
            {
                if (profile != null)
                {
                    EarthAnimationGraphSettings current = profile.Settings;
                    bool graphFlagChanged = current.UsePlayablesAnimationGraph !=
                                            _settings.UsePlayablesAnimationGraph;
                    _settings = current;
                    if (graphFlagChanged) ApplyFeatureState();
                }

                if (!IsActive) return;
                MirrorAnimatorControllerInputs();
                RefreshControlSettings();
                UpdateOwnershipMask();
                if (_rigLayersAppended && _rigBuilder != null) _rigBuilder.SyncLayers();
            }
        }

        private void OnDisable()
        {
            ShutdownGraph(true, EarthAnimationGraphFallbackReason.ComponentDisabled);
        }

        private void OnDestroy()
        {
            ShutdownGraph(true, EarthAnimationGraphFallbackReason.ComponentDisabled);
        }

        private bool ApplyFeatureState()
        {
            if (!_settings.UsePlayablesAnimationGraph)
            {
                ShutdownGraph(true, EarthAnimationGraphFallbackReason.FeatureDisabled);
                return false;
            }
            return IsActive || TryBuildGraph();
        }

        private bool TryBuildGraph()
        {
            if (animator == null)
                return FailToLegacy(EarthAnimationGraphFallbackReason.MissingAnimator, null);
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            if (controller == null)
                return FailToLegacy(EarthAnimationGraphFallbackReason.MissingController, null);

            ShutdownGraph(true, EarthAnimationGraphFallbackReason.None);
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
                RefreshControlSettings();

                _graph = PlayableGraph.Create($"{animator.gameObject.name}_EarthAnimationGraph");
                _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                _controllerPlayable = AnimatorControllerPlayable.Create(_graph, controller);
                _controllerParameters = animator.parameters;
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

                _rigLayersAppended = _rigBuilder != null &&
                                     _rigBuilder.layers.Count > 0 &&
                                     _rigBuilder.Build(_graph);
                _graph.Play();
                _fallbackReason = EarthAnimationGraphFallbackReason.None;
                return ValidateTopology() ||
                       FailToLegacy(EarthAnimationGraphFallbackReason.InvalidTopology, null);
            }
            catch (Exception exception)
            {
                return FailToLegacy(EarthAnimationGraphFallbackReason.GraphBuildFailed, exception);
            }
        }

        private bool FailToLegacy(EarthAnimationGraphFallbackReason reason, Exception exception)
        {
            ShutdownGraph(true, reason);
            string detail = exception != null ? $": {exception.Message}" : string.Empty;
            Debug.LogWarning(
                $"Earth animation graph fell back to legacy Animator ({reason}){detail}",
                this);
            return false;
        }

        private void ShutdownGraph(bool restoreLegacyRig, EarthAnimationGraphFallbackReason reason)
        {
            bool hadExternalRig = _rigLayersAppended;
            _rigLayersAppended = false;
            if (hadExternalRig && _rigBuilder != null) _rigBuilder.Clear();
            if (_graph.IsValid())
            {
                _graph.Stop();
                _graph.Destroy();
            }
            _controllerPlayable = default;
            _controllerParameters = null;
            _inertializationPlayable = default;
            _baseOutput = default;
            _poseHistory?.Dispose();
            _poseHistory = null;
            _fallbackReason = reason;

            if (!restoreLegacyRig || _rigBuilder == null) return;
            _rigBuilder.enabled = _legacyRigBuilderWasEnabled;
            if (_legacyRigBuilderWasEnabled && Application.isPlaying &&
                _rigBuilder.isActiveAndEnabled && !_rigBuilder.graph.IsValid())
                _rigBuilder.Build();
        }

        private void RefreshControlSettings()
        {
            if (_poseHistory == null || !_poseHistory.Control.IsCreated) return;
            EarthAnimationGraphControl control = _poseHistory.Control[0];
            control.UsePoseInertialization = _settings.UsePoseInertialization ? (byte)1 : (byte)0;
            control.PositionHalfLifeSeconds = _settings.PositionHalfLifeSeconds;
            control.RotationHalfLifeSeconds = _settings.RotationHalfLifeSeconds;
            control.MaximumDurationSeconds = _settings.MaximumDurationSeconds;
            control.MaximumPositionOffset = _settings.MaximumPositionOffsetMeters;
            control.MaximumRotationOffsetRadians = _settings.MaximumRotationOffsetRadians;
            control.MaximumLinearVelocity = _settings.MaximumLinearVelocity;
            control.MaximumAngularVelocity = _settings.MaximumAngularVelocityRadians;
            _poseHistory.Control[0] = control;
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
            EarthAnimationGraphControl control = _poseHistory.Control[0];
            control.ActiveOwnership = active;
            _poseHistory.Control[0] = control;
        }

        private void MirrorAnimatorControllerInputs()
        {
            if (!IsActive || animator == null || _controllerParameters == null) return;
            for (int index = 0; index < _controllerParameters.Length; index++)
            {
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
        }

        private bool ValidateTopology()
        {
            if (!_graph.IsValid() || !_controllerPlayable.IsValid() ||
                !_inertializationPlayable.IsValid() || !_baseOutput.IsOutputValid())
                return false;
            Playable input = _inertializationPlayable.GetInput(0);
            return input.IsValid() && input.Equals((Playable)_controllerPlayable) &&
                   _baseOutput.GetSourcePlayable().Equals((Playable)_inertializationPlayable);
        }

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
            int writeIndex = 0;
            for (int index = 0; index < EarthAnimationBoneMask.TrackedBoneCount; index++)
            {
                HumanBodyBones bone = EarthAnimationBoneMask.BoneAt(index);
                Transform transform = targetAnimator.GetBoneTransform(bone);
                if (transform == null) continue;
                history.BoneHandles[writeIndex] = targetAnimator.BindStreamTransform(transform);
                history.BoneOwnership[writeIndex] = EarthAnimationBoneMask.OwnershipFor(bone);
                writeIndex++;
            }
        }
    }
}
