using Unity.Profiling;
using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    public enum UnifiedLightingBindDisposition : byte
    {
        None = 0,
        Applied = 1,
        SkippedNonRenderable = 2,
        Rejected = 3
    }

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
        private static readonly int FractureNormalToStructureId =
            Shader.PropertyToID("_FractureNormalToStructure");

        [SerializeField] private UnifiedLightingMigrationProfile migrationProfile;

        private MaterialPropertyBlock _properties;

        public UnifiedLightingBindDisposition LastBindDisposition { get; private set; }
        public uint NonRenderableSkipCount { get; private set; }

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
                LastBindDisposition = UnifiedLightingBindDisposition.None;
                if (renderer == null || migrationProfile == null ||
                    !migrationProfile.IsComplete() || !frame.IsValid)
                {
                    LastBindDisposition = UnifiedLightingBindDisposition.Rejected;
                    Debug.LogError(
                        "Unified-lighting binding requires a renderer, complete migration " +
                        "profile, and finite projection frame.",
                        this);
                    return false;
                }
                // Imported collider helpers may retain a MeshRenderer solely
                // because they share source FBX geometry. A disabled renderer is
                // explicitly outside the lighting contract; dormant visual
                // fracture pieces remain renderer-enabled on inactive objects and
                // therefore still pass through full validation and binding.
                if (!renderer.enabled || renderer.forceRenderingOff)
                {
                    LastBindDisposition =
                        UnifiedLightingBindDisposition.SkippedNonRenderable;
                    if (NonRenderableSkipCount < uint.MaxValue)
                        NonRenderableSkipCount++;
                    return true;
                }
                if (!migrationProfile.TryResolve(
                        role,
                        out _,
                        out UnifiedLightingRoleContract contract) ||
                    contract.ProjectionMode != frame.Mode)
                {
                    LastBindDisposition = UnifiedLightingBindDisposition.Rejected;
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
                    LastBindDisposition = UnifiedLightingBindDisposition.Rejected;
                    Debug.LogError(
                        $"Renderer '{renderer.name}' slot {materialIndex} is not assigned " +
                        $"the unified {contract.Family} material family.",
                        renderer);
                    return false;
                }

                _properties ??= new MaterialPropertyBlock();
                _properties.Clear();
                renderer.GetPropertyBlock(_properties, materialIndex);
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
                        _properties.SetMatrix(
                            FractureNormalToStructureId,
                            frame.NormalToStructure);
                        break;
                    default:
                        _properties.SetFloat(UsePlanetFrameId, 0f);
                        _properties.SetFloat(FractureMappingEnabledId, 0f);
                        break;
                }
                renderer.SetPropertyBlock(_properties, materialIndex);
                LastBindDisposition = UnifiedLightingBindDisposition.Applied;
                return true;
            }
        }
    }
}
