using System;
using Elemental.Simulation.Characters;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    public enum EarthMotionStateResolutionKind : byte
    {
        None = 0,
        ExactActiveClip = 1,
        StatePrimaryClip = 2
    }

    [Serializable]
    public sealed class EarthMotionStateBinding
    {
        [SerializeField] private int layerIndex;
        [SerializeField] private string statePath = string.Empty;
        [SerializeField] private int stateHash;
        [SerializeField] private EarthMotionStateId motionState;
        [SerializeField] private EarthMotionCategory category;
        [SerializeField] private EarthMotionSemanticAction semanticRole;
        [SerializeField] private int[] clipProfileIndices = Array.Empty<int>();

        public EarthMotionStateBinding(
            int configuredLayerIndex,
            string configuredStatePath,
            int configuredStateHash,
            EarthMotionStateId configuredMotionState,
            EarthMotionCategory configuredCategory,
            EarthMotionSemanticAction configuredSemanticRole,
            int[] configuredClipProfileIndices)
        {
            layerIndex = configuredLayerIndex;
            statePath = configuredStatePath ?? string.Empty;
            stateHash = configuredStateHash;
            motionState = configuredMotionState;
            category = configuredCategory;
            semanticRole = configuredSemanticRole;
            clipProfileIndices = configuredClipProfileIndices ?? Array.Empty<int>();
        }

        public int LayerIndex => layerIndex;
        public string StatePath => statePath;
        public int StateHash => stateHash;
        public EarthMotionStateId MotionState => motionState;
        public EarthMotionCategory Category => category;
        public EarthMotionSemanticAction SemanticRole => semanticRole;
        public int ClipProfileCount => clipProfileIndices?.Length ?? 0;
        public int ClipProfileIndexAt(int index) =>
            clipProfileIndices != null && index >= 0 && index < clipProfileIndices.Length
                ? clipProfileIndices[index]
                : -1;

        internal bool TryResolveProfileIndex(
            EarthMotionCatalog catalog,
            AnimationClip activeClip,
            out int profileIndex,
            out EarthMotionStateResolutionKind kind)
        {
            profileIndex = -1;
            kind = EarthMotionStateResolutionKind.None;
            int count = ClipProfileCount;
            if (catalog == null || stateHash == 0 || count == 0) return false;

            if (activeClip != null)
            {
                for (int index = 0; index < count; index++)
                {
                    int candidateIndex = ClipProfileIndexAt(index);
                    EarthMotionClipProfile candidate = catalog.ClipAt(candidateIndex);
                    if (candidate?.Clip != activeClip) continue;
                    profileIndex = candidateIndex;
                    kind = EarthMotionStateResolutionKind.ExactActiveClip;
                    return true;
                }

                // An active clip that is absent from the authored state binding is
                // a provenance failure. Never disguise it as the primary clip.
                return false;
            }

            int primaryIndex = ClipProfileIndexAt(0);
            if (catalog.ClipAt(primaryIndex)?.Clip == null) return false;
            profileIndex = primaryIndex;
            kind = EarthMotionStateResolutionKind.StatePrimaryClip;
            return true;
        }
    }

    public readonly struct EarthMotionStateResolution
    {
        public EarthMotionStateResolution(
            EarthMotionStateResolutionKind kind,
            int stateBindingIndex,
            int profileIndex,
            EarthMotionStateBinding binding,
            EarthMotionClipProfile profile)
        {
            Kind = kind;
            StateBindingIndex = stateBindingIndex;
            ProfileIndex = profileIndex;
            StateHash = binding?.StateHash ?? 0;
            LayerIndex = binding?.LayerIndex ?? -1;
            MotionState = binding?.MotionState ?? EarthMotionStateId.None;
            Category = binding?.Category ?? EarthMotionCategory.None;
            SemanticRole = binding?.SemanticRole ?? EarthMotionSemanticAction.Unknown;
            Profile = profile;
        }

        public EarthMotionStateResolutionKind Kind { get; }
        public int StateBindingIndex { get; }
        public int ProfileIndex { get; }
        public int StateHash { get; }
        public int LayerIndex { get; }
        public EarthMotionStateId MotionState { get; }
        public EarthMotionCategory Category { get; }
        public EarthMotionSemanticAction SemanticRole { get; }
        public EarthMotionClipProfile Profile { get; }
        public bool IsVerified =>
            Kind != EarthMotionStateResolutionKind.None &&
            StateBindingIndex >= 0 &&
            ProfileIndex >= 0 &&
            StateHash != 0 &&
            Profile?.Clip != null;
    }

    public readonly struct EarthVerifiedTransitionPair
    {
        public EarthVerifiedTransitionPair(
            in EarthMotionStateResolution source,
            in EarthMotionStateResolution destination,
            int pairIndex,
            in EarthTransitionRule rule)
        {
            Source = source;
            Destination = destination;
            PairIndex = pairIndex;
            Rule = rule;
        }

        public EarthMotionStateResolution Source { get; }
        public EarthMotionStateResolution Destination { get; }
        public int PairIndex { get; }
        public EarthTransitionRule Rule { get; }
        public bool IsVerified =>
            Source.IsVerified && Destination.IsVerified && PairIndex >= 0 && Rule.Configured;
    }

    public static class EarthMotionTransitionCatalogResolver
    {
        public static bool TryResolveAuthoredPair(
            EarthMotionCatalog catalog,
            EarthTransitionProfile transitionProfile,
            int sourceStateHash,
            int destinationStateHash,
            in EarthAnimationTransitionContext context,
            out EarthVerifiedTransitionPair pair) =>
            TryResolveAuthoredPair(
                catalog,
                transitionProfile,
                sourceStateHash,
                null,
                destinationStateHash,
                null,
                in context,
                out pair);

        public static bool TryResolveAuthoredPair(
            EarthMotionCatalog catalog,
            EarthTransitionProfile transitionProfile,
            int sourceStateHash,
            AnimationClip sourceActiveClip,
            int destinationStateHash,
            AnimationClip destinationActiveClip,
            in EarthAnimationTransitionContext context,
            out EarthVerifiedTransitionPair pair)
        {
            pair = default;
            if (catalog == null || transitionProfile == null ||
                !catalog.TryResolveControllerState(
                    sourceStateHash,
                    sourceActiveClip,
                    out EarthMotionStateResolution source) ||
                !catalog.TryResolveControllerState(
                    destinationStateHash,
                    destinationActiveClip,
                    out EarthMotionStateResolution destination) ||
                !transitionProfile.TryResolve(
                    in context,
                    out EarthTransitionRule rule,
                    out int pairIndex,
                    out bool usedGenericFallback) ||
                usedGenericFallback || pairIndex < 0)
                return false;

            pair = new EarthVerifiedTransitionPair(
                in source,
                in destination,
                pairIndex,
                in rule);
            return pair.IsVerified;
        }
    }

    public readonly struct EarthRecoveryCatalogMatch
    {
        public EarthRecoveryCatalogMatch(
            in EarthRecoveryPoseMatch poseMatch,
            in EarthMotionStateResolution motion)
        {
            PoseMatch = poseMatch;
            Motion = motion;
        }

        public EarthRecoveryPoseMatch PoseMatch { get; }
        public EarthMotionStateResolution Motion { get; }
        public EarthRecoveryMarkerProfile Markers => PoseMatch.Candidate.Markers;
        public bool IsVerified =>
            PoseMatch.IsValid &&
            Motion.IsVerified &&
            Motion.SemanticRole == EarthMotionSemanticAction.Recovery &&
            Markers.IsValid;
    }

    /// <summary>
    /// Thin catalog adapter over the existing pure pose matcher. The physical
    /// animation stack remains the recovery/control/feet owner.
    /// </summary>
    public static class EarthRecoveryCatalogResolver
    {
        public static bool TryResolveClosest(
            EarthMotionCatalog catalog,
            EarthRecoveryPoseDatabase database,
            EarthRecoveryOrientation orientation,
            in EarthRecoveryPoseFeature current,
            in EarthRecoveryPoseMatchWeights weights,
            out EarthRecoveryCatalogMatch result)
        {
            result = default;
            if (catalog == null ||
                !EarthRecoveryPoseMatcher.TryMatch(
                    database,
                    orientation,
                    in current,
                    in weights,
                    out EarthRecoveryPoseMatch poseMatch) ||
                !catalog.TryResolveControllerState(
                    poseMatch.Candidate.AnimationStateId,
                    null,
                    EarthMotionSemanticAction.Recovery,
                    out EarthMotionStateResolution motion))
                return false;

            result = new EarthRecoveryCatalogMatch(in poseMatch, in motion);
            return result.IsVerified;
        }
    }
}
