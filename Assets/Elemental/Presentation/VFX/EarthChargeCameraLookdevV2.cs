using Elemental.Input.Gestures;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Elemental.Presentation.VFX
{
    internal static class EarthChargeCameraLookdevV2Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Object.FindFirstObjectByType<EarthChargeCameraLookdevV2Installer>(
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
        private float _nextScanAt;

        private void Update()
        {
            if (Time.unscaledTime < _nextScanAt) return;
            _nextScanAt = Time.unscaledTime + 1f;
            Camera[] cameras = Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < cameras.Length; index++)
            {
                Camera camera = cameras[index];
                if (camera == null || camera.cameraType != CameraType.Game) continue;
                EarthChargeCameraLookdev legacy =
                    camera.GetComponent<EarthChargeCameraLookdev>();
                if (legacy != null) legacy.enabled = false;
                if (camera.GetComponent<EarthChargeCameraLookdevV2>() == null)
                    camera.gameObject.AddComponent<EarthChargeCameraLookdevV2>();
            }
        }
    }

    /// <summary>
    /// Cinemachine-aware and SRP-safe charge presentation. The component tracks the
    /// externally authored lens every frame, adds only a reversible charge offset,
    /// and applies micro shake between begin/endCameraRendering so no transform drift
    /// can leak into gameplay or the next frame.
    /// </summary>
    [DefaultExecutionOrder(10100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class EarthChargeCameraLookdevV2 : MonoBehaviour
    {
        private Camera _camera;
        private MagicInputController _input;
        private Volume _volume;
        private DepthOfField _depthOfField;
        private Bloom _bloom;
        private Vignette _vignette;
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

        public float Charge01 => _charge;
        public float BaseFieldOfView => _baseFov;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
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
        }

        private void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
            RestoreRenderPose();
            RestoreBaseLens();
            if (_ownsProfile && _volume != null && _volume.sharedProfile != null)
                Destroy(_volume.sharedProfile);
        }

        private void LateUpdate()
        {
            if (_camera == null) return;
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
            float offset = Mathf.Lerp(0f, 6.5f, EaseOut(_charge));
            _lastAppliedFov = Mathf.Clamp(_baseFov + offset, 25f, 115f);
            _camera.fieldOfView = _lastAppliedFov;
            _hasAppliedFov = true;

            UpdateDepthOfField();
            if (_bloom != null)
                _bloom.intensity.value = Mathf.Lerp(0.18f, 0.44f, _charge);
            if (_vignette != null)
                _vignette.intensity.value = Mathf.Lerp(0.13f, 0.205f, _charge);
        }

        private void HandleBeginCameraRendering(
            ScriptableRenderContext context,
            Camera renderingCamera)
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
            Camera renderingCamera)
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
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            _input = inputs.Length > 0 ? inputs[0] : null;
        }

        private void ResolveOrBuildVolume()
        {
            Transform existing = transform.Find("Earth Runtime Lookdev Volume");
            _volume = existing != null ? existing.GetComponent<Volume>() : null;
            if (_volume == null)
            {
                var host = new GameObject("Earth Runtime Lookdev Volume V2");
                host.transform.SetParent(transform, false);
                _volume = host.AddComponent<Volume>();
                _volume.isGlobal = true;
                _volume.priority = 910f;
                _volume.weight = 1f;
                _volume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                _volume.sharedProfile.name = "Earth Runtime Lookdev V2";
                _ownsProfile = true;
            }

            VolumeProfile profile = _volume.sharedProfile;
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "Earth Runtime Lookdev V2";
                _volume.sharedProfile = profile;
                _ownsProfile = true;
            }

            if (!profile.TryGet(out _depthOfField))
                _depthOfField = profile.Add<DepthOfField>(true);
            _depthOfField.mode.Override(DepthOfFieldMode.Bokeh);
            _depthOfField.focusDistance.Override(8f);
            _depthOfField.focalLength.Override(45f);
            _depthOfField.aperture.Override(8f);
            _depthOfField.bladeCount.Override(7);

            if (!profile.TryGet(out _bloom))
                _bloom = profile.Add<Bloom>(true);
            _bloom.threshold.Override(1.08f);
            _bloom.intensity.Override(0.18f);
            _bloom.scatter.Override(0.56f);

            if (!profile.TryGet(out _vignette))
                _vignette = profile.Add<Vignette>(true);
            _vignette.intensity.Override(0.13f);
            _vignette.smoothness.Override(0.52f);
        }

        private void UpdateDepthOfField()
        {
            if (_depthOfField == null || _camera == null) return;
            float targetDistance = 8f;
            if (_input != null && _charge > 0.02f)
            {
                Vector3 target = _input.BendTargetPosition;
                if (float.IsFinite(target.x) &&
                    float.IsFinite(target.y) &&
                    float.IsFinite(target.z))
                    targetDistance = Vector3.Distance(
                        _camera.transform.position,
                        target);
            }
            _depthOfField.focusDistance.value = Mathf.Clamp(
                targetDistance,
                1.4f,
                42f);
            _depthOfField.aperture.value = Mathf.Lerp(10f, 5.6f, _charge);
            _depthOfField.focalLength.value = Mathf.Lerp(38f, 52f, _charge);
        }

        private static float EaseOut(float value)
        {
            value = Mathf.Clamp01(value);
            return 1f - (1f - value) * (1f - value);
        }
    }
}
