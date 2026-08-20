using Elemental.Simulation.Time;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    [DisallowMultipleComponent]
    public sealed class EarthSkyController : MonoBehaviour
    {
        private static readonly ProfilerMarker UpdateMarker =
            new ProfilerMarker("Elemental.Earth.Sky.Update");

        [SerializeField] private EarthSkyProfile profile;
        [SerializeField] private UnityEngine.Camera targetCamera;
        [SerializeField] private Material sourceSkybox;

        private Material _runtimeSkybox;

        public Material RuntimeSkybox => _runtimeSkybox != null ? _runtimeSkybox : sourceSkybox;
        public float LastStarVisibility { get; private set; }
        public Color LastZenithColor { get; private set; }
        public Color LastHorizonColor { get; private set; }

        public void Configure(EarthSkyProfile configuredProfile, UnityEngine.Camera camera, Material skybox)
        {
            profile = configuredProfile;
            targetCamera = camera;
            sourceSkybox = skybox;
            EnsureRuntimeMaterial();
        }

        public void Apply(CelestialSnapshot snapshot, Vector3 sunDirection)
        {
            using var marker = UpdateMarker.Auto();
            EnsureRuntimeMaterial();
            if (_runtimeSkybox == null) return;

            float daylight = 1f - Mathf.Clamp01(snapshot.Night01);
            float horizon = Mathf.Clamp01(1f - Mathf.Abs(sunDirection.y) * 4.2f);
            Color dayZenith = profile != null ? profile.DayZenith : new Color(0.075f, 0.31f, 0.72f, 1f);
            Color dayHorizon = profile != null ? profile.DayHorizon : new Color(0.56f, 0.79f, 0.98f, 1f);
            Color duskZenith = profile != null ? profile.DuskZenith : new Color(0.12f, 0.16f, 0.38f, 1f);
            Color duskHorizon = profile != null ? profile.DuskHorizon : new Color(1f, 0.42f, 0.19f, 1f);
            Color nightZenith = profile != null ? profile.NightZenith : new Color(0.0025f, 0.006f, 0.026f, 1f);
            Color nightHorizon = profile != null ? profile.NightHorizon : new Color(0.015f, 0.03f, 0.075f, 1f);

            Color litZenith = Color.Lerp(dayZenith, duskZenith, horizon);
            Color litHorizon = Color.Lerp(dayHorizon, duskHorizon, horizon);
            LastZenithColor = Color.Lerp(nightZenith, litZenith, daylight);
            LastHorizonColor = Color.Lerp(nightHorizon, litHorizon, daylight);
            LastStarVisibility = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.32f, 0.82f, snapshot.Night01));

            _runtimeSkybox.SetColor("_ZenithColor", LastZenithColor);
            _runtimeSkybox.SetColor("_HorizonColor", LastHorizonColor);
            _runtimeSkybox.SetFloat("_StarVisibility", LastStarVisibility);
            _runtimeSkybox.SetFloat("_Exposure", profile != null ? profile.StarExposure : 1.15f);
            _runtimeSkybox.SetFloat("_Rotation", snapshot.Orbit01 * 360f);
            _runtimeSkybox.SetVector("_SunDirection", sunDirection.normalized);
            _runtimeSkybox.SetColor("_SunColor", profile != null ? profile.SunColor : new Color(1f, 0.88f, 0.62f, 1f));
            _runtimeSkybox.SetFloat("_SunDiscDegrees", profile != null ? profile.SunDiscDegrees : 0.44f);
            _runtimeSkybox.SetFloat("_SunGlow", profile != null ? profile.SunGlow : 0.72f);
        }

        private void Awake() => EnsureRuntimeMaterial();

        private void EnsureRuntimeMaterial()
        {
            if (targetCamera == null) targetCamera = UnityEngine.Camera.main;
            if (_runtimeSkybox == null && sourceSkybox != null)
                _runtimeSkybox = new Material(sourceSkybox) { name = sourceSkybox.name + " (Earth Sky Runtime)" };
            if (_runtimeSkybox != null) RenderSettings.skybox = _runtimeSkybox;
            if (targetCamera != null) targetCamera.clearFlags = CameraClearFlags.Skybox;
        }

        private void OnDestroy()
        {
            if (_runtimeSkybox == null) return;
            if (RenderSettings.skybox == _runtimeSkybox) RenderSettings.skybox = sourceSkybox;
            Destroy(_runtimeSkybox);
            _runtimeSkybox = null;
        }
    }
}
