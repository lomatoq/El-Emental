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

        public EarthMotionStateId ActiveState => _activeState;
        public int ActiveStateHash => _activeStateHash;
        public EarthAnimationTransitionDecision LastDecision { get; private set; }
        public EarthAnimationTransitionKind ActiveTransitionKind => LastDecision.Kind;
        public EarthAnimationTransitionReason LastReason => LastDecision.Reason;
        public EarthAnimationInertializationReason LastInertializationReason { get; private set; }
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

        public bool RequestTransition(
            int destinationHash,
            in EarthAnimationTransitionContext context)
        {
            using (TransitionMarker.Auto())
            {
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
