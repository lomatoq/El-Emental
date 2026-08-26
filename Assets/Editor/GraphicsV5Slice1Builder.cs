using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class GraphicsV5Slice1Builder
{
    private const int SliceVersion = 4;
    private const string ShaderPath = "Assets/Elemental/Content/Shaders/RumbleRockLit.shader";
    private const string RootFolder = "Assets/Elemental/Content/GraphicsV5";
    private const string RockFolder = RootFolder + "/Rocks";
    private const string GroundFolder = RootFolder + "/Ground";
    private const string MaterialFolder = RootFolder + "/Materials";
    private const string ProfileFolder = RootFolder + "/Profiles";
    private const string SceneFolder = "Assets/Elemental/Content/Scenes";
    private const string ScenePath = SceneFolder + "/RumbleLookdevLab.unity";
    private const string VersionPath = RootFolder + "/Slice1.version.txt";
    private const string VolumeProfilePath = ProfileFolder + "/RumbleLookdevVolume.asset";
    private const string SkyboxMaterialPath = MaterialFolder + "/RumbleDaySky.mat";
    private const string DustTexturePath = MaterialFolder + "/RumbleDustSoft.asset";
    private const string DustMaterialPath = MaterialFolder + "/RumbleDustLit.mat";
    private const string SessionKey = "Elemental.GraphicsV5.Slice1.AutoBuild.v4";

    private static readonly string[] RockMaterialPaths =
    {
        MaterialFolder + "/RumbleSandstone.mat",
        MaterialFolder + "/RumbleLimestone.mat",
        MaterialFolder + "/RumbleBasalt.mat",
        MaterialFolder + "/RumbleClay.mat"
    };

    private sealed class SliceAssets
    {
        public readonly List<Mesh> Rocks = new List<Mesh>(20);
        public readonly List<Mesh> GroundTiles = new List<Mesh>(4);
        public readonly List<Material> RockMaterials = new List<Material>(4);
        public Material GroundMaterial;
        public Material DustMaterial;
        public VolumeProfile VolumeProfile;
        public Material Skybox;
    }

    static GraphicsV5Slice1Builder()
    {
        EditorApplication.delayCall += TryAutoBuild;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += TryAutoBuild;
    }

    [MenuItem("Elemental/Graphics V5/Build and Open Slice 1", priority = 1)]
    public static void BuildAndOpen()
    {
        BuildSlice(openScene: true, force: true);
    }

    [MenuItem("Elemental/Graphics V5/Open Rumble Lookdev Lab", priority = 2)]
    public static void OpenLookdevLab()
    {
        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        if (sceneAsset == null)
        {
            BuildSlice(openScene: true, force: true);
            return;
        }
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    [MenuItem("Elemental/Graphics V5/Rebuild Baked Rock Library", priority = 20)]
    public static void RebuildRockLibrary()
    {
        EnsureFolders();
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null)
        {
            Debug.LogError($"[Graphics V5] Shader is not imported yet: {ShaderPath}");
            return;
        }
        BuildRockAssets(force: true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Graphics V5] Rebuilt 20 deterministic beveled rock meshes.");
    }

    [MenuItem("Elemental/Graphics V5/Apply One-Sun Policy To Open Scene", priority = 30)]
    public static void ApplyOneSunPolicyToOpenScene()
    {
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
        Light key = RenderSettings.sun;
        if (key == null)
            key = lights.FirstOrDefault(light => light != null && light.type == LightType.Directional);
        if (key == null)
        {
            GameObject sunObject = new GameObject("V5 Sun");
            key = sunObject.AddComponent<Light>();
            key.type = LightType.Directional;
        }
        ConfigureKeyLight(key);
        for (int index = 0; index < lights.Length; index++)
        {
            Light light = lights[index];
            if (light == null || light == key) continue;
            Undo.RecordObject(light, "Disable legacy fill light");
            light.enabled = false;
            light.intensity = 0f;
        }
        RenderSettings.sun = key;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Graphics V5] One-sun policy applied. All persistent secondary lights are disabled.");
    }

    private static void TryAutoBuild()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);

        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.delayCall += TryAutoBuild;
            return;
        }

        bool stale = ReadBuiltVersion() != SliceVersion ||
                     AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null ||
                     AssetDatabase.LoadAssetAtPath<Mesh>(RockAssetPath(19)) == null;
        if (!stale) return;
        BuildSlice(openScene: !SceneManager.GetActiveScene().isDirty, force: true);
    }

    private static void BuildSlice(bool openScene, bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Graphics V5] Exit Play Mode before rebuilding the lookdev lab.");
            return;
        }

        try
        {
            EnsureFolders();
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
                throw new InvalidOperationException($"Rumble rock shader has not imported: {ShaderPath}");

            SliceAssets assets = BuildAssets(shader, force);
            Scene scene = BuildScene(assets);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterEditorScene(ScenePath);
            File.WriteAllText(VersionPath, SliceVersion.ToString());
            AssetDatabase.ImportAsset(VersionPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (openScene)
            {
                Scene active = SceneManager.GetActiveScene();
                if (!active.isDirty || active.path == ScenePath)
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log("[Graphics V5] Slice 1 built: 20 baked rocks, one-sun lab, explicit Bokeh DOF, seamless ground tiles and layered Earth VFX.");
        }
        catch (Exception exception)
        {
            SessionState.SetBool(SessionKey, false);
            Debug.LogException(exception);
        }
    }

    private static SliceAssets BuildAssets(Shader shader, bool force)
    {
        var assets = new SliceAssets();
        assets.Rocks.AddRange(BuildRockAssets(force));
        assets.GroundTiles.AddRange(BuildGroundAssets(force));
        assets.RockMaterials.Add(CreateRockMaterial(
            RockMaterialPaths[0], shader,
            new Color(0.54f, 0.37f, 0.245f),
            new Color(0.205f, 0.15f, 0.125f),
            new Color(0.68f, 0.50f, 0.35f),
            3.5f, 0.085f, 0.82f));
        assets.RockMaterials.Add(CreateRockMaterial(
            RockMaterialPaths[1], shader,
            new Color(0.52f, 0.48f, 0.41f),
            new Color(0.205f, 0.19f, 0.18f),
            new Color(0.66f, 0.61f, 0.52f),
            4.4f, 0.07f, 0.88f));
        assets.RockMaterials.Add(CreateRockMaterial(
            RockMaterialPaths[2], shader,
            new Color(0.24f, 0.275f, 0.29f),
            new Color(0.085f, 0.095f, 0.105f),
            new Color(0.35f, 0.39f, 0.40f),
            3.0f, 0.06f, 0.91f));
        assets.RockMaterials.Add(CreateRockMaterial(
            RockMaterialPaths[3], shader,
            new Color(0.43f, 0.285f, 0.225f),
            new Color(0.17f, 0.115f, 0.105f),
            new Color(0.56f, 0.38f, 0.30f),
            5.0f, 0.065f, 0.89f));

        string groundPath = MaterialFolder + "/RumbleGround.mat";
        assets.GroundMaterial = CreateRockMaterial(
            groundPath, shader,
            new Color(0.355f, 0.255f, 0.19f),
            new Color(0.135f, 0.105f, 0.095f),
            new Color(0.47f, 0.345f, 0.26f),
            7.2f, 0.055f, 0.94f);
        assets.GroundMaterial.SetFloat("_UsePlanetFrame", 1f);
        assets.GroundMaterial.SetVector("_PlanetCenter", Vector4.zero);
        assets.GroundMaterial.SetFloat("_TextureStrength", 0.035f);
        EditorUtility.SetDirty(assets.GroundMaterial);

        Texture2D dustTexture = CreateSoftDustTexture();
        assets.DustMaterial = CreateDustMaterial(dustTexture);
        assets.VolumeProfile = CreateVolumeProfile();
        assets.Skybox = CreateSkyboxMaterial();
        return assets;
    }

    private static IReadOnlyList<Mesh> BuildRockAssets(bool force)
    {
        var results = new List<Mesh>(20);
        for (int index = 0; index < 20; index++)
        {
            RumbleRockFamily family = index switch
            {
                < 8 => RumbleRockFamily.Boulder,
                < 12 => RumbleRockFamily.Slab,
                < 16 => RumbleRockFamily.Wedge,
                _ => RumbleRockFamily.Pebble
            };
            int seed = 51803 + index * 7919;
            float scale = index < 3 ? 1.35f : index < 8 ? 1.1f : index < 16 ? 1.22f : 0.62f;
            RumbleRockRecipe recipe = RumbleRockMeshFactory.CreateDefaultRecipe(seed, family, scale);
            string name = $"V5_{family}_{index:00}";
            Mesh generated = RumbleRockMeshFactory.Build(in recipe, name);
            if (!RumbleRockMeshFactory.Validate(generated, out string reason))
            {
                Object.DestroyImmediate(generated);
                throw new InvalidOperationException($"Generated rock {name} is invalid: {reason}");
            }
            Mesh asset = SaveOrReplaceMesh(generated, RockAssetPath(index));
            results.Add(asset);
        }
        return results;
    }

    private static IReadOnlyList<Mesh> BuildGroundAssets(bool force)
    {
        var results = new List<Mesh>(4);
        int index = 0;
        for (int z = 0; z < 2; z++)
        for (int x = 0; x < 2; x++)
        {
            Mesh mesh = CreateGroundTileMesh(x, z, 12f, 28);
            mesh.name = $"V5_SeamlessGround_{x}_{z}";
            results.Add(SaveOrReplaceMesh(mesh, GroundFolder + $"/{mesh.name}.asset"));
            index++;
        }
        return results;
    }

    private static Mesh SaveOrReplaceMesh(Mesh generated, string path)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(generated, path);
            return generated;
        }
        EditorUtility.CopySerialized(generated, existing);
        Object.DestroyImmediate(generated);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static Material CreateRockMaterial(
        string path,
        Shader shader,
        Color baseColor,
        Color shadowColor,
        Color edgeColor,
        float macroScale,
        float macroStrength,
        float roughness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.enableInstancing = true;
        material.SetColor("_BaseColor", baseColor);
        material.SetColor("_ShadowColor", shadowColor);
        material.SetColor("_EdgeColor", edgeColor);
        material.SetColor("_FractureColor", Color.Lerp(baseColor, Color.white, 0.18f));
        material.SetFloat("_TextureScale", 0.22f);
        material.SetFloat("_TextureStrength", 0f);
        material.SetFloat("_TriplanarSharpness", 4.0f);
        material.SetFloat("_MacroScale", macroScale);
        material.SetFloat("_MacroStrength", macroStrength);
        material.SetFloat("_FacetContrast", 0.34f);
        material.SetFloat("_Roughness", roughness);
        material.SetFloat("_BevelLight", 0.42f);
        material.SetFloat("_SideShadingSmoothness", 1f);
        material.SetFloat("_AmbientStrength", 0.86f);
        material.SetFloat("_Fade", 1f);
        material.SetFloat("_DebugMode", 0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D CreateSoftDustTexture()
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(DustTexturePath);
        if (texture == null)
        {
            texture = new Texture2D(64, 64, TextureFormat.RGBA32, false, true)
            {
                name = "RumbleDustSoft",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };
            Color[] pixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float nx = (x + 0.5f) / 64f * 2f - 1f;
                float ny = (y + 0.5f) / 64f * 2f - 1f;
                float radius = Mathf.Sqrt(nx * nx + ny * ny);
                float core = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.12f, 1f, radius));
                float breakup = Mathf.Lerp(0.82f, 1.08f,
                    Mathf.PerlinNoise(x * 0.115f + 7.3f, y * 0.115f + 19.7f));
                float alpha = Mathf.Clamp01(core * breakup);
                pixels[y * 64 + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            AssetDatabase.CreateAsset(texture, DustTexturePath);
        }
        return texture;
    }

    private static Material CreateDustMaterial(Texture2D texture)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) throw new InvalidOperationException("No compatible particle shader is available.");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(DustMaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "RumbleDustLit" };
            AssetDatabase.CreateAsset(material, DustMaterialPath);
        }
        material.shader = shader;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", new Color(0.50f, 0.38f, 0.29f, 0.58f));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static VolumeProfile CreateVolumeProfile()
    {
        AssetDatabase.DeleteAsset(VolumeProfilePath);
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "RumbleLookdevVolume";
        AssetDatabase.CreateAsset(profile, VolumeProfilePath);

        Tonemapping tonemapping = AddVolumeComponent<Tonemapping>(profile);
        tonemapping.mode.Override(TonemappingMode.ACES);
        ColorAdjustments color = AddVolumeComponent<ColorAdjustments>(profile);
        color.postExposure.Override(0f);
        color.contrast.Override(7f);
        color.saturation.Override(-8f);
        WhiteBalance balance = AddVolumeComponent<WhiteBalance>(profile);
        balance.temperature.Override(2f);
        balance.tint.Override(-1f);
        Bloom bloom = AddVolumeComponent<Bloom>(profile);
        bloom.threshold.Override(1.12f);
        bloom.intensity.Override(0.07f);
        bloom.scatter.Override(0.54f);
        bloom.clamp.Override(20f);
        DepthOfField depth = AddVolumeComponent<DepthOfField>(profile);
        depth.mode.Override(DepthOfFieldMode.Bokeh);
        depth.focusDistance.Override(16f);
        depth.focalLength.Override(35f);
        depth.aperture.Override(16f);
        depth.bladeCount.Override(7);
        depth.bladeCurvature.Override(0.82f);
        depth.bladeRotation.Override(18f);
        Vignette vignette = AddVolumeComponent<Vignette>(profile);
        vignette.intensity.Override(0.075f);
        vignette.smoothness.Override(0.48f);
        vignette.rounded.Override(true);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(profile);
        return profile;
    }

    private static T AddVolumeComponent<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T component = profile.Add<T>(true);
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    private static Material CreateSkyboxMaterial()
    {
        Shader shader = Shader.Find("Skybox/Procedural");
        if (shader == null) throw new InvalidOperationException("Built-in procedural skybox shader is unavailable.");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "RumbleDaySky" };
            AssetDatabase.CreateAsset(material, SkyboxMaterialPath);
        }
        material.shader = shader;
        material.SetFloat("_SunDisk", 2f);
        material.SetFloat("_SunSize", 0.035f);
        material.SetFloat("_SunSizeConvergence", 5f);
        material.SetFloat("_AtmosphereThickness", 0.78f);
        material.SetColor("_SkyTint", new Color(0.42f, 0.53f, 0.68f));
        material.SetColor("_GroundColor", new Color(0.19f, 0.145f, 0.115f));
        material.SetFloat("_Exposure", 1.05f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Scene BuildScene(SliceAssets assets)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "RumbleLookdevLab";
        GameObject root = new GameObject("GRAPHICS V5 — RUMBLE LOOKDEV LAB");

        Light sun = CreateSun(root.transform);
        RenderSettings.sun = sun;
        RenderSettings.skybox = assets.Skybox;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.32f, 0.39f, 0.48f);
        RenderSettings.ambientEquatorColor = new Color(0.20f, 0.18f, 0.17f);
        RenderSettings.ambientGroundColor = new Color(0.075f, 0.065f, 0.06f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.reflectionIntensity = 0.72f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.37f, 0.42f, 0.47f);
        RenderSettings.fogDensity = 0.0035f;
        QualitySettings.shadowDistance = 48f;
        QualitySettings.shadowCascades = 2;

        Volume volume = CreateGlobalVolume(root.transform, assets.VolumeProfile);
        Camera camera = CreateMainCamera(root.transform);
        Transform groundRoot = CreateGround(root.transform, assets.GroundTiles, assets.GroundMaterial);
        EnvironmentResult environment = CreateEnvironment(
            root.transform,
            assets.Rocks,
            assets.RockMaterials);
        CreateReflectionProbe(root.transform);

        RumbleLensDirector lens = camera.gameObject.AddComponent<RumbleLensDirector>();
        lens.Configure(
            volume,
            environment.NearFocus,
            environment.MidFocus,
            environment.FarFocus,
            sun,
            assets.RockMaterials.Concat(new[] { assets.GroundMaterial }).ToArray());
        EditorUtility.SetDirty(lens);

        CreateVfxCourt(
            root.transform,
            assets,
            environment.WallStones,
            environment.ImpactPoint,
            lens);
        RumbleLookdevSceneGuard guard = root.AddComponent<RumbleLookdevSceneGuard>();
        guard.Configure(camera, sun, volume);
        EditorUtility.SetDirty(guard);
        CreateEvidenceCameras(root.transform, camera);
        CreateSceneReadme(root.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        return scene;
    }

    private readonly struct EnvironmentResult
    {
        public EnvironmentResult(
            Transform nearFocus,
            Transform midFocus,
            Transform farFocus,
            Transform[] wallStones,
            Transform impactPoint)
        {
            NearFocus = nearFocus;
            MidFocus = midFocus;
            FarFocus = farFocus;
            WallStones = wallStones;
            ImpactPoint = impactPoint;
        }

        public Transform NearFocus { get; }
        public Transform MidFocus { get; }
        public Transform FarFocus { get; }
        public Transform[] WallStones { get; }
        public Transform ImpactPoint { get; }
    }

    private static Light CreateSun(Transform parent)
    {
        GameObject sunObject = new GameObject("V5 Sun — Sole Lighting Owner");
        sunObject.transform.SetParent(parent, false);
        Light sun = sunObject.AddComponent<Light>();
        ConfigureKeyLight(sun);
        return sun;
    }

    private static void ConfigureKeyLight(Light sun)
    {
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.91f, 0.78f);
        sun.intensity = 1.28f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.74f;
        sun.shadowBias = 0.11f;
        sun.shadowNormalBias = 0.20f;
        sun.transform.rotation = Quaternion.Euler(42f, -34f, 0f);
        sun.enabled = true;
    }

    private static Volume CreateGlobalVolume(Transform parent, VolumeProfile profile)
    {
        GameObject volumeObject = new GameObject("V5 Authored Global Volume — Inspect Me");
        volumeObject.transform.SetParent(parent, false);
        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 500f;
        volume.weight = 1f;
        volume.sharedProfile = profile;
        return volume;
    }

    private static Camera CreateMainCamera(Transform parent)
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(parent, false);
        cameraObject.transform.position = new Vector3(0f, 5.2f, -16.5f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 46f;
        camera.nearClipPlane = 0.08f;
        camera.farClipPlane = 260f;
        camera.allowHDR = true;
        camera.clearFlags = CameraClearFlags.Skybox;
        cameraObject.AddComponent<AudioListener>();
        UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality = AntialiasingQuality.High;
        data.dithering = true;
        data.stopNaN = true;
        LookAt(cameraObject.transform, new Vector3(0f, 1.65f, 2.8f));
        return camera;
    }

    private static Transform CreateGround(
        Transform parent,
        IReadOnlyList<Mesh> tiles,
        Material groundMaterial)
    {
        GameObject groundRoot = new GameObject("V5 Seam Truth Ground — Four Independent Tiles");
        groundRoot.transform.SetParent(parent, false);
        for (int index = 0; index < tiles.Count; index++)
        {
            Mesh mesh = tiles[index];
            GameObject tile = new GameObject(mesh.name);
            tile.transform.SetParent(groundRoot.transform, false);
            MeshFilter filter = tile.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = tile.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = groundMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            MeshCollider collider = tile.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            RumbleRockVariation variation = tile.AddComponent<RumbleRockVariation>();
            variation.Configure(
                new Color(0.355f, 0.255f, 0.19f),
                new Color(0.135f, 0.105f, 0.095f),
                new Color(0.47f, 0.345f, 0.26f),
                7.2f,
                0.055f,
                0.20f,
                true,
                Vector3.zero);
        }
        return groundRoot.transform;
    }

    private static EnvironmentResult CreateEnvironment(
        Transform parent,
        IReadOnlyList<Mesh> rocks,
        IReadOnlyList<Material> materials)
    {
        GameObject environment = new GameObject("V5 Authored Geological Corner — No Hero Primitives");
        environment.transform.SetParent(parent, false);

        GameObject nearRock = CreateRock(
            "Near Focus Rock",
            rocks[0], materials[0], environment.transform,
            new Vector3(-3.2f, GroundHeight(-3.2f, -7.6f), -7.6f),
            new Vector3(0f, 22f, -4f),
            new Vector3(1.15f, 1.15f, 1.15f),
            true, 101);
        GameObject midRock = CreateRock(
            "Mid Hero Rock",
            rocks[1], materials[1], environment.transform,
            new Vector3(0.4f, GroundHeight(0.4f, 1.6f), 1.6f),
            new Vector3(0f, -18f, 0f),
            new Vector3(1.65f, 1.65f, 1.65f),
            true, 202);
        GameObject farRock = CreateRock(
            "Far Focus Monolith",
            rocks[14], materials[2], environment.transform,
            new Vector3(2.8f, GroundHeight(2.8f, 11.8f), 11.8f),
            new Vector3(-4f, 31f, 5f),
            new Vector3(2.0f, 2.5f, 2.0f),
            true, 303);

        Transform nearFocus = CreateFocusAnchor(nearRock, "DOF Near — 2");
        Transform midFocus = CreateFocusAnchor(midRock, "DOF Mid — 3");
        Transform farFocus = CreateFocusAnchor(farRock, "DOF Far — 4");

        for (int index = 3; index < 12; index++)
        {
            float t = (index - 3) / 8f;
            float x = Mathf.Lerp(-11.5f, 11.5f, t);
            float z = 12.5f + Mathf.Sin(index * 1.87f) * 2.4f;
            float scale = Mathf.Lerp(1.25f, 2.35f, Mathf.Repeat(index * 0.37f, 1f));
            CreateRock(
                $"Background Ridge {index:00}",
                rocks[index], materials[index % materials.Count], environment.transform,
                new Vector3(x, GroundHeight(x, z), z),
                new Vector3(Mathf.Sin(index) * 7f, index * 37f, Mathf.Cos(index) * 5f),
                new Vector3(scale, scale * Mathf.Lerp(0.85f, 1.25f, t), scale),
                false, 400 + index);
        }

        Vector3[] sidePositions =
        {
            new Vector3(-9.4f, 0f, -2.5f),
            new Vector3(-7.8f, 0f, 4.3f),
            new Vector3(8.7f, 0f, -1.3f),
            new Vector3(9.6f, 0f, 5.4f),
            new Vector3(-5.8f, 0f, 9.3f)
        };
        for (int index = 0; index < sidePositions.Length; index++)
        {
            Vector3 position = sidePositions[index];
            position.y = GroundHeight(position.x, position.z);
            int rockIndex = 12 + index;
            float scale = 1.0f + index * 0.18f;
            CreateRock(
                $"Midground Rock {index:00}",
                rocks[rockIndex], materials[(index + 1) % materials.Count], environment.transform,
                position,
                new Vector3(index * 3f, 28f + index * 51f, -index * 2f),
                Vector3.one * scale,
                index < 4, 510 + index);
        }

        GameObject wallRoot = new GameObject("V5 Emergence Wall");
        wallRoot.transform.SetParent(environment.transform, false);
        var wallStones = new Transform[5];
        for (int index = 0; index < wallStones.Length; index++)
        {
            float x = -3.2f + index * 1.6f;
            float z = 6.2f + Mathf.Abs(index - 2) * 0.14f;
            GameObject stone = CreateRock(
                $"Wall Stone {index:00}",
                rocks[8 + index % 4], materials[index % 2], wallRoot.transform,
                new Vector3(x, GroundHeight(x, z), z),
                new Vector3(0f, -8f + index * 5f, 0f),
                new Vector3(1.45f, 2.05f + (index % 2) * 0.28f, 0.78f),
                true, 620 + index);
            wallStones[index] = stone.transform;
        }

        GameObject impact = new GameObject("Heavy Impact Target — H");
        impact.transform.SetParent(environment.transform, false);
        impact.transform.position = new Vector3(4.35f, GroundHeight(4.35f, -0.4f), -0.4f);
        return new EnvironmentResult(
            nearFocus, midFocus, farFocus, wallStones, impact.transform);
    }

    private static GameObject CreateRock(
        string name,
        Mesh mesh,
        Material material,
        Transform parent,
        Vector3 position,
        Vector3 euler,
        Vector3 scale,
        bool collider,
        int variationSeed)
    {
        GameObject rock = new GameObject(name);
        rock.transform.SetParent(parent, false);
        rock.transform.position = position;
        rock.transform.rotation = Quaternion.Euler(euler);
        rock.transform.localScale = scale;
        MeshFilter filter = rock.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = rock.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        if (collider)
        {
            MeshCollider meshCollider = rock.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh;
            meshCollider.convex = false;
        }

        float hue = Mathf.Repeat(variationSeed * 0.071f, 1f);
        Color materialBase = material.HasProperty("_BaseColor")
            ? material.GetColor("_BaseColor")
            : new Color(0.5f, 0.34f, 0.23f);
        Color baseColor = Color.Lerp(materialBase * 0.92f, materialBase * 1.06f, hue);
        baseColor.a = 1f;
        Color shadow = material.HasProperty("_ShadowColor")
            ? material.GetColor("_ShadowColor")
            : baseColor * 0.36f;
        Color edge = material.HasProperty("_EdgeColor")
            ? material.GetColor("_EdgeColor")
            : Color.Lerp(baseColor, Color.white, 0.18f);
        RumbleRockVariation variation = rock.AddComponent<RumbleRockVariation>();
        variation.Configure(
            baseColor,
            shadow,
            edge,
            Mathf.Lerp(2.8f, 5.8f, Mathf.Repeat(variationSeed * 0.113f, 1f)),
            Mathf.Lerp(0.045f, 0.10f, Mathf.Repeat(variationSeed * 0.173f, 1f)),
            Mathf.Lerp(0.17f, 0.31f, Mathf.Repeat(variationSeed * 0.217f, 1f)),
            false,
            Vector3.zero);
        return rock;
    }

    private static Transform CreateFocusAnchor(GameObject rock, string name)
    {
        GameObject anchor = new GameObject(name);
        anchor.transform.SetParent(rock.transform, false);
        MeshFilter filter = rock.GetComponent<MeshFilter>();
        anchor.transform.localPosition = filter != null && filter.sharedMesh != null
            ? filter.sharedMesh.bounds.center
            : Vector3.up * 0.8f;
        anchor.transform.localRotation = Quaternion.identity;
        anchor.transform.localScale = Vector3.one;
        return anchor.transform;
    }

    private static void CreateVfxCourt(
        Transform parent,
        SliceAssets assets,
        Transform[] wallStones,
        Transform impactPoint,
        RumbleLensDirector lens)
    {
        GameObject vfxRoot = new GameObject("V5 Earth VFX Court");
        vfxRoot.transform.SetParent(parent, false);
        ParticleSystem pressure = CreateDustSystem(
            "Pressure Dust — dense source puff",
            vfxRoot.transform,
            assets.DustMaterial,
            DustKind.Pressure,
            assets.Rocks[17]);
        ParticleSystem ground = CreateDustSystem(
            "Ground Dust — slow rolling sheet",
            vfxRoot.transform,
            assets.DustMaterial,
            DustKind.Ground,
            assets.Rocks[18]);
        ParticleSystem gravel = CreateDustSystem(
            "Ballistic Gravel — weighted chips",
            vfxRoot.transform,
            assets.RockMaterials[0],
            DustKind.Gravel,
            assets.Rocks[19]);

        RumbleEarthVfxDemo demo = vfxRoot.AddComponent<RumbleEarthVfxDemo>();
        SerializedObject serialized = new SerializedObject(demo);
        SetObjectArray(serialized.FindProperty("wallStones"), wallStones.Cast<Object>().ToArray());
        serialized.FindProperty("pressureDust").objectReferenceValue = pressure;
        serialized.FindProperty("groundDust").objectReferenceValue = ground;
        serialized.FindProperty("gravel").objectReferenceValue = gravel;
        serialized.FindProperty("impactPoint").objectReferenceValue = impactPoint;
        SetObjectArray(serialized.FindProperty("debrisMeshes"),
            assets.Rocks.Skip(16).Take(4).Cast<Object>().ToArray());
        serialized.FindProperty("debrisMaterial").objectReferenceValue = assets.RockMaterials[0];
        serialized.FindProperty("lensDirector").objectReferenceValue = lens;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(demo);
    }

    private enum DustKind : byte
    {
        Pressure,
        Ground,
        Gravel
    }

    private static ParticleSystem CreateDustSystem(
        string name,
        Transform parent,
        Material material,
        DustKind kind,
        Mesh gravelMesh)
    {
        GameObject objectRoot = new GameObject(name);
        objectRoot.transform.SetParent(parent, false);
        ParticleSystem system = objectRoot.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = kind == DustKind.Pressure ? 280 : kind == DustKind.Ground ? 240 : 180;
        main.startLifetime = kind switch
        {
            DustKind.Pressure => new ParticleSystem.MinMaxCurve(1.15f, 2.15f),
            DustKind.Ground => new ParticleSystem.MinMaxCurve(1.8f, 3.2f),
            _ => new ParticleSystem.MinMaxCurve(3.2f, 5.8f)
        };
        main.startSpeed = kind switch
        {
            DustKind.Pressure => new ParticleSystem.MinMaxCurve(0.35f, 1.75f),
            DustKind.Ground => new ParticleSystem.MinMaxCurve(0.65f, 2.45f),
            _ => new ParticleSystem.MinMaxCurve(2.0f, 6.4f)
        };
        main.startSize = kind switch
        {
            DustKind.Pressure => new ParticleSystem.MinMaxCurve(0.38f, 1.28f),
            DustKind.Ground => new ParticleSystem.MinMaxCurve(0.85f, 2.15f),
            _ => new ParticleSystem.MinMaxCurve(0.075f, 0.22f)
        };
        main.startColor = kind == DustKind.Gravel
            ? new Color(0.49f, 0.35f, 0.24f, 1f)
            : new Color(0.46f, 0.34f, 0.27f, 0.58f);
        main.gravityModifier = kind == DustKind.Gravel ? 1.18f : kind == DustKind.Pressure ? 0.035f : 0.015f;
        main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        if (kind == DustKind.Pressure)
        {
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.42f;
            shape.radiusThickness = 0.32f;
        }
        else if (kind == DustKind.Ground)
        {
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.05f;
            shape.radiusThickness = 0.42f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
        }
        else
        {
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.34f;
            shape.radiusThickness = 0.6f;
        }

        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.39f, 0.29f, 0.23f), 0f),
                new GradientColorKey(new Color(0.55f, 0.43f, 0.34f), 0.45f),
                new GradientColorKey(new Color(0.42f, 0.36f, 0.32f), 1f)
            },
            kind == DustKind.Gravel
                ? new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.72f),
                    new GradientAlphaKey(0f, 1f)
                }
                : new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.62f, 0.08f),
                    new GradientAlphaKey(0.42f, 0.52f),
                    new GradientAlphaKey(0f, 1f)
                });
        color.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        AnimationCurve sizeCurve = kind == DustKind.Gravel
            ? new AnimationCurve(
                new Keyframe(0f, 0.45f),
                new Keyframe(0.12f, 1f),
                new Keyframe(0.82f, 0.92f),
                new Keyframe(1f, 0f))
            : new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.14f, 0.85f),
                new Keyframe(0.72f, 1.35f),
                new Keyframe(1f, 1.55f));
        size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = kind != DustKind.Gravel;
        noise.quality = ParticleSystemNoiseQuality.High;
        noise.strength = kind == DustKind.Pressure ? 0.34f : 0.22f;
        noise.frequency = kind == DustKind.Pressure ? 0.42f : 0.27f;
        noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.11f);
        noise.damping = true;

        ParticleSystem.LimitVelocityOverLifetimeModule drag = system.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.limit = kind == DustKind.Gravel ? 8f : 2.2f;
        drag.dampen = kind == DustKind.Gravel ? 0.18f : 0.42f;
        drag.drag = new ParticleSystem.MinMaxCurve(kind == DustKind.Gravel ? 0.08f : 0.22f);

        if (kind == DustKind.Gravel)
        {
            ParticleSystem.CollisionModule collision = system.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.collidesWith = ~0;
            collision.dampen = 0.34f;
            collision.bounce = 0.16f;
            collision.lifetimeLoss = 0.16f;
            collision.quality = ParticleSystemCollisionQuality.High;
        }

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        if (kind == DustKind.Gravel)
        {
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = gravelMesh;
        }
        else
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.minParticleSize = 0.002f;
            renderer.maxParticleSize = 0.22f;
        }
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return system;
    }

    private static void CreateReflectionProbe(Transform parent)
    {
        GameObject probeObject = new GameObject("V5 Reflection Probe — Environment Only");
        probeObject.transform.SetParent(parent, false);
        probeObject.transform.position = new Vector3(0f, 3.5f, 2f);
        ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
        probe.size = new Vector3(34f, 18f, 38f);
        probe.intensity = 0.62f;
        probe.boxProjection = true;
        probe.cullingMask = ~0;
        probe.resolution = 128;
    }

    private static void CreateEvidenceCameras(Transform parent, Camera source)
    {
        CreateEvidenceCamera(parent, source, "Evidence Camera — Day Hero", new Vector3(0f, 5.2f, -16.5f), new Vector3(0f, 1.65f, 2.8f));
        CreateEvidenceCamera(parent, source, "Evidence Camera — Sunset Side", new Vector3(-12.5f, 4.4f, -5.8f), new Vector3(0f, 1.5f, 3.8f));
        CreateEvidenceCamera(parent, source, "Evidence Camera — Night Wide", new Vector3(11.2f, 7.1f, -11.6f), new Vector3(0f, 1.6f, 4.6f));
    }

    private static void CreateEvidenceCamera(
        Transform parent,
        Camera source,
        string name,
        Vector3 position,
        Vector3 target)
    {
        GameObject cameraObject = new GameObject(name);
        cameraObject.transform.SetParent(parent, false);
        cameraObject.transform.position = position;
        LookAt(cameraObject.transform, target);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.CopyFrom(source);
        camera.enabled = false;
        UniversalAdditionalCameraData sourceData = source.GetUniversalAdditionalCameraData();
        UniversalAdditionalCameraData targetData = camera.GetUniversalAdditionalCameraData();
        targetData.renderPostProcessing = sourceData.renderPostProcessing;
        targetData.antialiasing = sourceData.antialiasing;
        targetData.antialiasingQuality = sourceData.antialiasingQuality;
        targetData.dithering = sourceData.dithering;
        targetData.stopNaN = sourceData.stopNaN;
    }

    private static void CreateSceneReadme(Transform parent)
    {
        GameObject readme = new GameObject("READ ME — Play and use 1-4, C, F1-F3, Tab, Space, H, R");
        readme.transform.SetParent(parent, false);
        readme.transform.localPosition = Vector3.zero;
    }

    private static Mesh CreateGroundTileMesh(int tileX, int tileZ, float tileSize, int resolution)
    {
        int row = resolution + 1;
        var vertices = new Vector3[row * row];
        var normals = new Vector3[row * row];
        var colors = new Color[row * row];
        var triangles = new int[resolution * resolution * 6];
        float originX = (tileX - 1) * tileSize;
        float originZ = (tileZ - 1) * tileSize;
        int vertexIndex = 0;
        for (int z = 0; z <= resolution; z++)
        for (int x = 0; x <= resolution; x++)
        {
            float fx = originX + x / (float)resolution * tileSize;
            float fz = originZ + z / (float)resolution * tileSize;
            float y = GroundHeight(fx, fz);
            vertices[vertexIndex] = new Vector3(fx, y, fz);
            const float derivativeStep = 0.05f;
            float dx = (GroundHeight(fx + derivativeStep, fz) -
                        GroundHeight(fx - derivativeStep, fz)) / (derivativeStep * 2f);
            float dz = (GroundHeight(fx, fz + derivativeStep) -
                        GroundHeight(fx, fz - derivativeStep)) / (derivativeStep * 2f);
            normals[vertexIndex] = new Vector3(-dx, 1f, -dz).normalized;
            float tone = Mathf.Lerp(0.90f, 1.06f,
                Mathf.PerlinNoise(fx * 0.067f + 3.2f, fz * 0.067f + 9.7f));
            colors[vertexIndex] = new Color(tone, tone, tone, 0.38f);
            vertexIndex++;
        }

        int triangleIndex = 0;
        for (int z = 0; z < resolution; z++)
        for (int x = 0; x < resolution; x++)
        {
            int a = z * row + x;
            int b = a + 1;
            int c = a + row;
            int d = c + 1;
            triangles[triangleIndex++] = a;
            triangles[triangleIndex++] = c;
            triangles[triangleIndex++] = b;
            triangles[triangleIndex++] = b;
            triangles[triangleIndex++] = c;
            triangles[triangleIndex++] = d;
        }

        var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private static float GroundHeight(float x, float z)
    {
        float broad = Mathf.Sin(x * 0.16f) * 0.16f + Mathf.Cos(z * 0.14f) * 0.13f;
        float diagonal = Mathf.Sin((x + z) * 0.095f + 1.2f) * 0.11f;
        float basin = -Mathf.Exp(-((x - 4.5f) * (x - 4.5f) + (z - 0.5f) * (z - 0.5f)) / 22f) * 0.18f;
        return broad + diagonal + basin;
    }

    private static void SetObjectArray(SerializedProperty property, Object[] values)
    {
        property.arraySize = values?.Length ?? 0;
        for (int index = 0; index < property.arraySize; index++)
            property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
    }

    private static void LookAt(Transform transform, Vector3 target)
    {
        Vector3 direction = target - transform.position;
        if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private static void RegisterEditorScene(string path)
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.All(scene => scene.path != path))
        {
            scenes.Add(new EditorBuildSettingsScene(path, false));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }

    private static int ReadBuiltVersion()
    {
        if (!File.Exists(VersionPath)) return 0;
        return int.TryParse(File.ReadAllText(VersionPath).Trim(), out int value) ? value : 0;
    }

    private static string RockAssetPath(int index)
    {
        RumbleRockFamily family = index switch
        {
            < 8 => RumbleRockFamily.Boulder,
            < 12 => RumbleRockFamily.Slab,
            < 16 => RumbleRockFamily.Wedge,
            _ => RumbleRockFamily.Pebble
        };
        return RockFolder + $"/V5_{family}_{index:00}.asset";
    }

    private static void EnsureFolders()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(RockFolder);
        EnsureFolder(GroundFolder);
        EnsureFolder(MaterialFolder);
        EnsureFolder(ProfileFolder);
        EnsureFolder(SceneFolder);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name)) return;
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
