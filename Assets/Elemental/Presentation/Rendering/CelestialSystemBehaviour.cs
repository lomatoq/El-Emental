using Elemental.Runtime.World;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Time;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    [DisallowMultipleComponent]
    public sealed class CelestialSystemBehaviour : MonoBehaviour
    {
        [SerializeField] private CelestialSystemProfile profile;
        [SerializeField] private AtmosphereProfile atmosphereProfile;
        [SerializeField] private EarthSkyProfile skyProfile;
        [SerializeField] private Transform planet;
        [SerializeField] private UnityEngine.Camera targetCamera;
        [SerializeField] private Light sunLight;
        [SerializeField] private Transform sunDisc;
        [SerializeField] private Transform moon;
        [SerializeField] private Transform distantPlanet;
        [SerializeField] private Renderer atmosphereShell;
        [SerializeField] private Material starSkybox;

        private double _elapsed;
        private MaterialPropertyBlock _atmosphereProperties;
        private bool _qaDawnShowcase;
        private bool _qaNightShowcase;
        private EarthSkyController _skyController;

        public CelestialSnapshot Snapshot { get; private set; }
        public Material StarSkybox => _skyController != null ? _skyController.RuntimeSkybox : starSkybox;

        public void SetTimeOfDayForQa(float timeOfDay01)
        {
            if (profile == null) return;
            float target = Mathf.Repeat(timeOfDay01, 1f);
            _elapsed = (target - profile.StartTime01) * profile.DaySeconds;
            _qaDawnShowcase = target < 0.08f;
            _qaNightShowcase = target > 0.55f && target < 0.9f;
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
            _skyController = GetComponent<EarthSkyController>();
            _skyController?.Configure(skyProfile, targetCamera, starSkybox);
            ApplyStaticAtmosphere();
        }

        private void Awake()
        {
            if (targetCamera == null) targetCamera = UnityEngine.Camera.main;
            _skyController = GetComponent<EarthSkyController>();
            if (_skyController == null)
                Debug.LogError("[Elemental] Celestial system requires an authored EarthSkyController.", this);
            else
                _skyController.Configure(skyProfile, targetCamera, starSkybox);
            ApplyStaticAtmosphere();
        }

        private void Update()
        {
            if (profile == null || targetCamera == null) return;
            _elapsed += Time.deltaTime * profile.TimeScale;
            Snapshot = CelestialEphemerisSolver.Evaluate(
                _elapsed,
                profile.DaySeconds,
                profile.VisualYearSeconds,
                profile.MoonOrbitSeconds,
                profile.AxialTiltDegrees,
                profile.StartTime01);
            Vector3 sunDirection = ToVector3(Snapshot.SunDirection);
            Vector3 moonDirection = ToVector3(Snapshot.MoonDirection);
            if (_qaDawnShowcase)
                sunDirection = (targetCamera.transform.forward + targetCamera.transform.up * 0.18f -
                                targetCamera.transform.right * 0.38f).normalized;
            if (_qaNightShowcase)
                moonDirection = (targetCamera.transform.forward + targetCamera.transform.up * 0.28f -
                                 targetCamera.transform.right * 0.22f).normalized;
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
                Vector3 direction = _qaNightShowcase
                    ? (targetCamera.transform.forward + targetCamera.transform.up * 0.5f +
                       targetCamera.transform.right * 0.56f).normalized
                    : new Vector3(Mathf.Cos(angle), 0.24f, Mathf.Sin(angle)).normalized;
                float planetDistance = distance * 0.94f;
                distantPlanet.position = center + direction * planetDistance;
                distantPlanet.localScale = Vector3.one * (planetDistance * Mathf.Tan(profile.DistantPlanetAngularSize * Mathf.Deg2Rad));
            }
            if (sunLight != null)
            {
                sunLight.transform.rotation = Quaternion.LookRotation(-sunDirection, Vector3.up);
                float daylight = 1f - Snapshot.Night01;
                float horizon = Mathf.Clamp01(1f - Mathf.Abs(sunDirection.y) * 5f);
                sunLight.color = Color.Lerp(profile.DayColor, profile.DuskColor, horizon);
                sunLight.intensity = Mathf.Lerp(profile.MoonlightIntensity, profile.DaylightIntensity, daylight);
            }
            RenderSettings.ambientLight = Color.Lerp(profile.NightAmbient, profile.DayColor * 0.34f, 1f - Snapshot.Night01);
            _skyController?.Apply(Snapshot, sunDirection);
            Shader.SetGlobalVector("_ElementalSunDirection", new Vector4(sunDirection.x, sunDirection.y, sunDirection.z, 0f));
            float radius = ResolvePlanetRadius();
            Shader.SetGlobalVector("_ElementalPlanetCenterRadius", new Vector4(
                planet.position.x, planet.position.y, planet.position.z, radius));
            if (atmosphereProfile != null)
            {
                Shader.SetGlobalVector("_ElementalAtmosphereParams", new Vector4(
                    atmosphereProfile.OuterRadiusMultiplier,
                    atmosphereProfile.RayleighStrength,
                    atmosphereProfile.MieStrength,
                    atmosphereProfile.HorizonStrength));
                Shader.SetGlobalColor("_ElementalRayleighColor", atmosphereProfile.RayleighColor);
                Shader.SetGlobalColor("_ElementalMieColor", atmosphereProfile.MieColor);
            }
            UpdateAtmosphereProperties();
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
            _atmosphereProperties.SetColor("_MieColor", atmosphereProfile.MieColor);
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
