using System;
using Elemental.Simulation.Characters;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    [CreateAssetMenu(
        fileName = "EarthMotionCatalog",
        menuName = "Elemental/Animation/Earth Motion Catalog")]
    public sealed class EarthMotionCatalog : ScriptableObject
    {
        public const int CurrentSchemaVersion = 3;
        public const int ExpectedCuratedClipCount = 51;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private int expectedClipCount = ExpectedCuratedClipCount;
        [SerializeField] private string sourceIdentityHash = string.Empty;
        [SerializeField] private EarthMotionClipProfile[] clips =
            Array.Empty<EarthMotionClipProfile>();
        [SerializeField] private EarthMotionStateBinding[] stateBindings =
            Array.Empty<EarthMotionStateBinding>();

        public int SchemaVersion => schemaVersion;
        public int ExpectedClipCount => expectedClipCount;
        public string SourceIdentityHash => sourceIdentityHash;
        public int ClipCount => clips?.Length ?? 0;
        public int StateBindingCount => stateBindings?.Length ?? 0;
        public EarthMotionClipProfile ClipAt(int index) =>
            clips != null && index >= 0 && index < clips.Length ? clips[index] : null;
        public EarthMotionStateBinding StateBindingAt(int index) =>
            stateBindings != null && index >= 0 && index < stateBindings.Length
                ? stateBindings[index]
                : null;

        public bool TryFind(
            string assetGuid,
            long localFileId,
            out EarthMotionClipProfile profile)
        {
            int count = clips?.Length ?? 0;
            for (int index = 0; index < count; index++)
            {
                EarthMotionClipProfile candidate = clips[index];
                if (candidate == null || candidate.LocalFileId != localFileId ||
                    !string.Equals(candidate.AssetGuid, assetGuid, StringComparison.Ordinal))
                    continue;
                profile = candidate;
                return true;
            }
            profile = null;
            return false;
        }

        public bool TryResolveControllerState(
            int stateHash,
            AnimationClip activeClip,
            out EarthMotionStateResolution resolution) =>
            TryResolveControllerState(
                stateHash,
                activeClip,
                EarthMotionSemanticAction.Unknown,
                out resolution);

        public bool TryResolveControllerState(
            int stateHash,
            AnimationClip activeClip,
            EarthMotionSemanticAction requiredRole,
            out EarthMotionStateResolution resolution)
        {
            int count = stateBindings?.Length ?? 0;
            for (int index = 0; index < count; index++)
            {
                EarthMotionStateBinding binding = stateBindings[index];
                if (binding == null || binding.StateHash != stateHash ||
                    (requiredRole != EarthMotionSemanticAction.Unknown &&
                     binding.SemanticRole != requiredRole) ||
                    !binding.TryResolveProfileIndex(
                        this,
                        activeClip,
                        out int profileIndex,
                        out EarthMotionStateResolutionKind kind))
                    continue;
                resolution = new EarthMotionStateResolution(
                    kind,
                    index,
                    profileIndex,
                    binding,
                    ClipAt(profileIndex));
                return resolution.IsVerified;
            }
            resolution = default;
            return false;
        }

        public void ReplaceProfiles(
            EarthMotionClipProfile[] profiles,
            EarthMotionStateBinding[] configuredStateBindings,
            string identityHash)
        {
            schemaVersion = CurrentSchemaVersion;
            expectedClipCount = ExpectedCuratedClipCount;
            clips = profiles ?? Array.Empty<EarthMotionClipProfile>();
            stateBindings = configuredStateBindings ?? Array.Empty<EarthMotionStateBinding>();
            sourceIdentityHash = identityHash ?? string.Empty;
        }

        private void OnValidate()
        {
            schemaVersion = CurrentSchemaVersion;
            expectedClipCount = ExpectedCuratedClipCount;
            clips ??= Array.Empty<EarthMotionClipProfile>();
            stateBindings ??= Array.Empty<EarthMotionStateBinding>();
            sourceIdentityHash ??= string.Empty;
        }
    }
}
