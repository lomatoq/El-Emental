using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            if (!EditorSceneManager.SaveScene(source, ScenePath, true))
                throw new UnityEditor.Build.BuildFailedException("Earth Polish Lab scene could not be saved.");

            Object.DestroyImmediate(marker);
            EditorSceneManager.SaveScene(source, EarthCoreScenePath);
            EnsureDisabledBuildEntry();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Elemental] Earth Polish Lab rebuilt at " + ScenePath);
        }

        private static void EnsureDisabledBuildEntry()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int existing = scenes.FindIndex(item => item.path == ScenePath);
            if (existing >= 0)
            {
                scenes[existing] = new EditorBuildSettingsScene(ScenePath, false);
            }
            else
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, false));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
