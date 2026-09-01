using Unity.Profiling;
using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    /// <summary>
    /// Thin, explicit presentation binder. The caller assigns a material from the
    /// migration profile first; this component validates the slot and merges only
    /// projection/family state into the renderer's existing property block.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnifiedLightingMaterialBinder : MonoBehaviour
    {
        private static readonly ProfilerMarker BindMarker =
            new ProfilerMarker("Elemental.Rendering.UnifiedMaterialBind");
        private static readonly int MaterialFamilyId =
            Shader.PropertyToID("_MaterialFamily");
        private static readonly int SurfaceModeId =
            Shader.PropertyToID("_SurfaceMode");
        private static readonly int UsePlanetFrameId =
            Shader.PropertyToID("_UsePlanetFrame");
        private static readonly int PlanetCenterId =
            Shader.PropertyToID("_PlanetCenter");
        private static readonly int FractureMappingEnabledId =
            Shader.PropertyToID("_FractureMappingEnabled");
        private static readonly int FractureLocalToStructureId =
            Shader.PropertyToID("_FractureLocalToStructure");

        [SerializeField] private UnifiedLightingMigrationProfile migrationProfile;

        private MaterialPropertyBlock _properties;

        public void Configure(UnifiedLightingMigrationProfile profile)
        {
            migrationProfile = profile;
        }

        public bool Bind(
            Renderer renderer,
            int materialIndex,
            UnifiedLightingMaterialRole role,
            in UnifiedLightingProjectionFrame frame)
        {
            using (BindMarker.Auto())
            {
                if (renderer == null || migrationProfile == null ||
                    !migrationProfile.IsComplete() || !frame.IsValid)
                {
                    Debug.LogError(
                        "Unified-lighting binding requires a renderer, complete migration " +
                        "profile, and finite projection frame.",
                        this);
                    return false;
                }
                if (!migrationProfile.TryResolve(
                        role,
                        out _,
                        out UnifiedLightingRoleContract contract) ||
                    contract.ProjectionMode != frame.Mode)
                {
                    Debug.LogError(
                        $"Unified-lighting role '{role}' rejected projection mode '{frame.Mode}'.",
                        this);
                    return false;
                }

                Material[] assignedMaterials = renderer.sharedMaterials;
                if (materialIndex < 0 || materialIndex >= assignedMaterials.Length ||
                    !UnifiedLightingMigrationProfile.IsCompatible(
                        assignedMaterials[materialIndex],
                        contract.Family))
                {
                    Debug.LogError(
                        $"Renderer '{renderer.name}' slot {materialIndex} is not assigned " +
                        $"the unified {contract.Family} material family.",
                        renderer);
                    return false;
                }

                _properties ??= new MaterialPropertyBlock();
                renderer.GetPropertyBlock(_properties);
                _properties.SetFloat(MaterialFamilyId, (float)contract.Family);
                _properties.SetFloat(
                    SurfaceModeId,
                    contract.Family == UnifiedLightingMaterialFamily.Character ? 1f : 0f);
                switch (frame.Mode)
                {
                    case UnifiedLightingProjectionMode.PlanetLocal:
                        _properties.SetFloat(UsePlanetFrameId, 1f);
                        _properties.SetVector(PlanetCenterId, frame.PlanetCenterWorld);
                        _properties.SetFloat(FractureMappingEnabledId, 0f);
                        break;
                    case UnifiedLightingProjectionMode.CapturedStructureLocal:
                        _properties.SetFloat(UsePlanetFrameId, 0f);
                        _properties.SetFloat(FractureMappingEnabledId, 1f);
                        _properties.SetMatrix(
                            FractureLocalToStructureId,
                            frame.LocalToStructure);
                        break;
                    default:
                        _properties.SetFloat(UsePlanetFrameId, 0f);
                        _properties.SetFloat(FractureMappingEnabledId, 0f);
                        break;
                }
                renderer.SetPropertyBlock(_properties);
                return true;
            }
        }
    }
}
