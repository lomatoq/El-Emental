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
        [SerializeField, Range(0f, 2f)] private float starExposure = 1.15f;
        [Header("Sun")]
        [SerializeField] private Color sunColor = new Color(1f, 0.88f, 0.62f, 1f);
        [SerializeField, Range(0.05f, 2f)] private float sunDiscDegrees = 0.44f;
        [SerializeField, Range(0f, 2f)] private float sunGlow = 0.72f;

        public Color DayZenith => dayZenith;
        public Color DayHorizon => dayHorizon;
        public Color DuskZenith => duskZenith;
        public Color DuskHorizon => duskHorizon;
        public Color NightZenith => nightZenith;
        public Color NightHorizon => nightHorizon;
        public float StarExposure => starExposure;
        public Color SunColor => sunColor;
        public float SunDiscDegrees => sunDiscDegrees;
        public float SunGlow => sunGlow;
    }
}
