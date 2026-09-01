using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    public readonly struct CapsuleShadowCasterIdentity
    {
        public CapsuleShadowCasterIdentity(
            uint stableGroupId,
            uint generation,
            CapsuleShadowCasterClass classification)
        {
            StableGroupId = stableGroupId;
            Generation = generation;
            Classification = classification;
        }

        public uint StableGroupId { get; }
        public uint Generation { get; }
        public CapsuleShadowCasterClass Classification { get; }
        public bool IsValid => StableGroupId != 0u;
    }

    /// <summary>
    /// Stateless presentation boundary for character, rock, and fracture owners.
    /// Producers supply canonical identity on every pool acquisition and explicitly
    /// commit a fully staged generation before changing visible representation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CapsuleShadowCasterBinder : MonoBehaviour
    {
        public bool Bind(
            CapsuleShadowCaster caster,
            in CapsuleShadowCasterIdentity identity)
        {
            return caster != null && caster.Bind(
                identity.StableGroupId,
                identity.Generation,
                identity.Classification);
        }

        public void Unbind(CapsuleShadowCaster caster)
        {
            if (caster != null)
                caster.Unbind();
        }

        public bool CommitGeneration(uint stableGroupId, uint generation)
        {
            return CapsuleShadowCaster.CommitGeneration(stableGroupId, generation);
        }

        public bool ReleaseGroup(uint stableGroupId, uint committedGeneration)
        {
            return CapsuleShadowCaster.ReleaseGroup(stableGroupId, committedGeneration);
        }
    }
}
