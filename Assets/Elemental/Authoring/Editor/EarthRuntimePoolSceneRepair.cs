using System;
using System.Collections.Generic;
using Elemental.Runtime.Physics;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    /// <summary>Removes interrupted runtime shells and restores complete saved platform pools.</summary>
    public static class EarthRuntimePoolSceneRepair
    {
        public const string ShippingScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const int PreparedPiecesPerPlatform = 48;

        [MenuItem("Elemental/Earth/Repair Saved Runtime Pools")]
        public static void RepairShippingScene()
        {
            if (Application.isPlaying)
                throw new BuildFailedException("Stop Play Mode before repairing saved runtime pools.");

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ShippingScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ShippingScenePath, OpenSceneMode.Additive);
            try
            {
                EarthRuntimePoolRepairResult result = Repair(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new BuildFailedException("Unity did not save the repaired shipping scene.");
                Debug.Log($"[Elemental] Runtime pool repair removed {result.RemovedArmorRoots} " +
                          $"ownerless armor shells and restored {result.Platforms} platforms " +
                          $"with {result.PlatformPieces} prepared pieces.");
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        public static EarthRuntimePoolRepairResult Repair(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new ArgumentException("Repair requires a loaded scene.", nameof(scene));

            var armorRoots = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root != null && root.name.StartsWith("Earth Armor Piece ", StringComparison.Ordinal))
                    armorRoots.Add(root);
            foreach (GameObject root in armorRoots)
                UnityEngine.Object.DestroyImmediate(root);

            int platformCount = 0;
            int platformPieceCount = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                EarthPlatformPool[] pools = root.GetComponentsInChildren<EarthPlatformPool>(true);
                foreach (EarthPlatformPool pool in pools)
                {
                    pool.PrewarmAll();
                    EarthPlatform[] platforms = pool.GetComponentsInChildren<EarthPlatform>(true);
                    platformCount += platforms.Length;
                    foreach (EarthPlatform platform in platforms)
                    {
                        EarthPlatformPiece[] pieces =
                            platform.GetComponentsInChildren<EarthPlatformPiece>(true);
                        if (pieces.Length != PreparedPiecesPerPlatform)
                            throw new BuildFailedException(
                                $"{platform.name} contains {pieces.Length} prepared pieces; " +
                                $"expected {PreparedPiecesPerPlatform}.");
                        var indices = new HashSet<int>();
                        foreach (EarthPlatformPiece piece in pieces)
                        {
                            if (piece.Owner != platform || !indices.Add(piece.PieceIndex) ||
                                piece.GetComponent<Rigidbody>() == null ||
                                piece.GetComponent<Collider>() == null ||
                                piece.GetComponent<GravityBody>() == null)
                                throw new BuildFailedException(
                                    $"{platform.name}/{piece.name} has an invalid prepared-piece contract.");
                        }
                        platformPieceCount += pieces.Length;
                    }
                }
            }

            foreach (GameObject root in scene.GetRootGameObjects())
                if (root != null && root.name.StartsWith("Earth Armor Piece ", StringComparison.Ordinal))
                    throw new BuildFailedException($"Runtime armor shell survived repair: {root.name}.");

            int missingScripts = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject);
            if (missingScripts != 0)
                throw new BuildFailedException($"Runtime pool repair left {missingScripts} missing scripts in {scene.path}.");

            return new EarthRuntimePoolRepairResult(armorRoots.Count, platformCount, platformPieceCount);
        }
    }

    public readonly struct EarthRuntimePoolRepairResult
    {
        public EarthRuntimePoolRepairResult(int removedArmorRoots, int platforms, int platformPieces)
        {
            RemovedArmorRoots = removedArmorRoots;
            Platforms = platforms;
            PlatformPieces = platformPieces;
        }

        public int RemovedArmorRoots { get; }
        public int Platforms { get; }
        public int PlatformPieces { get; }
    }
}
