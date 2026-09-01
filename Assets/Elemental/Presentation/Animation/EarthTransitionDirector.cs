using System.Collections.Generic;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Characters;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    /// <summary>
    /// Sole adapter allowed to change the Humanoid base state. The pure policy
    /// selects timing/continuity; this component only executes the Animator call
    /// and exposes bounded telemetry.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class EarthTransitionDirector : MonoBehaviour
    {
        private static readonly ProfilerMarker TransitionMarker =
            new ProfilerMarker("Elemental.Character.Transition");
        private static readonly ProfilerMarker QueueMarker =
            new ProfilerMarker("Elemental.Character.TransitionQueue");
        private static readonly ProfilerMarker MotionBindingMarker =
            new ProfilerMarker("Elemental.Character.MotionCatalogBinding");
        private static readonly int CanExitHash = Animator.StringToHash(
            EarthAnimationClipMetadata.CanExit);

        [SerializeField] private Animator animator;
        [SerializeField] private CharacterPresentationProfile profile;
        [SerializeField] private EarthAnimationGraph animationGraph;
        [SerializeField] private EarthTransitionProfile transitionProfile;
        [SerializeField] private EarthMotionCatalog motionCatalog;

        private readonly EarthTransitionQueue _transitionQueue =
            new EarthTransitionQueue(EarthTransitionQueue.MaximumCapacity);
        private readonly bool[] _warnedFallbackPairs = new bool[256];
        private readonly List<AnimatorClipInfo> _clipInfoScratch =
            new List<AnimatorClipInfo>(32);
        private EarthFootContactController _footContactController;
        private EarthMotionStateId _activeState;
        private int _activeStateHash;
        private EarthAnimationTransitionPriority _activePriority;
        private float _transitionStartedAt;
        private float _transitionDuration;
        private CharacterPhysicalMode _baseStateOwnerMode;
        private int _ownedBaseStateHash;
        private float _ownedBaseStatePhase;
        private bool _hasOwnedBaseStatePhase;
        private bool _hasCanExitParameter;
        private EarthTransitionProfileResolution _lastProfileResolution;
        private int _lastPairIndex = -1;
        private EarthTransitionRule _lastRule;
        private uint _authoredPairExecutionCount;
        private uint _genericFallbackExecutionCount;
        private uint _queuedRequestCount;
        private uint _dequeuedExecutionCount;
        private uint _queueRejectionCount;
        private int _runtimeLayerCount;
        private int _verifiedRuntimeLayerCount;
        private int _inactiveRuntimeLayerCount;
        private int _unresolvedRuntimeLayerCount;
        private EarthMotionStateResolution _baseLayerMotion;
        private uint _motionResolutionCount;
        private uint _motionResolutionMissCount;
        private bool _lastAuthoredPairProfilesVerified;
        private int _lastPairSourceProfileIndex = -1;
        private int _lastPairDestinationProfileIndex = -1;

        public EarthMotionStateId ActiveState => _activeState;
        public int ActiveStateHash => _activeStateHash;
        public EarthAnimationTransitionDecision LastDecision { get; private set; }
        public EarthAnimationTransitionKind ActiveTransitionKind => LastDecision.Kind;
        public EarthAnimationTransitionReason LastReason => LastDecision.Reason;
        public EarthAnimationInertializationReason LastInertializationReason { get; private set; }
        public uint ImmediateEvaluationSequence { get; private set; }
        public CharacterPhysicalMode BaseStateOwnerMode => _baseStateOwnerMode;
        public int OwnedBaseStateHash => _ownedBaseStateHash;
        public uint RecoveryOwnedTransitionRejectCount { get; private set; }
        public uint RecoveryOwnedStateRestoreCount { get; private set; }
        public int LastRecoveryOwnedRejectedStateHash { get; private set; }
        public EarthTransitionProfile TransitionProfile => transitionProfile;
        public EarthMotionCatalog MotionCatalog => motionCatalog;
        public EarthTransitionDirectorDiagnostics Diagnostics =>
            new EarthTransitionDirectorDiagnostics(
                transitionProfile != null && transitionProfile.UseTransitionProfile,
                transitionProfile != null && transitionProfile.UseTransitionQueue,
                _transitionQueue.Count,
                _lastProfileResolution,
                _lastPairIndex,
                in _lastRule,
                _authoredPairExecutionCount,
                _genericFallbackExecutionCount,
                _queuedRequestCount,
                _dequeuedExecutionCount,
                _queueRejectionCount,
                motionCatalog != null,
                _runtimeLayerCount,
                _verifiedRuntimeLayerCount,
                _inactiveRuntimeLayerCount,
                _unresolvedRuntimeLayerCount,
                in _baseLayerMotion,
                _motionResolutionCount,
                _motionResolutionMissCount,
                _lastAuthoredPairProfilesVerified,
                _lastPairSourceProfileIndex,
                _lastPairDestinationProfileIndex);
        public float TransitionElapsedSeconds => Mathf.Max(0f, Time.time - _transitionStartedAt);
        public float TransitionWeight => _transitionDuration > 0.0001f
            ? Mathf.Clamp01(TransitionElapsedSeconds / _transitionDuration)
            : 1f;

        public void Configure(Animator configuredAnimator, CharacterPresentationProfile configuredProfile)
        {
            animator = configuredAnimator;
            profile = configuredProfile;
            animationGraph = animator != null ? animator.GetComponent<EarthAnimationGraph>() : null;
            _footContactController = animator != null
                ? animator.GetComponent<EarthFootContactController>()
                : null;
            CacheCanExitParameter();
            ResetProfileRuntimeState();
            ResetMotionCatalogDiagnostics();
            _activeState = EarthMotionStateId.None;
            _activeStateHash = 0;
            _activePriority = EarthAnimationTransitionPriority.Idle;
            _baseStateOwnerMode = CharacterPhysicalMode.AnimatedMotor;
            _ownedBaseStateHash = 0;
            LastDecision = default;
            LastInertializationReason = EarthAnimationInertializationReason.None;
        }

        public void ConfigureTransitionProfile(EarthTransitionProfile configuredProfile)
        {
            transitionProfile = configuredProfile;
            ResetProfileRuntimeState();
        }

        public void ConfigureMotionCatalog(EarthMotionCatalog configuredCatalog)
        {
            motionCatalog = configuredCatalog;
            ResetMotionCatalogDiagnostics();
        }

        public bool TryResolveMotionState(
            int stateHash,
            AnimationClip activeClip,
            out EarthMotionStateResolution resolution)
        {
            if (motionCatalog != null)
                return motionCatalog.TryResolveControllerState(
                    stateHash,
                    activeClip,
                    out resolution);
            resolution = default;
            return false;
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (animationGraph == null) animationGraph = GetComponent<EarthAnimationGraph>();
            if (_footContactController == null)
                _footContactController = GetComponent<EarthFootContactController>();
            CacheCanExitParameter();
        }

        private void OnDisable() => _transitionQueue.Clear();

        public bool RequestPoseInertialization(
            EarthAnimationInertializationReason reason,
            float durationSeconds)
        {
            if (reason == EarthAnimationInertializationReason.None || animationGraph == null)
                return false;
            bool accepted = animationGraph.BeginInertialization(durationSeconds);
            if (accepted) LastInertializationReason = reason;
            return accepted;
        }

        public void SynchronizeBaseStateOwnership(
            CharacterPhysicalMode mode,
            int ownedStateHash)
        {
            if (mode == CharacterPhysicalMode.Recovery && ownedStateHash != 0)
            {
                if (_baseStateOwnerMode != mode || _ownedBaseStateHash != ownedStateHash)
                    _hasOwnedBaseStatePhase = false;
                _baseStateOwnerMode = mode;
                _ownedBaseStateHash = ownedStateHash;
                return;
            }

            _baseStateOwnerMode = mode;
            _ownedBaseStateHash = 0;
            _hasOwnedBaseStatePhase = false;
        }

        public bool RequestTransition(
            int destinationHash,
            in EarthAnimationTransitionContext context)
        {
            using (TransitionMarker.Auto())
            {
                if (_baseStateOwnerMode == CharacterPhysicalMode.Recovery &&
                    _ownedBaseStateHash != 0 &&
                    destinationHash != _ownedBaseStateHash)
                {
                    LastRecoveryOwnedRejectedStateHash = destinationHash;
                    RecoveryOwnedTransitionRejectCount =
                        RecoveryOwnedTransitionRejectCount == uint.MaxValue
                            ? 1u
                            : RecoveryOwnedTransitionRejectCount + 1u;
                    return false;
                }
                EarthTransitionRule rule = default;
                int pairIndex = -1;
                bool usedGenericFallback = false;
                bool profileResolved = transitionProfile != null &&
                    transitionProfile.TryResolve(
                        in context,
                        out rule,
                        out pairIndex,
                        out usedGenericFallback);
                EarthAnimationTransitionDecision decision;
                if (profileResolved)
                {
                    decision = EarthTransitionRulePolicy.Resolve(in context, in rule);
                    _lastRule = rule;
                    _lastPairIndex = pairIndex;
                    _lastProfileResolution = usedGenericFallback
                        ? EarthTransitionProfileResolution.GenericFallback
                        : EarthTransitionProfileResolution.AuthoredPair;
                }
                else
                {
                    EarthAnimationTransitionTuning tuning = ResolveTuning();
                    decision = EarthAnimationTransitionPolicy.Resolve(in context, in tuning);
                    _lastRule = default;
                    _lastPairIndex = -1;
                    _lastProfileResolution = EarthTransitionProfileResolution.LegacyPolicy;
                    if (_transitionQueue.Count > 0) _transitionQueue.Clear();
                }
                LastDecision = decision;
                if (!decision.ShouldTransition)
                {
                    if (profileResolved && ShouldQueue(in decision, in rule))
                        QueueTransition(destinationHash, in context, in rule);
                    return false;
                }
                if (animator == null || !animator.enabled) return false;

                return ExecuteTransition(
                    destinationHash,
                    in context,
                    in decision,
                    profileResolved,
                    usedGenericFallback,
                    in rule,
                    false);
            }
        }

        public void SynchronizeState(
            EarthMotionStateId state,
            int stateHash,
            EarthAnimationTransitionPriority priority)
        {
            _activeState = state;
            _activeStateHash = stateHash;
            _activePriority = priority;
        }

        public void ForcePlayImmediate(
            EarthMotionStateId state,
            int stateHash,
            float normalizedTime = 0f)
        {
            if (animator == null || !animator.enabled || stateHash == 0) return;
            if (animationGraph != null && animationGraph.IsActive)
                animationGraph.Play(stateHash, 0, Mathf.Clamp01(normalizedTime));
            else
                animator.Play(stateHash, 0, Mathf.Clamp01(normalizedTime));
            _activeState = state;
            _activeStateHash = stateHash;
            _activePriority = EarthAnimationTransitionPriority.Idle;
            _transitionStartedAt = Time.time;
            _transitionDuration = 0f;
            // Animator.Play queues the state change until the graph evaluates.
            // This method is the sole immediate base-state writer, so complete
            // that evaluation before returning to ordered handoff observers.
            animator.Update(0f);
            CaptureOwnedBaseStatePhase();
            ImmediateEvaluationSequence = ImmediateEvaluationSequence == uint.MaxValue
                ? 1u
                : ImmediateEvaluationSequence + 1u;
        }

        private void LateUpdate()
        {
            RefreshMotionCatalogDiagnostics();
            if (_baseStateOwnerMode != CharacterPhysicalMode.Recovery ||
                _ownedBaseStateHash == 0 || animator == null || !animator.enabled)
            {
                ProcessQueuedTransition();
                return;
            }

            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            bool currentOwned = current.fullPathHash == _ownedBaseStateHash;
            bool leavingOwnedState = animator.IsInTransition(0) &&
                                     animator.GetNextAnimatorStateInfo(0).fullPathHash !=
                                     _ownedBaseStateHash;
            if (currentOwned && !leavingOwnedState)
            {
                _ownedBaseStatePhase = Mathf.Repeat(current.normalizedTime, 1f);
                _hasOwnedBaseStatePhase = true;
                return;
            }

            float recoveryPhase = currentOwned
                ? Mathf.Repeat(current.normalizedTime, 1f)
                : _hasOwnedBaseStatePhase
                    ? _ownedBaseStatePhase
                    : 0f;
            ForcePlayImmediate(
                EarthMotionStateId.KnockdownRecovery,
                _ownedBaseStateHash,
                recoveryPhase);
            SynchronizeState(
                EarthMotionStateId.KnockdownRecovery,
                _ownedBaseStateHash,
                EarthAnimationTransitionPriority.HeavyImpact);
            RecoveryOwnedStateRestoreCount = RecoveryOwnedStateRestoreCount == uint.MaxValue
                ? 1u
                : RecoveryOwnedStateRestoreCount + 1u;
        }

        private bool ExecuteTransition(
            int destinationHash,
            in EarthAnimationTransitionContext context,
            in EarthAnimationTransitionDecision decision,
            bool profileResolved,
            bool usedGenericFallback,
            in EarthTransitionRule rule,
            bool dequeued)
        {
            if (profileResolved && !usedGenericFallback)
                CapturePairCatalogBinding(destinationHash, in context);
            else
                ResetPairCatalogBinding();

            bool inertialized = decision.RequestsInertialization &&
                                animationGraph != null &&
                                (profileResolved
                                    ? animationGraph.BeginInertialization(
                                        decision.DurationSeconds,
                                        rule.HalfLifeSeconds,
                                        rule.BodyMask)
                                    : animationGraph.BeginInertialization(
                                        decision.DurationSeconds));
            if (profileResolved && _footContactController != null)
            {
                EarthTransitionFootReleasePolicy releasePolicy =
                    EarthTransitionRulePolicy.ResolveFootReleasePolicy(in rule);
                _footContactController.BeginTransitionFootRelease(
                    releasePolicy,
                    rule.FootReleaseSeconds);
            }

            if (inertialized)
            {
                LastInertializationReason = ResolveInertializationReason(in context);
                if (decision.UseNormalizedStart)
                    animationGraph.Play(
                        destinationHash,
                        0,
                        decision.DestinationNormalizedTime);
                else
                    animationGraph.PlayInFixedTime(
                        destinationHash,
                        0,
                        decision.DestinationStartSeconds);
            }
            else if (decision.UseNormalizedStart)
            {
                float normalizedDuration = Mathf.Clamp01(
                    decision.DurationSeconds /
                    Mathf.Max(0.01f, context.DestinationCycleSeconds));
                if (animationGraph != null && animationGraph.IsActive)
                    animationGraph.CrossFade(
                        destinationHash,
                        normalizedDuration,
                        0,
                        decision.DestinationNormalizedTime);
                else
                    animator.CrossFade(
                        destinationHash,
                        normalizedDuration,
                        0,
                        decision.DestinationNormalizedTime);
            }
            else
            {
                if (animationGraph != null && animationGraph.IsActive)
                    animationGraph.CrossFadeInFixedTime(
                        destinationHash,
                        decision.DurationSeconds,
                        0,
                        decision.DestinationStartSeconds);
                else
                    animator.CrossFadeInFixedTime(
                        destinationHash,
                        decision.DurationSeconds,
                        0,
                        decision.DestinationStartSeconds);
            }

            if (profileResolved)
            {
                _transitionQueue.CancelAtOrBelow(rule.Priority);
                if (usedGenericFallback)
                {
                    _genericFallbackExecutionCount = Increment(
                        _genericFallbackExecutionCount);
                    WarnGenericFallback(in context);
                }
                else
                {
                    _authoredPairExecutionCount = Increment(
                        _authoredPairExecutionCount);
                }
                if (dequeued)
                {
                    _dequeuedExecutionCount = Increment(_dequeuedExecutionCount);
                    _lastProfileResolution = EarthTransitionProfileResolution.Queued;
                }
            }

            _activeState = context.DestinationState;
            _activeStateHash = destinationHash;
            _activePriority = profileResolved ? rule.Priority : context.RequestPriority;
            _transitionStartedAt = Time.time;
            _transitionDuration = decision.DurationSeconds;
            return true;
        }

        private bool ShouldQueue(
            in EarthAnimationTransitionDecision decision,
            in EarthTransitionRule rule) =>
            transitionProfile != null &&
            transitionProfile.UseTransitionQueue &&
            rule.QueueWhenBlocked &&
            (decision.Reason == EarthAnimationTransitionReason.ProtectedSource ||
             decision.Reason == EarthAnimationTransitionReason.LowerPriority);

        private void QueueTransition(
            int destinationHash,
            in EarthAnimationTransitionContext context,
            in EarthTransitionRule rule)
        {
            if (transitionProfile == null)
            {
                _queueRejectionCount = Increment(_queueRejectionCount);
                _lastProfileResolution = EarthTransitionProfileResolution.QueueRejected;
                return;
            }

            EarthTransitionQueueResult result = _transitionQueue.Enqueue(
                destinationHash,
                in context,
                in rule,
                Time.time,
                transitionProfile.QueueCapacity);
            if (result == EarthTransitionQueueResult.Enqueued ||
                result == EarthTransitionQueueResult.ReplacedDuplicate)
            {
                _queuedRequestCount = Increment(_queuedRequestCount);
                _lastProfileResolution = EarthTransitionProfileResolution.Queued;
            }
            else
            {
                _queueRejectionCount = Increment(_queueRejectionCount);
                _lastProfileResolution = EarthTransitionProfileResolution.QueueRejected;
            }
        }

        private void ProcessQueuedTransition()
        {
            if (_transitionQueue.Count == 0) return;
            if (transitionProfile == null || !transitionProfile.UseTransitionQueue)
            {
                _transitionQueue.Clear();
                return;
            }
            if (animator == null || !animator.enabled) return;

            using (QueueMarker.Auto())
            {
                AnimatorStateInfo state = animationGraph != null
                    ? animationGraph.GetCurrentAnimatorStateInfo(0)
                    : animator.GetCurrentAnimatorStateInfo(0);
                EarthMotionStateId sourceState = _activeState;
                float sourcePhase = Mathf.Repeat(state.normalizedTime, 1f);
                bool mayInterrupt = !_hasCanExitParameter ||
                                    animator.GetFloat(CanExitHash) >= 0.5f;
                EarthTransitionQueueGate gate = new EarthTransitionQueueGate(
                    sourceState,
                    sourcePhase,
                    _activePriority,
                    mayInterrupt);
                if (!_transitionQueue.TryDequeueEligible(
                        in gate,
                        out EarthQueuedTransition queued))
                    return;

                EarthAnimationTransitionContext queuedContext = queued.Context;
                EarthAnimationTransitionContext refreshed = RefreshQueuedContext(
                    in queuedContext,
                    sourceState == EarthMotionStateId.None
                        ? queuedContext.SourceState
                        : sourceState,
                    sourcePhase,
                    _activePriority,
                    mayInterrupt);
                EarthTransitionRule queuedRule = queued.Rule;
                EarthAnimationTransitionDecision decision =
                    EarthTransitionRulePolicy.Resolve(in refreshed, in queuedRule);
                LastDecision = decision;
                _lastRule = queuedRule;
                _lastPairIndex = ResolvePairIndex(in refreshed, in queuedRule);
                if (!decision.ShouldTransition) return;

                ExecuteTransition(
                    queued.DestinationHash,
                    in refreshed,
                    in decision,
                    true,
                    false,
                    in queuedRule,
                    true);
            }
        }

        private int ResolvePairIndex(
            in EarthAnimationTransitionContext context,
            in EarthTransitionRule expectedRule)
        {
            if (transitionProfile == null ||
                !transitionProfile.TryResolve(
                    in context,
                    out EarthTransitionRule resolved,
                    out int pairIndex,
                    out bool usedFallback) ||
                usedFallback)
                return -1;
            return resolved.Family == expectedRule.Family &&
                   resolved.Priority == expectedRule.Priority
                ? pairIndex
                : -1;
        }

        private static EarthAnimationTransitionContext RefreshQueuedContext(
            in EarthAnimationTransitionContext queued,
            EarthMotionStateId sourceState,
            float sourceNormalizedTime,
            EarthAnimationTransitionPriority activePriority,
            bool mayInterrupt) =>
            new EarthAnimationTransitionContext(
                sourceState,
                queued.DestinationState,
                queued.SourceCategory,
                queued.DestinationCategory,
                queued.RequestPriority,
                activePriority,
                sourceNormalizedTime,
                queued.GaitPhase01,
                queued.DestinationCycleSeconds,
                queued.LandingContactSeconds,
                queued.PredictedTimeToContact,
                queued.HasLandingPrediction,
                mayInterrupt,
                queued.ForceRestart,
                queued.RequestInertialization);

        private void CaptureOwnedBaseStatePhase()
        {
            if (_baseStateOwnerMode != CharacterPhysicalMode.Recovery ||
                animator == null || !animator.enabled)
                return;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash != _ownedBaseStateHash) return;
            _ownedBaseStatePhase = Mathf.Repeat(state.normalizedTime, 1f);
            _hasOwnedBaseStatePhase = true;
        }

        private void CacheCanExitParameter()
        {
            _hasCanExitParameter = false;
            if (animator == null) return;
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int index = 0; index < parameters.Length; index++)
            {
                if (parameters[index].nameHash != CanExitHash) continue;
                _hasCanExitParameter = true;
                return;
            }
        }

        private void RefreshMotionCatalogDiagnostics()
        {
            using (MotionBindingMarker.Auto())
            {
                _runtimeLayerCount = animator != null ? animator.layerCount : 0;
                _verifiedRuntimeLayerCount = 0;
                _inactiveRuntimeLayerCount = 0;
                _unresolvedRuntimeLayerCount = 0;
                _baseLayerMotion = default;
                if (motionCatalog == null || animator == null || !animator.enabled)
                    return;

                for (int layerIndex = 0;
                     layerIndex < _runtimeLayerCount;
                     layerIndex++)
                {
                    AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layerIndex);
                    _clipInfoScratch.Clear();
                    animator.GetCurrentAnimatorClipInfo(layerIndex, _clipInfoScratch);
                    AnimationClip dominantClip = null;
                    float dominantWeight = float.NegativeInfinity;
                    for (int clipIndex = 0;
                         clipIndex < _clipInfoScratch.Count;
                         clipIndex++)
                    {
                        AnimatorClipInfo clipInfo = _clipInfoScratch[clipIndex];
                        if (clipInfo.clip == null || clipInfo.weight <= dominantWeight) continue;
                        dominantClip = clipInfo.clip;
                        dominantWeight = clipInfo.weight;
                    }

                    if (motionCatalog.TryResolveControllerState(
                            state.fullPathHash,
                            dominantClip,
                            out EarthMotionStateResolution resolution))
                    {
                        _verifiedRuntimeLayerCount++;
                        _motionResolutionCount = Increment(_motionResolutionCount);
                        if (layerIndex == 0) _baseLayerMotion = resolution;
                    }
                    else
                    {
                        if (dominantClip == null)
                        {
                            _inactiveRuntimeLayerCount++;
                            continue;
                        }
                        _unresolvedRuntimeLayerCount++;
                        _motionResolutionMissCount = Increment(_motionResolutionMissCount);
                    }
                }
            }
        }

        private void CapturePairCatalogBinding(
            int destinationHash,
            in EarthAnimationTransitionContext context)
        {
            int sourceHash = _baseLayerMotion.IsVerified
                ? _baseLayerMotion.StateHash
                : _activeStateHash;
            AnimationClip sourceClip = _baseLayerMotion.IsVerified &&
                                       _baseLayerMotion.StateHash == sourceHash
                ? _baseLayerMotion.Profile?.Clip
                : null;
            if (!EarthMotionTransitionCatalogResolver.TryResolveAuthoredPair(
                    motionCatalog,
                    transitionProfile,
                    sourceHash,
                    sourceClip,
                    destinationHash,
                    null,
                    in context,
                    out EarthVerifiedTransitionPair pair))
            {
                ResetPairCatalogBinding();
                return;
            }

            _lastAuthoredPairProfilesVerified = true;
            _lastPairSourceProfileIndex = pair.Source.ProfileIndex;
            _lastPairDestinationProfileIndex = pair.Destination.ProfileIndex;
        }

        private void ResetPairCatalogBinding()
        {
            _lastAuthoredPairProfilesVerified = false;
            _lastPairSourceProfileIndex = -1;
            _lastPairDestinationProfileIndex = -1;
        }

        private void ResetMotionCatalogDiagnostics()
        {
            _runtimeLayerCount = 0;
            _verifiedRuntimeLayerCount = 0;
            _inactiveRuntimeLayerCount = 0;
            _unresolvedRuntimeLayerCount = 0;
            _baseLayerMotion = default;
            _motionResolutionCount = 0u;
            _motionResolutionMissCount = 0u;
            ResetPairCatalogBinding();
        }

        private void ResetProfileRuntimeState()
        {
            _transitionQueue.Clear();
            for (int index = 0; index < _warnedFallbackPairs.Length; index++)
                _warnedFallbackPairs[index] = false;
            _lastProfileResolution = EarthTransitionProfileResolution.LegacyPolicy;
            _lastPairIndex = -1;
            _lastRule = default;
            _authoredPairExecutionCount = 0u;
            _genericFallbackExecutionCount = 0u;
            _queuedRequestCount = 0u;
            _dequeuedExecutionCount = 0u;
            _queueRejectionCount = 0u;
            ResetPairCatalogBinding();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void WarnGenericFallback(in EarthAnimationTransitionContext context)
        {
            int key = (((int)context.SourceState & 0x0f) << 4) |
                      ((int)context.DestinationState & 0x0f);
            if (_warnedFallbackPairs[key]) return;
            _warnedFallbackPairs[key] = true;
            Debug.LogWarning(
                $"EarthTransitionProfile used generic fixed crossfade for " +
                $"{context.SourceState} -> {context.DestinationState}. " +
                "Author an explicit transition pair before shipping.",
                this);
        }

        private static uint Increment(uint value) =>
            value == uint.MaxValue ? 1u : value + 1u;

        private EarthAnimationTransitionTuning ResolveTuning() => profile != null
            ? new EarthAnimationTransitionTuning(
                profile.LocomotionTransitionSeconds,
                profile.TurnTransitionSeconds,
                profile.TakeoffTransitionSeconds,
                profile.AirborneTransitionSeconds,
                profile.LandingTransitionSeconds,
                profile.AuthoredActionTransitionSeconds,
                profile.RagdollRecoveryTransitionSeconds,
                profile.SurfTransitionSeconds,
                profile.FixedTransitionSeconds,
                profile.UseLegacyTransitionPolicy,
                profile.EnableAnimationInertialization ||
                (animationGraph != null && animationGraph.UsePoseInertialization))
            : EarthAnimationTransitionTuning.Default;

        private static EarthAnimationInertializationReason ResolveInertializationReason(
            in EarthAnimationTransitionContext context)
        {
            if (context.SourceCategory == EarthMotionCategory.RagdollRecovery &&
                context.DestinationCategory == EarthMotionCategory.Locomotion)
                return EarthAnimationInertializationReason.RecoveryToLocomotion;
            if (context.SourceCategory == EarthMotionCategory.Airborne &&
                context.DestinationCategory == EarthMotionCategory.Landing)
                return EarthAnimationInertializationReason.FallToLanding;
            return EarthAnimationInertializationReason.None;
        }
    }
}
