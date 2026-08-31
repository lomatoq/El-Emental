using UnityEngine;

namespace Elemental.Runtime.World
{
    [CreateAssetMenu(menuName = "Elemental/World/Celestial System Profile", fileName = "CelestialSystemProfile")]
    public sealed class CelestialSystemProfile : ScriptableObject
    {
        [SerializeField, Min(10f)] private float daySeconds = 480f;
        [SerializeField, Min(10f)] private float visualYearSeconds = 1440f;
        [SerializeField, Min(10f)] private float moonOrbitSeconds = 240f;
        [SerializeField, Range(-45f, 45f)] private float axialTiltDegrees = 18f;
        [SerializeField, Range(0f, 1f)] private float startTime01 = 0.21f;
        [SerializeField, Min(0f)] private float timeScale = 1f;
        [SerializeField] private bool paused;
        [Header("Lighting")]
        [SerializeField, Min(0f)] private float daylightIntensity = 1.55f;
        [SerializeField, Min(0f)] private float moonlightIntensity = 0.22f;
        [SerializeField] private Color dayColor = new Color(1f, 0.90f, 0.74f);
        [SerializeField] private Color duskColor = new Color(1f, 0.55f, 0.30f);
        [SerializeField] private Color dayAmbientSky = new Color(0.18f, 0.23f, 0.31f);
        [SerializeField] private Color dayAmbientEquator = new Color(0.12f, 0.105f, 0.10f);
        [SerializeField] private Color dayAmbientGround = new Color(0.045f, 0.035f, 0.03f);
        [SerializeField] private Color nightAmbient = new Color(0.025f, 0.04f, 0.085f);
        [SerializeField, Range(0.1f, 8f)] private float sunAngularSize = 2.2f;
        [SerializeField, Range(0.05f, 5f)] private float moonAngularSize = 1.25f;
        [SerializeField, Range(0.05f, 8f)] private float distantPlanetAngularSize = 2.8f;
        [SerializeField] private Color sunDiscColor = new Color(3.4f, 1.55f, 0.34f);
        [SerializeField] private Color moonColor = new Color(0.52f, 0.57f, 0.68f);
        [SerializeField] private Color distantPlanetColor = new Color(0.16f, 0.22f, 0.42f);
        [SerializeField, Min(1f)] private float scaledSpaceDistance = 1200f;

        public float DaySeconds => daySeconds;
        public float VisualYearSeconds => visualYearSeconds;
        public float MoonOrbitSeconds => moonOrbitSeconds;
        public float AxialTiltDegrees => axialTiltDegrees;
        public float StartTime01 => startTime01;
        public float TimeScale => paused ? 0f : timeScale;
        public float DaylightIntensity => daylightIntensity;
        public float MoonlightIntensity => moonlightIntensity;
        public Color DayColor => dayColor;
        public Color DuskColor => duskColor;
        public Color DayAmbientSky => dayAmbientSky;
        public Color DayAmbientEquator => dayAmbientEquator;
        public Color DayAmbientGround => dayAmbientGround;
        public Color NightAmbient => nightAmbient;
        public float SunAngularSize => sunAngularSize;
        public float MoonAngularSize => moonAngularSize;
        public float DistantPlanetAngularSize => distantPlanetAngularSize;
        public Color SunDiscColor => sunDiscColor;
        public Color MoonColor => moonColor;
        public Color DistantPlanetColor => distantPlanetColor;
        public float ScaledSpaceDistance => scaledSpaceDistance;
    }
}
