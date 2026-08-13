using System.Collections.Generic;
using Elemental.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    public static class M2VoxelPlanetSetup
    {
        public const string VoxelLabScenePath = "Assets/Elemental/Content/Scenes/VoxelPlanetLab.unity";
        private const string MaterialPath = "Assets/Elemental/Content/Materials/VoxelPlanetSurface.mat";
        public const string WorldProfilePath = "Assets/Elemental/Content/Profiles/PlanetWorldProfile.asset";

        [MenuItem("Elemental/Setup/Create M2 Voxel Planet Lab")]
        public static void Configure()
        {
            PlanetWorldProfile worldProfile = CreateOrLoadWorldProfile();
            M1GravityToySetup.Configure(worldProfile);
            // M1 saves and refreshes the AssetDatabase. Reacquire the persistent
            // profile because Unity may unload the pre-refresh wrapper in batch mode.
            worldProfile = CreateOrLoadWorldProfile();
            Scene scene = SceneManager.GetActiveScene();

            GameObject primitivePlanet = GameObject.Find("Primitive Planet");
            if (primitivePlanet != null)
            {
                primitivePlanet.name = "Planet Collision Proxy";
                MeshRenderer renderer = primitivePlanet.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }

            Material material = CreateOrLoadMaterial();
            GameObject voxelObject = new GameObject("Editable Voxel Planet");
            voxelObject.SetActive(false);
            VoxelPlanetBehaviour voxelPlanet = voxelObject.AddComponent<VoxelPlanetBehaviour>();
            voxelPlanet.Configure(worldProfile, material);
            voxelObject.SetActive(true);

            EditorSceneManager.SaveScene(scene, VoxelLabScenePath);
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool alreadyPresent = scenes.Exists(item => item.path == VoxelLabScenePath);
            if (!alreadyPresent)
            {
                scenes.Add(new EditorBuildSettingsScene(VoxelLabScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Elemental] M2 Voxel Planet Lab configured.");
        }

        private static Material CreateOrLoadMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new UnityEditor.Build.BuildFailedException("URP Lit shader was not found.");
            }

            material = new Material(shader)
            {
                name = "Voxel Planet Surface",
                color = new Color(0.18f, 0.52f, 0.42f)
            };
            material.SetFloat("_Smoothness", 0.18f);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        public static PlanetWorldProfile CreateOrLoadWorldProfile()
        {
            PlanetWorldProfile profile = AssetDatabase.LoadAssetAtPath<PlanetWorldProfile>(WorldProfilePath);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<PlanetWorldProfile>();
            AssetDatabase.CreateAsset(profile, WorldProfilePath);
            return profile;
        }
    }
}
