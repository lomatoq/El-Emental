using System;
using System.Collections.Generic;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Elemental.Presentation.Rendering
{
    internal static class EarthWorldLookdevRescueBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EarthWorldLookdevRescueInstaller>(
                    FindObjectsInactive.Include) != null)
                return;
            var host = new GameObject("Earth World Lookdev Rescue Installer")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<EarthWorldLookdevRescueInstaller>();
        }
    }

    internal sealed class EarthWorldLookdevRescueInstaller : MonoBehaviour
    {
        private float _nextScanAt;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            Scan();
        }

        private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

        private void Update()
        {
            if (Time.unscaledTime < _nextScanAt) return;
            _nextScanAt = Time.unscaledTime + 1.5f;
            Scan();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => Scan();

        private static void Scan()
        {
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < cameras.Length; index++)
            {
                Camera camera = cameras[index];
                if (camera == null || camera.cameraType != CameraType.Game) continue;
                if (camera.GetComponent<EarthRenderPipelineLookdevGuard>() == null)
                    camera.gameObject.AddComponent<EarthRenderPipelineLookdevGuard>();
            }

            Scene scene = SceneManager.GetActiveScene();
            bool earthLab = scene.IsValid() &&
                            (scene.name.IndexOf("EarthPolishLab", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             scene.name.IndexOf("EarthCore", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!earthLab) return;
            VoxelPlanetBehaviour[] planets = UnityEngine.Object.FindObjectsByType<VoxelPlanetBehaviour>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int index = 0; index < planets.Length; index++)
            {
                VoxelPlanetBehaviour planet = planets[index];
                if (planet != null && planet.GetComponent<EarthDistantRockDressing>() == null)
                    planet.gameObject.AddComponent<EarthDistantRockDressing>();
            }
        }
    }

    /// <summary>
    /// Ensures the runtime camera actually evaluates the lookdev volume and uses a
    /// stable, high-quality URP baseline. It does not replace Cinemachine or change
    /// gameplay camera ownership.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class EarthRenderPipelineLookdevGuard : MonoBehaviour
    {
        private Camera _camera;
        private float _nextLightAuditAt;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            ApplyCameraQuality();
            ApplyGlobalQuality();
            AuditDirectionalLights();
        }

        private void OnEnable()
        {
            ApplyCameraQuality();
            ApplyGlobalQuality();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextLightAuditAt) return;
            _nextLightAuditAt = Time.unscaledTime + 2f;
            AuditDirectionalLights();
        }

        private void ApplyCameraQuality()
        {
            if (_camera == null) return;
            UniversalAdditionalCameraData data = _camera.GetUniversalAdditionalCameraData();
            if (data == null) return;
            data.renderPostProcessing = true;
            data.renderShadows = true;
            data.stopNaN = true;
            data.dithering = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
        }

        private static void ApplyGlobalQuality()
        {
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowDistance = Mathf.Max(QualitySettings.shadowDistance, 90f);
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.softParticles = true;
            QualitySettings.realtimeReflectionProbes = true;
        }

        private static void AuditDirectionalLights()
        {
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Light key = null;
            for (int index = 0; index < lights.Length; index++)
            {
                Light light = lights[index];
                if (light == null || light.type != LightType.Directional || !light.enabled) continue;
                if (key == null || light.intensity > key.intensity) key = light;
            }
            if (key == null) return;

            key.shadows = LightShadows.Soft;
            key.shadowStrength = Mathf.Clamp(key.shadowStrength, 0.76f, 0.92f);
            key.shadowBias = Mathf.Clamp(key.shadowBias, 0.018f, 0.065f);
            key.shadowNormalBias = Mathf.Clamp(key.shadowNormalBias, 0.16f, 0.34f);
            key.shadowNearPlane = Mathf.Clamp(key.shadowNearPlane, 0.1f, 0.5f);

            // A second directional light reads as a second sun. Keep secondary
            // directionals only as very weak, non-shadowing fill when authored.
            for (int index = 0; index < lights.Length; index++)
            {
                Light light = lights[index];
                if (light == null || light == key || light.type != LightType.Directional) continue;
                light.shadows = LightShadows.None;
                light.intensity = Mathf.Min(light.intensity, key.intensity * 0.075f);
            }
        }
    }

    /// <summary>
    /// Deterministic, visual-only geological dressing for the Earth QA court. It
    /// replaces the empty debug silhouette with large rock groups, fins and strata
    /// without adding hidden collision or affecting the bending simulation.
    /// </summary>
    [DefaultExecutionOrder(1200)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VoxelPlanetBehaviour))]
    public sealed class EarthDistantRockDressing : MonoBehaviour
    {
        private const int FormationCount = 26;
        private readonly List<Mesh> _ownedMeshes = new List<Mesh>(FormationCount);
        private VoxelPlanetBehaviour _planet;
        private Transform _root;
        private Material _material;
        private bool _built;

        private void Awake()
        {
            _planet = GetComponent<VoxelPlanetBehaviour>();
        }

        private void Start() => BuildIfNeeded();

        private void OnDestroy()
        {
            for (int index = 0; index < _ownedMeshes.Count; index++)
            {
                Mesh mesh = _ownedMeshes[index];
                if (mesh != null) Destroy(mesh);
            }
        }

        private void BuildIfNeeded()
        {
            if (_built || _planet == null) return;
            _built = true;
            Transform existing = transform.Find("Earth Visual Dressing");
            if (existing != null)
            {
                _root = existing;
                return;
            }

            var rootObject = new GameObject("Earth Visual Dressing");
            rootObject.transform.SetParent(transform, false);
            _root = rootObject.transform;
            _material = ResolveEarthMaterial();

            PlanetMotor motor = UnityEngine.Object.FindFirstObjectByType<PlanetMotor>(
                FindObjectsInactive.Exclude);
            Vector3 center = transform.position;
            Vector3 baseUp = motor != null
                ? (motor.transform.position - center).normalized
                : transform.up;
            if (baseUp.sqrMagnitude < 0.5f) baseUp = Vector3.up;
            Vector3 tangent = Vector3.Cross(
                baseUp,
                Mathf.Abs(Vector3.Dot(baseUp, Vector3.forward)) < 0.86f
                    ? Vector3.forward
                    : Vector3.right).normalized;
            Vector3 bitangent = Vector3.Cross(baseUp, tangent).normalized;
            uint seed = 0xEA47D51u;

            for (int index = 0; index < FormationCount; index++)
            {
                float angle = index * 2.39996323f + Hash01(seed + (uint)index * 17u) * 0.58f;
                float arc = Mathf.Lerp(0.34f, 1.04f, Hash01(seed + (uint)index * 31u));
                Vector3 radialTangent = tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle);
                Vector3 direction = (baseUp * Mathf.Cos(arc) + radialTangent * Mathf.Sin(arc)).normalized;
                Vector3 surfacePoint = FindSurfacePoint(center, direction);
                if (!float.IsFinite(surfacePoint.x)) continue;

                var formation = new GameObject($"Geological Formation {index + 1:00}");
                formation.transform.SetParent(_root, false);
                formation.transform.position = surfacePoint;
                Vector3 localForward = Vector3.ProjectOnPlane(radialTangent, direction).normalized;
                if (localForward.sqrMagnitude < 0.1f) localForward = tangent;
                formation.transform.rotation = Quaternion.LookRotation(localForward, direction) *
                                               Quaternion.Euler(0f, Hash01(seed + (uint)index * 43u) * 360f, 0f);

                Mesh mesh = EarthWebWaveCellMeshFactory.Create(9800 + index * 97);
                mesh.name = $"Distant Formation {index + 1:00}";
                _ownedMeshes.Add(mesh);
                formation.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = formation.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;

                float silhouette = Hash01(seed + (uint)index * 59u);
                float width = Mathf.Lerp(0.75f, 2.25f, Hash01(seed + (uint)index * 71u));
                float height = silhouette < 0.24f
                    ? Mathf.Lerp(3.8f, 7.4f, Hash01(seed + (uint)index * 83u))
                    : silhouette > 0.78f
                        ? Mathf.Lerp(0.8f, 1.8f, Hash01(seed + (uint)index * 89u))
                        : Mathf.Lerp(1.7f, 4.2f, Hash01(seed + (uint)index * 101u));
                float depth = Mathf.Lerp(0.65f, 1.75f, Hash01(seed + (uint)index * 109u));
                formation.transform.localScale = new Vector3(width, height, depth);
            }
        }

        private Vector3 FindSurfacePoint(Vector3 center, Vector3 direction)
        {
            float radius = Mathf.Max(1f, _planet.Radius);
            Vector3 origin = center + direction * (radius + 12f);
            if (Physics.Raycast(
                    origin,
                    -direction,
                    out RaycastHit hit,
                    28f,
                    ~0,
                    QueryTriggerInteraction.Ignore))
                return hit.point + hit.normal * 0.025f;
            return center + direction * radius;
        }

        private static Material ResolveEarthMaterial()
        {
            Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < renderers.Length; index++)
            {
                Material material = renderers[index] != null ? renderers[index].sharedMaterial : null;
                if (material != null && material.shader != null &&
                    material.shader.name == "Elemental/SG Earth Master")
                    return material;
            }
            Shader shader = Shader.Find("Elemental/SG Earth Master") ??
                            Shader.Find("Universal Render Pipeline/Lit");
            return shader != null ? new Material(shader) { name = "Earth Dressing Runtime" } : null;
        }

        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }
    }
}
