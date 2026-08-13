using System.Collections.Generic;
using Elemental.Presentation.UI;
using Elemental.Runtime.Capabilities;
using Elemental.Simulation.Capabilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Elemental.Authoring.Editor
{
    public static class M9WebLabSetup
    {
        public const string ScenePath = "Assets/Elemental/Content/Scenes/WebLab.unity";
        private const string ProfileFolder = "Assets/Elemental/Content/CapabilityProfiles/";
        private const string PanelPath = "Assets/Elemental/Content/UI/CapabilityPanelSettings.asset";

        [MenuItem("Elemental/Setup/Create M9 Web Magic Lab")]
        public static void Configure()
        {
            M3EarthCoreSetup.Configure();
            if (!AssetDatabase.IsValidFolder(ProfileFolder.TrimEnd('/')))
                AssetDatabase.CreateFolder("Assets/Elemental/Content", "CapabilityProfiles");
            CreateOrLoadProfile("NativeHigh.asset", CapabilityProfileData.NativeHigh);
            CreateOrLoadProfile("NativeLow.asset", CapabilityProfileData.NativeLow);
            CapabilityProfileAsset webProfile = CreateOrLoadProfile("WebLab.asset", CapabilityProfileData.WebLab);
            Scene scene = SceneManager.GetActiveScene();
            ParticleSystem[] particles = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include);
            GameObject runtimeObject = new GameObject("WebLab Capability Runtime");
            CapabilityRuntimeBehaviour runtime = runtimeObject.AddComponent<CapabilityRuntimeBehaviour>();
            runtime.Configure(webProfile, particles);
            CreateHud(runtime);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddScene(); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("[Elemental] M9 WebLab configured with explicit WebGL2 capability profile.");
        }

        private static CapabilityProfileAsset CreateOrLoadProfile(string file, CapabilityProfileData data)
        {
            string path = ProfileFolder + file;
            CapabilityProfileAsset asset = AssetDatabase.LoadAssetAtPath<CapabilityProfileAsset>(path);
            if (asset == null)
            { asset = ScriptableObject.CreateInstance<CapabilityProfileAsset>(); AssetDatabase.CreateAsset(asset, path); }
            asset.Configure(in data); EditorUtility.SetDirty(asset); return asset;
        }

        private static void CreateHud(CapabilityRuntimeBehaviour runtime)
        {
            PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>(); panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1920, 1080); AssetDatabase.CreateAsset(panel, PanelPath);
            }
            VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Elemental/Content/UI/CapabilityHud.uxml");
            GameObject hudObject = new GameObject("WebLab UI Toolkit HUD"); hudObject.SetActive(false);
            UIDocument document = hudObject.AddComponent<UIDocument>(); document.panelSettings = panel; document.visualTreeAsset = tree;
            CapabilityHud hud = hudObject.AddComponent<CapabilityHud>(); hud.Configure(runtime); hudObject.SetActive(true);
        }

        private static void AddScene()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(item => item.path == ScenePath))
            { scenes.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
        }
    }
}
