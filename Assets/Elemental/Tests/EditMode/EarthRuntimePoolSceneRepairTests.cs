using System.Collections.Generic;
using Elemental.Authoring.Editor;
using Elemental.Runtime.Physics;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthRuntimePoolSceneRepairTests
    {
        [Test]
        public void IncompletePreparedPlatformSetIsReplacedInsteadOfAppended()
        {
            var host = new GameObject("Platform Repair Fixture");
            host.SetActive(false);
            try
            {
                EarthPlatform platform = host.AddComponent<EarthPlatform>();
                for (int index = 0; index < 3; index++)
                {
                    var child = new GameObject($"Platform Piece {index + 1:00}");
                    child.transform.SetParent(host.transform, false);
                    child.AddComponent<Rigidbody>();
                    EarthPlatformPiece piece = child.AddComponent<EarthPlatformPiece>();
                    piece.Configure(platform, index);
                }

                platform.Configure(null, null);

                EarthPlatformPiece[] repaired = host.GetComponentsInChildren<EarthPlatformPiece>(true);
                Assert.That(repaired, Has.Length.EqualTo(48));
                var indices = new HashSet<int>();
                foreach (EarthPlatformPiece piece in repaired)
                {
                    Assert.That(indices.Add(piece.PieceIndex), Is.True, piece.name);
                    Assert.That(piece.Owner, Is.SameAs(platform), piece.name);
                    Assert.That(piece.GetComponent<Rigidbody>(), Is.Not.Null, piece.name);
                    Assert.That(piece.GetComponent<Collider>(), Is.Not.Null, piece.name);
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ShippingSceneContainsNoPersistentArmorShellsOrIncompletePlatformPools()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(EarthRuntimePoolSceneRepair.ShippingScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
                scene = EditorSceneManager.OpenScene(EarthRuntimePoolSceneRepair.ShippingScenePath, OpenSceneMode.Additive);
            try
            {
                int missingScripts = 0;
                int armorRoots = 0;
                int platformPools = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name.StartsWith("Earth Armor Piece ", System.StringComparison.Ordinal))
                        armorRoots++;
                    foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                        missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject);
                    foreach (EarthPlatformPool pool in root.GetComponentsInChildren<EarthPlatformPool>(true))
                    {
                        platformPools++;
                        var serialized = new SerializedObject(pool);
                        int capacity = serialized.FindProperty("capacity").intValue;
                        EarthPlatform[] platforms = pool.GetComponentsInChildren<EarthPlatform>(true);
                        Assert.That(platforms, Has.Length.EqualTo(capacity), pool.name);
                        foreach (EarthPlatform platform in platforms)
                        {
                            EarthPlatformPiece[] pieces = platform.GetComponentsInChildren<EarthPlatformPiece>(true);
                            Assert.That(pieces, Has.Length.EqualTo(48), platform.name);
                            var indices = new HashSet<int>();
                            foreach (EarthPlatformPiece piece in pieces)
                            {
                                Assert.That(indices.Add(piece.PieceIndex), Is.True, piece.name);
                                Assert.That(piece.Owner, Is.SameAs(platform), piece.name);
                                Assert.That(piece.GetComponent<Rigidbody>(), Is.Not.Null, piece.name);
                                Assert.That(piece.GetComponent<Collider>(), Is.Not.Null, piece.name);
                                Assert.That(piece.GetComponent<GravityBody>(), Is.Not.Null, piece.name);
                            }
                        }
                    }
                }

                Assert.That(armorRoots, Is.Zero,
                    "Armor shells are runtime-owned and must never be serialized as scene roots.");
                Assert.That(missingScripts, Is.Zero);
                Assert.That(platformPools, Is.GreaterThan(0));
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
