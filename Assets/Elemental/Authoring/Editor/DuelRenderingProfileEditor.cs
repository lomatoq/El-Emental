using Elemental.Presentation.Rendering;
using Elemental.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    [CustomEditor(typeof(DuelRenderingProfile))]
    public sealed class DuelRenderingProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(10f);
            EditorGUILayout.HelpBox(
                "Material values are edited on the three referenced materials. " +
                "Shadow values are staged here; press Apply & Save to push them " +
                "to the open camera, Sun, arena renderers and voxel planet.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("Apply & Save To Open Scene", GUILayout.Height(30f)))
                    DuelRenderingProfileSceneApplier.ApplyAndSave(
                        (DuelRenderingProfile)target);
            }
        }
    }

    public static class DuelRenderingProfileSceneApplier
    {
        private const string ProfilePath =
            "Assets/Elemental/Content/GraphicsVNext/Rendering/DuelRenderingProfile.asset";

        [MenuItem("Elemental/Rendering/Apply & Save Duel Rendering Profile")]
        public static void ApplySelectedProfile()
        {
            DuelRenderingProfile profile = Selection.activeObject as DuelRenderingProfile ??
                AssetDatabase.LoadAssetAtPath<DuelRenderingProfile>(ProfilePath);
            if (profile == null)
            {
                Debug.LogError("DuelRenderingProfile asset was not found.");
                return;
            }
            ApplyAndSave(profile);
            Selection.activeObject = profile;
        }

        public static void ApplyAndSave(DuelRenderingProfile profile)
        {
            if (profile == null || Application.isPlaying) return;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("No loaded scene is available for rendering-profile application.");
                return;
            }

            int cameras = ApplyCameras(profile);
            int lights = ApplySun(profile);
            int arenaRenderers = ApplyArena(profile);
            int planetRenderers = ApplyPlanet(profile);
            int removedLegacyBindings = RemoveLegacyUnifiedBindings();

            EditorUtility.SetDirty(profile);
            SaveReferencedMaterial(profile.ArenaExteriorMaterial);
            SaveReferencedMaterial(profile.ArenaInteriorMaterial);
            SaveReferencedMaterial(profile.PlanetSurfaceMaterial);
            AssetDatabase.SaveAssetIfDirty(profile);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            SceneView.RepaintAll();

            Debug.Log(
                $"[Elemental] Applied and saved DuelRenderingProfile: " +
                $"cameras={cameras}, lights={lights}, arenaRenderers={arenaRenderers}, " +
                $"planetRenderers={planetRenderers}, " +
                $"removedLegacyBindings={removedLegacyBindings}.");
        }

        private static int ApplyCameras(DuelRenderingProfile profile)
        {
            int count = 0;
            Camera[] all = Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include);
            for (int index = 0; index < all.Length; index++)
            {
                Camera camera = all[index];
                if (camera == null || !camera.gameObject.scene.IsValid()) continue;
                if (!camera.CompareTag("MainCamera") && camera.name != "Gravity Toy Camera")
                    continue;
                Undo.RecordObject(camera, "Apply duel shadow camera settings");
                UniversalAdditionalCameraData data =
                    camera.GetUniversalAdditionalCameraData();
                Undo.RecordObject(data, "Apply duel shadow camera settings");
                data.renderShadows = profile.RenderRealtimeDirectionalShadows;
                data.requiresDepthTexture = true;
                EditorUtility.SetDirty(data);
                count++;
            }
            return count;
        }

        private static int ApplySun(DuelRenderingProfile profile)
        {
            Light sun = RenderSettings.sun ?? GameObject.Find("Sun")?.GetComponent<Light>();
            if (sun == null) return 0;
            Undo.RecordObject(sun, "Apply duel Sun shadow settings");
            sun.shadows = profile.SunShadowType;
            sun.shadowStrength = profile.SunShadowStrength;
            RenderSettings.sun = sun;
            EditorUtility.SetDirty(sun);
            return 1;
        }

        private static int ApplyArena(DuelRenderingProfile profile)
        {
            GameObject root = GameObject.Find("Broken Crown Arena");
            if (root == null) return 0;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            int count = 0;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null) continue;
                Undo.RecordObject(renderer, "Apply arena shadow settings");
                renderer.shadowCastingMode = profile.ArenaShadowCastingMode;
                renderer.receiveShadows = profile.ArenaReceiveShadows;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                EditorUtility.SetDirty(renderer);
                count++;
            }
            return count;
        }

        private static int ApplyPlanet(DuelRenderingProfile profile)
        {
            VoxelPlanetBehaviour planet = Object.FindAnyObjectByType<VoxelPlanetBehaviour>(
                FindObjectsInactive.Include);
            if (planet == null) return 0;
            Undo.RecordObject(planet, "Apply voxel planet shadow settings");
            planet.ConfigureRendering(
                profile.PlanetShadowCastingMode,
                profile.PlanetReceiveShadows);
            EditorUtility.SetDirty(planet);

            Renderer[] renderers = planet.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                Undo.RecordObject(renderer, "Apply voxel planet shadow settings");
                renderer.shadowCastingMode = profile.PlanetShadowCastingMode;
                renderer.receiveShadows = profile.PlanetReceiveShadows;
                EditorUtility.SetDirty(renderer);
            }
            return renderers.Length;
        }

        private static int RemoveLegacyUnifiedBindings()
        {
            int count = 0;
            UnifiedLightingRendererBinding[] bindings =
                Object.FindObjectsByType<UnifiedLightingRendererBinding>(
                    FindObjectsInactive.Include);
            for (int index = 0; index < bindings.Length; index++)
            {
                UnifiedLightingRendererBinding binding = bindings[index];
                if (binding == null || !binding.gameObject.scene.IsValid()) continue;
                Undo.DestroyObjectImmediate(binding);
                count++;
            }

            UnifiedLightingMaterialBinder[] binders =
                Object.FindObjectsByType<UnifiedLightingMaterialBinder>(
                    FindObjectsInactive.Include);
            for (int index = 0; index < binders.Length; index++)
            {
                UnifiedLightingMaterialBinder binder = binders[index];
                if (binder == null || !binder.gameObject.scene.IsValid()) continue;
                Undo.DestroyObjectImmediate(binder);
                count++;
            }
            return count;
        }

        private static void SaveReferencedMaterial(Material material)
        {
            if (material == null) return;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
        }
    }
}
