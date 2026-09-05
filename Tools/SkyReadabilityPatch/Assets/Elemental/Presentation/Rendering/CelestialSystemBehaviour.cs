using Elemental.Runtime.World;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Time;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;

namespace Elemental.Presentation.Rendering
{
    [DisallowMultipleComponent]
    public sealed class CelestialSystemBehaviour : MonoBehaviour
    {
        private static readonly ProfilerMarker UpdateMarker = new ProfilerMarker("Elemental.Celestial.Update");
        private const string RuntimeMoonLightName = "__Runtime Moon Key";
        [SerializeField] private CelestialSystemProfile profile;
        [SerializeField] private AtmosphereProfile atmosphereProfile;
        [SerializeField] private EarthSkyProfile skyProfile;
        [SerializeField] private Transform planet;
        [SerializeField] private Transform lightingAnchor;
        [SerializeField] private UnityEngine.Camera targetCamera;
        [SerializeField] private Light sunLight;
        [SerializeField] private Transform sunDisc;
        [SerializeField] private Transform moon;
        [SerializeField] private Transform distantPlanet;
        [SerializeField] private Renderer atmosphereShell;
        [SerializeField] private Material starSkybox;
        [SerializeField] private CelestialLightingAuthorityMode lightingAuthority =
            CelestialLightingAuthorityMode.AnimatedEphemeris;

        private double _elapsed;
        private MaterialPropertyBlock _atmosphereProperties;
        private EarthSkyController _skyController;
        private bool _authoredLightingCaptured;
        private Quaternion _authoredSunRotation;
        private Color _authoredSunColor;
        private float _authoredSunIntensity;
        private AmbientMode _authoredAmbientMode;
        private Color _authoredAmbientSky;
        private Color _authoredAmbientEquator;
        private Color _authoredAmbientGround;
        private Color _authoredAmbientLight;
        private float _authoredAmbientIntensity;
        private Light _runtimeMoonLight;

        public CelestialSnapshot Snapshot { get; private set; }
        public Material StarSkybox => _skyController != null ? _skyController.RuntimeSkybox : starSkybox;
        public CelestialLightingAuthorityMode LightingAuthority => lightingAuthority;
        public CelestialSystemProfile Profile => profile;
        public Light SunLight => sunLight;
        public Light MoonLight => _runtimeMoonLight;
        public UnityEngine.Camera TargetCamera => targetCamera;
        public Vector3 ObserverUp { get; private set; } = Vector3.up;
        public Vector3 LightingUp { get; private set; } = Vector3.up;
        public Transform LightingAnchor => lightingAnchor;
        public Color SolarColor { get; private set; } = Color.white;
        public bool HasRequiredBindings => profile != null && skyProfile != null && planet != null &&
            targetCamera != null && sunLight != null && starSkybox != null && _skyController != null && lightingAnchor != null;

        public void ConfigureLightingAnchor(Transform anchor) => lightingAnchor = anchor;

        public void SetTimeOfDayForQa(float timeOfDay01)
        {
            if (profile == null) return;
            float target = Mathf.Repeat(timeOfDay01, 1f);
            _elapsed = CelestialDayNightCycle.SecondsAtPhase(target, profile.DaylightSeconds, profile.NightSeconds) -
                CelestialDayNightCycle.SecondsAtPhase(profile.StartTime01, profile.DaylightSeconds, profile.NightSeconds);
        }

        public void SetLightingAuthorityForQa(CelestialLightingAuthorityMode mode)
        {
            if (lightingAuthority == mode) return;
            lightingAuthority = mode;
            if (lightingAuthority == CelestialLightingAuthorityMode.GameplayLocked)
            {
                CaptureAuthoredLighting(false);
                ApplyAuthoredGameplayLighting();
            }
        }

