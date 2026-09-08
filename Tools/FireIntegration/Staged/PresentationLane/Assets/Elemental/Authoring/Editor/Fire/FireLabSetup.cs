using System;
using System.IO;
using Elemental.Presentation.Fire;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor.Fire
{
    public static class FireLabSetup
    {
        public const string ScenePath="Assets/Elemental/Content/Scenes/FireLab.unity";
        [MenuItem("Elemental/Fire/Create Isolated FireLab")]
        public static void Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode before authoring FireLab.");
            FireGraphBuilder.Build();
            bool exists=File.Exists(ScenePath);
            var previous=SceneManager.GetActiveScene();
            var scene=exists ? EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive) : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                FireLabDriver driver=null;
                foreach(var candidate in scene.GetRootGameObjects()) { driver=candidate.GetComponent<FireLabDriver>();if(driver!=null)break; }
                if(driver==null){var root=new GameObject("FireLab");SceneManager.MoveGameObjectToScene(root,scene);driver=root.AddComponent<FireLabDriver>();}
                driver.Configure(AssetDatabase.LoadAssetAtPath<FireVisualProfile>(FireGraphBuilder.Content+"/Fire_Default.asset"),
                    AssetDatabase.LoadAssetAtPath<Material>(FireGraphBuilder.Content+"/Fire_LabSurface.mat"));
                EditorUtility.SetDirty(driver);
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                if(!EditorSceneManager.SaveScene(scene,ScenePath)) throw new IOException("Could not save isolated FireLab scene.");
            }
            finally { EditorSceneManager.CloseScene(scene,true); if(previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
        }
        [MenuItem("Elemental/Fire/Open FireLab")]
        public static void Open()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode before opening FireLab.");
            if(!File.Exists(ScenePath)) Create();
            // Additive open preserves the user's current scene and authored state.
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        }
        [MenuItem("Elemental/Fire/Build Standalone FireLab")]
        public static void BuildStandalone()
        {
            Create(); Directory.CreateDirectory("Builds/FireLab");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes=new[]{ScenePath},locationPathName="Builds/FireLab/FireLab.exe",
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development
            });
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("FireLab standalone build failed: " + report.summary.result);
        }
    }
}

