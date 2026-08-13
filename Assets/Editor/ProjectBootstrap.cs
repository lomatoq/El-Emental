using ElEmental.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ElEmental.Editor
{
    public static class ProjectBootstrap
    {
        private const string MainScenePath = "Assets/Scenes/Main.unity";
        private const string RendererPath = "Assets/Settings/ElEmentalRenderer.asset";
        private const string PipelinePath = "Assets/Settings/ElEmentalURP.asset";
        private const string PanelSettingsPath = "Assets/Settings/ElEmentalPanelSettings.asset";

        public static void Configure()
        {
            ConfigureRenderPipeline();
            PanelSettings panelSettings = CreateOrLoadPanelSettings();
            CreateMainScene(panelSettings);

            PlayerSettings.companyName = "El-Emental Team";
            PlayerSettings.productName = "El-Emental";

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[El-Emental] Project bootstrap completed successfully.");
        }

        private static void ConfigureRenderPipeline()
        {
            UniversalRendererData rendererData =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);

            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                rendererData.name = "El-Emental Renderer";
                AssetDatabase.CreateAsset(rendererData, RendererPath);
            }

            UniversalRenderPipelineAsset pipelineAsset =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);

            if (pipelineAsset == null)
            {
                pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
                pipelineAsset.name = "El-Emental URP";
                AssetDatabase.CreateAsset(pipelineAsset, PipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipelineAsset;
            QualitySettings.renderPipeline = pipelineAsset;
            EditorUtility.SetDirty(pipelineAsset);
        }

        private static PanelSettings CreateOrLoadPanelSettings()
        {
            PanelSettings panelSettings =
                AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);

            if (panelSettings != null)
            {
                return panelSettings;
            }

            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "El-Emental Panel Settings";
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.match = 0.5f;
            panelSettings.sortingOrder = 0;
            AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            return panelSettings;
        }

        private static void CreateMainScene(PanelSettings panelSettings)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            camera.transform.position = new Vector3(0f, 1.5f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.027f, 0.059f, 0.082f);

            GameObject lightObject = new GameObject("Sun");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            GameObject uiObject = new GameObject("Main Menu UI");
            UIDocument document = uiObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/MainMenu.uxml");

            MainMenuController controller = uiObject.AddComponent<MainMenuController>();
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("theme").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/Theme.uss");
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, MainScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainScenePath, true)
            };
        }
    }
}
