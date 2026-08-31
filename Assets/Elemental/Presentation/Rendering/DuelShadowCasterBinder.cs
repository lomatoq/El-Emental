using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    public readonly struct DuelShadowCasterIdentity
    {
        public DuelShadowCasterIdentity(
            uint stableGroupId,
            uint generation,
            DuelShadowCasterClass classification)
        {
            StableGroupId = stableGroupId;
            Generation = generation;
            Classification = classification;
        }

        public uint StableGroupId { get; }
        public uint Generation { get; }
        public DuelShadowCasterClass Classification { get; }
        public bool IsValid => StableGroupId != 0u;
    }

    /// <summary>
    /// Presentation-owned boundary for pooled runtime producers. It retains no
    /// producer or caster state: every acquisition supplies a canonical identity,
    /// every release explicitly unbinds, and generation commit remains atomic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DuelShadowCasterBinder : MonoBehaviour
    {
        public bool Bind(
            DuelShadowCaster caster,
            in DuelShadowCasterIdentity identity)
        {
            if (caster == null)
                return false;
            return caster.Bind(
                identity.StableGroupId,
                identity.Generation,
                identity.Classification);
        }

        public void Unbind(DuelShadowCaster caster)
        {
            if (caster != null)
                caster.Unbind();
        }

        public bool CommitGeneration(uint stableGroupId, uint generation)
        {
            return DuelShadowCaster.CommitGeneration(stableGroupId, generation);
        }

        public bool ReleaseGroup(uint stableGroupId, uint committedGeneration)
        {
            return DuelShadowCaster.ReleaseGroup(stableGroupId, committedGeneration);
        }
    }
}
