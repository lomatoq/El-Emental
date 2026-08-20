using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Elemental.Runtime.World;

namespace Elemental.Authoring.Editor
{
    /// <summary>
    /// Rebuilds the Earth MVP and stores a deterministic, non-shipping copy for
    /// fracture/repair, camera, material and performance comparisons.
    /// </summary>
    public static class EarthPolishLabSetup
    {
        public const string ScenePath = "Assets/Elemental/Content/Scenes/EarthPolishLab.unity";
        private const string EarthCoreScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";

        [MenuItem("Elemental/Setup/Create Earth Polish Lab")]
        public static void Configure()
        {
            M3EarthCoreSetup.Configure();
            Scene source = SceneManager.GetActiveScene();
            if (!source.IsValid())
                throw new UnityEditor.Build.BuildFailedException("Earth Core source scene was not created.");

            GameObject marker = new GameObject("Earth Polish Lab — non-shipping QA");
            marker.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            marker.AddComponent<EarthPolishLabController>();
            if (!EditorSceneManager.SaveScene(source, ScenePath, true))
                throw new UnityEditor.Build.BuildFailedException("Earth Polish Lab scene could not be saved.");

            Object.DestroyImmediate(marker);
            EditorSceneManager.SaveScene(source, EarthCoreScenePath);
            EnsureEditorTestBuildEntry();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Elemental] Earth Polish Lab rebuilt at " + ScenePath);
        }

        [MenuItem("Elemental/QA/Open Earth Polish Lab")]
        public static void Open()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                Configure();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[Elemental] Earth Polish Lab opened for interactive QA.");
        }

        internal static void EnsureEditorTestBuildEntry()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int existing = scenes.FindIndex(item => item.path == ScenePath);
            if (existing >= 0)
            {
                scenes[existing] = new EditorBuildSettingsScene(ScenePath, true);
            }
            else
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
