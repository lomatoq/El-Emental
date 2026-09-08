using System;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    public static class FrontendAudioSetup
    {
        public const string Root = "Assets/Elemental/Content/Audio/Frontend/";
        public const string ProfilePath = Root + "FrontendAudio.asset";
        [MenuItem("Elemental/Audio/Install Supplied Frontend Tracks")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Install outside Play Mode.");
            var menu=Import("Main Menu.mp3",true);
            var game=Import("Sky Temple Gate.mp3",true);
            var panel=Import("Panel Move Sound.mp3",false);
            var profile=AssetDatabase.LoadAssetAtPath<FrontendAudioProfile>(ProfilePath);
            if(profile==null){profile=ScriptableObject.CreateInstance<FrontendAudioProfile>();AssetDatabase.CreateAsset(profile,ProfilePath);}
            Undo.RecordObject(profile,"Bind supplied frontend audio");
            profile.mainMenu=menu;profile.game=game;profile.panelMove=panel;
            EditorUtility.SetDirty(profile);
            var theme=AssetDatabase.LoadAssetAtPath<ElementalUITheme>(AlphaFrontendSetup.ThemePath);
            if(theme==null)throw new InvalidOperationException("Frontend theme missing.");
            Undo.RecordObject(theme,"Bind frontend audio profile");theme.frontendAudio=profile;EditorUtility.SetDirty(theme);
            var scene=SceneManager.GetSceneByPath("Assets/Elemental/Content/Scenes/EarthCoreSlice.unity");
            if(!scene.isLoaded)throw new InvalidOperationException("Open EarthCoreSlice first.");
            FrontendFlowController flow=null;CelestialSystemBehaviour sky=null;
            foreach(var root in scene.GetRootGameObjects())
            { if(flow==null)flow=root.GetComponentInChildren<FrontendFlowController>(true);if(sky==null)sky=root.GetComponentInChildren<CelestialSystemBehaviour>(true); }
            if(flow==null || sky==null)throw new InvalidOperationException("Frontend or celestial scene reference missing.");
            Undo.RecordObject(flow,"Bind day reset to match lifecycle");flow.ConfigureEnvironment(sky);EditorUtility.SetDirty(flow);
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Frontend audio] Menu {menu.length:F2}s; game {game.length:F2}s; panel {panel.length:F2}s. Profile: {ProfilePath}");
        }
        private static AudioClip Import(string file,bool streaming)
        {
            string path=Root+file;AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(path) as AudioImporter;
            if(importer==null)throw new InvalidOperationException("Audio missing: "+path);
            importer.forceToMono=false;importer.loadInBackground=streaming;
            var settings=importer.defaultSampleSettings;
            settings.loadType=streaming?AudioClipLoadType.Streaming:AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.85f;
            importer.defaultSampleSettings=settings;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
