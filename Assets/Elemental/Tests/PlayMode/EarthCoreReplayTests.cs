using System.Collections;
using System.Collections.Generic;
using System.IO;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Voxel;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthCoreReplayTests
    {
        [UnityTest]
        public IEnumerator SixtySecondReplayExecutesEarthVerticalSliceAndImpact()
        {
            GameObject planetObject = new GameObject("Earth Replay Planet");
            planetObject.SetActive(false);
            VoxelPlanetBehaviour planet = planetObject.AddComponent<VoxelPlanetBehaviour>();
            planet.Configure(8f, 77u, 8, 1f, 8, 8, null);
            planetObject.SetActive(true);

            GameObject poolObject = new GameObject("Earth Replay Fragment Pool");
            poolObject.SetActive(false);
            EarthFragmentPool pool = poolObject.AddComponent<EarthFragmentPool>();
            pool.Configure(2, null, null);
            poolObject.SetActive(true);

            GameObject wallPoolObject = new GameObject("Earth Replay Wall Pool");
            wallPoolObject.SetActive(false);
            EarthWallPool wallPool = wallPoolObject.AddComponent<EarthWallPool>();
            wallPool.Configure(2, null, null);
            wallPoolObject.SetActive(true);

            GameObject executorObject = new GameObject("Earth Replay Executor");
            MagicExecutor executor = executorObject.AddComponent<MagicExecutor>();
            executor.Configure(planet, pool, planetObject.transform, wallPool);
            executor.ConfigureRecipes(CreateRecipes());

            int impactCount = 0;
            executor.Events.ImpactOccurred += _ => impactCount++;

            var replay = new MagicReplayRecorder();
            var wallPath = new List<float3>
            {
                new float3(-1f, 8f, 0f),
                new float3(0f, 8f, 0f),
                new float3(1f, 8f, 0f)
            };
            var anchorPath = new List<float3> { new float3(0f, 8f, 0f) };
            Record(replay, 60u, EarthAbilityIds.LineWall, wallPath, new float3(0f, 1f, 0f), 1f);
            Record(replay, 1800u, EarthAbilityIds.PullRock, anchorPath, new float3(0f, 1f, 0f), 1f);
            Record(replay, 1860u, EarthAbilityIds.FlickThrow, anchorPath, new float3(1f, 0f, 0f), 1f);

            int executed = MagicReplayRunner.Run(replay, 3600u, executor);

            Assert.That(executed, Is.EqualTo(3));
            Assert.That(executor.SuccessfulCommandCount, Is.EqualTo(3));
            Assert.That(planet.State.EditCount, Is.EqualTo(1));
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(wallPool.ActiveCount, Is.EqualTo(1));
            Assert.That(pool.LastAcquired, Is.Not.Null);
            Assert.That(pool.LastAcquired.Body.isKinematic, Is.False);
            yield return new WaitForFixedUpdate();
            Assert.That(pool.LastAcquired.Body.linearVelocity.x, Is.GreaterThan(0f));

            GameObject dummyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummyObject.name = "Replay Impact Dummy";
            dummyObject.transform.position = new Vector3(2f, 8f, 0f);
            Rigidbody dummyBody = dummyObject.AddComponent<Rigidbody>();
            dummyBody.useGravity = false;
            dummyBody.mass = 10f;
            PhysicalImpactTarget dummy = dummyObject.AddComponent<PhysicalImpactTarget>();
            dummy.Configure(dummyBody, 1f);

            int impactsBeforeManualCheck = impactCount;
            int editsBeforeManualCheck = planet.State.EditCount;

            executor.ApplyFragmentImpact(
                pool.LastAcquired,
                dummyObject.transform.position,
                Vector3.left,
                100f,
                dummy,
                Vector3.right);

            Assert.That(impactCount, Is.EqualTo(impactsBeforeManualCheck + 1));
            Assert.That(planet.State.EditCount, Is.EqualTo(editsBeforeManualCheck + 1));
            Assert.That(dummy.ImpactCount, Is.EqualTo(1));
            Assert.That(dummy.AccumulatedImpulse, Is.EqualTo(100f).Within(0.001f));
            yield return new WaitForFixedUpdate();
            Assert.That(dummyBody.linearVelocity.x, Is.GreaterThan(0f));

            ChunkCoord checkpointChunk = new ChunkCoord(0, 1, 0);
            ulong replayHash = planet.State.ComputeChunkHash(checkpointChunk);
            using (var stream = new MemoryStream())
            {
                VoxelSaveCodec.Write(stream, planet.State);
                stream.Position = 0;
                VoxelPlanetState restored = VoxelSaveCodec.Read(stream);
                Assert.That(restored.EditCount, Is.EqualTo(planet.State.EditCount));
                Assert.That(restored.ComputeChunkHash(checkpointChunk), Is.EqualTo(replayHash));
            }

            Object.Destroy(dummyObject);
            Object.Destroy(executorObject);
            Object.Destroy(wallPoolObject);
            Object.Destroy(poolObject);
            Object.Destroy(planetObject);
            yield return null;
        }

        private static CompiledAbilityRecipe[] CreateRecipes()
        {
            var compiler = new AbilityCompiler();
            return new[]
            {
                compiler.Compile(new AbilityRecipeData(
                    EarthAbilityIds.LineWall,
                    MagicSelectorKind.PlanetSurface,
                    MagicGeometryKind.WallSpline,
                    new[] { MagicOperatorKind.AddSolid },
                    0.45f,
                    1f)),
                compiler.Compile(new AbilityRecipeData(
                    EarthAbilityIds.PullRock,
                    MagicSelectorKind.PlanetSurface,
                    MagicGeometryKind.AnchorSphere,
                    new[] { MagicOperatorKind.SubtractSolid, MagicOperatorKind.SpawnFragment },
                    1.2f,
                    1f)),
                compiler.Compile(new AbilityRecipeData(
                    EarthAbilityIds.FlickThrow,
                    MagicSelectorKind.HeldFragment,
                    MagicGeometryKind.Direction,
                    new[] { MagicOperatorKind.ApplyImpulse },
                    0.25f,
                    12f))
            };
        }

        private static void Record(
            MagicReplayRecorder replay,
            uint tick,
            AbilityId ability,
            IReadOnlyList<float3> path,
            float3 aim,
            float intensity)
        {
            var command = new MagicCommand(
                tick,
                1u,
                ElementId.Earth,
                ability,
                path[0],
                aim,
                path,
                intensity,
                0u,
                tick * 2654435761u);
            replay.Record(in command);
        }
    }
}