        public void Configure(
            CelestialSystemProfile configuredProfile,
            AtmosphereProfile configuredAtmosphere,
            EarthSkyProfile configuredSky,
            Transform planetTransform,
            UnityEngine.Camera camera,
            Light directionalLight,
            Transform visibleSun,
            Transform visibleMoon,
            Transform visibleDistantPlanet,
            Renderer shell,
            Material skybox)
        {
            profile = configuredProfile;
            atmosphereProfile = configuredAtmosphere;
            skyProfile = configuredSky;
            planet = planetTransform;
            targetCamera = camera;
            sunLight = directionalLight;
            sunDisc = visibleSun;
            moon = visibleMoon;
            distantPlanet = visibleDistantPlanet;
            atmosphereShell = shell;
            starSkybox = skybox;
            CaptureAuthoredLighting(true);
            _skyController = GetComponent<EarthSkyController>();
            _skyController?.Configure(skyProfile, targetCamera, starSkybox);
            ApplyStaticAtmosphere();
        }

        private void Awake()
        {
            if (targetCamera == null) targetCamera = UnityEngine.Camera.main;
            CaptureAuthoredLighting(false);
            _skyController = GetComponent<EarthSkyController>();
            if (_skyController == null)
                Debug.LogError("[Elemental] Celestial system requires an authored EarthSkyController.", this);
            else
                _skyController.Configure(skyProfile, targetCamera, starSkybox);
            ApplyStaticAtmosphere();
        }

        private void OnEnable()
        {
            CaptureAuthoredLighting(false);
            if (lightingAuthority == CelestialLightingAuthorityMode.GameplayLocked)
                ApplyAuthoredGameplayLighting();
        }

        private void OnDisable()
        {
            if (_runtimeMoonLight != null)
                _runtimeMoonLight.enabled = false;
        }

        private void Update() => EvaluateFrame(Time.deltaTime);
        public void EvaluatePresentationForQa() => EvaluateFrame(0f);

        private void EvaluateFrame(float deltaTime)
        {
            using var marker = UpdateMarker.Auto();
            if (profile == null || targetCamera == null) return;
            _elapsed = CelestialLightingClockPolicy.Step(
                _elapsed,
                deltaTime,
                profile.TimeScale,
                lightingAuthority);
            Snapshot = CelestialEphemerisSolver.EvaluateAtPhase(
                _elapsed,
                CelestialDayNightCycle.Phase(_elapsed, profile.StartTime01, profile.DaylightSeconds, profile.NightSeconds),
                profile.VisualYearSeconds,
                profile.MoonOrbitSeconds,
                profile.AxialTiltDegrees);
            Vector3 sunDirection = ToVector3(Snapshot.SunDirection);
            Vector3 moonDirection = ToVector3(Snapshot.MoonDirection);
            Vector3 lightingUp = lightingAnchor != null && planet != null
                ? (lightingAnchor.position - planet.position).normalized : Vector3.up;
            if (lightingUp.sqrMagnitude < .5f) lightingUp = Vector3.up;
            LightingUp = lightingUp;
            Quaternion solarFrame = Quaternion.FromToRotation(Vector3.up, lightingUp);
            sunDirection = solarFrame * sunDirection;
            moonDirection = solarFrame * moonDirection;
            Vector3 localUp = planet != null
                ? (targetCamera.transform.position - planet.position).normalized
                : targetCamera.transform.up;
            if (localUp.sqrMagnitude < 0.5f) localUp = targetCamera.transform.up;
            ObserverUp = localUp;
            Snapshot = new CelestialSnapshot(Snapshot.TimeOfDay01, Snapshot.Orbit01, Snapshot.MoonOrbit01,
                new float3(sunDirection.x, sunDirection.y, sunDirection.z),
                new float3(moonDirection.x, moonDirection.y, moonDirection.z), Snapshot.MoonPhase01,
                CelestialDayNightCycle.Night(new float3(sunDirection.x, sunDirection.y, sunDirection.z),
                    new float3(lightingUp.x, lightingUp.y, lightingUp.z)));
            Vector3 center = targetCamera.transform.position;
            float distance = profile.ScaledSpaceDistance;
            if (sunDisc != null)
            {
                sunDisc.position = center + sunDirection * distance;
                sunDisc.localScale = Vector3.one * (distance * Mathf.Tan(profile.SunAngularSize * Mathf.Deg2Rad));
            }
            if (moon != null)
            {
                float moonDistance = distance * 0.82f;
                moon.position = center + moonDirection * moonDistance;
                moon.localScale = Vector3.one * (moonDistance * Mathf.Tan(profile.MoonAngularSize * Mathf.Deg2Rad));
            }
            if (distantPlanet != null)
            {
                float angle = Snapshot.Orbit01 * Mathf.PI * 2f + 1.7f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0.24f, Mathf.Sin(angle)).normalized;
                float planetDistance = distance * 0.94f;
                distantPlanet.position = center + direction * planetDistance;
                distantPlanet.localScale = Vector3.one * (planetDistance * Mathf.Tan(profile.DistantPlanetAngularSize * Mathf.Deg2Rad));
            }

