using System;
using System.Collections;
using System.Collections.Generic;
using Elemental.Input.Gestures;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Fields;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Materials;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class CrossElementMagicIngressTests
    {
        [UnityTest]
        public IEnumerator SavedLabsWakeWithAnElementSpecificPlayableAbility()
        {
            yield return AssertSavedLabDefault(
                "Assets/Elemental/Content/Scenes/WindLab.unity",
                ElementId.Air,
                AirAbilityIds.GustCorridor);
            yield return AssertSavedLabDefault(
                "Assets/Elemental/Content/Scenes/ElementLab.unity",
                ElementId.Water,
                WaterAbilityIds.GatherWater);
        }

        [UnityTest]
        public IEnumerator RealScreenPathCommitsOneVisibleCommandThroughEverySchoolIngress()
        {
            var roots = new List<GameObject>(12);
            try
            {
                Camera camera = CreateCamera(roots);
                SphereCollider collider = CreatePlanetCollider(roots);
                IReadOnlyList<float2> line = BuildSurfaceLine(camera);

                MagicInputController input = CreateInput(roots);
                var observed = new List<MagicCommand>(4);
                input.MagicCommandExecuted += command => observed.Add(command);

                MagicExecutor earth = CreateEarthExecutor(roots, collider.transform);
                input.Configure(null, camera, earth, collider, null);
                Assert.That(input.TryCommitScreenPath(line, .45f), Is.True, "Earth input ingress rejected its default wall line.");
                AssertCommitted(observed, 0, ElementId.Earth, EarthAbilityIds.LineWall,
                    EarthHumanoidPoseSlot.RaiseWall);

                AirMagicExecutor air = CreateAirExecutor(roots);
                input.ConfigureAir(null, camera, air, collider, null);
                Assert.That(input.TryCommitScreenPath(line, .45f), Is.True, "Air input ingress rejected its default gust line.");
                AssertCommitted(observed, 1, ElementId.Air, AirAbilityIds.GustCorridor,
                    EarthHumanoidPoseSlot.VectorPush);

                ThermalWaterMagicExecutor thermal = CreateThermalExecutor(roots, collider);
                input.ConfigureThermalWater(null, camera, thermal, collider, null, ElementId.Fire);
                Assert.That(input.TryCommitScreenPath(line, .45f), Is.True, "Fire input ingress rejected its default heat line.");
                AssertCommitted(observed, 2, ElementId.Fire, FireAbilityIds.HeatJet,
                    EarthHumanoidPoseSlot.VectorPush);

                input.SelectElement(ElementId.Water);
                Assert.That(input.TryCommitScreenPath(line, .45f), Is.True, "Water input ingress rejected its default gather line.");
                AssertCommitted(observed, 3, ElementId.Water, WaterAbilityIds.GatherWater,
                    EarthHumanoidPoseSlot.PullStone);

                Assert.That(earth.SuccessfulCommandCount, Is.EqualTo(1));
                Assert.That(air.SuccessfulCommandCount, Is.EqualTo(1));
                Assert.That(thermal.SuccessfulCommandCount, Is.EqualTo(2));
                Assert.That(observed.Count, Is.EqualTo(4),
                    "Each accepted physical command must publish exactly one input presentation edge.");
            }
            finally
            {
                for (int index = roots.Count - 1; index >= 0; index--)
                    if (roots[index] != null) UnityEngine.Object.Destroy(roots[index]);
            }
            yield return null;
        }

        [Test]
        public void SerializedElementRestoresAPlayableDefaultInsteadOfEarthLineWall()
        {
            Assert.That(
                MagicGesturePolicy.NormalizeSelection(ElementId.Air, EarthAbilityIds.LineWall),
                Is.EqualTo(AirAbilityIds.GustCorridor));
            Assert.That(
                MagicGesturePolicy.NormalizeSelection(ElementId.Fire, EarthAbilityIds.LineWall),
                Is.EqualTo(FireAbilityIds.HeatJet));
            Assert.That(
                MagicGesturePolicy.NormalizeSelection(ElementId.Water, EarthAbilityIds.LineWall),
                Is.EqualTo(WaterAbilityIds.GatherWater));
            Assert.That(
                MagicGesturePolicy.NormalizeSelection(ElementId.Water, WaterAbilityIds.SteamBurst),
                Is.EqualTo(WaterAbilityIds.SteamBurst),
                "A valid runtime selection must survive a component re-enable.");
        }

        private static void AssertCommitted(
            IReadOnlyList<MagicCommand> observed,
            int index,
            ElementId element,
            AbilityId ability,
            EarthHumanoidPoseSlot slot)
        {
            Assert.That(observed.Count, Is.EqualTo(index + 1));
            MagicCommand command = observed[index];
            Assert.That(command.Element, Is.EqualTo(element));
            Assert.That(command.Ability, Is.EqualTo(ability));
            EarthTechniqueId technique = MagicPresentationSemanticResolver.ResolveTechnique(element, ability);
            Assert.That(EarthHumanoidMotionResolver.Resolve(technique), Is.EqualTo(slot));
        }

        private static IEnumerator AssertSavedLabDefault(
            string path,
            ElementId expectedElement,
            AbilityId expectedAbility)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null, path);
            yield return load;
            Scene scene = SceneManager.GetSceneByPath(path);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True, path);
            MagicInputController input = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                input = root.GetComponentInChildren<MagicInputController>(true);
                if (input != null) break;
            }
            Assert.That(input, Is.Not.Null, $"{path} has no configured MagicInputController.");
            Assert.That(input.SelectedElement, Is.EqualTo(expectedElement), path);
            Assert.That(input.SelectedAbility, Is.EqualTo(expectedAbility),
                $"{path} restored a cross-element scene with an ability owned by another school.");
            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) yield return unload;
        }

        private static Camera CreateCamera(ICollection<GameObject> roots)
        {
            var gameObject = new GameObject("Cross Element Input Camera");
            roots.Add(gameObject);
            Camera camera = gameObject.AddComponent<Camera>();
            camera.pixelRect = new Rect(0f, 0f, 800f, 600f);
            camera.transform.position = new Vector3(0f, 0f, -12f);
            camera.transform.LookAt(Vector3.zero);
            return camera;
        }

        private static SphereCollider CreatePlanetCollider(ICollection<GameObject> roots)
        {
            GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            gameObject.name = "Cross Element Input Planet";
            roots.Add(gameObject);
            SphereCollider collider = gameObject.GetComponent<SphereCollider>();
            collider.radius = 4f;
            UnityEngine.Object.Destroy(gameObject.GetComponent<MeshRenderer>());
            return collider;
        }

        private static IReadOnlyList<float2> BuildSurfaceLine(Camera camera)
        {
            float z = -Mathf.Sqrt(16f - 2.25f);
            Vector3 start = camera.WorldToScreenPoint(new Vector3(-1.5f, 0f, z));
            Vector3 end = camera.WorldToScreenPoint(new Vector3(1.5f, 0f, z));
            Assert.That(Vector2.Distance(start, end), Is.GreaterThan(40f));
            return new[]
            {
                new float2(start.x, start.y),
                new float2((start.x + end.x) * .5f, (start.y + end.y) * .5f),
                new float2(end.x, end.y)
            };
        }

        private static MagicInputController CreateInput(ICollection<GameObject> roots)
        {
            var gameObject = new GameObject("Cross Element Input Controller");
            roots.Add(gameObject);
            gameObject.SetActive(false);
            return gameObject.AddComponent<MagicInputController>();
        }

        private static MagicExecutor CreateEarthExecutor(
            ICollection<GameObject> roots,
            Transform planetCenter)
        {
            GameObject voxelObject = new GameObject("Cross Element Voxel Authority");
            roots.Add(voxelObject);
            voxelObject.SetActive(false);
            VoxelPlanetBehaviour voxel = voxelObject.AddComponent<VoxelPlanetBehaviour>();
            voxel.Configure(4f, 91u, 8, 1f, 4, 4, null);
            voxelObject.SetActive(true);

            GameObject fragmentObject = new GameObject("Cross Element Fragment Pool");
            roots.Add(fragmentObject);
            fragmentObject.SetActive(false);
            EarthFragmentPool fragments = fragmentObject.AddComponent<EarthFragmentPool>();
            fragments.Configure(1, null, null);
            fragmentObject.SetActive(true);

            GameObject wallObject = new GameObject("Cross Element Wall Pool");
            roots.Add(wallObject);
            wallObject.SetActive(false);
            EarthWallPool walls = wallObject.AddComponent<EarthWallPool>();
            walls.Configure(1, null, null);
            wallObject.SetActive(true);

            GameObject executorObject = new GameObject("Cross Element Earth Executor");
            roots.Add(executorObject);
            MagicExecutor executor = executorObject.AddComponent<MagicExecutor>();
            executor.Configure(voxel, fragments, planetCenter, walls);
            executor.ConfigureRecipes(new[]
            {
                Compile(EarthAbilityIds.LineWall, MagicGeometryKind.WallSpline, MagicOperatorKind.AddSolid)
            });
            return executor;
        }

        private static AirMagicExecutor CreateAirExecutor(ICollection<GameObject> roots)
        {
            GameObject worldObject = new GameObject("Cross Element Air Runtime");
            roots.Add(worldObject);
            worldObject.SetActive(false);
            FieldWorldBehaviour world = worldObject.AddComponent<FieldWorldBehaviour>();
            world.Configure(8, 8, 20f, 8);
            AirMagicExecutor executor = worldObject.AddComponent<AirMagicExecutor>();
            executor.Configure(world);
            executor.ConfigureRecipes(new[]
            {
                Compile(AirAbilityIds.GustCorridor, MagicGeometryKind.Direction, MagicOperatorKind.SpawnField)
            });
            worldObject.SetActive(true);
            return executor;
        }

        private static ThermalWaterMagicExecutor CreateThermalExecutor(
            ICollection<GameObject> roots,
            Collider planet)
        {
            GameObject worldObject = new GameObject("Cross Element Thermal Runtime");
            roots.Add(worldObject);
            worldObject.SetActive(false);
            ThermalWaterWorldBehaviour world = worldObject.AddComponent<ThermalWaterWorldBehaviour>();
            world.Configure(8, 8, 8, 10f, 8);
            ThermalWaterMagicExecutor executor = worldObject.AddComponent<ThermalWaterMagicExecutor>();
            executor.Configure(world);
            executor.ConfigureRecipes(new[]
            {
                Compile(FireAbilityIds.HeatJet, MagicGeometryKind.Direction, MagicOperatorKind.AddHeat),
                Compile(WaterAbilityIds.GatherWater, MagicGeometryKind.Direction, MagicOperatorKind.TransferMass)
            });
            MaterialDefinition material = MaterialDefinition.Water;
            var water = new WaterVolume(
                new WaterVolumeId(1),
                91u,
                new float3(0f, 0f, -4f),
                float3.zero,
                1f,
                new PhaseState(material.Id, PhaseKind.Liquid, 20f, 4f));
            Assert.That(world.Water.Register(in water), Is.True);
            worldObject.SetActive(true);
            return executor;
        }

        private static CompiledAbilityRecipe Compile(
            AbilityId id,
            MagicGeometryKind geometry,
            MagicOperatorKind operation) => new AbilityCompiler().Compile(
                new AbilityRecipeData(
                    id,
                    MagicSelectorKind.PlanetSurface,
                    geometry,
                    new[] { operation },
                    1f,
                    1f));
    }
}
