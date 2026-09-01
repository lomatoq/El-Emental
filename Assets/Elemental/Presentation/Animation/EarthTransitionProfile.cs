using System;
using Elemental.Simulation.Characters;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    [Serializable]
    public sealed class EarthTransitionPairOverride
    {
        [Header("Pair selector")]
        [SerializeField] private bool matchSourceState = true;
        [SerializeField] private EarthMotionStateId sourceState;
        [SerializeField] private bool matchDestinationState = true;
        [SerializeField] private EarthMotionStateId destinationState;
        [SerializeField] private bool matchSourceCategory;
        [SerializeField] private EarthMotionCategory sourceCategory;
        [SerializeField] private bool matchDestinationCategory;
        [SerializeField] private EarthMotionCategory destinationCategory;

        [Header("Transition behavior")]
        [SerializeField] private EarthTransitionFamily family =
            EarthTransitionFamily.FixedDurationFallback;
        [SerializeField] private EarthAnimationTransitionPriority priority =
            EarthAnimationTransitionPriority.Locomotion;
        [SerializeField, Range(0.01f, 0.25f)] private float halfLifeSeconds = 0.08f;
        [SerializeField, Range(0.01f, 0.5f)] private float fallbackDurationSeconds = 0.10f;
        [SerializeField] private EarthTransitionGaitPhaseRule gaitPhaseRule;
        [SerializeField] private EarthTransitionContactPolicy contactPolicy =
            EarthTransitionContactPolicy.PreserveCurrentPlants;
        [SerializeField] private EarthTransitionCancelPolicy cancelPolicy =
            EarthTransitionCancelPolicy.OutsideProtectedWindow;

        [Header("Authored windows")]
        [SerializeField] private bool protectedWindowEnabled;
        [SerializeField, Range(0f, 1f)] private float protectedWindowStart01;
        [SerializeField, Range(0f, 1f)] private float protectedWindowEnd01;
        [SerializeField] private bool cancelWindowEnabled;
        [SerializeField, Range(0f, 1f)] private float cancelWindowStart01;
        [SerializeField, Range(0f, 1f)] private float cancelWindowEnd01;
        [SerializeField, Range(0f, 1f)] private float targetPhase01;

        [Header("Body/contact ownership")]
        [SerializeField] private EarthTransitionBodyMask bodyMask =
            EarthTransitionBodyMask.FullBody;
        [SerializeField] private EarthTransitionFootReleasePolicy footReleasePolicy =
            EarthTransitionFootReleasePolicy.PreservePlanted;
        [SerializeField, Range(0f, 0.5f)] private float footReleaseSeconds;
        [SerializeField] private bool queueWhenBlocked;

        public EarthTransitionPairOverride()
        {
        }

        public EarthTransitionPairOverride(
            EarthMotionStateId source,
            EarthMotionStateId destination,
            in EarthTransitionRule rule)
        {
            matchSourceState = true;
            sourceState = source;
            matchDestinationState = true;
            destinationState = destination;
            CopyRule(in rule);
        }

        public EarthTransitionPairOverride(
            EarthMotionCategory source,
            EarthMotionCategory destination,
            in EarthTransitionRule rule)
        {
            matchSourceState = false;
            matchDestinationState = false;
            matchSourceCategory = true;
            sourceCategory = source;
            matchDestinationCategory = true;
            destinationCategory = destination;
            CopyRule(in rule);
        }

        public bool MatchSourceState => matchSourceState;
        public EarthMotionStateId SourceState => sourceState;
        public bool MatchDestinationState => matchDestinationState;
        public EarthMotionStateId DestinationState => destinationState;
        public bool MatchSourceCategory => matchSourceCategory;
        public EarthMotionCategory SourceCategory => sourceCategory;
        public bool MatchDestinationCategory => matchDestinationCategory;
        public EarthMotionCategory DestinationCategory => destinationCategory;
        public EarthTransitionFamily Family => family;
        public EarthAnimationTransitionPriority Priority => priority;
        public float HalfLifeSeconds => halfLifeSeconds;
        public float FallbackDurationSeconds => fallbackDurationSeconds;
        public EarthTransitionGaitPhaseRule GaitPhaseRule => gaitPhaseRule;
        public EarthTransitionContactPolicy ContactPolicy => contactPolicy;
        public EarthTransitionCancelPolicy CancelPolicy => cancelPolicy;
        public bool ProtectedWindowEnabled => protectedWindowEnabled;
        public float ProtectedWindowStart01 => protectedWindowStart01;
        public float ProtectedWindowEnd01 => protectedWindowEnd01;
        public bool CancelWindowEnabled => cancelWindowEnabled;
        public float CancelWindowStart01 => cancelWindowStart01;
        public float CancelWindowEnd01 => cancelWindowEnd01;
        public float TargetPhase01 => targetPhase01;
        public EarthTransitionBodyMask BodyMask => bodyMask;
        public EarthTransitionFootReleasePolicy FootReleasePolicy => footReleasePolicy;
        public float FootReleaseSeconds => footReleaseSeconds;
        public bool QueueWhenBlocked => queueWhenBlocked;

        public bool HasValidSelector =>
            (matchDestinationState && destinationState != EarthMotionStateId.None) ||
            (matchDestinationCategory && destinationCategory != EarthMotionCategory.None);

        public int MatchSpecificity =>
            (matchSourceState ? 8 : 0) +
            (matchDestinationState ? 8 : 0) +
            (matchSourceCategory ? 4 : 0) +
            (matchDestinationCategory ? 4 : 0);

        public bool Matches(in EarthAnimationTransitionContext context)
        {
            if (!HasValidSelector) return false;
            if (matchSourceState && sourceState != context.SourceState) return false;
            if (matchDestinationState && destinationState != context.DestinationState) return false;
            if (matchSourceCategory && sourceCategory != context.SourceCategory) return false;
            if (matchDestinationCategory &&
                destinationCategory != context.DestinationCategory) return false;
            return true;
        }

        public EarthTransitionRule ToRule()
        {
            EarthNormalizedAnimationWindow protectedWindow =
                new EarthNormalizedAnimationWindow(
                    protectedWindowEnabled,
                    protectedWindowStart01,
                    protectedWindowEnd01);
            EarthNormalizedAnimationWindow cancelWindow =
                new EarthNormalizedAnimationWindow(
                    cancelWindowEnabled,
                    cancelWindowStart01,
                    cancelWindowEnd01);
            return new EarthTransitionRule(
                true,
                family,
                priority,
                halfLifeSeconds,
                fallbackDurationSeconds,
                gaitPhaseRule,
                contactPolicy,
                cancelPolicy,
                in protectedWindow,
                in cancelWindow,
                targetPhase01,
                bodyMask,
                footReleasePolicy,
                footReleaseSeconds,
                queueWhenBlocked);
        }

        private void CopyRule(in EarthTransitionRule rule)
        {
            family = rule.Family;
            priority = rule.Priority;
            halfLifeSeconds = rule.HalfLifeSeconds;
            fallbackDurationSeconds = rule.FallbackDurationSeconds;
            gaitPhaseRule = rule.GaitPhaseRule;
            contactPolicy = rule.ContactPolicy;
            cancelPolicy = rule.CancelPolicy;
            protectedWindowEnabled = rule.ProtectedWindow.Enabled;
            protectedWindowStart01 = rule.ProtectedWindow.Start01;
            protectedWindowEnd01 = rule.ProtectedWindow.End01;
            cancelWindowEnabled = rule.CancelWindow.Enabled;
            cancelWindowStart01 = rule.CancelWindow.Start01;
            cancelWindowEnd01 = rule.CancelWindow.End01;
            targetPhase01 = rule.TargetPhase01;
            bodyMask = rule.BodyMask;
            footReleasePolicy = rule.FootReleasePolicy;
            footReleaseSeconds = rule.FootReleaseSeconds;
            queueWhenBlocked = rule.QueueWhenBlocked;
        }
    }

    [CreateAssetMenu(
        fileName = "EarthTransitionProfile",
        menuName = "Elemental/Animation/Earth Transition Profile")]
    public sealed class EarthTransitionProfile : ScriptableObject
    {
        [Header("Feature flags (default off)")]
        [SerializeField] private bool useTransitionProfile;
        [SerializeField] private bool useTransitionQueue;
        [SerializeField, Range(1, EarthTransitionQueue.MaximumCapacity)]
        private int queueCapacity = EarthTransitionQueue.DefaultCapacity;

        [Header("Fallback used only when an enabled profile has no pair")]
        [SerializeField, Range(0.01f, 0.5f)] private float genericFallbackDurationSeconds = 0.08f;
        [SerializeField] private EarthTransitionPairOverride[] pairOverrides =
            Array.Empty<EarthTransitionPairOverride>();

        public bool UseTransitionProfile => useTransitionProfile;
        public bool UseTransitionQueue => useTransitionProfile && useTransitionQueue;
        public int QueueCapacity => Mathf.Clamp(
            queueCapacity,
            1,
            EarthTransitionQueue.MaximumCapacity);
        public float GenericFallbackDurationSeconds => Mathf.Clamp(
            float.IsFinite(genericFallbackDurationSeconds)
                ? genericFallbackDurationSeconds
                : 0.08f,
            0.01f,
            0.5f);
        public int PairCount => pairOverrides?.Length ?? 0;

        public EarthTransitionPairOverride PairAt(int index) =>
            pairOverrides != null && index >= 0 && index < pairOverrides.Length
                ? pairOverrides[index]
                : null;

        public bool TryResolve(
            in EarthAnimationTransitionContext context,
            out EarthTransitionRule rule,
            out int pairIndex,
            out bool usedGenericFallback)
        {
            if (!useTransitionProfile)
            {
                rule = default;
                pairIndex = -1;
                usedGenericFallback = false;
                return false;
            }

            int bestIndex = -1;
            int bestSpecificity = -1;
            int count = pairOverrides?.Length ?? 0;
            for (int index = 0; index < count; index++)
            {
                EarthTransitionPairOverride candidate = pairOverrides[index];
                if (candidate == null || !candidate.Matches(in context)) continue;
                int specificity = candidate.MatchSpecificity;
                if (specificity <= bestSpecificity) continue;
                bestIndex = index;
                bestSpecificity = specificity;
            }

            if (bestIndex >= 0)
            {
                rule = pairOverrides[bestIndex].ToRule();
                pairIndex = bestIndex;
                usedGenericFallback = false;
                return true;
            }

            rule = EarthTransitionRule.FixedFallback(
                context.RequestPriority,
                GenericFallbackDurationSeconds);
            pairIndex = -1;
            usedGenericFallback = true;
            return true;
        }

        public void Configure(
            bool profileEnabled,
            bool queueEnabled,
            int configuredQueueCapacity,
            float fallbackDurationSeconds,
            EarthTransitionPairOverride[] configuredPairs)
        {
            useTransitionProfile = profileEnabled;
            useTransitionQueue = queueEnabled;
            queueCapacity = Mathf.Clamp(
                configuredQueueCapacity,
                1,
                EarthTransitionQueue.MaximumCapacity);
            genericFallbackDurationSeconds = Mathf.Clamp(
                float.IsFinite(fallbackDurationSeconds) ? fallbackDurationSeconds : 0.08f,
                0.01f,
                0.5f);
            pairOverrides = configuredPairs ?? Array.Empty<EarthTransitionPairOverride>();
        }

        private void OnValidate()
        {
            queueCapacity = Mathf.Clamp(
                queueCapacity,
                1,
                EarthTransitionQueue.MaximumCapacity);
            genericFallbackDurationSeconds = Mathf.Clamp(
                float.IsFinite(genericFallbackDurationSeconds)
                    ? genericFallbackDurationSeconds
                    : 0.08f,
                0.01f,
                0.5f);
            pairOverrides ??= Array.Empty<EarthTransitionPairOverride>();
        }
    }
}
