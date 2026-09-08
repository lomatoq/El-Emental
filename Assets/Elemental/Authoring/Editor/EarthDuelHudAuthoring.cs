using System;
using Elemental.Input.Actions;
using Elemental.Input.Gestures;
using Elemental.Presentation.UI;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Elemental.Authoring.Editor
{
    public static class EarthDuelHudAuthoring
    {
        public const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        public static void InstallAll()
        {
            InstallSavedScene();
            EarthSpinKickClipAuthoring.Bake();
            EarthQuickStoneComboAuthoring.ConfigureSavedController();
        }
        [MenuItem("Elemental/UI/Install Duel HUD in Saved Scene")]
        public static void InstallSavedScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before installing duel UI.");
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Install(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[DuelHud] Saved explicit match/HUD bindings; arena geometry was not rebuilt.");
        }
        public static void Install(Scene scene)
        {
            var duel = Unique<EarthMvpDuelController>(scene);
            var magic = Unique<MagicInputController>(scene);
            var executor = Unique<MagicExecutor>(scene);
            var gate = Unique<EarthSceneReadinessGate>(scene);
            var old = Unique<EarthCoreHud>(scene);
            var router = magic.GetComponent<EarthActionRouterBehaviour>();
            if (router == null || duel.PlayerTransform == null || executor.PlanetCenterTransform == null)
                throw new InvalidOperationException("Duel HUD needs the authored player, action router and planet center.");
            var shots = magic.GetComponent<EarthDualMouseAbilityController>() ?? magic.gameObject.AddComponent<EarthDualMouseAbilityController>();
            var camera = new SerializedObject(magic).FindProperty("castCamera").objectReferenceValue as UnityEngine.Camera;
            if (camera == null) throw new InvalidOperationException("Player magic camera must be authored before installing HUD.");
            shots.Configure(executor, Unique<EarthPillarWavePool>(scene), magic.GetComponent<PlanetMotor>(), camera, magic.GetComponent<Rigidbody>());
            magic.enabled = true;
            shots.enabled = true;
            EarthArenaStructure mainArena = null;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var structure in root.GetComponentsInChildren<EarthArenaStructure>(true))
                if (mainArena == null || structure.PieceCount > mainArena.PieceCount) mainArena = structure;
            if (mainArena == null) throw new InvalidOperationException("No authored arena found for globe marker.");
            var hud = old.GetComponent<EarthDuelHud>() ?? old.gameObject.AddComponent<EarthDuelHud>();
            hud.Configure(duel, gate, magic, executor, shots,
                duel.PlayerTransform, executor.PlanetCenterTransform, mainArena.transform,
                magic.GetComponent<EarthPillarMobility>(), magic.GetComponent<EarthPillarWaveAbility>());
            old.Configure(magic, executor, magic.GetComponent<EarthPillarMobility>(), magic.GetComponent<EarthLandingCushion>());
            // Legacy diagnostics remain available without paying their string/layout cost in normal gameplay.
            old.enabled = true;
            old.ShowDiagnostics = false;
            var diagnostics = old.GetComponent<BendingDebugOverlay>();
            if (diagnostics != null) diagnostics.Configure(magic, executor);
            router.BindDuel(duel);
            magic.BindDuel(duel);
            EditorUtility.SetDirty(magic);
            magic.GetComponent<EarthDualMouseAbilityController>()?.BindDuel(duel);
            duel.ConfigureRoundControls(magic, magic.GetComponent<EarthDualMouseAbilityController>());
            var panel = old.GetComponent<UIDocument>().panelSettings;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.match = 1f;
            EditorUtility.SetDirty(panel); EditorUtility.SetDirty(hud); EditorUtility.SetDirty(old);
            EditorUtility.SetDirty(router);
            EditorUtility.SetDirty(duel);
            if (magic.GetComponent<EarthDualMouseAbilityController>() != null)
                EditorUtility.SetDirty(magic.GetComponent<EarthDualMouseAbilityController>());
        }
        private static T Unique<T>(Scene scene) where T : Component
        {
            T found = null;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var candidate in root.GetComponentsInChildren<T>(true))
            {
                if (found != null) throw new InvalidOperationException($"Expected exactly one {typeof(T).Name} in {scene.name}.");
                found = candidate;
            }
            return found != null ? found : throw new InvalidOperationException($"Missing {typeof(T).Name} in {scene.name}.");
        }
    }
}
