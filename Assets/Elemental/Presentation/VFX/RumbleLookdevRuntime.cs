using System;
using System.Collections;
using System.Collections.Generic;
using Elemental.Presentation.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
    [RequireComponent(typeof(Camera))]
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

        private Camera _camera;
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
            _camera = GetComponent<Camera>();
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
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetFocusState(RumbleLookdevFocusState.Explore);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetFocusState(RumbleLookdevFocusState.Near);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetFocusState(RumbleLookdevFocusState.Mid);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetFocusState(RumbleLookdevFocusState.Far);
            if (Input.GetKeyDown(KeyCode.F1)) SetLightState(RumbleLookdevLightState.Day);
            if (Input.GetKeyDown(KeyCode.F2)) SetLightState(RumbleLookdevLightState.Sunset);
            if (Input.GetKeyDown(KeyCode.F3)) SetLightState(RumbleLookdevLightState.Night);
            if (Input.GetKeyDown(KeyCode.Tab)) CycleSeamDebug();
            ChargeActive = Input.GetKey(KeyCode.C);

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
            keyLight.shadowStrength = 0.82f;
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

        private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
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

        private void HandleEndCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
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

    [DisallowMultipleComponent]
    public sealed class RumbleRockVariation : MonoBehaviour
    {
        [SerializeField] private Color baseColor = new Color(0.50f, 0.34f, 0.23f, 1f);
        [SerializeField] private Color shadowColor = new Color(0.20f, 0.15f, 0.13f, 1f);
        [SerializeField] private Color edgeColor = new Color(0.64f, 0.47f, 0.34f, 1f);
        [SerializeField] private float macroScale = 3.2f;
        [SerializeField] private float macroStrength = 0.10f;
        [SerializeField] private float textureScale = 0.24f;
        [SerializeField] private bool usePlanetFrame;
        [SerializeField] private Vector3 planetCenter;

        private Renderer[] _renderers;
        private MaterialPropertyBlock _properties;

        public void Configure(
            Color configuredBase,
            Color configuredShadow,
            Color configuredEdge,
            float configuredMacroScale,
            float configuredMacroStrength,
            float configuredTextureScale,
            bool configuredPlanetFrame,
            Vector3 configuredPlanetCenter)
        {
            baseColor = configuredBase;
            shadowColor = configuredShadow;
            edgeColor = configuredEdge;
            macroScale = configuredMacroScale;
            macroStrength = configuredMacroStrength;
            textureScale = configuredTextureScale;
            usePlanetFrame = configuredPlanetFrame;
            planetCenter = configuredPlanetCenter;
            Apply();
        }

        private void OnEnable() => Apply();

        private void Apply()
        {
            _renderers ??= GetComponentsInChildren<Renderer>(true);
            _properties ??= new MaterialPropertyBlock();
            for (int index = 0; index < _renderers.Length; index++)
            {
                Renderer renderer = _renderers[index];
                if (renderer == null) continue;
                renderer.GetPropertyBlock(_properties);
                _properties.SetColor("_BaseColor", baseColor);
                _properties.SetColor("_ShadowColor", shadowColor);
                _properties.SetColor("_EdgeColor", edgeColor);
                _properties.SetFloat("_MacroScale", macroScale);
                _properties.SetFloat("_MacroStrength", macroStrength);
                _properties.SetFloat("_TextureScale", textureScale);
                _properties.SetFloat("_UsePlanetFrame", usePlanetFrame ? 1f : 0f);
                _properties.SetVector("_PlanetCenter", planetCenter);
                renderer.SetPropertyBlock(_properties);
            }
        }
    }

    /// <summary>
    /// Visible-debris lifecycle: physical settle, dust pause, gradual sink and
    /// dither fade. Pool/destruction happens only after the renderer is effectively
    /// invisible, never as an abrupt mid-air SetActive(false).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RumbleDebrisLifecycle : MonoBehaviour
    {
        [SerializeField] private float minimumPhysicalSeconds = 1.4f;
        [SerializeField] private float sleepConfirmSeconds = 0.75f;
        [SerializeField] private float sinkSeconds = 0.9f;
        [SerializeField] private float sinkDistance = 0.22f;
        [SerializeField] private float maximumLifetime = 8f;

        private Rigidbody _body;
        private Renderer[] _renderers;
        private readonly List<MaterialPropertyBlock> _blocks = new List<MaterialPropertyBlock>(4);
        private float _age;
        private float _sleepTime;
        private float _sinkTime;
        private bool _sinking;
        private Vector3 _sinkStart;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < _renderers.Length; index++)
                _blocks.Add(new MaterialPropertyBlock());
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (!_sinking)
            {
                bool sleeping = _body == null || _body.IsSleeping() ||
                                (_body.linearVelocity.sqrMagnitude < 0.012f &&
                                 _body.angularVelocity.sqrMagnitude < 0.04f);
                _sleepTime = sleeping ? _sleepTime + Time.deltaTime : 0f;
                if ((_age >= minimumPhysicalSeconds && _sleepTime >= sleepConfirmSeconds) ||
                    _age >= maximumLifetime)
                    BeginSink();
                return;
            }

            _sinkTime += Time.deltaTime;
            float amount = Mathf.Clamp01(_sinkTime / Mathf.Max(0.05f, sinkSeconds));
            float eased = amount * amount * (3f - 2f * amount);
            transform.position = _sinkStart - Vector3.up * sinkDistance * eased;
            float visible = 1f - amount;
            for (int index = 0; index < _renderers.Length; index++)
            {
                Renderer renderer = _renderers[index];
                if (renderer == null) continue;
                MaterialPropertyBlock block = _blocks[index];
                renderer.GetPropertyBlock(block);
                block.SetFloat("_Fade", visible);
                renderer.SetPropertyBlock(block);
            }
            if (amount >= 1f) Destroy(gameObject);
        }

        private void BeginSink()
        {
            if (_sinking) return;
            _sinking = true;
            _sinkStart = transform.position;
            if (_body != null)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
                _body.isKinematic = true;
                _body.detectCollisions = false;
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class RumbleEarthVfxDemo : MonoBehaviour
    {
        [SerializeField] private Transform[] wallStones = Array.Empty<Transform>();
        [SerializeField] private ParticleSystem pressureDust;
        [SerializeField] private ParticleSystem groundDust;
        [SerializeField] private ParticleSystem gravel;
        [SerializeField] private Transform impactPoint;
        [SerializeField] private Mesh[] debrisMeshes = Array.Empty<Mesh>();
        [SerializeField] private Material debrisMaterial;
        [SerializeField] private RumbleLensDirector lensDirector;
        [SerializeField] private float wallTravel = 3.2f;

        private Vector3[] _wallTargets = Array.Empty<Vector3>();
        private Coroutine _wallRoutine;
        private bool _wallRaised;

        public void Configure(
            Transform[] configuredWall,
            ParticleSystem configuredPressureDust,
            ParticleSystem configuredGroundDust,
            ParticleSystem configuredGravel,
            Transform configuredImpactPoint,
            Mesh[] configuredDebrisMeshes,
            Material configuredDebrisMaterial,
            RumbleLensDirector configuredLens)
        {
            wallStones = configuredWall ?? Array.Empty<Transform>();
            pressureDust = configuredPressureDust;
            groundDust = configuredGroundDust;
            gravel = configuredGravel;
            impactPoint = configuredImpactPoint;
            debrisMeshes = configuredDebrisMeshes ?? Array.Empty<Mesh>();
            debrisMaterial = configuredDebrisMaterial;
            lensDirector = configuredLens;
            CacheWallTargets(true);
        }

        private void Awake() => CacheWallTargets(true);

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) RaiseWall();
            if (Input.GetKeyDown(KeyCode.H)) HeavyImpact();
            if (Input.GetKeyDown(KeyCode.R)) ResetWall();
        }

        public void RaiseWall()
        {
            if (_wallRaised || wallStones.Length == 0) return;
            if (_wallRoutine != null) StopCoroutine(_wallRoutine);
            _wallRoutine = StartCoroutine(RaiseWallRoutine());
        }

        public void ResetWall()
        {
            if (_wallRoutine != null) StopCoroutine(_wallRoutine);
            _wallRoutine = null;
            _wallRaised = false;
            for (int index = 0; index < wallStones.Length; index++)
            {
                Transform stone = wallStones[index];
                if (stone != null) stone.localPosition = _wallTargets[index] - Vector3.up * wallTravel;
            }
        }

        public void HeavyImpact()
        {
            Vector3 point = impactPoint != null ? impactPoint.position : transform.position;
            Quaternion rotation = Quaternion.identity;
            EmitAt(pressureDust, point + Vector3.up * 0.08f, rotation, 68);
            EmitAt(groundDust, point + Vector3.up * 0.03f, rotation, 42);
            EmitAt(gravel, point + Vector3.up * 0.10f, rotation, 28);
            SpawnPhysicalDebris(point);
            lensDirector?.AddImpulse(0.82f);
        }

        private IEnumerator RaiseWallRoutine()
        {
            const float duration = 1.15f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                for (int index = 0; index < wallStones.Length; index++)
                {
                    Transform stone = wallStones[index];
                    if (stone == null) continue;
                    float delay = index * 0.055f;
                    float local = Mathf.Clamp01((elapsed - delay) / Mathf.Max(0.1f, duration - delay));
                    float eased = 1f - Mathf.Pow(1f - local, 3f);
                    stone.localPosition = Vector3.LerpUnclamped(
                        _wallTargets[index] - Vector3.up * wallTravel,
                        _wallTargets[index],
                        eased);
                }
                if (groundDust != null && UnityEngine.Random.value < Time.deltaTime * 18f)
                {
                    int index = UnityEngine.Random.Range(0, wallStones.Length);
                    Transform stone = wallStones[index];
                    if (stone != null) EmitAt(groundDust, stone.position, Quaternion.identity, 3);
                }
                yield return null;
            }
            for (int index = 0; index < wallStones.Length; index++)
            {
                if (wallStones[index] != null) wallStones[index].localPosition = _wallTargets[index];
            }
            Vector3 center = AverageWallPosition();
            EmitAt(pressureDust, center + Vector3.up * 0.08f, Quaternion.identity, 45);
            EmitAt(groundDust, center, Quaternion.identity, 34);
            EmitAt(gravel, center + Vector3.up * 0.08f, Quaternion.identity, 18);
            lensDirector?.AddImpulse(0.46f);
            _wallRaised = true;
            _wallRoutine = null;
        }

        private void CacheWallTargets(bool lowerWall)
        {
            if (wallStones == null) wallStones = Array.Empty<Transform>();
            _wallTargets = new Vector3[wallStones.Length];
            for (int index = 0; index < wallStones.Length; index++)
            {
                Transform stone = wallStones[index];
                if (stone == null) continue;
                _wallTargets[index] = stone.localPosition;
                if (lowerWall) stone.localPosition -= Vector3.up * wallTravel;
            }
            _wallRaised = !lowerWall;
        }

        private void SpawnPhysicalDebris(Vector3 origin)
        {
            if (debrisMeshes == null || debrisMeshes.Length == 0 || debrisMaterial == null) return;
            const int count = 12;
            for (int index = 0; index < count; index++)
            {
                Mesh mesh = debrisMeshes[index % debrisMeshes.Length];
                if (mesh == null) continue;
                var debris = new GameObject($"V5 Impact Debris {index:00}");
                debris.transform.position = origin + Vector3.up * 0.16f;
                debris.transform.rotation = UnityEngine.Random.rotation;
                float scale = UnityEngine.Random.Range(0.10f, 0.28f);
                debris.transform.localScale = Vector3.one * scale;
                MeshFilter filter = debris.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                MeshRenderer renderer = debris.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = debrisMaterial;
                MeshCollider collider = debris.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                collider.convex = true;
                Rigidbody body = debris.AddComponent<Rigidbody>();
                body.mass = Mathf.Lerp(0.35f, 2.1f, scale / 0.28f);
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                Vector3 radial = UnityEngine.Random.onUnitSphere;
                radial.y = Mathf.Abs(radial.y) * 0.8f + 0.25f;
                body.linearVelocity = radial.normalized * UnityEngine.Random.Range(2.2f, 6.5f);
                body.angularVelocity = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(4f, 11f);
                debris.AddComponent<RumbleDebrisLifecycle>();
            }
        }

        private static void EmitAt(
            ParticleSystem system,
            Vector3 position,
            Quaternion rotation,
            int count)
        {
            if (system == null || count <= 0) return;
            system.transform.SetPositionAndRotation(position, rotation);
            system.Emit(count);
        }

        private Vector3 AverageWallPosition()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int index = 0; index < wallStones.Length; index++)
            {
                if (wallStones[index] == null) continue;
                sum += wallStones[index].position;
                count++;
            }
            return count > 0 ? sum / count : transform.position;
        }
    }
}
