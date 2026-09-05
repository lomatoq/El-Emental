using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    [CreateAssetMenu(menuName = "Elemental/World/Earth Sky Profile", fileName = "EarthSkyProfile")]
    public sealed class EarthSkyProfile : ScriptableObject
    {
        [Header("Day")]
        [SerializeField] private Color dayZenith = new Color(0.075f, 0.31f, 0.72f, 1f);
        [SerializeField] private Color dayHorizon = new Color(0.56f, 0.79f, 0.98f, 1f);
        [SerializeField] private Color duskZenith = new Color(0.12f, 0.16f, 0.38f, 1f);
        [SerializeField] private Color duskHorizon = new Color(1f, 0.42f, 0.19f, 1f);
        [Header("Night")]
        [SerializeField] private Color nightZenith = new Color(0.0025f, 0.006f, 0.026f, 1f);
        [SerializeField] private Color nightHorizon = new Color(0.015f, 0.03f, 0.075f, 1f);
        [Tooltip("Night sky luminance only. World silhouette readability is controlled by the celestial night ambient setting.")]
        [SerializeField, Range(0.5f, 3f)] private float nightSkyIntensity = 1.45f;
        [SerializeField, Range(0f, 2f)] private float starExposure = 1.15f;
        [SerializeField, Range(256, 12000)] private int starCount = 4500;
        [SerializeField] private int starSeed = 57824;
        [SerializeField] private Color duskPink = new Color(0.72f, 0.24f, 0.39f, 1f);
        [Header("Sun")]
        [SerializeField] private Color sunColor = new Color(1f, 0.88f, 0.62f, 1f);
        [Tooltip("Visible solar disc angular diameter in degrees.")]
        [SerializeField, Range(0.05f, 2f)] private float sunDiscDegrees = 0.44f;
        [SerializeField, Range(0f, 2f)] private float sunGlow = 0.72f;

        public Color DayZenith => dayZenith;
        public Color DayHorizon => dayHorizon;
        public Color DuskZenith => duskZenith;
        public Color DuskHorizon => duskHorizon;
        public Color NightZenith => nightZenith;
        public Color NightHorizon => nightHorizon;
        public float NightSkyIntensity =>
            !float.IsNaN(nightSkyIntensity) && !float.IsInfinity(nightSkyIntensity) && nightSkyIntensity >= .5f
                ? Mathf.Clamp(nightSkyIntensity, .5f, 3f)
                : 1.45f;
        public float StarExposure => starExposure;
        public int StarCount => starCount;
        public int StarSeed => starSeed;
        public Color DuskPink => duskPink;
        public Color SunColor => sunColor;
        public float SunDiscDegrees => sunDiscDegrees;
        public float SunGlow => sunGlow;
    }
}
