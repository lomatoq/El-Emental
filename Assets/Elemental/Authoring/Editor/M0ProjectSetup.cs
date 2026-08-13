using System;
using System.Collections.Generic;
using System.IO;
using Elemental.Authoring.Build;
using Elemental.Runtime.Bootstrap;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    public static class M0ProjectSetup
    {
        public const string BootstrapScenePath = "Assets/Elemental/Content/Scenes/Bootstrap.unity";
        private const string MainScenePath = "Assets/Scenes/Main.unity";
        private const string GravityToyScenePath = "Assets/Elemental/Content/Scenes/GravityToy.unity";
        private const string VoxelLabScenePath = "Assets/Elemental/Content/Scenes/VoxelPlanetLab.unity";
        private const string EarthCoreScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const string CharacterFeelScenePath = "Assets/Elemental/Content/Scenes/CharacterFeelLab.unity";
        private const string WindLabScenePath = "Assets/Elemental/Content/Scenes/WindLab.unity";
        private const string ElementLabScenePath = "Assets/Elemental/Content/Scenes/ElementLab.unity";
        private const string VolcanoVillageScenePath = "Assets/Elemental/Content/Scenes/VolcanoVillage.unity";
        private const string OnlineSpikeScenePath = "Assets/Elemental/Content/Scenes/OnlineSpike.unity";
        private const string WebLabScenePath = "Assets/Elemental/Content/Scenes/WebLab.unity";

        [MenuItem("Elemental/Setup/Configure M0 Bootstrap")]
        public static void Configure()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject bootstrapObject = new GameObject("Elemental World Bootstrap");
            bootstrapObject.AddComponent<WorldBootstrap>();

            GameObject cameraObject = new GameObject("Bootstrap Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.028f, 0.04f);

            EditorSceneManager.SaveScene(scene, BootstrapScenePath);

            string[] candidateScenes =
            {
                BootstrapScenePath,
                MainScenePath,
                GravityToyScenePath,
                VoxelLabScenePath,
                EarthCoreScenePath,
                CharacterFeelScenePath,
                WindLabScenePath,
                ElementLabScenePath,
                VolcanoVillageScenePath,
                OnlineSpikeScenePath,
                WebLabScenePath
            };
            var buildScenes = new List<EditorBuildSettingsScene>(candidateScenes.Length);
            for (int index = 0; index < candidateScenes.Length; index++)
            {
                string path = candidateScenes[index];
                if (File.Exists(Path.GetFullPath(path)))
                {
                    buildScenes.Add(new EditorBuildSettingsScene(path, true));
                }
            }

            EditorBuildSettings.scenes = buildScenes.ToArray();
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.StandaloneWindows64,
                new[] { GraphicsDeviceType.Direct3D11 });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Elemental] M0 Bootstrap scene configured.");
        }

        public static void BuildWindowsSmoke()
        {
            Configure();

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Could not resolve project root.");
            string buildDirectory = Path.Combine(projectRoot, "Builds", "Windows");
            Directory.CreateDirectory(buildDirectory);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = NativeBuildSceneOrder.Create(
                    Array.ConvertAll(EditorBuildSettings.scenes, item => item.path),
                    EarthCoreScenePath),
                locationPathName = Path.Combine(buildDirectory, "ElEmental.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"Windows smoke build failed: {report.summary.result}, {report.summary.totalErrors} errors.");
            }

            Debug.Log($"[Elemental] Windows smoke build succeeded in {report.summary.totalTime}.");
        }
    }
}
