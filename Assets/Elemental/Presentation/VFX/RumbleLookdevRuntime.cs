using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityCamera = global::UnityEngine.Camera;

namespace Elemental.Presentation.VFX
{
    public enum RumbleLookdevFocusState : byte
    {
        Explore = 0,
        Near = 1,
        Mid = 2,
        Far = 3
    }

    public enum RumbleLookdevLightState : byte
    {
        Day = 0,
        Sunset = 1,
        Night = 2
    }

    /// <summary>
    /// Explicit, inspectable lens and lighting owner for the Graphics V5 proof.
    /// It never creates hidden volumes, scans every camera or permanently offsets
    /// the camera transform. All render-only vibration is restored by SRP callbacks.
    /// </summary>
    [DefaultExecutionOrder(10050)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityCamera))]
    public sealed class RumbleLensDirector : MonoBehaviour
    {
        [SerializeField] private Volume volume;
        [SerializeField] private Transform nearFocus;
        [SerializeField] private Transform midFocus;
        [SerializeField] private Transform farFocus;
        [SerializeField] private Light keyLight;
        [SerializeField] private Material[] seamDebugMaterials = Array.Empty<Material>();
        [SerializeField] private RumbleLookdevFocusState focusState = RumbleLookdevFocusState.Explore;
        [SerializeField] private RumbleLookdevLightState lightState = RumbleLookdevLightState.Day;
        [SerializeField] private bool showOverlay = true;

        private UnityCamera _camera;
        private DepthOfField _depthOfField;
        private Bloom _bloom;
        private Vignette _vignette;
        private ColorAdjustments _colorAdjustments;
        private float _baseFieldOfView;
        private float _lensBlend;
        private float _lensVelocity;
        private float _focusDistance = 8f;
        private float _focusVelocity;
        private float _impulse;
        private float _impulseVelocity;
        private bool _renderPoseSaved;
        private Vector3 _savedPosition;
        private Quaternion _savedRotation;
        private int _debugMode;

        public RumbleLookdevFocusState FocusState => focusState;
        public RumbleLookdevLightState LightState => lightState;
        public float FocusDistance => _focusDistance;
        public float Aperture => _depthOfField != null ? _depthOfField.aperture.value : 0f;
        public float FocalLength => _depthOfField != null ? _depthOfField.focalLength.value : 0f;
        public bool ChargeActive { get; private set; }

        public void Configure(
            Volume configuredVolume,
            Transform configuredNear,
            Transform configuredMid,
            Transform configuredFar,
            Light configuredKey,
            Material[] configuredDebugMaterials)
        {
            volume = configuredVolume;
            nearFocus = configuredNear;
            midFocus = configuredMid;
            farFocus = configuredFar;
            keyLight = configuredKey;
            seamDebugMaterials = configuredDebugMaterials ?? Array.Empty<Material>();
            ResolveVolumeOverrides();
            ApplyLightState(lightState, true);
        }

        private void Awake()
        {
            _camera = GetComponent<UnityCamera>();
            _baseFieldOfView = _camera.fieldOfView;
            UniversalAdditionalCameraData cameraData = _camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            cameraData.dithering = true;
            cameraData.stopNaN = true;
            ResolveVolumeOverrides();
            ApplyLightState(lightState, true);
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
            if (_camera != null) _camera.fieldOfView = _baseFieldOfView;
        }

        private void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
            RestoreRenderPose();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) SetFocusState(RumbleLookdevFocusState.Explore);
                if (keyboard.digit2Key.wasPressedThisFrame) SetFocusState(RumbleLookdevFocusState.Near);
                if (keyboard.digit3Key.wasPressedThisFrame) SetFocusState(RumbleLookdevFocusState.Mid);
                if (keyboard.digit4Key.wasPressedThisFrame) SetFocusState(RumbleLookdevFocusState.Far);
                if (keyboard.f1Key.wasPressedThisFrame) SetLightState(RumbleLookdevLightState.Day);
                if (keyboard.f2Key.wasPressedThisFrame) SetLightState(RumbleLookdevLightState.Sunset);
                if (keyboard.f3Key.wasPressedThisFrame) SetLightState(RumbleLookdevLightState.Night);
                if (keyboard.tabKey.wasPressedThisFrame) CycleSeamDebug();
            }
            ChargeActive = keyboard != null && keyboard.cKey.isPressed;

            float targetLens = ChargeActive ? 1f : 0f;
            _lensBlend = Mathf.SmoothDamp(
                _lensBlend,
                targetLens,
                ref _lensVelocity,
                targetLens > _lensBlend ? 0.09f : 0.17f,
                12f,
                Mathf.Max(0.0001f, Time.unscaledDeltaTime));
            _impulse = Mathf.SmoothDamp(
                _impulse,
                0f,
                ref _impulseVelocity,
                0.22f,
                12f,
                Mathf.Max(0.0001f, Time.unscaledDeltaTime));
            UpdateLens();
        }

        public void SetFocusState(RumbleLookdevFocusState state) => focusState = state;

        public void SetLightState(RumbleLookdevLightState state)
        {
            lightState = state;
            ApplyLightState(state, false);
        }

        public void AddImpulse(float amount)
        {
            _impulse = Mathf.Clamp01(Mathf.Max(_impulse, amount));
            _impulseVelocity = 0f;
        }

        private void ResolveVolumeOverrides()
        {
            _depthOfField = null;
            _bloom = null;
            _vignette = null;
            _colorAdjustments = null;
            if (volume == null || volume.profile == null) return;
            volume.profile.TryGet(out _depthOfField);
            volume.profile.TryGet(out _bloom);
            volume.profile.TryGet(out _vignette);
            volume.profile.TryGet(out _colorAdjustments);
            if (_depthOfField != null)
            {
                _depthOfField.active = true;
                _depthOfField.mode.Override(DepthOfFieldMode.Bokeh);
            }
        }

        private void UpdateLens()
        {
            if (_camera == null) return;
            Transform target = focusState switch
            {
                RumbleLookdevFocusState.Near => nearFocus,
                RumbleLookdevFocusState.Mid => midFocus,
                RumbleLookdevFocusState.Far => farFocus,
                _ => null
            };
            float desiredDistance = target != null
                ? Vector3.Distance(_camera.transform.position, target.position)
                : 16f;
            if (ChargeActive && midFocus != null)
                desiredDistance = Vector3.Distance(_camera.transform.position, midFocus.position);
            _focusDistance = Mathf.SmoothDamp(
                _focusDistance,
                Mathf.Clamp(desiredDistance, 1.25f, 60f),
                ref _focusVelocity,
                focusState == RumbleLookdevFocusState.Explore ? 0.28f : 0.18f,
                100f,
                Mathf.Max(0.0001f, Time.unscaledDeltaTime));

            float authoredAperture = focusState switch
            {
                RumbleLookdevFocusState.Near => 2.8f,
                RumbleLookdevFocusState.Mid => 4.0f,
                RumbleLookdevFocusState.Far => 5.6f,
                _ => 16f
            };
            float authoredFocalLength = focusState switch
            {
                RumbleLookdevFocusState.Near => 58f,
                RumbleLookdevFocusState.Mid => 52f,
                RumbleLookdevFocusState.Far => 48f,
                _ => 35f
            };
            float aperture = Mathf.Lerp(authoredAperture, 3.2f, _lensBlend);
            float focalLength = Mathf.Lerp(authoredFocalLength, 58f, _lensBlend);
            if (_depthOfField != null)
            {
                _depthOfField.focusDistance.value = _focusDistance;
                _depthOfField.aperture.value = aperture;
                _depthOfField.focalLength.value = focalLength;
                _depthOfField.bladeCount.value = 7;
                _depthOfField.bladeCurvature.value = 0.82f;
                _depthOfField.bladeRotation.value = 18f;
            }
            if (_bloom != null)
                _bloom.intensity.value = Mathf.Lerp(0.07f, 0.24f, _lensBlend);
            if (_vignette != null)
                _vignette.intensity.value = Mathf.Lerp(0.075f, 0.145f, _lensBlend);
            _camera.fieldOfView = Mathf.Lerp(_baseFieldOfView, _baseFieldOfView + 5.5f, _lensBlend);
        }

        private void ApplyLightState(RumbleLookdevLightState state, bool immediate)
        {
            if (keyLight == null) return;
            Quaternion rotation;
            Color color;
            float intensity;
            Color sky;
            Color equator;
            Color ground;
            float exposure;
            switch (state)
            {
                case RumbleLookdevLightState.Sunset:
                    rotation = Quaternion.Euler(16f, -42f, 0f);
                    color = new Color(1f, 0.66f, 0.42f);
                    intensity = 1.05f;
                    sky = new Color(0.24f, 0.29f, 0.42f);
                    equator = new Color(0.38f, 0.23f, 0.18f);
                    ground = new Color(0.075f, 0.055f, 0.055f);
                    exposure = -0.18f;
                    break;
                case RumbleLookdevLightState.Night:
                    rotation = Quaternion.Euler(48f, 138f, 0f);
                    color = new Color(0.48f, 0.63f, 1f);
                    intensity = 0.34f;
                    sky = new Color(0.055f, 0.075f, 0.13f);
                    equator = new Color(0.028f, 0.038f, 0.072f);
                    ground = new Color(0.012f, 0.014f, 0.022f);
                    exposure = -0.55f;
                    break;
                default:
                    rotation = Quaternion.Euler(42f, -34f, 0f);
                    color = new Color(1f, 0.91f, 0.78f);
                    intensity = 1.28f;
                    sky = new Color(0.32f, 0.39f, 0.48f);
                    equator = new Color(0.20f, 0.18f, 0.17f);
                    ground = new Color(0.075f, 0.065f, 0.06f);
                    exposure = 0f;
                    break;
            }

            keyLight.type = LightType.Directional;
            keyLight.shadows = LightShadows.Soft;
            keyLight.shadowStrength = 0.76f;
            keyLight.shadowBias = 0.50f;
            keyLight.shadowNormalBias = 0.30f;
            keyLight.color = color;
            keyLight.intensity = intensity;
            keyLight.transform.rotation = rotation;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.reflectionIntensity = state == RumbleLookdevLightState.Night ? 0.42f : 0.72f;
            if (_colorAdjustments != null)
                _colorAdjustments.postExposure.value = exposure;
        }

        private void CycleSeamDebug()
        {
            _debugMode = (_debugMode + 1) % 5;
            for (int index = 0; index < seamDebugMaterials.Length; index++)
            {
                Material material = seamDebugMaterials[index];
                if (material != null && material.HasProperty("_DebugMode"))
                    material.SetFloat("_DebugMode", _debugMode);
            }
        }

        private void HandleBeginCameraRendering(ScriptableRenderContext context, UnityCamera renderingCamera)
        {
            if (renderingCamera != _camera || _renderPoseSaved) return;
            float strength = Mathf.Clamp01(_lensBlend * 0.38f + _impulse);
            if (strength <= 0.0001f) return;
            _renderPoseSaved = true;
            _savedPosition = transform.position;
            _savedRotation = transform.rotation;
            float time = Time.unscaledTime * Mathf.Lerp(18f, 31f, strength);
            float x = Mathf.PerlinNoise(time, 11.3f) * 2f - 1f;
            float y = Mathf.PerlinNoise(7.9f, time * 1.09f) * 2f - 1f;
            float z = Mathf.PerlinNoise(time * 0.87f, 29.1f) * 2f - 1f;
            float positionAmplitude = Mathf.Lerp(0f, 0.0075f, strength * strength);
            float rotationAmplitude = Mathf.Lerp(0f, 0.17f, strength * strength);
            transform.position += transform.right * x * positionAmplitude +
                                  transform.up * y * positionAmplitude;
            transform.rotation = Quaternion.Euler(
                                     y * rotationAmplitude,
                                     x * rotationAmplitude,
                                     z * rotationAmplitude * 0.4f) * transform.rotation;
        }

        private void HandleEndCameraRendering(ScriptableRenderContext context, UnityCamera renderingCamera)
        {
            if (renderingCamera == _camera) RestoreRenderPose();
        }

        private void RestoreRenderPose()
        {
            if (!_renderPoseSaved) return;
            transform.SetPositionAndRotation(_savedPosition, _savedRotation);
            _renderPoseSaved = false;
        }

        private void OnGUI()
        {
            if (!showOverlay) return;
            const int width = 430;
            GUILayout.BeginArea(new Rect(18, 18, width, 220), GUI.skin.box);
            GUILayout.Label("GRAPHICS V5 — RUMBLE LOOKDEV LAB");
            GUILayout.Label("1 Explore  2 Near DOF  3 Mid DOF  4 Far DOF  |  Hold C: charge lens");
            GUILayout.Label("F1 Day  F2 Sunset  F3 Night  |  Tab: seam debug");
            GUILayout.Label("Space: raise wall  H: heavy impact  R: reset wall");
            GUILayout.Space(4);
            GUILayout.Label($"Focus: {focusState}   Distance: {_focusDistance:0.00} m");
            GUILayout.Label($"Aperture: {Aperture:0.0}   Focal: {FocalLength:0} mm   FOV: {_camera.fieldOfView:0.0}°");
            GUILayout.Label($"Light: {lightState}   Charge: {(ChargeActive ? "ON" : "off")}   Seam debug: {_debugMode}");
            GUILayout.EndArea();
        }
    }

}
