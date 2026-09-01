using System;
using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    [Serializable]
    public struct UnifiedLightingMigrationEntry
    {
        [SerializeField] private UnifiedLightingMaterialRole role;
        [SerializeField] private Material material;

        public UnifiedLightingMigrationEntry(
            UnifiedLightingMaterialRole role,
            Material material)
        {
            this.role = role;
            this.material = material;
        }

        public UnifiedLightingMaterialRole Role => role;
        public Material Material => material;
    }

    [CreateAssetMenu(
        fileName = "UnifiedLightingMigrationProfile",
        menuName = "Elemental/Rendering/Unified Lighting Migration Profile")]
    public sealed class UnifiedLightingMigrationProfile : ScriptableObject
    {
        public const string UnifiedShaderName =
            "Elemental/Graphics VNext/Unified Lit";
        public const int RequiredRoleCount = 7;

        [SerializeField] private UnifiedLightingMigrationEntry[] entries =
            Array.Empty<UnifiedLightingMigrationEntry>();

        public int EntryCount => entries != null ? entries.Length : 0;

        /// <summary>
        /// Configures a transient profile without persisting an asset mutation.
        /// Intended for capture owners and lifecycle tests that restore/destroy
        /// their ScriptableObject after use.
        /// </summary>
        public bool TryConfigureRuntime(
            UnifiedLightingMigrationEntry[] configuredEntries,
            out string failure)
        {
            if (!TryValidateEntries(configuredEntries, out failure))
                return false;
            entries = (UnifiedLightingMigrationEntry[])configuredEntries.Clone();
            return true;
        }

        public bool TryResolve(
            UnifiedLightingMaterialRole role,
            out Material material,
            out UnifiedLightingRoleContract contract)
        {
            contract = UnifiedLightingRoleContract.Resolve(role);
            material = null;
            if (entries == null)
                return false;
            for (int index = 0; index < entries.Length; index++)
            {
                if (entries[index].Role != role)
                    continue;
                material = entries[index].Material;
                return IsCompatible(material, contract.Family);
            }
            return false;
        }

        public bool IsComplete()
        {
            return TryValidateEntries(entries, out _);
        }

        private static bool TryValidateEntries(
            UnifiedLightingMigrationEntry[] candidates,
            out string failure)
        {
            failure = string.Empty;
            if (candidates == null || candidates.Length != RequiredRoleCount)
            {
                failure = $"Migration profile requires exactly {RequiredRoleCount} roles.";
                return false;
            }
            int seenRoles = 0;
            for (int index = 0; index < candidates.Length; index++)
            {
                int roleIndex = (int)candidates[index].Role;
                if (roleIndex < 0 || roleIndex >= RequiredRoleCount)
                {
                    failure = $"Migration role value {roleIndex} is outside the contract.";
                    return false;
                }
                int roleBit = 1 << roleIndex;
                if ((seenRoles & roleBit) != 0)
                {
                    failure = $"Migration role '{candidates[index].Role}' appears more than once.";
                    return false;
                }
                UnifiedLightingRoleContract contract =
                    UnifiedLightingRoleContract.Resolve(candidates[index].Role);
                if (!IsCompatible(candidates[index].Material, contract.Family))
                {
                    failure = $"Migration role '{candidates[index].Role}' has an incompatible material.";
                    return false;
                }
                seenRoles |= roleBit;
            }
            return seenRoles == (1 << RequiredRoleCount) - 1;
        }

        public static bool IsCompatible(
            Material material,
            UnifiedLightingMaterialFamily family)
        {
            return material != null &&
                material.shader != null &&
                material.shader.name == UnifiedShaderName &&
                material.HasProperty("_MaterialFamily") &&
                Mathf.RoundToInt(material.GetFloat("_MaterialFamily")) == (int)family;
        }
    }

    public static class UnifiedLightingMaterialMigration
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int NormalMapId = Shader.PropertyToID("_NormalMap");
        private static readonly int NormalStrengthId = Shader.PropertyToID("_NormalStrength");
        private static readonly int ShadowColorId = Shader.PropertyToID("_ShadowColor");
        private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        private static readonly int FractureColorId = Shader.PropertyToID("_FractureColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int TextureScaleId = Shader.PropertyToID("_TextureScale");
        private static readonly int TextureStrengthId = Shader.PropertyToID("_TextureStrength");
        private static readonly int TriplanarSharpnessId = Shader.PropertyToID("_TriplanarSharpness");
        private static readonly int RoughnessId = Shader.PropertyToID("_Roughness");
        private static readonly int AmbientStrengthId = Shader.PropertyToID("_AmbientStrength");
        private static readonly int ShadowFloorId = Shader.PropertyToID("_ShadowFloor");
        private static readonly int SpecularStrengthId = Shader.PropertyToID("_SpecularStrength");
        private static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");
        private static readonly int MagicAmountId = Shader.PropertyToID("_MagicAmount");
        private static readonly int ReceiveSsaoId = Shader.PropertyToID("_ReceiveSsao");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");

        /// <summary>
        /// Explicit authoring-time copy seam for character/material variants.
        /// It mutates only the destination supplied by the caller and never swaps
        /// a renderer or edits a shared production material implicitly.
        /// </summary>
        public static bool CopyPreservedProperties(Material source, Material destination)
        {
            if (source == null || destination == null ||
                destination.shader == null ||
                destination.shader.name != UnifiedLightingMigrationProfile.UnifiedShaderName)
                return false;
            if (source.HasProperty(BaseMapId) && destination.HasProperty(BaseMapId))
            {
                destination.SetTexture(BaseMapId, source.GetTexture(BaseMapId));
                destination.SetTextureScale("_BaseMap", source.GetTextureScale("_BaseMap"));
                destination.SetTextureOffset("_BaseMap", source.GetTextureOffset("_BaseMap"));
            }
            CopyColor(source, destination, BaseColorId);
            CopyColor(source, destination, ShadowColorId);
            CopyColor(source, destination, EdgeColorId);
            CopyColor(source, destination, FractureColorId);
            CopyColor(source, destination, EmissionColorId);
            if (source.HasProperty(NormalMapId) && destination.HasProperty(NormalMapId))
                destination.SetTexture(NormalMapId, source.GetTexture(NormalMapId));
            CopyFloat(source, destination, NormalStrengthId);
            CopyFloat(source, destination, TextureScaleId);
            CopyFloat(source, destination, TextureStrengthId);
            CopyFloat(source, destination, TriplanarSharpnessId);
            CopyFloat(source, destination, RoughnessId);
            CopyFloat(source, destination, AmbientStrengthId);
            CopyFloat(source, destination, ShadowFloorId);
            CopyFloat(source, destination, SpecularStrengthId);
            CopyFloat(source, destination, RimStrengthId);
            CopyFloat(source, destination, MagicAmountId);
            CopyFloat(source, destination, ReceiveSsaoId);
            CopyFloat(source, destination, FadeId);
            return true;
        }

        private static void CopyColor(Material source, Material destination, int propertyId)
        {
            if (source.HasProperty(propertyId) && destination.HasProperty(propertyId))
                destination.SetColor(propertyId, source.GetColor(propertyId));
        }

        private static void CopyFloat(Material source, Material destination, int propertyId)
        {
            if (source.HasProperty(propertyId) && destination.HasProperty(propertyId))
                destination.SetFloat(propertyId, source.GetFloat(propertyId));
        }
    }
}
