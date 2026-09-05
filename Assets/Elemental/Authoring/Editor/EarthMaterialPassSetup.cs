using System.Collections.Generic;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Geometry;
using Elemental.Runtime.Matter;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Presentation.Animation;
using Elemental.Presentation.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    public static class EarthMaterialPassSetup
    {
        private const string Generated = "Assets/Elemental/Content/Generated/MaterialPass";
        [MenuItem("Elemental/Setup/Integrate Earth Material Pass (Preserve Scene)")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play before integrating the saved scene.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "EarthCoreSlice") throw new System.InvalidOperationException("Open EarthCoreSlice; integration does not replace other scenes.");
            var profile = AssetDatabase.LoadAssetAtPath<EarthEffectsTuningProfile>("Assets/Elemental/Content/Profiles/EarthEffectsTuningProfile.asset");
            var rockProfile = AssetDatabase.LoadAssetAtPath<EarthRockProfile>("Assets/Elemental/Content/Profiles/EarthRockProfile.asset");
            if (rockProfile != null) EditorUtility.SetDirty(rockProfile); // Serialize newly added controls without overwriting authored values.
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Elemental/Content/GraphicsV5/Materials/RumbleSandstone.mat");
            VoxelPlanetBehaviour planet = null; GravityWorldBehaviour gravity = null; EarthSurfaceQueryService surfaces = null; EarthRockDebrisPool debris = null;
            EarthMatterKernelBehaviour matterKernel = null;
            var motors = new List<PlanetMotor>();
            var exclusions = new List<Bounds>();
            var arenaRenderers = new List<Renderer>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (planet == null) planet = root.GetComponentInChildren<VoxelPlanetBehaviour>(true);
                if (gravity == null) gravity = root.GetComponentInChildren<GravityWorldBehaviour>(true);
                if (surfaces == null) surfaces = root.GetComponentInChildren<EarthSurfaceQueryService>(true);
                if (debris == null) debris = root.GetComponentInChildren<EarthRockDebrisPool>(true);
                if (matterKernel == null) matterKernel = root.GetComponentInChildren<EarthMatterKernelBehaviour>(true);
                motors.AddRange(root.GetComponentsInChildren<PlanetMotor>(true));
                if (root.name == "Broken Crown Arena")
                {
                    bool first = true; Bounds bounds = default;
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    {
                        if (renderer is ParticleSystemRenderer || renderer is LineRenderer) continue;
                        arenaRenderers.Add(renderer);
                        if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                        if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
                    }
                    if (!first) { bounds.Expand(6f); exclusions.Add(bounds); }
                }
            }
            if (profile == null || material == null || planet == null || gravity == null || surfaces == null || debris == null || matterKernel == null)
                throw new System.InvalidOperationException("Material pass requires existing effects, planet, surfaces, gravity and rock debris pool; no scene rebuild was attempted.");
            EditorUtility.SetDirty(profile); // Persist new per-event defaults alongside untouched user layer/material values.
            foreach (var renderer in arenaRenderers)
            {
                Undo.RecordObject(renderer, "Restore arena shadows");
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                EditorUtility.SetDirty(renderer);
            }
            foreach (var motor in motors) exclusions.Add(new Bounds(motor.transform.position, Vector3.one * 8f));
            const string bevelProfilePath = "Assets/Elemental/Content/Profiles/EarthStoneBevelProfile.asset";
            var bevelProfile = AssetDatabase.LoadAssetAtPath<EarthStoneBevelProfile>(bevelProfilePath);
            if (bevelProfile == null)
            {
                bevelProfile = ScriptableObject.CreateInstance<EarthStoneBevelProfile>();
                AssetDatabase.CreateAsset(bevelProfile, bevelProfilePath);
            }
            EnsureFolder(Generated);
            var visual = new Mesh[12]; var colliders = new Mesh[12];
            for (int i = 0; i < visual.Length; i++)
            {
                string basePath = Generated + "/Rock" + i + "Collider.asset";
                colliders[i] = AssetDatabase.LoadAssetAtPath<Mesh>(basePath);
                if (colliders[i] == null) { colliders[i] = EarthRockMeshFactory.Create((EarthRockArchetype)i, 0xD15300u + (uint)i); colliders[i].hideFlags = HideFlags.None; AssetDatabase.CreateAsset(colliders[i], basePath); }
                if (colliders[i].hideFlags != HideFlags.None)
                {
                    colliders[i].hideFlags = HideFlags.None;
                    EditorUtility.SetDirty(colliders[i]);
                }
                string renderPath = Generated + "/Rock" + i + "Bevel.asset";
                visual[i] = AssetDatabase.LoadAssetAtPath<Mesh>(renderPath);
                Mesh rebuilt = EarthFractureBevelMeshBuilder.Create(colliders[i], bevelProfile.Width, bevelProfile.MaxLocalEdgeFraction);
                rebuilt.hideFlags = HideFlags.None;
                rebuilt.name = "Rock" + i + "Bevel";
                if (visual[i] == null) { visual[i] = rebuilt; AssetDatabase.CreateAsset(visual[i], renderPath); }
                else { EditorUtility.CopySerialized(rebuilt, visual[i]); Object.DestroyImmediate(rebuilt); EditorUtility.SetDirty(visual[i]); }
                if (visual[i].hideFlags != HideFlags.None) { visual[i].hideFlags = HideFlags.None; EditorUtility.SetDirty(visual[i]); }
            }
            GameObject feedbackRoot = FindRoot(scene, "Earth Material Feedback") ?? NewRoot("Earth Material Feedback", scene);
            var hub = Ensure<EarthMaterialFeedbackHub>(feedbackRoot);
            hub.Configure(profile, motors.Count > 0 ? motors[0].transform : null);
            var presenter = Ensure<EarthMaterialFeedbackPresenter>(feedbackRoot);
            var dust = Ensure<ParticleSystem>(Child(feedbackRoot.transform, "Material Contact Dust"));
            var chips = Ensure<ParticleSystem>(Child(feedbackRoot.transform, "Material Contact Chips"));
            var fractureDust = Ensure<ParticleSystem>(Child(feedbackRoot.transform, "Material Fracture Dust"));
            dust.GetComponent<ParticleSystemRenderer>().sharedMaterial = profile.Materials.ImpactDust;
            chips.GetComponent<ParticleSystemRenderer>().sharedMaterial = profile.Materials.ImpactRubble;
            fractureDust.GetComponent<ParticleSystemRenderer>().sharedMaterial = profile.Materials.FractureDust;
            presenter.Configure(hub, profile, planet.transform, dust, chips, visual[0], fractureDust);
            EditorUtility.SetDirty(hub); EditorUtility.SetDirty(presenter);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null) continue;
                    var serialized = new SerializedObject(component);
                    SetReference(serialized, "materialFeedback", hub, false);
                    SetReference(serialized, "effectsProfile", profile, true);
                    SetReference(serialized, "gravityLaunchWorld", gravity, false);
                    SetReference(serialized, "stoneBevelProfile", bevelProfile, true);
                    if (component is EarthRockDebrisPool) SetReference(serialized, "matterKernel", matterKernel, true);
                    if (component is EarthWallPool || component is EarthArenaStructure)
                        SetReference(serialized, "rockDebrisPool", debris, true);
                    if (component is MeteorShowerBehaviour || component is EarthDestructibleDecorRock)
                        SetReference(serialized, "debrisPool", debris, true);
                    if (component is EarthMagicFeedback)
                    {
                        SetReference(serialized, "dust", component.transform.Find("Chunky Earth Dust")?.GetComponent<ParticleSystem>(), true);
                        SetReference(serialized, "rubble", component.transform.Find("Loose Earth Chips")?.GetComponent<ParticleSystem>(), true);
                        SetReference(serialized, "sparks", component.transform.Find("Amber Shards")?.GetComponent<ParticleSystem>(), true);
                    }
                    serialized.ApplyModifiedProperties();
                }
                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                {
                    var data = camera.GetUniversalAdditionalCameraData();
                    Undo.RecordObject(data, "Restore authored camera shadows"); data.renderShadows = true; EditorUtility.SetDirty(data);
                }
            }
            foreach (var motor in motors)
            {
                var presentation = motor.GetComponentInChildren<HumanoidCharacterPresentation>(true);
                var actorFeedback = Ensure<EarthCharacterGroundFeedback>(motor.gameObject);
                actorFeedback.Configure(motor, motor.GetComponent<EarthPillarMobility>(), hub, presentation != null ? presentation.Animator : null);
                EditorUtility.SetDirty(actorFeedback);
            }
            // Only the shadow-specific material switch changes, never the user's palette.
            foreach (string path in new[] { "Assets/Elemental/Content/GraphicsV5/Materials/RumbleArenaSandstone.mat" })
            {
                var arenaMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (arenaMaterial != null && arenaMaterial.HasProperty("_SideShadowFade"))
                { Undo.RecordObject(arenaMaterial, "Restore arena self-shadow receivers"); arenaMaterial.SetFloat("_SideShadowFade", 0f); EditorUtility.SetDirty(arenaMaterial); }
            }
            QualitySettings.shadows = UnityEngine.ShadowQuality.All;
            var scatterProfile = AssetDatabase.LoadAssetAtPath<EarthPlanetRockScatterProfile>("Assets/Elemental/Content/Profiles/EarthPlanetRockScatterProfile.asset");
            if (scatterProfile == null)
            {
                scatterProfile = ScriptableObject.CreateInstance<EarthPlanetRockScatterProfile>();
                AssetDatabase.CreateAsset(scatterProfile, "Assets/Elemental/Content/Profiles/EarthPlanetRockScatterProfile.asset");
            }
            var scatter = Ensure<EarthPlanetRockScatter>(planet.gameObject);
            scatter.Configure(scatterProfile, planet, surfaces, gravity, debris, hub, material, visual, colliders, exclusions.ToArray());
            EditorUtility.SetDirty(scatter);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("[Earth Material Pass] Integrated bindings, shadows and deterministic scatter without moving authored scene objects.");
        }
        private static void SetReference(SerializedObject so, string name, Object value, bool onlyMissing)
        {
            var p = so.FindProperty(name);
            if (p == null || p.propertyType != SerializedPropertyType.ObjectReference || value == null || (onlyMissing && p.objectReferenceValue != null)) return;
            p.objectReferenceValue = value;
        }
        private static T Ensure<T>(GameObject go) where T : Component
        {
            // Unity's destroyed/native-null wrappers are not C# null. In particular,
            // GetComponent<ParticleSystem>() must use Unity's overloaded null test.
            T component = go.GetComponent<T>();
            if (component == null) component = Undo.AddComponent<T>(go);
            if (component == null) throw new System.InvalidOperationException("Could not add " + typeof(T).Name + " to " + go.name);
            return component;
        }
        private static GameObject Child(Transform root, string name)
        {
            Transform old = root.Find(name); if (old != null) return old.gameObject;
            var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Add material feedback view"); go.transform.SetParent(root, false); return go;
        }
        private static GameObject NewRoot(string name, Scene scene) { var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, name); SceneManager.MoveGameObjectToScene(go, scene); return go; }
        private static GameObject FindRoot(Scene scene, string name) { foreach (var root in scene.GetRootGameObjects()) if (root.name == name) return root; return null; }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, slash)); AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
