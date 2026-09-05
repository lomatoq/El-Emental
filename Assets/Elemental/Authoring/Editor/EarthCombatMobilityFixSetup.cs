using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Presentation.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    public static class EarthCombatMobilityFixSetup
    {
        [MenuItem("Elemental/Setup/Apply Combat and Mobility Fixes (Preserve Scene)")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Stop Play before applying saved-scene bindings.");
            WireScene();
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("[Earth fixes] Bound cumulative fracture, natural stones and pillar effects; preserved authored scene objects and settings.");
        }

        public static void WireScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "EarthCoreSlice") return;
            EarthRockDebrisPool debris = null;
            EarthMaterialFeedbackHub hub = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (debris == null) debris = root.GetComponentInChildren<EarthRockDebrisPool>(true);
                if (hub == null) hub = root.GetComponentInChildren<EarthMaterialFeedbackHub>(true);
            }
            if (debris == null || hub == null)
                throw new System.InvalidOperationException("EarthCoreSlice needs its authored debris pool and material-feedback hub.");
            foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component is EarthArenaStructure arena) arena.ConfigureRockBreakup(debris);
                else if (component is EarthWallPool walls) walls.ConfigureNaturalFracture(debris);
                else if (component is EarthLandingCushion cushion) cushion.ConfigureMaterialFeedback(hub);
                else if (component is EarthPillarFeedback pillar) pillar.ConfigureMaterialFeedback(hub);
                else continue;
                EditorUtility.SetDirty(component);
            }
            foreach (string name in new[] { "EarthEffectsTuningProfile", "EarthSurfProfile", "EarthPillarWaveProfile" })
            {
                var profile = AssetDatabase.LoadMainAssetAtPath("Assets/Elemental/Content/Profiles/" + name + ".asset");
                if (profile != null) EditorUtility.SetDirty(profile);
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }

        [MenuItem("Elemental/Tuning/Wave Animation")]
        public static void ShowWaveAnimation()
        {
            var profile = AssetDatabase.LoadMainAssetAtPath("Assets/Elemental/Content/Profiles/EarthPillarWaveProfile.asset");
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
        }
    }
}
