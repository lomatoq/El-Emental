using System;
using System.IO;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Camera;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.UI;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace Elemental.Authoring.Editor
{
    public static class AlphaFrontendSetup
    {
        public const string ThemePath = "Assets/Elemental/Content/UI/Frontend/ElementalUITheme.asset";
        private const string RootPath = "Assets/Elemental/Content/UI/Frontend/";
        [MenuItem("Elemental/UI/Select In-Game HUD Theme")]
        public static void SelectHudTheme()
        { Selection.activeObject = AssetDatabase.LoadAssetAtPath<ElementalUITheme>(ThemePath); EditorGUIUtility.PingObject(Selection.activeObject); }

        [MenuItem("Elemental/UI/Apply Elemental App Icon")]
        public static void ApplyAppIcon()
        {
            var logo = AssetDatabase.LoadAssetAtPath<Texture2D>(RootPath + "ElementalLogo.png");
            if (logo == null) throw new InvalidOperationException("The supplied Elemental logo is missing.");
            foreach (var target in new[] { UnityEditor.Build.NamedBuildTarget.Unknown, UnityEditor.Build.NamedBuildTarget.Standalone })
            {
                int count = Mathf.Max(1, PlayerSettings.GetIconSizes(target, IconKind.Any).Length);
                var icons = new Texture2D[count];
                for (int i = 0; i < count; i++) icons[i] = logo;
                PlayerSettings.SetIcons(target, icons, IconKind.Any);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Elemental] Saved original Elemental logo as default and Windows application icons.");
        }

        [MenuItem("Elemental/UI/Install Alpha Frontend")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Install outside Play Mode.");
            Scene scene = SceneManager.GetSceneByPath("Assets/Elemental/Content/Scenes/EarthCoreSlice.unity");
            if (!scene.isLoaded) throw new InvalidOperationException("Open the existing EarthCoreSlice scene first.");
            var duel = Find<EarthMvpDuelController>(scene); var gate = Find<EarthSceneReadinessGate>(scene);
            var hud = Find<EarthDuelHud>(scene); var director = Find<EarthCameraDirector>(scene);
            var cameraController = Find<EarthCinemachineCameraController>(scene);
            if (duel == null || director == null || director.Player == null || cameraController == null) throw new InvalidOperationException("Existing arena/character/camera references are required.");
            var brain = Find<CinemachineBrain>(scene); var output = brain.GetComponent<UnityEngine.Camera>();
            ElementalUITheme theme = PrepareTheme();
            FrontendFlowController flow = Find<FrontendFlowController>(scene);
            GameObject root = flow != null ? flow.gameObject : new GameObject("Alpha Frontend");
            SceneManager.MoveGameObjectToScene(root, scene);
            flow ??= Undo.AddComponent<FrontendFlowController>(root);
            var audio = root.GetComponent<UIAudioFeedback>() ?? Undo.AddComponent<UIAudioFeedback>(root);
            var menuCamera = root.GetComponent<CinematicMenuCamera>() ?? Undo.AddComponent<CinematicMenuCamera>(root);
            var view = root.GetComponentInChildren<FrontendMenuView>(true);
            if (view == null) { var go = new GameObject("Frontend Canvas", typeof(RectTransform)); go.transform.SetParent(root.transform, false); view = Undo.AddComponent<FrontendMenuView>(go); }
            var vcam = root.GetComponentInChildren<CinemachineCamera>(true);
            if (vcam == null) { var go = new GameObject("Cinematic Menu Camera"); go.transform.SetParent(root.transform, false); vcam = Undo.AddComponent<CinemachineCamera>(go); }
            vcam.Priority = -1000; vcam.gameObject.SetActive(false);
            menuCamera.Configure(vcam, brain, output, Find<EarthChargeCameraLookdevV2>(scene),
                output.GetComponent<EarthCinematicDepthOfFieldController>(), director.Player.GetComponentInChildren<EarthAnimationDriver>(true),
                director.Player.GetComponent<PlanetMotor>(), director.Player, duel.BotTransform, cameraController.VirtualCamera, cameraController);
            flow.Configure(theme, view, audio, menuCamera, hud, duel, gate, director);
            flow.ConfigureEnvironment(Find<Elemental.Presentation.Rendering.CelestialSystemBehaviour>(scene));
            var debug = new System.Collections.Generic.List<Behaviour>();
            foreach (var sceneRoot in scene.GetRootGameObjects()) debug.AddRange(sceneRoot.GetComponentsInChildren<BendingDebugOverlay>(true));
            flow.ConfigureDebugOverlays(debug.ToArray());
            if (Find<EventSystem>(scene) == null)
            {
                var events = new GameObject("Frontend Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(root.transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            EditorUtility.SetDirty(flow); EditorUtility.SetDirty(menuCamera); EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("[Elemental] Saved alpha frontend on existing arena. User animation controller/clip assignments untouched.");
        }
        public static ElementalUITheme PrepareTheme()
        {
            ElementalUITheme theme = AssetDatabase.LoadAssetAtPath<ElementalUITheme>(ThemePath);
            if (theme == null) { theme = ScriptableObject.CreateInstance<ElementalUITheme>(); AssetDatabase.CreateAsset(theme, ThemePath); }
            var readable = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (readable == null) throw new InvalidOperationException("Import TMP Essential Resources first.");
            theme.readableFont = readable;
            theme.displayFont = FontAsset("DonGraffiti", "DonGraffiti.otf", readable);
            theme.labelFont = FontAsset("Varose", "Varose-Regular.otf", readable);
            theme.hudFont = AssetDatabase.LoadAssetAtPath<Font>(RootPath + "Fonts/Varose-Regular.otf");
            theme.logo = Sprite(RootPath + "ElementalLogo.png");
            theme.panelDetail = Sprite(RootPath + "Details/CharacterCreate_Separator.png");
            theme.hover = Clip("Hover"); theme.press = Clip("Press"); theme.confirm = Clip("Confirm"); theme.back = Clip("Back");
            theme.error = Clip("Error"); theme.copy = Clip("Copy"); theme.connect = Clip("Connect");
            EditorUtility.SetDirty(theme); AssetDatabase.SaveAssets(); return theme;
        }
        private static TMP_FontAsset FontAsset(string name, string source, TMP_FontAsset fallback)
        {
            string path = RootPath + "Fonts/" + name + " SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path); if (existing != null) return existing;
            Font font = AssetDatabase.LoadAssetAtPath<Font>(RootPath + "Fonts/" + source);
            if (font == null) throw new InvalidOperationException("Missing font: " + source);
            var asset = TMP_FontAsset.CreateFontAsset(font, 80, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (asset == null) throw new InvalidOperationException("TMP could not create font: " + source);
            asset.name = name + " SDF";
            asset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { fallback };
            AssetDatabase.CreateAsset(asset, path);
            if (asset.material != null) AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (var atlas in asset.atlasTextures) if (atlas != null) AssetDatabase.AddObjectToAsset(atlas, asset);
            asset.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 /:.!?+-()%", out string missing);
            EditorUtility.SetDirty(asset); return asset;
        }
        private static Sprite Sprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            if (importer.textureType != TextureImporterType.Sprite)
            { importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(RootPath + "Audio/" + name + ".wav");
        private static T Find<T>(Scene scene) where T : Component
        { foreach (var root in scene.GetRootGameObjects()) { var value = root.GetComponentInChildren<T>(true); if (value != null) return value; } return null; }
    }
}
