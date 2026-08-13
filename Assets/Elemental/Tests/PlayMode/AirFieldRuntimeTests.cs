using System.Collections;
using System.Collections.Generic;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Fields;
using Elemental.Simulation.Magic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class AirFieldRuntimeTests
    {
        [UnityTest]
        public IEnumerator GustAndAirBrakeAffectRigidbodiesWithBoundedAcceleration()
        {
            GameObject worldObject = new GameObject("Air Runtime Test World");
            FieldWorldBehaviour world = worldObject.AddComponent<FieldWorldBehaviour>();
            world.Configure(8, 8, 20f, 8);
            FieldRegion gust = new FieldRegion(
                new FieldRegionId(1u), 1u, AirFieldKind.GustCorridor,
                new float3(-2f, 0f, 0f), new float3(1f, 0f, 0f), 3f, 8f, 18f, 1f, 5f, 200);
            world.Register(in gust);

            GameObject bodyObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bodyObject.transform.position = Vector3.zero;
            Rigidbody body = bodyObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = 1f;
            AirFieldBody airBody = bodyObject.AddComponent<AirFieldBody>();
            airBody.Configure(world, body, null, 0.8f, 1f, 0.1f, 35f);

            for (int index = 0; index < 12; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(body.linearVelocity.x, Is.GreaterThan(0.5f));
            Assert.That(airBody.LastAcceleration.magnitude, Is.LessThanOrEqualTo(35.01f));
            Assert.That(IsFinite(body.linearVelocity), Is.True);

            Object.Destroy(worldObject);
            Object.Destroy(bodyObject);
            yield return null;

            worldObject = new GameObject("Air Brake Runtime Test World");
            world = worldObject.AddComponent<FieldWorldBehaviour>();
            world.Configure(8, 8, 20f, 8);
            FieldRegion brake = new FieldRegion(
                new FieldRegionId(2u), 1u, AirFieldKind.AirBrake,
                float3.zero, new float3(0f, 1f, 0f), 5f, 0f, 8f, 0.5f, 5f, 220);
            world.Register(in brake);
            bodyObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bodyObject.transform.position = Vector3.zero;
            body = bodyObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = 1f;
            body.linearVelocity = new Vector3(0f, -18f, 0f);
            airBody = bodyObject.AddComponent<AirFieldBody>();
            airBody.Configure(world, body, null, 1f, 1f, 0f, 45f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.y, Is.GreaterThan(-18f));
            Assert.That(airBody.LastSample.DragMultiplier, Is.GreaterThan(1f));

            Object.Destroy(worldObject);
            Object.Destroy(bodyObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExecutorRecordsFourTypedAbilitiesAndEmitsEvents()
        {
            GameObject root = new GameObject("Air Executor Test");
            root.SetActive(false);
            FieldWorldBehaviour world = root.AddComponent<FieldWorldBehaviour>();
            world.Configure(16, 8, 20f, 8);
            AirMagicExecutor executor = root.AddComponent<AirMagicExecutor>();
            executor.Configure(world);
            executor.ConfigureRecipes(BuildRecipes());
            root.SetActive(true);

            int eventCount = 0;
            executor.Events.FieldSpawned += _ => eventCount++;
            AbilityId[] abilities =
            {
                AirAbilityIds.GustCorridor,
                AirAbilityIds.Vortex,
                AirAbilityIds.LiftColumn,
                AirAbilityIds.AirBrake
            };
            for (int index = 0; index < abilities.Length; index++)
            {
                var command = new MagicCommand(
                    (uint)index,
                    5u,
                    ElementId.Air,
                    abilities[index],
                    float3.zero,
                    new float3(0f, 1f, 0f),
                    new List<float3> { float3.zero, new float3(8f, 0f, 0f) },
                    0.75f,
                    0u,
                    (uint)(100 + index));
                Assert.That(executor.Execute(in command), Is.True);
            }

            Assert.That(executor.SuccessfulCommandCount, Is.EqualTo(4));
            Assert.That(executor.Recorder.Count, Is.EqualTo(4));
            Assert.That(world.World.Count, Is.EqualTo(4));
            Assert.That(eventCount, Is.EqualTo(4));
            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OneHundredBodiesKeepQueryWorkBounded()
        {
            GameObject root = new GameObject("Air Budget Test");
            FieldWorldBehaviour world = root.AddComponent<FieldWorldBehaviour>();
            world.Configure(64, 16, 20f, 12);
            for (uint index = 1; index <= 64; index++)
            {
                FieldRegion region = new FieldRegion(
                    new FieldRegionId(index), 1u, AirFieldKind.AirBrake,
                    float3.zero, new float3(0f, 1f, 0f), 25f, 0f, 2f, 0.2f, 5f, 100);
                world.Register(in region);
            }

            var objects = new GameObject[100];
            for (int index = 0; index < objects.Length; index++)
            {
                GameObject item = new GameObject($"Air Budget Body {index}");
                item.transform.position = new Vector3(index % 10, 0f, index / 10);
                Rigidbody body = item.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.constraints = RigidbodyConstraints.FreezeAll;
                item.AddComponent<AirFieldBody>().Configure(world, body, null, 0.2f, 0.5f, 0f, 10f);
                objects[index] = item;
            }

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            AirFieldBody[] bodies = Object.FindObjectsByType<AirFieldBody>();
            int checkedBodies = 0;
            for (int index = 0; index < bodies.Length; index++)
            {
                if (!bodies[index].name.StartsWith("Air Budget Body"))
                {
                    continue;
                }
                Assert.That(bodies[index].LastSample.RegionChecks, Is.LessThanOrEqualTo(16));
                checkedBodies++;
            }
            Assert.That(checkedBodies, Is.EqualTo(100));
            Assert.That(world.LastQueryDebt, Is.EqualTo(48));
            Assert.That(world.DeferredRegionUpdateCount, Is.EqualTo(52));

            for (int index = 0; index < objects.Length; index++)
            {
                Object.Destroy(objects[index]);
            }
            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WindLabRunsWithFiniteBodiesAndLivePresentation()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                "Assets/Elemental/Content/Scenes/WindLab.unity",
                LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;
            WindLabDriver driver = Object.FindAnyObjectByType<WindLabDriver>();
            FieldWorldBehaviour world = Object.FindAnyObjectByType<FieldWorldBehaviour>();
            Assert.That(driver, Is.Not.Null);
            Assert.That(world, Is.Not.Null);
            for (int tick = 0; tick < 80; tick++)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.That(driver.SuiteSpawnCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(world.World.Count, Is.GreaterThanOrEqualTo(4));
            AirFieldBody[] bodies = Object.FindObjectsByType<AirFieldBody>();
            Assert.That(bodies.Length, Is.GreaterThanOrEqualTo(60));
            for (int index = 0; index < bodies.Length; index++)
            {
                Assert.That(IsFinite(bodies[index].LastAcceleration), Is.True);
                Rigidbody body = bodies[index].GetComponent<Rigidbody>();
                Assert.That(IsFinite(body.linearVelocity), Is.True);
            }

            AsyncOperation unload = SceneManager.UnloadSceneAsync(
                SceneManager.GetSceneByPath("Assets/Elemental/Content/Scenes/WindLab.unity"));
            if (unload != null)
            {
                yield return unload;
            }
        }

        private static CompiledAbilityRecipe[] BuildRecipes()
        {
            var compiler = new AbilityCompiler();
            AbilityId[] ids =
            {
                AirAbilityIds.GustCorridor,
                AirAbilityIds.Vortex,
                AirAbilityIds.LiftColumn,
                AirAbilityIds.AirBrake
            };
            var recipes = new CompiledAbilityRecipe[ids.Length];
            for (int index = 0; index < ids.Length; index++)
            {
                recipes[index] = compiler.Compile(new AbilityRecipeData(
                    ids[index],
                    MagicSelectorKind.PlanetSurface,
                    MagicGeometryKind.Direction,
                    new[] { MagicOperatorKind.SpawnField },
                    1f,
                    1f));
            }
            return recipes;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }
    }
}
