using System;
using System.Collections.Generic;
using System.IO;
using Elemental.Authoring.Fracture;
using Elemental.Runtime.Physics;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Elemental.Authoring.Editor
{
    public static class OuterRingSceneMeshNormalizer
    {
        private const string SceneName = "EarthCoreSlice";
        private const string RingName = "Outer Stone Ring";
        private const int ExpectedPieceCount = 85;

        [MenuItem("Elemental/Arena/Remove Serialized Runtime Bevel Duplicates")]
        public static void Normalize()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new BuildFailedException("Stop Play Mode before normalizing saved mesh references.");
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != SceneName || string.IsNullOrEmpty(scene.path))
                throw new BuildFailedException("Open the saved EarthCoreSlice scene before normalizing mesh references.");
            if (scene.isDirty)
                throw new BuildFailedException("Save or discard current scene edits before transactional mesh normalization.");

            GameObject ring = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == RingName) { ring = root; break; }
            if (ring == null) throw new BuildFailedException("The saved Outer Stone Ring was not found.");

            EarthArenaStructure[] structures = ring.GetComponentsInChildren<EarthArenaStructure>(true);
            if (structures.Length != 7) throw new BuildFailedException("Expected seven outer-column structures.");

            var bindings = new List<Binding>(ExpectedPieceCount);
            var boundFilters = new HashSet<MeshFilter>();
            var transientMeshes = new HashSet<Mesh>();
            foreach (EarthArenaStructure structure in structures)
            {
                var serialized = new SerializedObject(structure);
                var asset = serialized.FindProperty("fractureAssetObject").objectReferenceValue as EarthFractureAsset;
                SerializedProperty pieces = serialized.FindProperty("pieces");
                if (asset == null || pieces == null || pieces.arraySize != asset.PieceCount)
                    throw new BuildFailedException(structure.name + ": invalid fracture bindings.");
                for (int index = 0; index < pieces.arraySize; index++)
                {
                    Transform piece = pieces.GetArrayElementAtIndex(index).objectReferenceValue as Transform;
                    Mesh expected = asset.GetPieceRenderMesh(index);
                    MeshFilter filter = piece != null ? piece.GetComponent<MeshFilter>() : null;
                    if (filter == null || expected == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(expected)))
                        throw new BuildFailedException(structure.name + $": missing persistent render mesh at piece {index}.");
                    Mesh current = filter.sharedMesh;
                    if (current != null && current != expected)
                    {
                        string currentPath = AssetDatabase.GetAssetPath(current);
                        bool knownInlineBevel = current.name.EndsWith(" Beveled Render", StringComparison.Ordinal) &&
                                                (string.IsNullOrEmpty(currentPath) ||
                                                 string.Equals(currentPath, scene.path, StringComparison.Ordinal));
                        if (!knownInlineBevel)
                            throw new BuildFailedException(
                                $"{piece.name}: refusing to replace unexpected mesh '{current.name}' at '{currentPath}'.");
                        transientMeshes.Add(current);
                    }
                    bindings.Add(new Binding(filter, expected));
                    boundFilters.Add(filter);
                }
            }
            if (bindings.Count != ExpectedPieceCount)
                throw new BuildFailedException($"Expected {ExpectedPieceCount} fracture-piece bindings, found {bindings.Count}.");

            // Destroying an embedded Mesh is safe only when every serialized use
            // belongs to the 85 bindings replaced below. Refuse the transaction
            // if a collider, skinned renderer or unrelated filter shares one.
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null && transientMeshes.Contains(filter.sharedMesh) &&
                        !boundFilters.Contains(filter))
                        throw new BuildFailedException(
                            $"Inline bevel '{filter.sharedMesh.name}' is shared by unrelated filter '{filter.name}'.");
                foreach (MeshCollider collider in root.GetComponentsInChildren<MeshCollider>(true))
                    if (collider.sharedMesh != null && transientMeshes.Contains(collider.sharedMesh))
                        throw new BuildFailedException(
                            $"Inline bevel '{collider.sharedMesh.name}' is shared by collider '{collider.name}'.");
                foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (renderer.sharedMesh != null && transientMeshes.Contains(renderer.sharedMesh))
                        throw new BuildFailedException(
                            $"Inline bevel '{renderer.sharedMesh.name}' is shared by skinned renderer '{renderer.name}'.");
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                                 ?? throw new BuildFailedException("Could not resolve project root.");
            string backupFolder = Path.Combine(projectRoot, "BuildReports", "StartupCacheRescue");
            Directory.CreateDirectory(backupFolder);
            string backup = Path.Combine(backupFolder,
                $"EarthCoreSlice.before-inline-bevel-cleanup-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.unity");
            File.Copy(Path.GetFullPath(scene.path), backup, false);

            foreach (Binding binding in bindings)
            {
                if (binding.Filter.sharedMesh == binding.Expected) continue;
                Undo.RecordObject(binding.Filter, "Remove serialized runtime bevel duplicate");
                binding.Filter.sharedMesh = binding.Expected;
                EditorUtility.SetDirty(binding.Filter);
            }
            foreach (Mesh mesh in transientMeshes)
                if (mesh != null) Object.DestroyImmediate(mesh);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new BuildFailedException("Unity failed to save normalized EarthCoreSlice; restore the timestamped backup.");
            Debug.Log($"[Elemental] Normalized {bindings.Count} outer-ring piece mesh references; " +
                      $"removed {transientMeshes.Count} transient bevel meshes. Backup: {backup}");
        }

        private readonly struct Binding
        {
            public Binding(MeshFilter filter, Mesh expected) { Filter = filter; Expected = expected; }
            public MeshFilter Filter { get; }
            public Mesh Expected { get; }
        }
    }
}
