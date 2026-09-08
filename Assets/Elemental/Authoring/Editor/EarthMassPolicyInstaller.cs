using System;
using System.Collections.Generic;
using Elemental.Runtime.Matter;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    public static class EarthMassPolicyInstaller
    {
        public const string AssetPath = "Assets/Elemental/Content/Profiles/EarthMatterMassPolicy.asset";

        [MenuItem("Elemental/Setup/Install Shared Stone Mass Policy")]
        public static void Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play before installing the world mass policy.");
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open the production scene first.");
            var components = new List<MonoBehaviour>();
            foreach (GameObject root in scene.GetRootGameObjects()) components.AddRange(root.GetComponentsInChildren<MonoBehaviour>(true));
            EarthMatterKernelBehaviour kernel = null;
            EarthRockDebrisPool debris = null;
            foreach (MonoBehaviour component in components)
            {
                if (component is EarthMatterKernelBehaviour candidate)
                {
                    if (kernel != null) throw new InvalidOperationException("The active scene contains multiple matter kernels. Assign one explicit world before installing.");
                    kernel = candidate;
                }
                if (debris == null && component is EarthRockDebrisPool pool) debris = pool;
            }
            if (debris == null) throw new InvalidOperationException("The scene needs its authored EarthRockDebrisPool before mass binding.");
            var asset = AssetDatabase.LoadAssetAtPath<EarthMatterMassPolicyAsset>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<EarthMatterMassPolicyAsset>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            if (kernel == null)
            {
                var host = new GameObject("Earth Matter Kernel");
                Undo.RegisterCreatedObjectUndo(host, "Install earth mass world");
                kernel = Undo.AddComponent<EarthMatterKernelBehaviour>(host);
            }
            Undo.RecordObject(kernel, "Bind shared earth mass policy");
            kernel.ConfigureMassPolicy(asset);
            EditorUtility.SetDirty(kernel);
            int bound = 0;
            foreach (MonoBehaviour component in components)
            {
                if (component == null) continue;
                Undo.RecordObject(component, "Bind explicit earth mass world");
                switch (component)
                {
                    case MagicExecutor executor: executor.ConfigureMatterKernel(kernel); break;
                    case EarthFragmentPool pool: pool.ConfigureMatterKernel(kernel); break;
                    case EarthRockDebrisPool pool: pool.ConfigureMatterKernel(kernel); break;
                    case EarthWallPool pool: pool.ConfigureMatterKernel(kernel); break;
                    case EarthPlatformPool pool: pool.ConfigureMatterKernel(kernel); break;
                    case EarthPillarWavePool pool: pool.ConfigureMatterKernel(kernel); break;
                    case EarthSurfController surf: surf.ConfigureMatterKernel(kernel); break;
                    case EarthArmorController armor: armor.ConfigureMatterKernel(kernel); break;
                    case EarthArenaStructure arena: BindMissing(arena, "rockDebrisPool", debris); break;
                    case EarthDestructibleDecorRock rock: BindMissing(rock, "debrisPool", debris); break;
                    case MeteorShowerBehaviour meteors: BindMissing(meteors, "debrisPool", debris); break;
                    default: continue;
                }
                EditorUtility.SetDirty(component);
                bound++;
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Earth mass policy] Bound {bound} owners to {asset.name}; creation uses immutable world policy, splits preserve parent mass and volume. Scene saved.");
        }

        private static void BindMissing(UnityEngine.Object owner, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) throw new InvalidOperationException(owner.name + " is missing mass owner field " + field);
            if (property.objectReferenceValue == null) property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }
    }
}
