using System;
using Elemental.Presentation.Rendering;
using Elemental.Runtime.World;
using Elemental.Simulation.Time;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    /// <summary>Repairs only the existing sky bindings; never regenerates the artist's scene.</summary>
    public static class DayNightSkyRestore
    {
        public const string MaterialPath = "Assets/Elemental/Content/Materials/DayNightSky.mat";
        public const string CubePath = "Assets/Elemental/Content/Materials/EqualAreaStars.cubemap";
        private const string Profiles = "Assets/Elemental/Content/Profiles/";

        [MenuItem("Elemental/World/Restore Day Night Sky In Current Scene")]
        public static void RestoreCurrentScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before restoring sky bindings.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity")
                throw new InvalidOperationException("Open the saved EarthCoreSlice scene before restoring its sky.");
            var system = Find<CelestialSystemBehaviour>(scene);
            var planet = Find<VoxelPlanetBehaviour>(scene);
            var skyController = system != null ? system.GetComponent<EarthSkyController>() : null;
            var camera = Named(scene, "Gravity Toy Camera")?.GetComponent<Camera>();
            var light = Named(scene, "Sun")?.GetComponent<Light>();
            var shell = Named(scene, "Planet Atmosphere Limb")?.GetComponent<Renderer>();
            var moon = Named(scene, "Distant Moon");
            var distant = Named(scene, "Ringed Ember Planet");
            var anchor = Named(scene, "Broken Crown Arena") ?? Named(scene, "Celestial Lighting Anchor");
            var duplicateSun = Named(scene, "Visible Sun");
            var celestial = AssetDatabase.LoadAssetAtPath<CelestialSystemProfile>(Profiles + "CelestialSystemProfile.asset");
            var sky = AssetDatabase.LoadAssetAtPath<EarthSkyProfile>(Profiles + "EarthSkyProfile.asset");
            var atmosphere = AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(Profiles + "AtmosphereProfile.asset");
            if (system == null || planet == null || skyController == null || camera == null || light == null ||
                shell == null || celestial == null || sky == null || atmosphere == null || anchor == null)
                throw new InvalidOperationException("Sky repair requires existing celestial system, EarthSkyController, planet, camera, Sun, atmosphere limb and the three profile assets.");

            // Resolve every required object and shader before baking or changing scene state.
            if (light.type != LightType.Directional || (anchor.transform.position - planet.transform.position).sqrMagnitude < 1f)
                throw new InvalidOperationException("Sky repair needs a directional Sun and a noncentral arena lighting anchor.");
            ValidateAssets(sky);
            Material material = PrepareSkyMaterial(sky, true);

            Undo.RecordObjects(new UnityEngine.Object[] { system, skyController, camera }, "Restore animated day and night sky");
            system.ConfigureLightingAnchor(anchor.transform);
            system.Configure(celestial, atmosphere, sky, planet.transform, camera, light,
                null, moon != null ? moon.transform : null, distant != null ? distant.transform : null, shell, material);
            system.SetLightingAuthorityForQa(CelestialLightingAuthorityMode.AnimatedEphemeris);
            // The sky shader owns the one solar disc. The retained mesh remains reversible.
            if (duplicateSun != null)
            {
                Undo.RecordObject(duplicateSun, "Use the sky solar disc");
                duplicateSun.SetActive(false);
                EditorUtility.SetDirty(duplicateSun);
            }
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, celestial.ScaledSpaceDistance * 1.35f);
            RenderSettings.sun = light;
            RenderSettings.skybox = material;
            EditorUtility.SetDirty(system);
            EditorUtility.SetDirty(skyController);
            EditorUtility.SetDirty(camera);
            if (!system.HasRequiredBindings || material.GetTexture("_StarCube") == null)
                throw new InvalidOperationException("Sky repair validation failed; scene was not saved.");
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Day/night restored: daylight {celestial.DaylightSeconds}s, night {celestial.NightSeconds}s, {sky.StarCount} equal-area stars. Existing Sun shadow settings preserved.");
        }

        public static Cubemap BakeStars(EarthSkyProfile profile)
        {
            const int size = 1024;
            ValidateAssets(profile);
            // Build a replacement fully in memory first. Re-baking never writes a partially
            // populated texture and does not depend on a retained runtime CPU pixel copy.
            Cubemap cube = new Cubemap(size, TextureFormat.RGBA32, true) { name = BakeName(profile) };
            cube.wrapMode = TextureWrapMode.Clamp;
            cube.filterMode = FilterMode.Trilinear;
            cube.anisoLevel = 0;
            try
            {
                // Every mip samples the same continuous spherical radiance function, with
                // an energy-preserving angular pixel filter. Face borders never clamp a
                // neighbouring face's contribution during mip generation.
                for (int mip = 0; mip < cube.mipmapCount; mip++)
                for (int face = 0; face < 6; face++)
                    cube.SetPixels(RenderStarFace(profile, face, Mathf.Max(1, size >> mip)), (CubemapFace)face, mip);
                cube.Apply(false, true);
                Cubemap existing = AssetDatabase.LoadAssetAtPath<Cubemap>(CubePath);
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(cube, CubePath);
                    return cube;
                }
                EditorUtility.CopySerialized(cube, existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            finally { if (!AssetDatabase.Contains(cube)) UnityEngine.Object.DestroyImmediate(cube); }
        }

        public static Color[] RenderStarFace(EarthSkyProfile profile, int face, int size)
        {
            FaceBasis(face, out Vector3 forward, out Vector3 right, out Vector3 up);
            var pixels = new Color[size * size];
            uint seed = unchecked((uint)profile.StarSeed);
            for (uint index = 0; index < profile.StarCount; index++)
            {
                var sample = CelestialStarDistribution.Direction(index, seed);
                Vector3 direction = new Vector3(sample.x, sample.y, sample.z);
                float depth = Vector3.Dot(direction, forward);
                if (depth <= .1f) continue;
                float u = Vector3.Dot(direction, right) / depth, v = Vector3.Dot(direction, up) / depth;
                float magnitude = CelestialStarDistribution.Random01(index, seed, 3);
                float intensity = .06f + .94f * Mathf.Pow(magnitude, 7f);
                float sigma = Mathf.Lerp(.00072f, .00135f, intensity);
                float filteredVariance = sigma * sigma + 1f / (3f * size * size);
                float filteredSigma = Mathf.Sqrt(filteredVariance);
                float margin = 4f * filteredSigma / (depth * depth);
                if (Mathf.Abs(u) > 1f + margin || Mathf.Abs(v) > 1f + margin) continue;
                float temperature = CelestialStarDistribution.Random01(index, seed, 4);
                Color color = temperature < .7f
                    ? Color.Lerp(new Color(1f, .72f, .53f), new Color(1f, .96f, .90f), temperature / .7f)
                    : Color.Lerp(new Color(1f, .96f, .90f), new Color(.64f, .79f, 1f), (temperature - .7f) / .3f);
                float cx = (u + 1f) * .5f * size - .5f, cy = (v + 1f) * .5f * size - .5f;
                int radius = Mathf.CeilToInt(margin * size);
                for (int y = Mathf.Max(0, Mathf.FloorToInt(cy) - radius); y <= Mathf.Min(size - 1, Mathf.CeilToInt(cy) + radius); y++)
                for (int x = Mathf.Max(0, Mathf.FloorToInt(cx) - radius); x <= Mathf.Min(size - 1, Mathf.CeilToInt(cx) + radius); x++)
                {
                    Vector3 ray = (forward + right * ((x + .5f) * 2f / size - 1f) + up * ((y + .5f) * 2f / size - 1f)).normalized;
                    // Difference of unit vectors avoids cancellation in 1-dot for subpixel stars.
                    float angleSquared = (direction - ray).sqrMagnitude;
                    float weight = Mathf.Exp(-.5f * angleSquared / filteredVariance) * intensity * sigma * sigma / filteredVariance;
                    pixels[y * size + x] += color * weight;
                }
            }
            return pixels;
        }

        public static Material PrepareSkyMaterial(EarthSkyProfile profile, bool rebake = false)
        {
            Shader shader = ValidateAssets(profile);
            Cubemap cube = AssetDatabase.LoadAssetAtPath<Cubemap>(CubePath);
            if (rebake || cube == null || cube.name != BakeName(profile)) cube = BakeStars(profile);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "DayNightSky" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_StarCube", cube);
            material.SetFloat("_Seed", profile.StarSeed);
            material.SetFloat("_Exposure", profile.StarExposure);
            material.SetFloat("_MilkyWayStrength", .22f);
            material.SetFloat("_StarVisibility", 0f);
            material.SetColor("_ZenithColor", profile.DayZenith);
            material.SetColor("_HorizonColor", profile.DayHorizon);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Shader ValidateAssets(EarthSkyProfile profile)
        {
            if (profile == null || profile.StarCount < 256 || profile.StarCount > 12000)
                throw new InvalidOperationException("A sky profile with 256–12000 stars is required.");
            Shader shader = Shader.Find("Elemental/Procedural Stars");
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Import and compile the Procedural Stars shader before restoring the sky.");
            var material = AssetDatabase.LoadMainAssetAtPath(MaterialPath);
            var cube = AssetDatabase.LoadMainAssetAtPath(CubePath);
            if ((material != null && (!(material is Material m) || m.shader != shader)) ||
                (cube != null && !(cube is Cubemap)))
                throw new InvalidOperationException("Generated sky paths contain incompatible assets; choose unused paths before restoring.");
            return shader;
        }

        private static string BakeName(EarthSkyProfile profile) => $"EqualAreaStars-v3-{profile.StarSeed}-{profile.StarCount}";

        public static void FaceBasis(int face, out Vector3 forward, out Vector3 right, out Vector3 up)
        {
            switch (face)
            {
                case 0: forward = Vector3.right; right = Vector3.back; up = Vector3.up; break;
                case 1: forward = Vector3.left; right = Vector3.forward; up = Vector3.up; break;
                case 2: forward = Vector3.up; right = Vector3.right; up = Vector3.back; break;
                case 3: forward = Vector3.down; right = Vector3.right; up = Vector3.forward; break;
                case 4: forward = Vector3.forward; right = Vector3.right; up = Vector3.up; break;
                default: forward = Vector3.back; right = Vector3.left; up = Vector3.up; break;
            }
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            T result = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (T found in root.GetComponentsInChildren<T>(true))
                {
                    if (result != null) throw new InvalidOperationException($"Sky repair found multiple {typeof(T).Name} components.");
                    result = found;
                }
            }
            return result;
        }

        private static GameObject Named(Scene scene, string name)
        {
            GameObject result = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name)
                {
                    if (result != null) throw new InvalidOperationException($"Sky repair found multiple objects named {name}.");
                    result = child.gameObject;
                }
            return result;
        }
    }
}
