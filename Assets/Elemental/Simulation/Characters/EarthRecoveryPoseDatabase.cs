using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public enum EarthRecoveryOrientation : byte
    {
        Unknown = 0,
        Front = 1,
        Back = 2,
        Left = 3,
        Right = 4
    }

    public readonly struct EarthRecoveryPoseFeature
    {
        public EarthRecoveryPoseFeature(
            float3 chestOffset,
            float3 leftHandOffset,
            float3 rightHandOffset,
            float3 leftFootOffset,
            float3 rightFootOffset,
            float3 chestOutward)
        {
            ChestOffset = chestOffset;
            LeftHandOffset = leftHandOffset;
            RightHandOffset = rightHandOffset;
            LeftFootOffset = leftFootOffset;
            RightFootOffset = rightFootOffset;
            ChestOutward = chestOutward;
        }

        public float3 ChestOffset { get; }
        public float3 LeftHandOffset { get; }
        public float3 RightHandOffset { get; }
        public float3 LeftFootOffset { get; }
        public float3 RightFootOffset { get; }
        public float3 ChestOutward { get; }

        public bool IsFinite =>
            math.all(math.isfinite(ChestOffset)) &&
            math.all(math.isfinite(LeftHandOffset)) &&
            math.all(math.isfinite(RightHandOffset)) &&
            math.all(math.isfinite(LeftFootOffset)) &&
            math.all(math.isfinite(RightFootOffset)) &&
            math.all(math.isfinite(ChestOutward));
    }

    public readonly struct EarthRecoveryPoseCandidate
    {
        public EarthRecoveryPoseCandidate(
            uint clipId,
            int animationStateId,
            EarthRecoveryOrientation orientation,
            float entryPhase,
            in EarthRecoveryPoseFeature feature,
            float3 pelvisOffsetLocal,
            in EarthRecoveryMarkerProfile markers,
            bool validEntry = true)
        {
            ClipId = clipId;
            AnimationStateId = animationStateId;
            Orientation = orientation;
            EntryPhase = entryPhase;
            Feature = feature;
            PelvisOffsetLocal = pelvisOffsetLocal;
            Markers = markers;
            ValidEntry = validEntry;
        }

        public uint ClipId { get; }
        public int AnimationStateId { get; }
        public EarthRecoveryOrientation Orientation { get; }
        public float EntryPhase { get; }
        public EarthRecoveryPoseFeature Feature { get; }
        public float3 PelvisOffsetLocal { get; }
        public EarthRecoveryMarkerProfile Markers { get; }
        public bool ValidEntry { get; }

        public bool IsUsable =>
            ValidEntry &&
            ClipId != 0u &&
            AnimationStateId != 0 &&
            Orientation != EarthRecoveryOrientation.Unknown &&
            math.isfinite(EntryPhase) &&
            EntryPhase >= 0f && EntryPhase <= 1f &&
            Feature.IsFinite &&
            math.all(math.isfinite(PelvisOffsetLocal)) &&
            Markers.IsValid;
    }

    /// <summary>
    /// Immutable-at-runtime sampled recovery poses. The authoring adapter builds
    /// this once; matching performs no allocation and never mutates the samples.
    /// </summary>
    public sealed class EarthRecoveryPoseDatabase
    {
        private readonly EarthRecoveryPoseCandidate[] _candidates;

        public EarthRecoveryPoseDatabase(EarthRecoveryPoseCandidate[] candidates)
        {
            if (candidates == null || candidates.Length == 0)
            {
                _candidates = Array.Empty<EarthRecoveryPoseCandidate>();
                return;
            }

            _candidates = new EarthRecoveryPoseCandidate[candidates.Length];
            Array.Copy(candidates, _candidates, candidates.Length);
        }

        public int Count => _candidates.Length;

        public bool TryGetCandidate(int index, out EarthRecoveryPoseCandidate candidate)
        {
            if (index < 0 || index >= _candidates.Length)
            {
                candidate = default;
                return false;
            }

            candidate = _candidates[index];
            return true;
        }
    }

    public readonly struct EarthRecoveryResult
    {
        public EarthRecoveryResult(
            EarthRecoveryOrientation orientation,
            uint clipId,
            int animationStateId,
            float entryPhase,
            float matchCost,
            float3 livePelvisPosition,
            float3 rootPosition,
            quaternion rootRotation,
            float3 radialUp,
            float3 radialFacing,
            in EarthRecoveryClearanceResult clearance,
            in EarthRecoveryMarkerProfile markers,
            bool usedFacingFallback)
        {
            Orientation = orientation;
            ClipId = clipId;
            AnimationStateId = animationStateId;
            EntryPhase = entryPhase;
            MatchCost = matchCost;
            LivePelvisPosition = livePelvisPosition;
            RootPosition = rootPosition;
            RootRotation = rootRotation;
            RadialUp = radialUp;
            RadialFacing = radialFacing;
            Clearance = clearance;
            Markers = markers;
            UsedFacingFallback = usedFacingFallback;
        }

        public EarthRecoveryOrientation Orientation { get; }
        public uint ClipId { get; }
        public int AnimationStateId { get; }
        public float EntryPhase { get; }
        public float MatchCost { get; }
        public float3 LivePelvisPosition { get; }
        public float3 RootPosition { get; }
        public quaternion RootRotation { get; }
        public float3 RadialUp { get; }
        public float3 RadialFacing { get; }
        public EarthRecoveryClearanceResult Clearance { get; }
        public EarthRecoveryMarkerProfile Markers { get; }
        public bool UsedFacingFallback { get; }

        public bool IsValid =>
            Orientation != EarthRecoveryOrientation.Unknown &&
            ClipId != 0u &&
            AnimationStateId != 0 &&
            math.isfinite(EntryPhase) &&
            math.isfinite(MatchCost) &&
            math.all(math.isfinite(LivePelvisPosition)) &&
            math.all(math.isfinite(RootPosition)) &&
            math.all(math.isfinite(RootRotation.value)) &&
            math.all(math.isfinite(RadialUp)) &&
            math.all(math.isfinite(RadialFacing)) &&
            Markers.IsValid;
    }
}
