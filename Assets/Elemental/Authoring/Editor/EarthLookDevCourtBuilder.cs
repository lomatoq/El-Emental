using Elemental.Presentation.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    /// <summary>
    /// Builds a deterministic A/B court for the four shipping Earth material
    /// families. It intentionally uses identical geometry and lighting so a visual
    /// review judges the shader/material response rather than silhouette bias.
    /// </summary>
    public static class EarthLookDevCourtBuilder
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthLookDevCourt.unity";
        private const string MaterialFolder = "Assets/Elemental/Content/Materials/LookDev";
        private const string ProfileFolder = "Assets/Elemental/Content/Profiles/LookDev";
        private const string TexturePath = "Assets/Elemental/Content/Textures/EarthStoneAlbedo.png";
        private const string VolumePath = "Assets/Elemental/Content/Profiles/LookDev/EarthLookDevVolume.asset";

        [MenuItem("Elemental Suite/LookDev/Rebuild Earth LookDev Court")]
        public static void Rebuild()
        {
            EnsureFolder(MaterialFolder);
            EnsureFolder(ProfileFolder);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "EarthLookDevCourt";

            Transform focus = new GameObject("LookDev Focus").transform;
            focus.position = new Vector3(0f, 1.15f, 0f);
            BuildCamera(focus.position);
            BuildLighting();
            BuildPostProcessing();
            BuildBackdrop();

            EarthStoneFamily[] families =
            {
                EarthStoneFamily.Basalt,
                EarthStoneFamily.Sandstone,
                EarthStoneFamily.Granite,
                EarthStoneFamily.Clay
            };
            for (int index = 0; index < families.Length; index++)
                BuildFamilyPod(families[index], (index - 1.5f) * 3.25f, 1009 + index * 197);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Elemental] Earth LookDev Court rebuilt at {ScenePath}");
        }

        private static void BuildCamera(Vector3 focus)
        {
            GameObject cameraObject = new GameObject("Earth LookDev Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.transform.position = new Vector3(0f, 4.25f, -13.6f);
            camera.transform.rotation = Quaternion.LookRotation(focus - camera.transform.position, Vector3.up);
            camera.fieldOfView = 46f;
            camera.nearClipPlane = 0.08f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.024f, 0.034f, 1f);
            camera.allowHDR = true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camera.tag = "MainCamera";
        }

        private static void BuildLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.19f, 0.23f, 0.31f);
            RenderSettings.ambientEquatorColor = new Color(0.105f, 0.095f, 0.09f);
            RenderSettings.ambientGroundColor = new Color(0.025f, 0.021f, 0.019f);

            GameObject keyObject = new GameObject("Warm Key");
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.79f, 0.60f);
            key.intensity = 2.15f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.86f;
            key.transform.rotation = Quaternion.Euler(46f, -34f, 0f);

            GameObject rimObject = new GameObject("Cool Rim");
            Light rim = rimObject.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = new Color(0.38f, 0.56f, 1f);
            rim.intensity = 0.72f;
            rim.shadows = LightShadows.None;
            rim.transform.rotation = Quaternion.Euler(24f, 148f, -8f);
        }

        private static void BuildPostProcessing()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumePath);
            }
            profile.components.Clear();
            Bloom bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.22f);
            bloom.scatter.Override(0.58f);
            ColorAdjustments color = profile.Add<ColorAdjustments>();
            color.postExposure.Override(0.18f);
            color.contrast.Override(12f);
            color.saturation.Override(-4f);
            Vignette vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.19f);
            vignette.smoothness.Override(0.72f);
            EditorUtility.SetDirty(profile);

            GameObject volumeObject = new GameObject("Earth LookDev Grade");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            volume.sharedProfile = profile;
        }

        private static void BuildBackdrop()
        {
            Material backdrop = GetOrCreateLitMaterial(
                $"{MaterialFolder}/LookDevBackdrop.mat",
                new Color(0.032f, 0.038f, 0.049f, 1f),
                0.12f);
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Neutral Reference Floor";
            floor.transform.localScale = new Vector3(1.35f, 1f, 0.72f);
            floor.GetComponent<Renderer>().sharedMaterial = backdrop;
            Object.DestroyImmediate(floor.GetComponent<Collider>());
        }

        private static void BuildFamilyPod(EarthStoneFamily family, float x, int seed)
        {
            Material material = GetOrCreateEarthMaterial(family);
            GameObject root = new GameObject($"{family} Court Pod");
            root.transform.position = new Vector3(x, 0f, 0f);

            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = $"{family} Pedestal";
            pedestal.transform.SetParent(root.transform, false);
            pedestal.transform.localPosition = new Vector3(0f, 0.26f, 0.35f);
            pedestal.transform.localScale = new Vector3(1.18f, 0.26f, 1.18f);
            pedestal.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(pedestal.GetComponent<Collider>());

            GameObject roundedMass = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            roundedMass.name = $"{family} Rounded Mass";
            roundedMass.transform.SetParent(root.transform, false);
            roundedMass.transform.localPosition = new Vector3(-0.48f, 1.22f, 0.22f);
            roundedMass.transform.localScale = new Vector3(1.02f, 1.34f, 0.92f);
            roundedMass.transform.localRotation = Quaternion.Euler(7f, seed % 37, -11f);
            roundedMass.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(roundedMass.GetComponent<Collider>());

            GameObject fracturedMass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fracturedMass.name = $"{family} Fracture Block";
            fracturedMass.transform.SetParent(root.transform, false);
            fracturedMass.transform.localPosition = new Vector3(0.48f, 1.18f, 0.28f);
            fracturedMass.transform.localScale = new Vector3(0.82f, 1.52f, 0.74f);
            fracturedMass.transform.localRotation = Quaternion.Euler(-8f, -(seed % 29), 7f);
            fracturedMass.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(fracturedMass.GetComponent<Collider>());

            GameObject interior = GameObject.CreatePrimitive(PrimitiveType.Cube);
            interior.name = $"{family} Fresh Interior";
            interior.transform.SetParent(root.transform, false);
            interior.transform.localPosition = new Vector3(0.62f, 1.24f, -0.12f);
            interior.transform.localScale = new Vector3(0.46f, 0.76f, 0.18f);
            Material interiorMaterial = new Material(material) { name = $"{family} Fresh Interior Runtime" };
            interiorMaterial.SetFloat("_InteriorAmount", 1f);
            interior.GetComponent<Renderer>().sharedMaterial = interiorMaterial;
            Object.DestroyImmediate(interior.GetComponent<Collider>());
        }

        private static Material GetOrCreateEarthMaterial(EarthStoneFamily family)
        {
            string profilePath = $"{ProfileFolder}/{family}EarthProfile.asset";
            EarthMaterialProfile profile = AssetDatabase.LoadAssetAtPath<EarthMaterialProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<EarthMaterialProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            profile.ConfigureLookDevPreset(family);
            EditorUtility.SetDirty(profile);

            string materialPath = $"{MaterialFolder}/{family}Earth.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Shader shader = Shader.Find("Elemental/SG Earth Master");
            if (material == null)
            {
                material = new Material(shader) { name = $"{family} Earth" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else MaterialShaderStateUtility.RebindShader(material, shader);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (texture != null) material.SetTexture("_BaseMap", texture);
            profile.Apply(material, false);
            material.SetFloat("_TriplanarSharpness", 5.5f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetOrCreateLitMaterial(string path, Color color, float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (material == null)
            {
                material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
