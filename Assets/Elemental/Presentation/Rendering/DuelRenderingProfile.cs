using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Presentation.Rendering
{
    [CreateAssetMenu(
        fileName = "DuelRenderingProfile",
        menuName = "Elemental/Rendering/Duel Rendering Profile")]
    public sealed class DuelRenderingProfile : ScriptableObject
    {
        [Header("Artist-owned scene materials")]
        [SerializeField] private Material arenaExteriorMaterial;
        [SerializeField] private Material arenaInteriorMaterial;
        [SerializeField] private Material planetSurfaceMaterial;

        [Header("Realtime directional shadows")]
        [Tooltip("Master switch used by the generated game camera. Keep this off for the stable shadow-free arena look.")]
        [SerializeField] private bool renderRealtimeDirectionalShadows = false;
        [SerializeField] private LightShadows sunShadowType = LightShadows.None;
        [SerializeField, Range(0f, 1f)] private float sunShadowStrength = 0f;

        [Header("Arena self / cast shadows")]
        [SerializeField] private ShadowCastingMode arenaShadowCastingMode = ShadowCastingMode.Off;
        [SerializeField] private bool arenaReceiveShadows = false;

        [Header("Voxel planet self / cast shadows")]
        [SerializeField] private ShadowCastingMode planetShadowCastingMode = ShadowCastingMode.On;
        [SerializeField] private bool planetReceiveShadows = true;

        [SerializeField] private bool useDuelShadowMap = false;
        [SerializeField] private DuelShadowSettings duelShadows = new DuelShadowSettings();
        [SerializeField] private bool useCapsuleContactShadows = false;
        [SerializeField] private CapsuleContactShadowSettings capsuleContactShadows =
            new CapsuleContactShadowSettings();

        public Material ArenaExteriorMaterial => arenaExteriorMaterial;
        public Material ArenaInteriorMaterial => arenaInteriorMaterial;
        public Material PlanetSurfaceMaterial => planetSurfaceMaterial;
        public bool RenderRealtimeDirectionalShadows => renderRealtimeDirectionalShadows;
        public LightShadows SunShadowType => sunShadowType;
        public float SunShadowStrength => Mathf.Clamp01(sunShadowStrength);
        public ShadowCastingMode ArenaShadowCastingMode => arenaShadowCastingMode;
        public bool ArenaReceiveShadows => arenaReceiveShadows;
        public ShadowCastingMode PlanetShadowCastingMode => planetShadowCastingMode;
        public bool PlanetReceiveShadows => planetReceiveShadows;
        public bool UseDuelShadowMap => useDuelShadowMap;
        public DuelShadowSettings DuelShadows => duelShadows;
        public bool UseCapsuleContactShadows => useCapsuleContactShadows;
        public CapsuleContactShadowSettings CapsuleContactShadows => capsuleContactShadows;

        /// <summary>
        /// Authoring-time feature selection used by the generated duel scene.
        /// The legacy realtime/duel shadow map remains independently disabled;
        /// this only enables the bounded contact-only capsule pass.
        /// </summary>
        public void ConfigureCapsuleContactShadows(bool enabled)
        {
            useCapsuleContactShadows = enabled;
        }

        /// <summary>
        /// Seeds missing references once. Existing artist assignments and every
        /// material property remain untouched by future scene rebuilds.
        /// </summary>
        public bool EnsureAuthoringMaterials(
            Material configuredArenaExterior,
            Material configuredArenaInterior,
            Material configuredPlanetSurface)
        {
            bool changed = false;
            if (arenaExteriorMaterial == null && configuredArenaExterior != null)
            {
                arenaExteriorMaterial = configuredArenaExterior;
                changed = true;
            }
            if (arenaInteriorMaterial == null && configuredArenaInterior != null)
            {
                arenaInteriorMaterial = configuredArenaInterior;
                changed = true;
            }
            if (planetSurfaceMaterial == null && configuredPlanetSurface != null)
            {
                planetSurfaceMaterial = configuredPlanetSurface;
                changed = true;
            }
            return changed;
        }
    }
}
