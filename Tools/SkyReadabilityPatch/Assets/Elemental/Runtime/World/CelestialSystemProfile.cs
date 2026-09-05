using UnityEngine;
using UnityEngine.Serialization;

namespace Elemental.Runtime.World
{
    [CreateAssetMenu(menuName = "Elemental/World/Celestial System Profile", fileName = "CelestialSystemProfile")]
    public sealed class CelestialSystemProfile : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector] private int cycleSchema;
        [SerializeField, HideInInspector, FormerlySerializedAs("daySeconds")] private float legacyCycleSeconds;
        [Header("Cycle (seconds at time scale 1)")]
        [Tooltip("Sunrise to sunset at the arena reference latitude; half of the solar orbit.")]
        [SerializeField, Min(10f)] private float daylightSeconds = 300f;
        [Tooltip("Sunset to the next sunrise. Independently configurable.")]
        [SerializeField, Min(10f)] private float nightSeconds = 300f;
        [SerializeField, Min(10f)] private float visualYearSeconds = 1440f;
        [SerializeField, Min(10f)] private float moonOrbitSeconds = 240f;
        [SerializeField, Range(-45f, 45f)] private float axialTiltDegrees = 18f;
        [SerializeField, Range(0f, 1f)] private float startTime01 = 0.21f;
        [SerializeField, Min(0f)] private float timeScale = 1f;
        [SerializeField] private bool paused;
        [Header("Lighting")]
        [SerializeField, Min(0f)] private float daylightIntensity = 1.55f;
        [SerializeField, Min(0f)] private float moonlightIntensity = 0.80f;
        [SerializeField] private Color dayColor = new Color(1f, 0.90f, 0.74f);
        [SerializeField] private Color duskColor = new Color(1f, 0.55f, 0.30f);
        [SerializeField] private Color dayAmbientSky = new Color(0.18f, 0.23f, 0.31f);
        [SerializeField] private Color dayAmbientEquator = new Color(0.12f, 0.105f, 0.10f);
        [SerializeField] private Color dayAmbientGround = new Color(0.045f, 0.035f, 0.03f);
        [SerializeField] private Color nightAmbient = new Color(0.025f, 0.04f, 0.085f);
        [Tooltip("Multiplier for the night Trilight ambient probe. Keep this subtle, but above zero so silhouettes remain readable.")]
        [SerializeField, Range(0.25f, 2f)] private float nightAmbientIntensity = 1.05f;
        [SerializeField, Range(0.1f, 8f)] private float sunAngularSize = 2.2f;
        [SerializeField, Range(0.05f, 5f)] private float moonAngularSize = 1.25f;
        [SerializeField, Range(0.05f, 8f)] private float distantPlanetAngularSize = 2.8f;
        [SerializeField] private Color sunDiscColor = new Color(3.4f, 1.55f, 0.34f);
        [SerializeField] private Color moonColor = new Color(0.52f, 0.57f, 0.68f);
        [SerializeField] private Color distantPlanetColor = new Color(0.16f, 0.22f, 0.42f);
        [SerializeField, Min(1f)] private float scaledSpaceDistance = 1200f;

        public float DaylightSeconds { get { UpgradeCycle(); return daylightSeconds; } }
        public float NightSeconds { get { UpgradeCycle(); return nightSeconds; } }
        public float DaySeconds => DaylightSeconds + NightSeconds;
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
        public float NightAmbientIntensity =>
            !float.IsNaN(nightAmbientIntensity) && !float.IsInfinity(nightAmbientIntensity) && nightAmbientIntensity >= .25f
                ? Mathf.Clamp(nightAmbientIntensity, .25f, 2f)
                : 1.05f;
        public float SunAngularSize => sunAngularSize;
        public float MoonAngularSize => moonAngularSize;
        public float DistantPlanetAngularSize => distantPlanetAngularSize;
        public Color SunDiscColor => sunDiscColor;
        public Color MoonColor => moonColor;
        public Color DistantPlanetColor => distantPlanetColor;
        public float ScaledSpaceDistance => scaledSpaceDistance;

        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize() => UpgradeCycle();
        private void UpgradeCycle()
        {
            if (cycleSchema >= 1) return;
            if (legacyCycleSeconds > 0f && !float.IsInfinity(legacyCycleSeconds))
                daylightSeconds = nightSeconds = System.Math.Max(10f, legacyCycleSeconds * .5f);
            cycleSchema = 1;
        }
    }
}
