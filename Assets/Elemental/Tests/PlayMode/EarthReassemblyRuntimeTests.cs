using System.Collections;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthReassemblyRuntimeTests
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";

        [UnityTest]
        public IEnumerator BakedWallPhysicallyReassemblesAndRestoresIntactProxy()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            EarthWall wall = SpawnFracturedWall(scene, 810u);
            yield return new WaitForSeconds(0.75f);
            Assert.That(wall.ApplyRockImpact(wall.transform.position, wall.transform.forward, 6000f), Is.True);
            yield return new WaitForFixedUpdate();

            EarthReassemblyController repair = wall.Reassembly;
            int repairedBonds = 0;
            int rebuiltEvents = 0;
            repair.BondRepaired += _ => repairedBonds++;
            repair.StructureRebuilt += _ => rebuiltEvents++;

            Assert.That(repair.TryBeginRepair(900u), Is.True);
            float deadline = Time.realtimeSinceStartup + 30f;
            while (repair.IsRepairing && Time.realtimeSinceStartup < deadline)
                yield return new WaitForFixedUpdate();

            Assert.That(repair.IsRepairing, Is.False,
                $"Repair stalled at {repair.WeldedPieceCount}/{repair.SelectedPieceCount}; " +
                $"piece {repair.CurrentPieceIndex} phase {repair.CurrentPiecePhase}, " +
                $"error {repair.CurrentPiecePositionError:F4}, speed {repair.CurrentPieceSpeed:F4}, " +
                $"angle {repair.CurrentPieceAngleErrorDegrees:F2}, angular {repair.CurrentPieceAngularSpeed:F3}, " +
                $"retry {repair.CurrentPieceRetryCount}.");
            Assert.That(repair.LastRepairWasPartial, Is.False);
            Assert.That(wall.IsCollapsing, Is.False);
            Assert.That(wall.StructureRuntime.State.Phase, Is.EqualTo(EarthStructurePhase.Rebuilt));
            Assert.That(wall.ActiveFracturePieceCount, Is.Zero);
            Assert.That(wall.GetComponent<MeshRenderer>().enabled, Is.True);
            Assert.That(wall.GetComponent<BoxCollider>().enabled, Is.True);
            Assert.That(repairedBonds, Is.EqualTo(wall.StructureRuntime.BondCount));
            Assert.That(rebuiltEvents, Is.EqualTo(1));
            Assert.That(wall.FirstFracturePiece.parent, Is.EqualTo(wall.transform));
            AssertFinite(wall.transform.position);

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator MissingPieceProducesStablePartialRepairWithoutProxySwap()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            EarthWall wall = SpawnFracturedWall(scene, 820u);
            yield return new WaitForSeconds(0.75f);
            wall.ApplyRockImpact(wall.transform.position, wall.transform.forward, 6000f);
            yield return new WaitForFixedUpdate();

            var targets = new IEarthPhysicalTarget[48];
            int targetCount = wall.CopyActiveTargetsNonAlloc(targets);
            EarthPieceRuntime missing = targets[targetCount - 1] as EarthPieceRuntime;
            missing.Body.detectCollisions = false;
            missing.Body.isKinematic = true;
            missing.gameObject.SetActive(false);

            EarthReassemblyController repair = wall.Reassembly;
            Assert.That(repair.TryBeginRepair(920u), Is.True);
            float deadline = Time.realtimeSinceStartup + 30f;
            while (repair.IsRepairing && Time.realtimeSinceStartup < deadline)
                yield return new WaitForFixedUpdate();

            Assert.That(repair.IsRepairing, Is.False,
                $"Partial repair stalled at {repair.WeldedPieceCount}/{repair.SelectedPieceCount}; " +
                $"piece {repair.CurrentPieceIndex} phase {repair.CurrentPiecePhase}, " +
                $"error {repair.CurrentPiecePositionError:F4}, speed {repair.CurrentPieceSpeed:F4}, " +
                $"angle {repair.CurrentPieceAngleErrorDegrees:F2}, angular {repair.CurrentPieceAngularSpeed:F3}, " +
                $"retry {repair.CurrentPieceRetryCount}.");
            Assert.That(repair.LastRepairWasPartial, Is.True);
            Assert.That(repair.SelectedPieceCount, Is.EqualTo(targetCount - 1));
            Assert.That(repair.WeldedPieceCount, Is.EqualTo(targetCount - 1));
            Assert.That(wall.IsCollapsing, Is.True);
            Assert.That(wall.StructureRuntime.State.Phase, Is.EqualTo(EarthStructurePhase.Fractured));
            Assert.That(wall.GetComponent<MeshRenderer>().enabled, Is.False);
            Assert.That(missing.gameObject.activeSelf, Is.False, "Repair must not invent missing mass.");

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator ReleaseInterruptsRepairAndLeavesUnweldedPiecesPhysical()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            EarthWall wall = SpawnFracturedWall(scene, 830u);
            yield return new WaitForSeconds(0.75f);
            wall.ApplyRockImpact(wall.transform.position, wall.transform.forward, 6000f);
            yield return new WaitForFixedUpdate();

            EarthReassemblyController repair = wall.Reassembly;
            int interruptedEvents = 0;
            repair.RepairInterrupted += value =>
            {
                if (value.Reason == EarthRepairInterruptReason.Released) interruptedEvents++;
            };
            Assert.That(repair.TryBeginRepair(930u), Is.True);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            repair.Interrupt(EarthRepairInterruptReason.Released, 931u);

            Assert.That(repair.IsRepairing, Is.False);
            Assert.That(interruptedEvents, Is.EqualTo(1));
            Assert.That(wall.IsCollapsing, Is.True);
            bool foundDynamic = false;
            for (int index = 0; index < wall.StructureRuntime.PieceCount; index++)
            {
                EarthPieceRuntime piece = wall.StructureRuntime.GetPieceRuntime(index);
                if (piece != null && piece.gameObject.activeSelf && !piece.Body.isKinematic)
                {
                    foundDynamic = true;
                    AssertFinite(piece.Body.position);
                    break;
                }
            }
            Assert.That(foundDynamic, Is.True);

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTearDown]
        public IEnumerator UnloadLeakedScene()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
        }

        private static EarthWall SpawnFracturedWall(Scene scene, uint tick)
        {
            EarthWallPool pool = FindInScene<EarthWallPool>(scene);
            Assert.That(pool, Is.Not.Null);
            EarthWall wall = pool.Acquire(
                new Vector3(-2.5f, 24f, -8f),
                new Vector3(2.5f, 24f, -8f),
                Vector3.zero,
                3f,
                0.6f,
                tick);
            Assert.That(wall, Is.Not.Null);
            return wall;
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

        private static void AssertFinite(Vector3 value)
        {
            Assert.That(float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z), Is.True);
        }
    }
}
