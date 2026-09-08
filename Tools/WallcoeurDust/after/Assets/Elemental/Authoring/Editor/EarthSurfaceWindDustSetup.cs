using System;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    public static class EarthSurfaceWindDustSetup
    {
        public const string ProfilePath = "Assets/Elemental/Content/Profiles/EarthSurfaceWindDustProfile.asset";
        [MenuItem("Elemental/VFX/Install Surface Wind Dust (Preserve Scene)")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before installing surface wind dust.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "EarthCoreSlice") throw new InvalidOperationException("Open the saved EarthCoreSlice scene first.");
            CelestialSystemBehaviour sky = null; EarthSurfaceWindDust dust = null; PlanetMotor motor = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (sky == null) sky = root.GetComponentInChildren<CelestialSystemBehaviour>(true);
                if (dust == null) dust = root.GetComponentInChildren<EarthSurfaceWindDust>(true);
                if (motor == null) motor = root.GetComponentInChildren<PlanetMotor>(true);
            }
            if (sky == null || sky.LightingAnchor == null || motor == null) throw new InvalidOperationException("Saved arena needs its explicit sky anchor and player ground mask.");
            var skyBindings = new SerializedObject(sky);
            var planet = skyBindings.FindProperty("planet").objectReferenceValue as Transform;
            var effects = AssetDatabase.LoadAssetAtPath<EarthEffectsTuningProfile>("Assets/Elemental/Content/Profiles/EarthEffectsTuningProfile.asset");
            if (planet == null || effects == null || effects.Materials.SurfDust == null) throw new InvalidOperationException("Planet or existing production dust material is missing.");
            var profile = AssetDatabase.LoadAssetAtPath<EarthSurfaceWindDustProfile>(ProfilePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<EarthSurfaceWindDustProfile>(); AssetDatabase.CreateAsset(profile, ProfilePath); }
            if (dust == null)
            {
                var host = new GameObject("Surface Wind Dust"); Undo.RegisterCreatedObjectUndo(host, "Install surface wind dust");
                dust = host.AddComponent<EarthSurfaceWindDust>();
            }
            Undo.RecordObject(dust, "Bind surface wind dust");
            dust.Configure(profile, sky.LightingAnchor, planet, motor.GroundMask, effects.Materials.SurfDust);
            EditorUtility.SetDirty(dust); EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene); Selection.activeObject = profile;
            Debug.Log("Surface wind dust installed without arena regeneration. Existing profile values and materials preserved. Elemental > VFX > Edit Surface Wind Dust.");
        }
        [MenuItem("Elemental/VFX/Edit Surface Wind Dust")]
        public static void SelectProfile() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<EarthSurfaceWindDustProfile>(ProfilePath);
    }
}
