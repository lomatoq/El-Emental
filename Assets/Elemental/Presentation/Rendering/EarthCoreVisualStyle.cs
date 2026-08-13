using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    [CreateAssetMenu(menuName = "Elemental/Presentation/Earth Core Visual Style")]
    public sealed class EarthCoreVisualStyle : ScriptableObject
    {
        [Header("Planet")]
        [SerializeField] private Color stoneColor = new Color(0.42f, 0.285f, 0.19f, 1f);
        [SerializeField] private Color stoneEmission = new Color(0.012f, 0.006f, 0.002f, 1f);
        [SerializeField, Range(0f, 1f)] private float stoneSmoothness = 0.08f;

        [Header("Character")]
        [SerializeField] private Color bodyColor = new Color(0.11f, 0.16f, 0.20f, 1f);
        [SerializeField] private Color scarfColor = new Color(0.92f, 0.34f, 0.09f, 1f);
        [SerializeField] private Color eyeColor = new Color(1f, 0.78f, 0.28f, 1f);

        [Header("Earth magic")]
        [SerializeField] private Color previewCoreColor = new Color(1f, 0.64f, 0.12f, 1f);
        [SerializeField] private Color previewEdgeColor = new Color(1f, 0.92f, 0.56f, 0.12f);
        [SerializeField] private Color dustColor = new Color(0.50f, 0.36f, 0.25f, 1f);
        [SerializeField] private Color sparkColor = new Color(0.88f, 0.63f, 0.24f, 1f);

        [Header("World")]
        [SerializeField] private Color skyColor = new Color(0.006f, 0.011f, 0.025f, 1f);
        [SerializeField] private Color ambientColor = new Color(0.14f, 0.18f, 0.27f, 1f);
        [SerializeField] private Color sunColor = new Color(1f, 0.78f, 0.55f, 1f);
        [SerializeField, Min(0f)] private float sunIntensity = 1.15f;
        [SerializeField, Min(0f)] private float rimIntensity = 1.8f;
        [SerializeField, Min(0f)] private float cameraDistance = 6.35f;
        [SerializeField, Min(0f)] private float cameraHeight = 2.35f;
        [SerializeField, Min(0f)] private float cameraFocusHeight = 1.05f;
        [SerializeField, Min(0f)] private float cameraLookAheadDistance = 3.2f;
        [SerializeField] private float cameraShoulderOffset = 0.82f;

        public Color StoneColor => stoneColor;
        public Color StoneEmission => stoneEmission;
        public float StoneSmoothness => stoneSmoothness;
        public Color BodyColor => bodyColor;
        public Color ScarfColor => scarfColor;
        public Color EyeColor => eyeColor;
        public Color PreviewCoreColor => previewCoreColor;
        public Color PreviewEdgeColor => previewEdgeColor;
        public Color DustColor => dustColor;
        public Color SparkColor => sparkColor;
        public Color SkyColor => skyColor;
        public Color AmbientColor => ambientColor;
        public Color SunColor => sunColor;
        public float SunIntensity => sunIntensity;
        public float RimIntensity => rimIntensity;
        public float CameraDistance => cameraDistance;
        public float CameraHeight => cameraHeight;
        public float CameraFocusHeight => cameraFocusHeight;
        public float CameraLookAheadDistance => cameraLookAheadDistance;
        public float CameraShoulderOffset => cameraShoulderOffset;
    }
}
