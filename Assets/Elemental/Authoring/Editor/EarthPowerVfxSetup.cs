using System;
using Elemental.Presentation.VFX;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    /// <summary>Targeted VFX additions; never rebuilds the arena or rewrites UI assets.</summary>
    public static class EarthPowerVfxSetup
    {
        public const string ProfilePath = "Assets/Elemental/Content/Profiles/EarthEffectsTuningProfile.asset";
        public const string AtmospherePath = "Assets/Elemental/Content/Materials/AtmosphereFullscreen.mat";

        [MenuItem("Elemental/VFX/Apply Capture Dust and Sunlight (Preserve Scene)")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before saving VFX settings.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "EarthCoreSlice")
                throw new InvalidOperationException("Open the saved EarthCoreSlice scene first.");
            var profile = AssetDatabase.LoadAssetAtPath<EarthEffectsTuningProfile>(ProfilePath);
            var atmosphere = AssetDatabase.LoadAssetAtPath<Material>(AtmospherePath);
            if (profile == null || atmosphere == null)
                throw new InvalidOperationException("Required effects profile or atmosphere material is missing.");
            EarthMaterialFeedbackHub hub = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<EarthMaterialFeedbackHub>(true);
                if (found != null) { hub = found; break; }
            }
            if (hub == null) throw new InvalidOperationException("Scene has no material feedback hub; existing material pass must be installed first.");
            Undo.RecordObject(profile, "Increase capture dust and chips");
            var serialized = new SerializedObject(profile);
            SetInt(serialized, "materialEvents.dustPerFrame", 512);
            SetInt(serialized, "materialEvents.chipsPerFrame", 192);
            SetInt(serialized, "fracture.dust.maxParticles", 2000);
            SetVector(serialized, "fracture.dust.size", new Vector2(.32f, 1.25f));
            SetVector(serialized, "fracture.dust.lifetime", new Vector2(1.05f, 2.1f));
            SetInt(serialized, "impact.rubble.maxParticles", 768);
            SetVector(serialized, "impact.rubble.lifetime", new Vector2(.65f, 1.35f));
            SetVector(serialized, "impact.rubble.size", new Vector2(.045f, .24f));
            SetInt(serialized, "fracture.minimumCount", 180);
            SetInt(serialized, "fracture.maximumCount", 340);
            serialized.ApplyModifiedProperties();
            TuneEvent(profile, EarthMaterialFeedbackKind.Extract, 256, 96, 1.1f);
            TuneEvent(profile, EarthMaterialFeedbackKind.Fracture, 144, 64, 1.1f);
            TuneEvent(profile, EarthMaterialFeedbackKind.ExtractionSurfaceContact, 22, 12, 1.1f);
            EditorUtility.SetDirty(profile);
            int bindings = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (var capture in root.GetComponentsInChildren<EarthGravityWellFeedback>(true))
                {
                    Undo.RecordObject(capture, "Connect capture dust feedback");
                    capture.ConfigureMaterialFeedback(hub);
                    EditorUtility.SetDirty(capture);
                    bindings++;
                }
            Undo.RecordObject(atmosphere, "Enable artistic sunlight dust shafts");
            atmosphere.SetFloat("_SunDustStrength", .18f);
            atmosphere.SetFloat("_SunDustDistance", 22f);
            atmosphere.SetFloat("_SunDustWidth", 3.5f);
            atmosphere.SetFloat("_SunDustHeight", 9f);
            atmosphere.SetColor("_SunDustColor", new Color(1f, .83f, .58f, 1f));
            EditorUtility.SetDirty(atmosphere);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = profile;
            Debug.Log($"Capture VFX saved: {bindings} gravity feedback bindings; 512 dust / 192 chip frame budget, 2000 broad dust / 768 chip live caps. UI and arena geometry preserved.");
        }

        private static void TuneEvent(EarthEffectsTuningProfile profile, EarthMaterialFeedbackKind kind, int dust, int chips, float size)
        {
            var entry = profile.MaterialEvents.For(kind);
            if (entry == null)
            {
                // Older authored arrays predate the surface-contact event. Append
                // its standard defaults without resetting or reordering user entries.
                var tuning = profile.MaterialEvents;
                int length = tuning.events != null ? tuning.events.Length : 0;
                var expanded = new EarthMaterialEventTuning[length + 1];
                if (length > 0) Array.Copy(tuning.events, expanded, length);
                entry = new EarthMaterialEventTuning { kind = kind };
                expanded[length] = entry;
                tuning.events = expanded;
            }
            entry.dustCount = dust; entry.chipCount = chips; entry.particleSizeScale = size;
        }
        private static void SetInt(SerializedObject target, string path, int value) => target.FindProperty(path).intValue = value;
        private static void SetVector(SerializedObject target, string path, Vector2 value) => target.FindProperty(path).vector2Value = value;

        [MenuItem("Elemental/VFX/Edit Capture Dust and Chips")]
        public static void SelectDust() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<EarthEffectsTuningProfile>(ProfilePath);
        [MenuItem("Elemental/VFX/Edit Sunlight Dust Shafts")]
        public static void SelectSun() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<Material>(AtmospherePath);
        [MenuItem("Elemental/VFX/Edit Gameplay Vignette")]
        public static void SelectGameplayVignette() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<Material>(AtmospherePath);
    }
}
