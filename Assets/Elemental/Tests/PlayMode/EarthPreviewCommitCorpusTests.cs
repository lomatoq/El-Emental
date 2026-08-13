using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Elemental.Input.Gestures;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Magic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthPreviewCommitCorpusTests
    {
        [UnityTest]
        public IEnumerator TwoHundredRecordedCameraCurvatureGesturesSharePreviewCommitHash()
        {
            List<PreviewFixture> fixtures = LoadCorpus(
                "Assets/Elemental/Tests/Replays/EarthPreviewCommitCorpus.csv");
            Assert.That(fixtures.Count, Is.GreaterThanOrEqualTo(200));

            GameObject planetObject = new GameObject("T06 Planet");
            planetObject.SetActive(false);
            VoxelPlanetBehaviour planet = planetObject.AddComponent<VoxelPlanetBehaviour>();
            planet.Configure(12f, 606u, 8, 1f, 4, 4, null);
            planetObject.SetActive(true);
            SphereCollider planetCollider = planetObject.AddComponent<SphereCollider>();

            GameObject fragmentPoolObject = new GameObject("T06 Fragment Pool");
            fragmentPoolObject.SetActive(false);
            EarthFragmentPool fragmentPool = fragmentPoolObject.AddComponent<EarthFragmentPool>();
            fragmentPool.Configure(1, null, null);
            fragmentPoolObject.SetActive(true);

            GameObject wallPoolObject = new GameObject("T06 Wall Pool");
            wallPoolObject.SetActive(false);
            EarthWallPool wallPool = wallPoolObject.AddComponent<EarthWallPool>();
            wallPool.Configure(8, null, null);
            wallPoolObject.SetActive(true);

            GameObject executorObject = new GameObject("T06 Executor");
            MagicExecutor executor = executorObject.AddComponent<MagicExecutor>();
            executor.Configure(planet, fragmentPool, planetObject.transform, wallPool);
            executor.ConfigureWallProfile(1.25f, 10.5f, 22f);
            executor.ConfigureRecipes(new[]
            {
                new AbilityCompiler().Compile(new AbilityRecipeData(
                    EarthAbilityIds.LineWall,
                    MagicSelectorKind.PlanetSurface,
                    MagicGeometryKind.WallSpline,
                    new[] { MagicOperatorKind.AddSolid },
                    0.45f,
                    1f))
            });

            GameObject cameraObject = new GameObject("T06 Camera");
            UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.pixelRect = new Rect(0f, 0f, 800f, 600f);
            camera.aspect = 4f / 3f;

            GameObject inputObject = new GameObject("T06 Input");
            inputObject.SetActive(false);
            LineRenderer line = inputObject.AddComponent<LineRenderer>();
            MagicInputController input = inputObject.AddComponent<MagicInputController>();
            input.Configure(null, camera, executor, planetCollider, line);

            var preview = new List<Vector3>(24);
            input.PreviewChanged += value =>
            {
                preview.Clear();
                for (int index = 0; index < value.Count; index++) preview.Add(value[index]);
            };

            for (int index = 0; index < fixtures.Count; index++)
            {
                PreviewFixture fixture = fixtures[index];
                planetCollider.radius = fixture.Radius;
                camera.fieldOfView = fixture.FieldOfView;
                float azimuth = math.radians(fixture.AzimuthDegrees);
                float elevation = math.radians(fixture.ElevationDegrees);
                Vector3 radial = new Vector3(
                    math.cos(elevation) * math.sin(azimuth),
                    math.sin(elevation),
                    math.cos(elevation) * math.cos(azimuth));
                camera.transform.position = radial * fixture.Distance;
                camera.transform.rotation = Quaternion.LookRotation(-radial, Vector3.up);
                Physics.SyncTransforms();

                var stroke = new List<float2>(2)
                {
                    new float2(fixture.StartX * 800f, fixture.StartY * 600f),
                    new float2(fixture.EndX * 800f, fixture.EndY * 600f)
                };
                preview.Clear();
                Assert.That(input.TryPreviewScreenPath(stroke, fixture.Duration), Is.True,
                    $"Preview fixture {fixture.Id} did not project.");
                Assert.That(preview.Count, Is.GreaterThanOrEqualTo(2),
                    $"Fixture {fixture.Id} did not retain enough projected surface points.");
                ulong previewHash = executor.LastPreviewGeometryHash;
                Assert.That(input.TryCommitScreenPath(stroke, fixture.Duration), Is.True,
                    $"Commit fixture {fixture.Id} failed.");

                EarthWall wall = wallPool.LastAcquired;
                Assert.That(wall, Is.Not.Null);
                Assert.That(executor.LastCommittedGeometryHash, Is.EqualTo(previewHash),
                    $"Fixture {fixture.Id} preview and commit did not share geometry hash.");
                Assert.That(Vector3.Distance(preview[0], wall.Start), Is.LessThanOrEqualTo(0.055f));
                Assert.That(Vector3.Distance(preview[preview.Count - 1], wall.End), Is.LessThanOrEqualTo(0.055f));
            }

            Assert.That(executor.SuccessfulCommandCount, Is.EqualTo(fixtures.Count));

            Object.Destroy(inputObject);
            Object.Destroy(cameraObject);
            Object.Destroy(executorObject);
            Object.Destroy(wallPoolObject);
            Object.Destroy(fragmentPoolObject);
            Object.Destroy(planetObject);
            yield return null;
        }

        private static List<PreviewFixture> LoadCorpus(string path)
        {
            string[] lines = File.ReadAllLines(path);
            var fixtures = new List<PreviewFixture>(lines.Length - 1);
            for (int index = 1; index < lines.Length; index++)
            {
                string[] value = lines[index].Split(',');
                fixtures.Add(new PreviewFixture(
                    int.Parse(value[0], CultureInfo.InvariantCulture),
                    Parse(value[1]), Parse(value[2]), Parse(value[3]), Parse(value[4]), Parse(value[5]),
                    Parse(value[6]), Parse(value[7]), Parse(value[8]), Parse(value[9]), Parse(value[10])));
            }
            return fixtures;
        }

        private static float Parse(string value) => float.Parse(value, CultureInfo.InvariantCulture);

        private readonly struct PreviewFixture
        {
            public PreviewFixture(
                int id, float radius, float azimuthDegrees, float elevationDegrees, float distance,
                float fieldOfView, float startX, float startY, float endX, float endY, float duration)
            {
                Id = id; Radius = radius; AzimuthDegrees = azimuthDegrees;
                ElevationDegrees = elevationDegrees; Distance = distance; FieldOfView = fieldOfView;
                StartX = startX; StartY = startY; EndX = endX; EndY = endY; Duration = duration;
            }

            public int Id { get; }
            public float Radius { get; }
            public float AzimuthDegrees { get; }
            public float ElevationDegrees { get; }
            public float Distance { get; }
            public float FieldOfView { get; }
            public float StartX { get; }
            public float StartY { get; }
            public float EndX { get; }
            public float EndY { get; }
            public float Duration { get; }
        }
    }
}
