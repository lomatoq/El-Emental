using System.Collections.Generic;
using Elemental.Presentation.UI;
using Elemental.Runtime.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Elemental.Authoring.Editor
{
    public static class M8OnlineSpikeSetup
    {
        public const string ScenePath = "Assets/Elemental/Content/Scenes/OnlineSpike.unity";
        private const string PanelPath = "Assets/Elemental/Content/UI/OnlineSpikePanelSettings.asset";

        [MenuItem("Elemental/Setup/Create M8 Online Spike")]
        public static void Configure()
        {
            M7VolcanoVillageSetup.Configure();
            Scene scene = SceneManager.GetActiveScene();
            var authority = new Transform[4]; var predicted = new Transform[4];
            for (int index = 0; index < 4; index++)
            {
                GameObject solid = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                solid.name = $"Authority Client {index + 1}"; solid.transform.localScale = Vector3.one * 0.55f;
                solid.GetComponent<MeshRenderer>().sharedMaterial = Material(index, false); authority[index] = solid.transform;
                GameObject ghost = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ghost.name = $"Predicted Client {index + 1}"; ghost.transform.localScale = Vector3.one * 0.35f;
                ghost.GetComponent<MeshRenderer>().sharedMaterial = Material(index, true); predicted[index] = ghost.transform;
                Object.DestroyImmediate(ghost.GetComponent<Collider>());
            }
            GameObject driverObject = new GameObject("Online Authority Spike Driver");
            OnlineSpikeDriver driver = driverObject.AddComponent<OnlineSpikeDriver>();
            driver.Configure(4, 6, 0.08f, authority, predicted);
            CreateHud(driver);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddScene(); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("[Elemental] M8 Online Spike configured for four clients with deterministic latency/loss.");
        }

        private static Material Material(int index, bool ghost)
        {
            string path = $"Assets/Elemental/Content/Materials/Net{(ghost ? "Ghost" : "Authority")}{index}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Color[] colors = { Color.cyan, Color.magenta, Color.yellow, Color.green };
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            { name = $"Net {(ghost ? "Ghost" : "Authority")} {index}", color = Color.Lerp(colors[index], Color.white, ghost ? 0.55f : 0f) };
            AssetDatabase.CreateAsset(material, path); return material;
        }

        private static void CreateHud(OnlineSpikeDriver driver)
        {
            PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>(); panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1920, 1080); AssetDatabase.CreateAsset(panel, PanelPath);
            }
            VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Elemental/Content/UI/OnlineSpikeHud.uxml");
            GameObject hudObject = new GameObject("Online Spike UI Toolkit HUD"); hudObject.SetActive(false);
            UIDocument document = hudObject.AddComponent<UIDocument>(); document.panelSettings = panel; document.visualTreeAsset = tree;
            OnlineSpikeHud hud = hudObject.AddComponent<OnlineSpikeHud>(); hud.Configure(driver); hudObject.SetActive(true);
        }

        private static void AddScene()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(item => item.path == ScenePath))
            { scenes.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
        }
    }
}
