using System;
using System.Collections.Generic;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Geometry;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityCamera = global::UnityEngine.Camera;
using UnityEngine.SceneManagement;

namespace Elemental.Presentation.Rendering
{
    internal static class EarthWorldLookdevRescueBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EarthWorldLookdevRescueInstaller>(
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
        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            Scan();
        }

        private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => Scan();

        private static void Scan()
        {
            UnityCamera[] cameras = UnityEngine.Object.FindObjectsByType<UnityCamera>(
                FindObjectsInactive.Include);
            for (int index = 0; index < cameras.Length; index++)
            {
                UnityCamera camera = cameras[index];
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
                FindObjectsInactive.Exclude);
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
    [RequireComponent(typeof(UnityCamera))]
    public sealed class EarthRenderPipelineLookdevGuard : MonoBehaviour
    {
        private UnityCamera _camera;
        private void Awake()
        {
            _camera = GetComponent<UnityCamera>();
            ApplyCameraQuality();
            ApplyGlobalQuality();
            AuditDirectionalLights();
        }

        private void OnEnable()
        {
            ApplyCameraQuality();
            ApplyGlobalQuality();
        }

        private void ApplyCameraQuality()
        {
            if (_camera == null) return;
            UniversalAdditionalCameraData data = _camera.GetUniversalAdditionalCameraData();
            if (data == null) return;
            data.renderPostProcessing = true;
            data.renderShadows = true;
            data.requiresDepthTexture = true;
            data.stopNaN = true;
            data.dithering = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
        }

        private static void ApplyGlobalQuality()
        {
            QualitySettings.shadows = UnityEngine.ShadowQuality.All;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowDistance = 48f;
            QualitySettings.shadowResolution = UnityEngine.ShadowResolution.High;
            QualitySettings.softParticles = true;
            QualitySettings.realtimeReflectionProbes = true;
        }

        private static void AuditDirectionalLights()
        {
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            Light key = null;
            for (int index = 0; index < lights.Length; index++)
            {
                Light light = lights[index];
                if (light == null || light.type != LightType.Directional || !light.enabled) continue;
                if (key == null || light.intensity > key.intensity) key = light;
            }
            if (key == null) return;

            key.shadows = LightShadows.Soft;
            key.shadowStrength = Mathf.Clamp(key.shadowStrength, 0.70f, 0.78f);
            key.shadowBias = Mathf.Clamp(key.shadowBias, 0.065f, 0.085f);
            key.shadowNormalBias = Mathf.Clamp(key.shadowNormalBias, 0.36f, 0.46f);
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

            PlanetMotor motor = UnityEngine.Object.FindAnyObjectByType<PlanetMotor>(
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
                if (!TryFindSurface(center, direction, out EarthSurfaceSample surface)) continue;

                var formation = new GameObject($"Geological Formation {index + 1:00}");
                formation.transform.SetParent(_root, false);
                Vector3 localForward = Vector3.ProjectOnPlane(radialTangent, direction).normalized;
                if (localForward.sqrMagnitude < 0.1f) localForward = tangent;
                Quaternion rotation = Quaternion.LookRotation(localForward, direction) *
                                      Quaternion.Euler(0f, Hash01(seed + (uint)index * 43u) * 360f, 0f);
                formation.transform.rotation = rotation;

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
                Vector3 scale = new Vector3(width, height, depth);
                formation.transform.localScale = scale;
                EarthSurfacePlacementResult placement = EarthSurfacePlacementSolver.Solve(
                    mesh,
                    ToVector3(surface.Point),
                    ToVector3(surface.Normal),
                    rotation,
                    scale,
                    0.035f,
                    surface.Handle);
                if (placement.IsValid) formation.transform.position = placement.RootPosition;
            }
        }

        private bool TryFindSurface(Vector3 center, Vector3 direction, out EarthSurfaceSample sample)
        {
            sample = default;
            float radius = Mathf.Max(1f, _planet.Radius);
            Vector3 origin = center + direction * (radius + 12f);
            EarthSurfaceQueryService surfaces = UnityEngine.Object.FindAnyObjectByType<EarthSurfaceQueryService>(
                FindObjectsInactive.Exclude);
            if (surfaces != null)
            {
                var query = new EarthSurfaceQuery(
                    new float3(origin.x, origin.y, origin.z),
                    new float3(-direction.x, -direction.y, -direction.z),
                    28f,
                    EarthSurfaceCapabilities.Support);
                if (surfaces.TrySample(in query, out sample)) return true;
            }

            // The canonical service can be unavailable during its first enable
            // frame. The fallback is the planet's mathematical surface, never a
            // broad physics ray that could accidentally land on another rock.
            Vector3 point = center + direction * radius;
            sample = new EarthSurfaceSample(
                new EarthSurfaceHandle(EarthSurfaceKind.Planet, 1u, 1u),
                new float3(point.x, point.y, point.z),
                new float3(direction.x, direction.y, direction.z),
                new float3(transform.right.x, transform.right.y, transform.right.z),
                default,
                12f,
                EarthSurfaceMaterial.PlanetStone,
                EarthSurfaceProvenance.VoxelPlanet,
                EarthSurfaceCapabilities.Support);
            return true;
        }

        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);

        private static Material ResolveEarthMaterial()
        {
            Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Include);
            const string rumbleShader = "Elemental/Graphics V5/Rumble Rock Lit";
            for (int index = 0; index < renderers.Length; index++)
            {
                Material material = renderers[index] != null ? renderers[index].sharedMaterial : null;
                if (material != null && material.shader != null &&
                    material.shader.name == rumbleShader)
                    return material;
            }

            for (int index = 0; index < renderers.Length; index++)
            {
                Material material = renderers[index] != null ? renderers[index].sharedMaterial : null;
                if (material != null && material.shader != null &&
                    material.shader.name == "Elemental/SG Earth Master")
                    return material;
            }

            Shader shader = Shader.Find(rumbleShader) ??
                            Shader.Find("Elemental/SG Earth Master") ??
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
