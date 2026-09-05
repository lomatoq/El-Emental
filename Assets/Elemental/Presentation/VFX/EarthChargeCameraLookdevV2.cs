using Elemental.Input.Gestures;
using Elemental.Presentation.Camera;
using Elemental.Presentation.Rendering;
using Elemental.Simulation.Capabilities;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Rendering;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityCamera = global::UnityEngine.Camera;

namespace Elemental.Presentation.VFX
{
    internal static class EarthChargeCameraLookdevV2Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Object.FindAnyObjectByType<EarthChargeCameraLookdevV2Installer>(
                    FindObjectsInactive.Include) != null)
                return;
            var host = new GameObject("Earth Charge Camera Lookdev V2 Installer")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Object.DontDestroyOnLoad(host);
            host.AddComponent<EarthChargeCameraLookdevV2Installer>();
        }
    }

    internal sealed class EarthChargeCameraLookdevV2Installer : MonoBehaviour
    {
        private void Start()
        {
            UnityCamera[] cameras = Object.FindObjectsByType<UnityCamera>(FindObjectsInactive.Include);
            for (int index = 0; index < cameras.Length; index++)
            {
                UnityCamera camera = cameras[index];
                if (camera == null || camera.cameraType != CameraType.Game) continue;
                EarthCameraDirector director = camera.GetComponent<EarthCameraDirector>();
                if (director == null) continue;
                EarthChargeCameraLookdev legacy =
                    camera.GetComponent<EarthChargeCameraLookdev>();
                if (legacy != null) legacy.enabled = false;
                if (camera.GetComponent<EarthChargeCameraLookdevV2>() == null)
                    camera.gameObject.AddComponent<EarthChargeCameraLookdevV2>();
            }
        }
    }

    /// <summary>
    /// Cinemachine-aware and SRP-safe visual-clarity controller. It owns the bounded
    /// focus/post tiers and sunlit air motes while keeping every camera offset
    /// reversible between begin/endCameraRendering.
    /// </summary>
    [DefaultExecutionOrder(10100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityCamera))]
    public sealed class EarthChargeCameraLookdevV2 : MonoBehaviour
    {
        private static readonly ProfilerMarker ClarityMarker =
            new ProfilerMarker("Elemental.Presentation.Clarity");
        [SerializeField] private EarthCameraDirector cameraDirector;
        [SerializeField] private Volume volume;
        [SerializeField] private ParticleSystem lightDust;
        [SerializeField] private CapabilityProfileKind capability = CapabilityProfileKind.NativeHigh;
        private UnityCamera _camera;
        private MagicInputController _input;
        private Bloom _bloom;
        private Vignette _vignette;
        private DepthOfField _depthOfField;
        private EarthCinematicDepthOfFieldController _cinematicDepthOfField;
        private bool _ownsProfile;
        private float _charge;
        private float _chargeVelocity;
        private float _baseFov;
        private float _lastAppliedFov;
        private bool _hasAppliedFov;
        private float _nextResolveAt;
        private Vector3 _savedRenderPosition;
        private Quaternion _savedRenderRotation;
        private bool _renderPoseSaved;
        private float _focusDistance = 8f;
        private float _focusVelocity;
        private bool _depthOfFieldActive;
        private int _appliedDustCapacity = -1;

        public float Charge01 => _charge;
        public float BaseFieldOfView => _baseFov;
        public float FocusDistance => _focusDistance;
        public EarthDepthOfFieldTier DepthOfFieldTier { get; private set; }
        public ParticleSystem LightMotes => lightDust;
        public CapabilityProfileKind Capability => capability;

        public void Configure(
            EarthCameraDirector configuredDirector,
            Volume configuredVolume,
            ParticleSystem configuredLightDust)
        {
            cameraDirector = configuredDirector;
            volume = configuredVolume;
            lightDust = configuredLightDust;
            capability = ResolveCapability();
            _cinematicDepthOfField = GetComponent<EarthCinematicDepthOfFieldController>();
            if (_cinematicDepthOfField == null)
                _cinematicDepthOfField = gameObject.AddComponent<EarthCinematicDepthOfFieldController>();
            if (volume != null)
            {
                volume.isGlobal = true;
                volume.priority = 910f;
                volume.weight = 1f;
            }
            if (isActiveAndEnabled) ResolveOrBuildVolume();
        }

        public void BindDirector(EarthCameraDirector configuredDirector)
        {
            cameraDirector = configuredDirector;
        }

        private void Awake()
        {
            _camera = GetComponent<UnityCamera>();
            _cinematicDepthOfField = GetComponent<EarthCinematicDepthOfFieldController>();
            if (_cinematicDepthOfField == null)
                _cinematicDepthOfField = gameObject.AddComponent<EarthCinematicDepthOfFieldController>();
            if (cameraDirector == null) cameraDirector = GetComponent<EarthCameraDirector>();
            capability = ResolveCapability();
            _baseFov = _camera != null ? _camera.fieldOfView : 60f;
            _lastAppliedFov = _baseFov;
            ResolveInput();
            ResolveOrBuildVolume();
            EarthChargeCameraLookdev legacy = GetComponent<EarthChargeCameraLookdev>();
            if (legacy != null) legacy.enabled = false;
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += HandleEndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
            RestoreRenderPose();
            RestoreBaseLens();
            _cinematicDepthOfField?.ApplyPolicy(false, _focusDistance, 5.6f, 50f);
            if (_depthOfField != null)
                _depthOfField.mode.value = DepthOfFieldMode.Off;
        }

        private void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
            RestoreRenderPose();
            RestoreBaseLens();
            if (_ownsProfile && volume != null && volume.sharedProfile != null)
                Destroy(volume.sharedProfile);
        }

        private void LateUpdate()
        {
            if (_camera == null) return;
            using var marker = ClarityMarker.Auto();
            if (_input == null && Time.unscaledTime >= _nextResolveAt)
            {
                _nextResolveAt = Time.unscaledTime + 1f;
                ResolveInput();
            }

            float requested = _input != null
                ? Mathf.Clamp01(Mathf.Max(
                    _input.BendCharge01,
                    _input.BendAmount01 * 0.72f))
                : 0f;
            _charge = Mathf.SmoothDamp(
                _charge,
                requested,
                ref _chargeVelocity,
                requested > _charge ? 0.075f : 0.16f,
                9f,
                Mathf.Max(0.0001f, Time.unscaledDeltaTime));

            // Cinemachine normally writes the lens before this LateUpdate. When the
            // observed value differs from our last output, it is a new authored base,
            // not accumulated charge offset.
            float observedFov = _camera.fieldOfView;
            if (!_hasAppliedFov || Mathf.Abs(observedFov - _lastAppliedFov) > 0.05f)
                _baseFov = Mathf.Clamp(observedFov, 25f, 110f);
            // The gameplay lens is fixed at 47 mm; charge must not pulse the zoom.
            float offset = 0f;
            _lastAppliedFov = Mathf.Clamp(_baseFov + offset, 25f, 115f);
            _camera.fieldOfView = _lastAppliedFov;
            _hasAppliedFov = true;

            Transform playerFocus = cameraDirector != null ? cameraDirector.Player : null;
            float desiredFocus;
            if (_cinematicDepthOfField != null &&
                _cinematicDepthOfField.HasRequiredSubjects)
            {
                desiredFocus = _cinematicDepthOfField.FocusDistance;
            }
            else if (playerFocus != null)
            {
                Vector3 focusPoint = playerFocus.position + playerFocus.up * 1.1f;
                desiredFocus = IsFinite(focusPoint)
                    ? Vector3.Dot(focusPoint - transform.position, transform.forward)
                    : 8f;
            }
            else
            {
                // Never interpret the director's default world-origin focus as
                // authored camera intent. Missing subjects make custom DOF fail
                // closed; this warm value only feeds non-bokeh clarity policy.
                desiredFocus = 8f;
            }
            _focusDistance = Mathf.SmoothDamp(
                _focusDistance,
                Mathf.Clamp(desiredFocus, 1.25f, 36f),
                ref _focusVelocity,
                0.06f,
                120f,
                Mathf.Max(0.0001f, Time.unscaledDeltaTime));
            EarthCameraState state = cameraDirector != null
                ? cameraDirector.State
                : EarthCameraState.Explore;
            float daylight = 1f - Mathf.Clamp01(Shader.GetGlobalFloat("_ElementalNight01"));
            var clarityInput = new EarthVisualClarityInput(
                state,
                capability,
                _charge,
                _focusDistance,
                daylight,
                _depthOfFieldActive);
            EarthVisualClarityOutput clarity = EarthVisualClaritySolver.Solve(in clarityInput);
            ApplyClarity(in clarity);
        }

        private void HandleBeginCameraRendering(
            ScriptableRenderContext context,
            UnityCamera renderingCamera)
        {
            if (renderingCamera != _camera || _charge <= 0.001f || _renderPoseSaved)
                return;
            _renderPoseSaved = true;
            _savedRenderPosition = transform.position;
            _savedRenderRotation = transform.rotation;

            float time = Time.unscaledTime * Mathf.Lerp(18f, 31f, _charge);
            float x = Mathf.PerlinNoise(time, 13.7f) * 2f - 1f;
            float y = Mathf.PerlinNoise(7.3f, time * 1.07f) * 2f - 1f;
            float z = Mathf.PerlinNoise(time * 0.83f, 27.1f) * 2f - 1f;
            float positionAmplitude = Mathf.Lerp(0f, 0.0065f, _charge * _charge);
            float rotationAmplitude = Mathf.Lerp(0f, 0.14f, _charge * _charge);
            transform.position += transform.right * x * positionAmplitude +
                                  transform.up * y * positionAmplitude;
            transform.rotation = Quaternion.Euler(
                                     y * rotationAmplitude,
                                     x * rotationAmplitude,
                                     z * rotationAmplitude * 0.45f) *
                                 transform.rotation;
        }

        private void HandleEndCameraRendering(
            ScriptableRenderContext context,
            UnityCamera renderingCamera)
        {
            if (renderingCamera == _camera) RestoreRenderPose();
        }

        private void RestoreRenderPose()
        {
            if (!_renderPoseSaved) return;
            transform.SetPositionAndRotation(
                _savedRenderPosition,
                _savedRenderRotation);
            _renderPoseSaved = false;
        }

        private void RestoreBaseLens()
        {
            if (_camera != null && _hasAppliedFov)
                _camera.fieldOfView = _baseFov;
            _hasAppliedFov = false;
        }

        private void ResolveInput()
        {
            MagicInputController[] inputs = Object.FindObjectsByType<MagicInputController>(
                FindObjectsInactive.Exclude);
            _input = inputs.Length > 0 ? inputs[0] : null;
        }

        private void ResolveOrBuildVolume()
        {
            if (volume == null)
            {
                Transform existing = transform.Find("Earth Runtime Lookdev Volume V2");
                volume = existing != null ? existing.GetComponent<Volume>() : null;
                if (volume == null)
                {
                    var host = new GameObject("Earth Runtime Lookdev Volume V2");
                    host.transform.SetParent(transform, false);
                    volume = host.AddComponent<Volume>();
                    volume.isGlobal = true;
                    volume.priority = 910f;
                    volume.weight = 1f;
                    volume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                    volume.sharedProfile.name = "Earth Runtime Lookdev V2";
                    _ownsProfile = true;
                }
            }

            VolumeProfile profile = Application.isPlaying ? volume.profile : volume.sharedProfile;
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "Earth Runtime Lookdev V2";
                volume.sharedProfile = profile;
                _ownsProfile = true;
            }

            if (!profile.TryGet(out Tonemapping tonemapping))
            {
                tonemapping = profile.Add<Tonemapping>(true);
                tonemapping.mode.Override(TonemappingMode.ACES);
            }

            // Color grading is authored by M3 and must remain identical in Scene,
            // Game and standalone rendering. This runtime adapter only supplies the
            // canonical M3 fallback when a caller provides an incomplete profile;
            // it never retunes an existing ColorAdjustments component.
            if (!profile.TryGet(out ColorAdjustments color))
            {
                color = profile.Add<ColorAdjustments>(true);
                color.active = true;
                color.postExposure.Override(0f);
                color.contrast.Override(7f);
                color.saturation.Override(-8f);
                color.colorFilter.Override(Color.white);
            }

            if (!profile.TryGet(out WhiteBalance balance))
            {
                balance = profile.Add<WhiteBalance>(true);
                balance.active = true;
                balance.temperature.Override(2f);
                balance.tint.Override(-1f);
            }

            if (!profile.TryGet(out _depthOfField))
                _depthOfField = profile.Add<DepthOfField>(true);
            _depthOfField.active = true;
            _depthOfField.mode.Override(DepthOfFieldMode.Off);
            _depthOfField.focusDistance.Override(8f);
            _depthOfField.aperture.Override(5.6f);
            _depthOfField.focalLength.Override(50f);
            _depthOfField.gaussianStart.Override(8.55f);
            _depthOfField.gaussianEnd.Override(12.75f);
            _depthOfField.gaussianMaxRadius.Override(2f);
            _depthOfField.highQualitySampling.Override(false);
            _depthOfField.bladeCount.Override(7);
            _depthOfField.bladeCurvature.Override(0.82f);
            _depthOfField.bladeRotation.Override(18f);

            if (!profile.TryGet(out _bloom))
                _bloom = profile.Add<Bloom>(true);
            _bloom.threshold.Override(1.1f);
            _bloom.intensity.Override(0f);
            _bloom.scatter.Override(0.58f);

            if (!profile.TryGet(out _vignette))
                _vignette = profile.Add<Vignette>(true);
            _vignette.intensity.Override(0.10f);
            _vignette.smoothness.Override(0.52f);
        }

        private void ApplyClarity(in EarthVisualClarityOutput clarity)
        {
            DepthOfFieldTier = clarity.DepthOfFieldTier;
            _depthOfFieldActive = clarity.DepthOfFieldActive;
            bool customNativeHigh =
                capability == CapabilityProfileKind.NativeHigh &&
                clarity.DepthOfFieldTier == EarthDepthOfFieldTier.Bokeh;
            if (_cinematicDepthOfField == null)
                _cinematicDepthOfField = GetComponent<EarthCinematicDepthOfFieldController>();
            _cinematicDepthOfField?.ApplyPolicy(
                customNativeHigh,
                clarity.FocusDistance,
                clarity.Aperture,
                clarity.FocalLength);
            if (_depthOfField != null)
            {
                // One DOF writer per frame: NativeHigh Bokeh belongs to the custom
                // depth-aware RenderGraph feature. Stock URP is retained only as
                // the bounded NativeLow Gaussian fallback.
                _depthOfField.mode.value = clarity.DepthOfFieldTier switch
                {
                    EarthDepthOfFieldTier.Gaussian => DepthOfFieldMode.Gaussian,
                    _ => DepthOfFieldMode.Off
                };
                _depthOfField.focusDistance.value = clarity.FocusDistance;
                _depthOfField.aperture.value = clarity.Aperture;
                _depthOfField.focalLength.value = clarity.FocalLength;
                _depthOfField.gaussianStart.value = clarity.GaussianStart;
                _depthOfField.gaussianEnd.value = clarity.GaussianEnd;
                _depthOfField.gaussianMaxRadius.value = clarity.GaussianMaxRadius;
                _depthOfField.highQualitySampling.value =
                    clarity.DepthOfFieldTier == EarthDepthOfFieldTier.Gaussian;
            }
            if (_bloom != null) _bloom.intensity.value = clarity.BloomIntensity;
            if (_vignette != null) _vignette.intensity.value = clarity.VignetteIntensity;
            if (lightDust == null) return;
            if (_appliedDustCapacity != clarity.DustCapacity)
            {
                ParticleSystem.MainModule main = lightDust.main;
                main.maxParticles = Mathf.Max(1, clarity.DustCapacity);
                _appliedDustCapacity = clarity.DustCapacity;
            }
            ParticleSystem.EmissionModule emission = lightDust.emission;
            emission.enabled = clarity.DustCapacity > 0 && clarity.DustRate > 0.001f;
            emission.rateOverTime = clarity.DustRate;
        }

        private static CapabilityProfileKind ResolveCapability()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return CapabilityProfileKind.WebLab;
#else
            string[] qualityNames = QualitySettings.names;
            int highest = Mathf.Max(0, qualityNames.Length - 1);
            return QualitySettings.GetQualityLevel() >= highest
                ? CapabilityProfileKind.NativeHigh
                : CapabilityProfileKind.NativeLow;
#endif
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static float EaseOut(float value)
        {
            value = Mathf.Clamp01(value);
            return 1f - (1f - value) * (1f - value);
        }
    }
}
