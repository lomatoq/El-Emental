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
        public const int CurrentSchemaVersion = 2;
        public const int ExpectedCuratedClipCount = 51;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private int expectedClipCount = ExpectedCuratedClipCount;
        [SerializeField] private string sourceIdentityHash = string.Empty;
        [SerializeField] private EarthMotionClipProfile[] clips =
            Array.Empty<EarthMotionClipProfile>();

        public int SchemaVersion => schemaVersion;
        public int ExpectedClipCount => expectedClipCount;
        public string SourceIdentityHash => sourceIdentityHash;
        public int ClipCount => clips?.Length ?? 0;
        public EarthMotionClipProfile ClipAt(int index) =>
            clips != null && index >= 0 && index < clips.Length ? clips[index] : null;

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

        public void ReplaceProfiles(
            EarthMotionClipProfile[] profiles,
            string identityHash)
        {
            schemaVersion = CurrentSchemaVersion;
            expectedClipCount = ExpectedCuratedClipCount;
            clips = profiles ?? Array.Empty<EarthMotionClipProfile>();
            sourceIdentityHash = identityHash ?? string.Empty;
        }

        private void OnValidate()
        {
            schemaVersion = CurrentSchemaVersion;
            expectedClipCount = ExpectedCuratedClipCount;
            clips ??= Array.Empty<EarthMotionClipProfile>();
            sourceIdentityHash ??= string.Empty;
        }
    }
}
