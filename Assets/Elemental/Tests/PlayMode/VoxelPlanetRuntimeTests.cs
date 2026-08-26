using System.Collections;
using Elemental.Runtime.World;
using Elemental.Simulation.Voxel;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class VoxelPlanetRuntimeTests
    {
        [UnityTest]
        public IEnumerator RuntimeBuildsAndRebuildsThroughSeparateBudgetedQueues()
        {
            GameObject planetObject = new GameObject("Voxel Runtime Test");
            planetObject.SetActive(false);
            VoxelPlanetBehaviour planet = planetObject.AddComponent<VoxelPlanetBehaviour>();
            planet.Configure(8f, 42u, 8, 1f, 8, 8, null);
            planetObject.SetActive(true);

            for (int frame = 0; frame < 30 && (planet.PendingRenderCount > 0 || planet.PendingColliderCount > 0); frame++)
            {
                yield return null;
            }

            Assert.That(planet.PendingRenderCount, Is.EqualTo(0));
            Assert.That(planet.PendingColliderCount, Is.EqualTo(0));
            Assert.That(planet.ProcessedChunkCount, Is.EqualTo(20),
                "The initial queue must contain only chunks intersecting the conservative SDF surface shell.");
            Assert.That(planet.RuntimeChunkCount, Is.GreaterThan(0));
            int beforeEdit = planet.ProcessedChunkCount;

            planet.ResetQueueTimingTelemetry();
            planet.ApplySphereEdit(new Vector3(0f, 8f, 0f), 1.5f, false);
            Assert.That(planet.PendingRenderCount, Is.GreaterThan(0));

            for (int frame = 0; frame < 30 && (planet.PendingRenderCount > 0 || planet.PendingColliderCount > 0); frame++)
            {
                yield return null;
            }

            Assert.That(planet.PendingRenderCount, Is.EqualTo(0));
            Assert.That(planet.PendingColliderCount, Is.EqualTo(0));
            Assert.That(planet.ProcessedChunkCount, Is.GreaterThan(beforeEdit));
            Assert.That(planet.State.EditCount, Is.EqualTo(1));
            Assert.That(planet.PeakRenderQueueMilliseconds, Is.LessThan(30.0),
                $"A bounded edited-chunk render pass took {planet.PeakRenderQueueMilliseconds:0.00} ms.");
            Debug.Log($"[Elemental.Tests] Edited voxel render queue peak: {planet.PeakRenderQueueMilliseconds:0.00} ms.");

            Object.Destroy(planetObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TransactionCommitsOnlyAfterAffectedVisualAndColliderVersions()
        {
            GameObject planetObject = new GameObject("Voxel Transaction Test");
            planetObject.SetActive(false);
            VoxelPlanetBehaviour planet = planetObject.AddComponent<VoxelPlanetBehaviour>();
            planet.Configure(8f, 43u, 8, 1f, 1, 1, null);
            planetObject.SetActive(true);
            for (int frame = 0; frame < 80 &&
                 (planet.PendingRenderCount > 0 || planet.PendingColliderCount > 0); frame++) yield return null;

            var originalMeshes = new System.Collections.Generic.Dictionary<string, Mesh>();
            MeshFilter[] initialFilters = planet.GetComponentsInChildren<MeshFilter>();
            for (int index = 0; index < initialFilters.Length; index++)
                originalMeshes[initialFilters[index].gameObject.name] = initialFilters[index].sharedMesh;

            bool callback = false;
            VoxelEditReceipt committed = default;
            planet.EditCommitted += receipt =>
            {
                callback = true;
                committed = receipt;
            };
            VoxelEditReceipt submitted = planet.ApplySphereEditTransactional(
                new Vector3(0f, 8f, 0f), 1.5f, true);

            Assert.That(submitted.IsValid, Is.True);
            Assert.That(planet.IsEditCommitted(submitted), Is.False);
            Assert.That(callback, Is.False);
            Assert.That(planet.PendingEditTransactionCount, Is.EqualTo(1));
            for (int frame = 0; frame < 80 && !callback; frame++)
            {
                yield return null;
                if (callback) break;
                MeshFilter[] stagedFilters = planet.GetComponentsInChildren<MeshFilter>();
                for (int index = 0; index < stagedFilters.Length; index++)
                {
                    MeshFilter filter = stagedFilters[index];
                    if (originalMeshes.TryGetValue(filter.gameObject.name, out Mesh original))
                        Assert.That(filter.sharedMesh, Is.SameAs(original),
                            "A transaction exposed one rebuilt chunk before its neighbours were ready.");
                }
            }

            Assert.That(callback, Is.True);
            Assert.That(committed, Is.EqualTo(submitted));
            Assert.That(planet.IsEditCommitted(submitted), Is.True);
            Assert.That(planet.PendingEditTransactionCount, Is.Zero);
            Assert.That(planet.PendingRenderCount, Is.Zero);
            Assert.That(planet.PendingColliderCount, Is.Zero);
            bool swappedMesh = false;
            MeshFilter[] committedFilters = planet.GetComponentsInChildren<MeshFilter>();
            for (int index = 0; index < committedFilters.Length; index++)
            {
                MeshFilter filter = committedFilters[index];
                if (originalMeshes.TryGetValue(filter.gameObject.name, out Mesh original) &&
                    filter.sharedMesh != original)
                {
                    swappedMesh = true;
                    break;
                }
            }
            Assert.That(swappedMesh, Is.True,
                "The completed transaction should atomically swap at least one staged mesh.");

            Object.Destroy(planetObject);
            yield return null;
        }
    }
}
