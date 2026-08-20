using System.Collections;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthSurfaceQueryRuntimeTests
    {
        [UnityTest]
        public IEnumerator RaisedSupportWinsOverFartherPlanetAndStaleGenerationRejects()
        {
            GameObject serviceObject = new GameObject("Surface Queries");
            EarthSurfaceQueryService service = serviceObject.AddComponent<EarthSurfaceQueryService>();

            GameObject planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet.name = "Test Planet";
            planet.transform.localScale = Vector3.one * 20f;
            VoxelPlanetEarthSurfaceProvider planetProvider =
                planet.AddComponent<VoxelPlanetEarthSurfaceProvider>();
            planetProvider.Configure(planet.GetComponent<Collider>(), null, service);

            GameObject platformObject = new GameObject("Test Raised Platform");
            platformObject.AddComponent<MeshFilter>();
            platformObject.AddComponent<MeshRenderer>();
            platformObject.AddComponent<MeshCollider>();
            Rigidbody platformBody = platformObject.AddComponent<Rigidbody>();
            platformBody.useGravity = false;
            platformBody.isKinematic = true;
            EarthPlatform platform = platformObject.AddComponent<EarthPlatform>();
            platform.Configure(null, null);
            EarthPlatformSurfaceProvider platformProvider =
                platformObject.AddComponent<EarthPlatformSurfaceProvider>();
            platformProvider.Configure(platform, service);
            EarthPlatformGeometry geometry = SquarePlatform(12f);
            platform.Initialize(27u, in geometry, 1f, 0.25f);

            for (int index = 0; index < 36; index++) yield return new WaitForFixedUpdate();

            var query = new EarthSurfaceQuery(
                new float3(0f, 20f, 0f),
                new float3(0f, -1f, 0f),
                30f,
                EarthSurfaceCapabilities.Support | EarthSurfaceCapabilities.Pillar);
            Assert.That(service.TrySample(in query, out EarthSurfaceSample sample), Is.True);
            Assert.That(sample.Handle.Kind, Is.EqualTo(EarthSurfaceKind.Platform));
            Assert.That(sample.Handle.StableId, Is.EqualTo(27u));
            Assert.That(sample.Point.y, Is.GreaterThan(12.8f));
            Assert.That(sample.Distance, Is.LessThan(8f));

            EarthSurfaceHandle stale = sample.Handle;
            platform.Initialize(27u, in geometry, 1f, 0.25f);
            Assert.That(service.IsCurrent(in stale), Is.False);

            Object.Destroy(platformObject);
            Object.Destroy(planet);
            Object.Destroy(serviceObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProviderRegistrationIsExplicitAndAllocationFreeAfterWarmup()
        {
            GameObject serviceObject = new GameObject("Surface Queries");
            EarthSurfaceQueryService service = serviceObject.AddComponent<EarthSurfaceQueryService>();
            GameObject planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet.transform.localScale = Vector3.one * 20f;
            var provider = planet.AddComponent<VoxelPlanetEarthSurfaceProvider>();
            provider.Configure(planet.GetComponent<Collider>(), null, service);
            yield return null;

            Assert.That(service.ProviderCount, Is.EqualTo(1));
            var query = new EarthSurfaceQuery(
                new float3(0f, 15f, 0f), new float3(0f, -1f, 0f), 20f,
                EarthSurfaceCapabilities.Support);
            Assert.That(service.TrySample(in query, out _), Is.True);

            provider.enabled = false;
            Assert.That(service.ProviderCount, Is.Zero);
            Assert.That(service.TrySample(in query, out _), Is.False);

            Object.Destroy(planet);
            Object.Destroy(serviceObject);
            yield return null;
        }

        private static EarthPlatformGeometry SquarePlatform(float radius) =>
            new EarthPlatformGeometry(
                new float3(0f, radius, 0f),
                new float3(0f, 1f, 0f),
                new float3(1f, 0f, 0f),
                new float3(0f, 0f, 1f),
                new[]
                {
                    new float2(-2f, -2f), new float2(2f, -2f),
                    new float2(2f, 2f), new float2(-2f, 2f)
                },
                16f,
                radius);
    }
}
