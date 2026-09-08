using System;
using Elemental.Presentation.DistantScenery;
using Elemental.Runtime.World;
using Elemental.Runtime.Characters;
using Elemental.Presentation.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    public static class DistantBackdropInstaller
    {
        private const string Root = "Assets/Elemental/Content/Environment/DistantStone";
        [MenuItem("Elemental/Environment/Install Distant Stone Backdrop")]
        public static void InstallActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "EarthCoreSlice")
                throw new InvalidOperationException("Open EarthCoreSlice before installing the optional distant backdrop. Existing scenes are never replaced.");
            Install(scene);
        }
        public static DistantBackdrop Install(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) throw new ArgumentException("A loaded scene is required.");
            VoxelPlanetBehaviour planet = null;
            DistantBackdrop existing = null;
            UnityEngine.Camera camera = null;
            FrontendFlowController settings = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (var candidate in root.GetComponentsInChildren<DistantBackdrop>(true))
                { if (existing != null) throw new InvalidOperationException("Multiple distant backdrops already exist; select their intended owner explicitly."); existing = candidate; }
                foreach (var candidate in root.GetComponentsInChildren<VoxelPlanetBehaviour>(true))
                { if (planet != null) throw new InvalidOperationException("Multiple voxel planets: an explicit backdrop anchor is required."); planet = candidate; }
                foreach (var candidate in root.GetComponentsInChildren<UnityEngine.Camera>(true))
                    if (candidate.CompareTag("MainCamera")) camera = candidate;
                foreach (var candidate in root.GetComponentsInChildren<FrontendFlowController>(true))
                { if(settings!=null)throw new InvalidOperationException("Multiple frontend preference owners: bind the backdrop explicitly.");settings=candidate; }
            }
            // Re-running the installer preserves authored profile settings and generated transforms.
            if (existing != null)
            {
                if(existing.SettingsSource!=settings && settings!=null)
                { existing.BindSettings(settings);EditorUtility.SetDirty(existing);EditorSceneManager.MarkSceneDirty(scene); }
                Selection.activeObject = existing; return existing;
            }
            if (planet == null) throw new InvalidOperationException("The scene needs its existing VoxelPlanetBehaviour; this installer never creates a planet.");
            Transform arenaAnchor = null;
            float nearestSurface = float.PositiveInfinity;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (var motor in root.GetComponentsInChildren<PlanetMotor>(true))
                {
                    if (!motor.gameObject.activeInHierarchy) continue;
                    float error = Mathf.Abs(Vector3.Distance(motor.transform.position, planet.transform.position) - planet.Radius);
                    if (error < nearestSurface) { nearestSurface = error; arenaAnchor = motor.transform; }
                }
            DistantBackdropProfile profile = BuildAssets(planet.Radius);
            var owner = new GameObject("Distant Stone Backdrop");
            Undo.RegisterCreatedObjectUndo(owner, "Install distant stone backdrop");
            SceneManager.MoveGameObjectToScene(owner, scene);
            var backdrop = owner.AddComponent<DistantBackdrop>();
            Vector3 up = arenaAnchor != null ? arenaAnchor.position - planet.transform.position :
                camera != null ? camera.transform.position - planet.transform.position : Vector3.up;
            if (up.sqrMagnitude < 0.01f) up = Vector3.up;
            backdrop.Configure(profile, planet.transform, up.normalized);
            backdrop.BindSettings(settings);
            backdrop.Rebuild();
            EditorUtility.SetDirty(backdrop); EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeObject = owner;
            return backdrop;
        }
        public static DistantBackdropProfile BuildAssets(float planetRadius)
        {
            EnsureFolder(Root + "/Meshes");
            const string profilePath = Root + "/DistantBackdropProfile.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DistantBackdropProfile>(profilePath);
            if (existing != null) return existing;
            var shader = Shader.Find("Elemental/Environment/DistantStoneURP");
            if (shader == null) throw new InvalidOperationException("Import the adapted DistantStoneURP shader first.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/DistantStone.mat");
            if (material == null)
            {
                material = new Material(shader) { name = "Distant Stone (shared atmosphere)" };
                material.SetColor("_BaseColor", new Color(.48f,.37f,.29f,1));
                material.SetColor("_ShadowTint", new Color(.38f,.48f,.61f,1));
                material.SetFloat("_DetailStrength", .08f);
                var detail = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Elemental/Content/UI/Stone/Art/Environment/distant_stone_detail.png");
                if (detail != null) material.SetTexture("_DetailTex", detail);
                AssetDatabase.CreateAsset(material, Root + "/DistantStone.mat");
            }
            var profile = ScriptableObject.CreateInstance<DistantBackdropProfile>();
            profile.planetRadius = planetRadius; profile.material = material;
            profile.silhouettes = new Mesh[6]; profile.lodSilhouettes = new Mesh[6];
            profile.islandSilhouettes = new Mesh[4]; profile.islandLodSilhouettes = new Mesh[4];
            for (int i = 0; i < 10; i++)
                for (int lod = 0; lod < 2; lod++)
                {
                    bool island = i >= 6;
                    string path = Root + "/Meshes/" + (island ? "Island_" : "Massif_") + i + "_LOD" + lod + ".asset";
                    Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (mesh == null)
                    {
                        mesh = ProceduralRockMesh.Create(620 + i * 131, lod == 0 ? 9 + i % 5 : 6, island, lod == 0 ? 6 : 4);
                        AssetDatabase.CreateAsset(mesh, path);
                    }
                    if (island) { if (lod == 0) profile.islandSilhouettes[i-6] = mesh; else profile.islandLodSilhouettes[i-6] = mesh; }
                    else { if (lod == 0) profile.silhouettes[i] = mesh; else profile.lodSilhouettes[i] = mesh; }
                }
            AssetDatabase.CreateAsset(profile, profilePath); AssetDatabase.SaveAssets();
            return profile;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); string parent = path.Substring(0, slash);
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
