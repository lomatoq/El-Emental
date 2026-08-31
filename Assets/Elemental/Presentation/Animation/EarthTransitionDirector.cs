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

        [SerializeField] private Animator animator;
        [SerializeField] private CharacterPresentationProfile profile;
        [SerializeField] private EarthAnimationGraph animationGraph;

        private EarthMotionStateId _activeState;
        private int _activeStateHash;
        private EarthAnimationTransitionPriority _activePriority;
        private float _transitionStartedAt;
        private float _transitionDuration;
        private CharacterPhysicalMode _baseStateOwnerMode;
        private int _ownedBaseStateHash;
        private float _ownedBaseStatePhase;
        private bool _hasOwnedBaseStatePhase;

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
        public float TransitionElapsedSeconds => Mathf.Max(0f, Time.time - _transitionStartedAt);
        public float TransitionWeight => _transitionDuration > 0.0001f
            ? Mathf.Clamp01(TransitionElapsedSeconds / _transitionDuration)
            : 1f;

        public void Configure(Animator configuredAnimator, CharacterPresentationProfile configuredProfile)
        {
            animator = configuredAnimator;
            profile = configuredProfile;
            animationGraph = animator != null ? animator.GetComponent<EarthAnimationGraph>() : null;
            _activeState = EarthMotionStateId.None;
            _activeStateHash = 0;
            _activePriority = EarthAnimationTransitionPriority.Idle;
            _baseStateOwnerMode = CharacterPhysicalMode.AnimatedMotor;
            _ownedBaseStateHash = 0;
            LastDecision = default;
            LastInertializationReason = EarthAnimationInertializationReason.None;
        }

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
                EarthAnimationTransitionTuning tuning = ResolveTuning();
                EarthAnimationTransitionDecision decision =
                    EarthAnimationTransitionPolicy.Resolve(in context, in tuning);
                LastDecision = decision;
                if (!decision.ShouldTransition || animator == null || !animator.enabled)
                    return false;

                bool inertialized = decision.RequestsInertialization &&
                                    animationGraph != null &&
                                    animationGraph.BeginInertialization(decision.DurationSeconds);
                if (inertialized)
                {
                    LastInertializationReason = ResolveInertializationReason(in context);
                    if (decision.UseNormalizedStart)
                        animationGraph.Play(destinationHash, 0, decision.DestinationNormalizedTime);
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

                _activeState = context.DestinationState;
                _activeStateHash = destinationHash;
                _activePriority = context.RequestPriority;
                _transitionStartedAt = Time.time;
                _transitionDuration = decision.DurationSeconds;
                return true;
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
            if (_baseStateOwnerMode != CharacterPhysicalMode.Recovery ||
                _ownedBaseStateHash == 0 || animator == null || !animator.enabled)
                return;

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