            ApplyLightingAuthority(
                sunDirection,
                moonDirection,
                lightingUp);

            float lightingSolarAltitude = Vector3.Dot(sunDirection.normalized, lightingUp);
            Shader.SetGlobalFloat("_ElementalNight01", Snapshot.Night01);
            _skyController?.Apply(
                Snapshot, sunDirection, localUp, SolarColor, lightingSolarAltitude);
            float twilight = _skyController != null
                ? _skyController.LastTwilight01
                : Mathf.Clamp01(1f - Mathf.Abs(lightingSolarAltitude) * 4.2f) *
                  Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-.18f, .08f, lightingSolarAltitude));
            Shader.SetGlobalFloat("_ElementalSolarAltitude", lightingSolarAltitude);
            Shader.SetGlobalFloat("_ElementalTwilight01", twilight);
            Shader.SetGlobalVector("_ElementalSunDirection",
                new Vector4(sunDirection.x, sunDirection.y, sunDirection.z, 0f));
            float radius = ResolvePlanetRadius();
            Vector3 planetCenter = planet != null ? planet.position : Vector3.zero;
            Shader.SetGlobalVector("_ElementalPlanetCenterRadius", new Vector4(
                planetCenter.x, planetCenter.y, planetCenter.z, radius));
            if (atmosphereProfile != null)
            {
                Shader.SetGlobalVector("_ElementalAtmosphereParams", new Vector4(
                    atmosphereProfile.OuterRadiusMultiplier,
                    atmosphereProfile.RayleighStrength,
                    atmosphereProfile.MieStrength,
                    atmosphereProfile.HorizonStrength));
                Shader.SetGlobalVector("_ElementalAerialPerspectiveParams", new Vector4(
                    atmosphereProfile.AerialPerspectiveStrength,
                    atmosphereProfile.AerialPerspectiveDistance,
                    atmosphereProfile.HeightFalloff,
                    atmosphereProfile.MaximumAerialOpacity));
                Shader.SetGlobalVector("_ElementalCloudParams", new Vector4(
                    atmosphereProfile.CloudCoverage,
                    atmosphereProfile.CloudOpacity,
                    atmosphereProfile.CloudScale,
                    atmosphereProfile.CloudSpeed));
                Shader.SetGlobalFloat("_ElementalNightOpacity", atmosphereProfile.NightOpacity);
                Shader.SetGlobalColor("_ElementalRayleighColor", atmosphereProfile.RayleighColor);
                Shader.SetGlobalColor("_ElementalMieColor", SolarColor);
            }
            UpdateAtmosphereProperties();
        }

        private Vector3 ApplyLightingAuthority(
            Vector3 sunDirection,
            Vector3 moonDirection,
            Vector3 localUp)
        {
            if (lightingAuthority == CelestialLightingAuthorityMode.GameplayLocked)
            {
                if (_runtimeMoonLight != null)
                    _runtimeMoonLight.enabled = false;
                ApplyAuthoredGameplayLighting();
                if (sunLight != null)
                {
                    Vector3 authoredDirection = -sunLight.transform.forward;
                    if (authoredDirection.sqrMagnitude > 0.001f)
                        return authoredDirection.normalized;
                }
                return sunDirection.sqrMagnitude > 0.001f
                    ? sunDirection.normalized
                    : localUp;
            }

            float daylight = 1f - Snapshot.Night01;
            Vector3 keyDirection = sunDirection;
            if (keyDirection.sqrMagnitude < 0.001f)
                keyDirection = localUp;

            if (sunLight != null)
            {
                Vector3 orientationUp = Mathf.Abs(Vector3.Dot(keyDirection.normalized, localUp)) > .98f
                    ? Vector3.Cross(keyDirection, Mathf.Abs(keyDirection.x) < .8f ? Vector3.right : Vector3.forward).normalized
                    : localUp;
                sunLight.transform.rotation = Quaternion.LookRotation(-keyDirection.normalized, orientationUp);
                float horizon = Mathf.Clamp01(
                    1f - Mathf.Abs(Vector3.Dot(sunDirection, localUp)) * 5f);
                Color solarColor = Color.Lerp(profile.DayColor, profile.DuskColor, horizon);
                SolarColor = solarColor;
                sunLight.color = solarColor;
                sunLight.intensity = profile.DaylightIntensity * CelestialDayNightCycle.SolarStrength(Vector3.Dot(sunDirection, localUp));
            }

            ApplyMoonFill(moonDirection, localUp);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(
                profile.NightAmbient * 1.55f,
                profile.DayAmbientSky,
                daylight);
            RenderSettings.ambientEquatorColor = Color.Lerp(
                profile.NightAmbient * 1.05f,
                profile.DayAmbientEquator,
                daylight);
            RenderSettings.ambientGroundColor = Color.Lerp(
                profile.NightAmbient * 0.65f,
                profile.DayAmbientGround,
                daylight);
            RenderSettings.ambientIntensity = Mathf.Lerp(
                profile.NightAmbientIntensity, 0.82f, daylight);
            return keyDirection.normalized;
        }

        private void ApplyMoonFill(Vector3 moonDirection, Vector3 localUp)
        {
            EnsureRuntimeMoonLight();
            if (_runtimeMoonLight == null) return;

            float night = Snapshot.Night01;
            _runtimeMoonLight.enabled = night > .001f && profile.MoonlightIntensity > .001f;
            _runtimeMoonLight.color = profile.MoonColor;
            _runtimeMoonLight.intensity = profile.MoonlightIntensity * night;
            if (!_runtimeMoonLight.enabled) return;

            Vector3 up = localUp.sqrMagnitude > .5f ? localUp.normalized : Vector3.up;
            Vector3 actualDirection = moonDirection.sqrMagnitude > .001f
                ? moonDirection.normalized
                : sunLight != null ? -sunLight.transform.forward : transform.forward;
            Vector3 tangent = Vector3.ProjectOnPlane(actualDirection, up);
            if (tangent.sqrMagnitude < .001f)
                tangent = Vector3.ProjectOnPlane(transform.forward, up);
            if (tangent.sqrMagnitude < .001f)
                tangent = Vector3.Cross(up, Vector3.right);

            // The visible moon keeps its ephemeris direction. This separate, shadowless
            // fill stays just above the local horizon so moon-below-horizon nights still
            // retain the subtle silhouettes required for playability.
            float altitude = Mathf.Max(Vector3.Dot(actualDirection, up), .28f);
            float tangentWeight = Mathf.Sqrt(Mathf.Max(0f, 1f - altitude * altitude));
            Vector3 fillDirection = (tangent.normalized * tangentWeight + up * altitude).normalized;
            Vector3 orientationUp = Mathf.Abs(Vector3.Dot(fillDirection, up)) > .98f
                ? Vector3.Cross(fillDirection, Mathf.Abs(fillDirection.x) < .8f ? Vector3.right : Vector3.forward).normalized
                : up;
            _runtimeMoonLight.transform.rotation = Quaternion.LookRotation(-fillDirection, orientationUp);
        }

        private void EnsureRuntimeMoonLight()
        {
            if (_runtimeMoonLight != null) return;
            Transform existing = transform.Find(RuntimeMoonLightName);
            GameObject lightObject;
            if (existing != null)
            {
                lightObject = existing.gameObject;
            }
            else
            {
                lightObject = new GameObject(RuntimeMoonLightName);
                lightObject.hideFlags = HideFlags.DontSave;
                lightObject.transform.SetParent(transform, false);
            }

            _runtimeMoonLight = lightObject.GetComponent<Light>();
            if (_runtimeMoonLight == null)
                _runtimeMoonLight = lightObject.AddComponent<Light>();
            _runtimeMoonLight.type = LightType.Directional;
            _runtimeMoonLight.shadows = LightShadows.None;
            _runtimeMoonLight.bounceIntensity = 0f;
            _runtimeMoonLight.renderMode = LightRenderMode.ForcePixel;
            if (sunLight != null)
                _runtimeMoonLight.cullingMask = sunLight.cullingMask;
        }

        private void CaptureAuthoredLighting(bool force)
        {
            if (_authoredLightingCaptured && !force) return;
            if (sunLight != null)
            {
                _authoredSunRotation = sunLight.transform.rotation;
                _authoredSunColor = sunLight.color;
                _authoredSunIntensity = sunLight.intensity;
            }
            _authoredAmbientMode = RenderSettings.ambientMode;
            _authoredAmbientSky = RenderSettings.ambientSkyColor;
            _authoredAmbientEquator = RenderSettings.ambientEquatorColor;
            _authoredAmbientGround = RenderSettings.ambientGroundColor;
            _authoredAmbientLight = RenderSettings.ambientLight;
            _authoredAmbientIntensity = RenderSettings.ambientIntensity;
            _authoredLightingCaptured = true;
        }

        private void ApplyAuthoredGameplayLighting()
        {
            if (!_authoredLightingCaptured) CaptureAuthoredLighting(false);
            if (sunLight != null)
            {
                sunLight.transform.rotation = _authoredSunRotation;
                sunLight.color = _authoredSunColor;
                sunLight.intensity = _authoredSunIntensity;
            }
            RenderSettings.ambientMode = _authoredAmbientMode;
            RenderSettings.ambientSkyColor = _authoredAmbientSky;
            RenderSettings.ambientEquatorColor = _authoredAmbientEquator;
            RenderSettings.ambientGroundColor = _authoredAmbientGround;
            RenderSettings.ambientLight = _authoredAmbientLight;
            RenderSettings.ambientIntensity = _authoredAmbientIntensity;
        }

        private void ApplyStaticAtmosphere()
        {
            if (atmosphereShell == null || atmosphereProfile == null || planet == null) return;
            _atmosphereProperties ??= new MaterialPropertyBlock();
            float radius = ResolvePlanetRadius();
            atmosphereShell.transform.position = planet.position;
            atmosphereShell.transform.localScale = Vector3.one * radius * 2f * atmosphereProfile.OuterRadiusMultiplier;
            UpdateAtmosphereProperties();
        }

        private void UpdateAtmosphereProperties()
        {
            if (atmosphereShell == null || atmosphereProfile == null || planet == null) return;
            _atmosphereProperties ??= new MaterialPropertyBlock();
            atmosphereShell.GetPropertyBlock(_atmosphereProperties);
            _atmosphereProperties.SetVector("_PlanetCenter", planet.position);
            _atmosphereProperties.SetFloat("_PlanetRadius", ResolvePlanetRadius());
            _atmosphereProperties.SetColor("_RayleighColor", atmosphereProfile.RayleighColor);
            _atmosphereProperties.SetColor("_MieColor", SolarColor);
            _atmosphereProperties.SetFloat("_RayleighStrength", atmosphereProfile.RayleighStrength);
            _atmosphereProperties.SetFloat("_MieStrength", atmosphereProfile.MieStrength);
            _atmosphereProperties.SetFloat("_HorizonStrength", atmosphereProfile.HorizonStrength);
            _atmosphereProperties.SetFloat("_NightOpacity", atmosphereProfile.NightOpacity);
            atmosphereShell.SetPropertyBlock(_atmosphereProperties);
        }

        private float ResolvePlanetRadius()
        {
            VoxelPlanetBehaviour voxel = planet != null ? planet.GetComponent<VoxelPlanetBehaviour>() : null;
            if (voxel != null) return voxel.Radius;
            PointPlanetGravitySource gravity = planet != null
                ? planet.GetComponent<PointPlanetGravitySource>()
                : null;
            return gravity != null ? gravity.Radius : 1f;
        }

        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
