using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    public enum CapsuleShadowProducerKind
    {
        Player = 0,
        OpponentBot = 1,
        Ragdoll = 2,
        IntactHeroRock = 3,
        LargeActiveFracture = 4,
        Debris = 5,
        Vfx = 6
    }

    public static class CapsuleShadowOwnershipPolicy
    {
        public static bool TryResolveClassification(
            CapsuleShadowProducerKind producer,
            out CapsuleShadowCasterClass classification)
        {
            switch (producer)
            {
                case CapsuleShadowProducerKind.Player:
                case CapsuleShadowProducerKind.OpponentBot:
                case CapsuleShadowProducerKind.Ragdoll:
                    classification = CapsuleShadowCasterClass.Character;
                    return true;
                case CapsuleShadowProducerKind.IntactHeroRock:
                    classification = CapsuleShadowCasterClass.HeroRock;
                    return true;
                case CapsuleShadowProducerKind.LargeActiveFracture:
                    classification = CapsuleShadowCasterClass.ActiveFragment;
                    return true;
                default:
                    classification = CapsuleShadowCasterClass.Other;
                    return false;
            }
        }

        internal static bool TryCreateIdentity(
            CapsuleShadowProducerKind producer,
            uint stableGroupId,
            uint generation,
            out CapsuleShadowCasterIdentity identity)
        {
            identity = default;
            if (stableGroupId == 0u ||
                !TryResolveClassification(producer, out CapsuleShadowCasterClass classification))
                return false;
            identity = new CapsuleShadowCasterIdentity(
                stableGroupId,
                generation,
                classification);
            return true;
        }
    }

    internal readonly struct CapsuleShadowCasterIdentity
    {
        internal CapsuleShadowCasterIdentity(
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
        public bool IsValid => StableGroupId != 0u &&
            CapsuleShadowCasterPolicy.IsAdmittedClassification(Classification);
    }

    /// <summary>
    /// Stateless presentation boundary for character, rock, and fracture owners.
    /// Producers supply their typed kind and canonical uint identity on every pool
    /// acquisition, then explicitly commit a fully staged generation before changing
    /// visible representation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CapsuleShadowCasterBinder : MonoBehaviour
    {
        private static bool Bind(
            CapsuleShadowCaster caster,
            in CapsuleShadowCasterIdentity identity)
        {
            return caster != null && identity.IsValid && caster.Bind(in identity);
        }

        public bool TryAcquire(
            CapsuleShadowCaster caster,
            CapsuleShadowProducerKind producer,
            uint stableGroupId,
            uint generation)
        {
            if (caster == null)
                return false;
            if (!CapsuleShadowOwnershipPolicy.TryCreateIdentity(
                    producer,
                    stableGroupId,
                    generation,
                    out CapsuleShadowCasterIdentity identity))
            {
                caster.Unbind();
                return false;
            }
            return Bind(caster, identity);
        }

        public void ReleaseAcquisition(CapsuleShadowCaster caster)
        {
            Unbind(caster);
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
