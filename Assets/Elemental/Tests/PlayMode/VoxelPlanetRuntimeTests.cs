using System.Collections;
using Elemental.Runtime.World;
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
    }
}
