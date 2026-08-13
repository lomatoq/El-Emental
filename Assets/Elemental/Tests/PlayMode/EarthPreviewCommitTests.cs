using System.Collections;
using System.Collections.Generic;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Presentation.VFX;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Voxel;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthPreviewCommitTests
    {
        [UnityTest]
        public IEnumerator FullProjectedWallPreviewExactlyMatchesCommittedCapsules()
        {
            GameObject planetObject = new GameObject("Preview Commit Planet");
            planetObject.SetActive(false);
            VoxelPlanetBehaviour planet = planetObject.AddComponent<VoxelPlanetBehaviour>();
            planet.Configure(8f, 91u, 8, 1f, 8, 8, null);
            planetObject.SetActive(true);

            GameObject poolObject = new GameObject("Preview Commit Pool");
            poolObject.SetActive(false);
            EarthFragmentPool pool = poolObject.AddComponent<EarthFragmentPool>();
            pool.Configure(1, null, null);
            poolObject.SetActive(true);

            GameObject wallPoolObject = new GameObject("Preview Commit Wall Pool");
            wallPoolObject.SetActive(false);
            EarthWallPool wallPool = wallPoolObject.AddComponent<EarthWallPool>();
            wallPool.Configure(1, null, null);
            wallPoolObject.SetActive(true);

            GameObject executorObject = new GameObject("Preview Commit Executor");
            MagicExecutor executor = executorObject.AddComponent<MagicExecutor>();
            executor.Configure(planet, pool, planetObject.transform, wallPool);
            executor.ConfigureRecipes(new[]
            {
                new AbilityCompiler().Compile(new AbilityRecipeData(
                    EarthAbilityIds.LineWall, MagicSelectorKind.PlanetSurface, MagicGeometryKind.WallSpline,
                    new[] { MagicOperatorKind.AddSolid }, 0.45f, 1f))
            });

            var path = new List<float3>
            {
                new float3(-2f, 8f, 0f),
                new float3(-1f, 8.1f, 0.5f),
                new float3(0f, 8.2f, 0.8f),
                new float3(1f, 8.1f, 0.5f),
                new float3(2f, 8f, 0f)
            };
            var command = new MagicCommand(
                60u, 1u, ElementId.Earth, EarthAbilityIds.LineWall, path[0], new float3(0f, 1f, 0f),
                path, 1f, 0u, 123u);
            var preview = new List<Vector3>(32);
            executor.BuildPreview(in command, preview);
            int pendingRenderBefore = planet.PendingRenderCount;

            Assert.That(executor.Execute(in command), Is.True);
            Assert.That(preview.Count, Is.EqualTo(path.Count));
            Assert.That(planet.State.EditCount, Is.Zero);
            Assert.That(wallPool.ActiveCount, Is.EqualTo(1));
            Assert.That(planet.PendingRenderCount, Is.EqualTo(pendingRenderBefore));
            Assert.That(math.distance(
                new float3(wallPool.LastAcquired.Start.x, wallPool.LastAcquired.Start.y, wallPool.LastAcquired.Start.z),
                path[0]), Is.LessThan(0.0001f));
            Assert.That(math.distance(
                new float3(wallPool.LastAcquired.End.x, wallPool.LastAcquired.End.y, wallPool.LastAcquired.End.z),
                path[path.Count - 1]), Is.LessThan(0.0001f));

            Object.Destroy(executorObject);
            Object.Destroy(wallPoolObject);
            Object.Destroy(poolObject);
            Object.Destroy(planetObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PresentationEnabledOrDisabledProducesIdenticalCanonicalHash()
        {
            GameObject withVfx = CreateReplayFixture("Presentation On", true, out MagicExecutor onExecutor, out VoxelPlanetBehaviour onPlanet);
            GameObject withoutVfx = CreateReplayFixture("Presentation Off", false, out MagicExecutor offExecutor, out VoxelPlanetBehaviour offPlanet);
            var path = new List<float3>
            {
                new float3(-1f, 8f, 0f), new float3(0f, 8.1f, 0.2f), new float3(1f, 8f, 0f)
            };
            var command = new MagicCommand(
                90u, 1u, ElementId.Earth, EarthAbilityIds.LineWall, path[0], new float3(0f, 1f, 0f),
                path, 1f, 0u, 456u);

            Assert.That(onExecutor.Execute(in command), Is.True);
            Assert.That(offExecutor.Execute(in command), Is.True);
            var checkpoint = new ChunkCoord(0, 1, 0);
            Assert.That(onPlanet.State.ComputeChunkHash(checkpoint), Is.EqualTo(offPlanet.State.ComputeChunkHash(checkpoint)));
            Assert.That(onPlanet.State.EditCount, Is.EqualTo(offPlanet.State.EditCount));

            Object.Destroy(withVfx);
            Object.Destroy(withoutVfx);
            yield return null;
        }

        private static GameObject CreateReplayFixture(
            string name,
            bool presentationEnabled,
            out MagicExecutor executor,
            out VoxelPlanetBehaviour planet)
        {
            GameObject root = new GameObject(name);
            GameObject planetObject = new GameObject("Planet");
            planetObject.transform.SetParent(root.transform);
            planetObject.SetActive(false);
            planet = planetObject.AddComponent<VoxelPlanetBehaviour>();
            planet.Configure(8f, 91u, 8, 1f, 8, 8, null);
            planetObject.SetActive(true);

            GameObject poolObject = new GameObject("Pool");
            poolObject.transform.SetParent(root.transform);
            poolObject.SetActive(false);
            EarthFragmentPool pool = poolObject.AddComponent<EarthFragmentPool>();
            pool.Configure(1, null, null);
            poolObject.SetActive(true);

            GameObject wallPoolObject = new GameObject("Wall Pool");
            wallPoolObject.transform.SetParent(root.transform);
            wallPoolObject.SetActive(false);
            EarthWallPool wallPool = wallPoolObject.AddComponent<EarthWallPool>();
            wallPool.Configure(1, null, null);
            wallPoolObject.SetActive(true);

            GameObject executorObject = new GameObject("Executor");
            executorObject.transform.SetParent(root.transform);
            executor = executorObject.AddComponent<MagicExecutor>();
            executor.Configure(planet, pool, planetObject.transform, wallPool);
            executor.ConfigureRecipes(new[]
            {
                new AbilityCompiler().Compile(new AbilityRecipeData(
                    EarthAbilityIds.LineWall, MagicSelectorKind.PlanetSurface, MagicGeometryKind.WallSpline,
                    new[] { MagicOperatorKind.AddSolid }, 0.45f, 1f))
            });

            GameObject feedbackObject = new GameObject("Typed Event Feedback");
            feedbackObject.transform.SetParent(root.transform);
            feedbackObject.SetActive(false);
            MagicFeedbackRouter feedback = feedbackObject.AddComponent<MagicFeedbackRouter>();
            feedback.Configure(executor);
            feedbackObject.SetActive(presentationEnabled);
            return root;
        }
    }
}
