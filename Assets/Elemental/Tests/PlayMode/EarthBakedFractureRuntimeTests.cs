using System.Collections;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthBakedFractureRuntimeTests
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";

        [UnityTest]
        public IEnumerator ProductionWallUsesBakedGraphAndDoesNotDecayBondsOnTimer()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            EarthWallPool pool = FindInScene<EarthWallPool>(scene);
            Assert.That(pool, Is.Not.Null);
            Assert.That(pool.UsingBakedFractureAsset, Is.True);
            Assert.That(pool.RuntimeFallbackUsed, Is.False);

            EarthWall wall = pool.Acquire(
                new Vector3(-3f, 24f, 0f),
                new Vector3(3f, 24f, 0f),
                Vector3.zero,
                3f,
                0.6f,
                100u);
            yield return new WaitForSeconds(0.8f);
            Assert.That(wall.UsesBakedFracture, Is.True);
            Assert.That(wall.IsCollapsing, Is.False);
            MeshRenderer bakedRenderer = wall.FirstFracturePiece.GetComponent<MeshRenderer>();
            Assert.That(bakedRenderer.sharedMaterials, Has.Length.EqualTo(2));
            Assert.That(bakedRenderer.sharedMaterials[0], Is.Not.SameAs(bakedRenderer.sharedMaterials[1]));
            Assert.That(bakedRenderer.sharedMaterials[0].shader.name, Is.EqualTo("Elemental/SG Earth Master"));
            Assert.That(wall.FirstFracturePiece.GetComponent<MeshFilter>().sharedMesh.colors32,
                Has.Length.EqualTo(wall.FirstFracturePiece.GetComponent<MeshFilter>().sharedMesh.vertexCount));

            Assert.That(wall.ApplyRockImpact(wall.transform.position, wall.transform.forward, 100f), Is.True);
            var targets = new IEarthPhysicalTarget[48];
            int targetCount = wall.CopyActiveTargetsNonAlloc(targets);
            Assert.That(targetCount, Is.EqualTo(40));
            var depthBands = new System.Collections.Generic.HashSet<int>();
            for (int index = 0; index < targetCount; index++)
            {
                EarthPieceRuntime piece = targets[index] as EarthPieceRuntime;
                MeshCollider pieceCollider = piece?.GetComponent<MeshCollider>();
                Assert.That(pieceCollider, Is.Not.Null);
                Assert.That(pieceCollider.sharedMesh.bounds.size.z, Is.GreaterThan(0.035f),
                    "Every fracture cell must retain real volume through wall depth.");
                depthBands.Add(Mathf.RoundToInt(piece.transform.localPosition.z * 20f));
            }
            Assert.That(depthBands.Count, Is.GreaterThanOrEqualTo(3),
                "The wall must fracture through at least three depth layers, not use full-depth prisms.");
            for (int index = 0; index < targetCount; index++)
            {
                EarthPieceRuntime piece = targets[index] as EarthPieceRuntime;
                Assert.That(piece, Is.Not.Null);
                piece.enabled = false;
                piece.Body.detectCollisions = false;
                piece.Body.isKinematic = true;
            }
            // Flush contacts already queued by the initial proxy swap before the
            // timer-only observation window begins.
            yield return new WaitForSeconds(0.5f);
            Assert.That(wall.StructureRuntime.State.Phase,
                Is.EqualTo(EarthStructurePhase.Fractured).Or.EqualTo(EarthStructurePhase.Damaged));
            Assert.That(wall.ActiveFracturePieceCount, Is.EqualTo(40));
            int bondsAfterImpact = wall.RemainingBondCount;
            Assert.That(bondsAfterImpact, Is.GreaterThan(0));

            yield return new WaitForSeconds(3.2f);
            Assert.That(wall.RemainingBondCount, Is.EqualTo(bondsAfterImpact),
                "Baked structural bonds may change only from impacts/repair, never a decay timer.");
            Assert.That(wall.ActiveFracturePieceCount, Is.EqualTo(40));

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTearDown]
        public IEnumerator UnloadLeakedEarthCoreScene()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator PoolReuseRestoresExactBakedPiecePoseAndProxyState()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            EarthWallPool pool = FindInScene<EarthWallPool>(scene);
            EarthWall first = pool.Acquire(
                new Vector3(-2f, 24f, -9f), new Vector3(2f, 24f, -9f),
                Vector3.zero, 2.5f, 0.55f, 200u);
            Vector3 restPosition = first.FirstFracturePiece.localPosition;
            Quaternion restRotation = first.FirstFracturePiece.localRotation;
            Vector3 restScale = first.FirstFracturePiece.localScale;
            yield return new WaitForSeconds(0.7f);
            first.ApplyRockImpact(first.transform.position, first.transform.forward, 5000f);
            yield return new WaitForFixedUpdate();
            Assert.That(first.IsCollapsing, Is.True);

            Assert.That(pool.ReleaseTransient(first), Is.True);
            EarthWall recycled = pool.Acquire(
                new Vector3(-2f, 24f, -5f), new Vector3(2f, 24f, -5f),
                Vector3.zero, 2.5f, 0.55f, 201u);

            Assert.That(recycled, Is.SameAs(first));
            Assert.That(recycled.IsCollapsing, Is.False);
            Assert.That(recycled.ActiveFracturePieceCount, Is.Zero);
            Assert.That(recycled.VisualEmergenceRoot.GetComponent<MeshRenderer>().enabled, Is.True);
            Assert.That(recycled.GetComponent<BoxCollider>().enabled, Is.False);
            Assert.That(recycled.StructureRuntime.State.Phase, Is.EqualTo(EarthStructurePhase.Intact));
            Assert.That(recycled.FirstFracturePiece.parent, Is.EqualTo(recycled.transform));
            Assert.That(recycled.FirstFracturePiece.localPosition, Is.EqualTo(restPosition));
            Assert.That(Quaternion.Angle(recycled.FirstFracturePiece.localRotation, restRotation), Is.LessThan(0.001f));
            Assert.That(recycled.FirstFracturePiece.localScale, Is.EqualTo(restScale));
            Rigidbody pieceBody = recycled.FirstFracturePiece.GetComponent<Rigidbody>();
            Assert.That(pieceBody.isKinematic, Is.True);
            Assert.That(pieceBody.detectCollisions, Is.False);
            Assert.That(pieceBody.linearVelocity.sqrMagnitude, Is.Zero);
            Assert.That(pieceBody.angularVelocity.sqrMagnitude, Is.Zero);

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T value = root.GetComponentInChildren<T>(true);
                if (value != null) return value;
            }
            return null;
        }
    }
}
