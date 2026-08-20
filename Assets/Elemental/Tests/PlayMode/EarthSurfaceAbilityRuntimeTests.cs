using System.Collections;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthSurfaceAbilityRuntimeTests
    {
        [UnityTest]
        public IEnumerator PillarAndLandingPreferRaisedPlatformSupport()
        {
            GameObject serviceObject = new GameObject("Ability Surface Queries");
            EarthSurfaceQueryService service = serviceObject.AddComponent<EarthSurfaceQueryService>();
            GameObject planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet.transform.localScale = Vector3.one * 20f;
            var planetProvider = planet.AddComponent<VoxelPlanetEarthSurfaceProvider>();
            planetProvider.Configure(planet.GetComponent<Collider>(), null, service);

            EarthPlatform platform = CreatePlatform(service, 12f);
            for (int index = 0; index < 36; index++) yield return new WaitForFixedUpdate();

            GameObject actor = new GameObject("Surface Ability Actor");
            Rigidbody body = actor.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.position = new Vector3(0f, 14.3f, 0f);
            EarthPillarMobility pillar = actor.AddComponent<EarthPillarMobility>();
            pillar.Configure(body, null, service);
            EarthLandingCushion landing = actor.AddComponent<EarthLandingCushion>();
            landing.Configure(body, null, null, null, null, null, service);

            Assert.That(pillar.TryResolveSupport(body.worldCenterOfMass, Vector3.up, out EarthSurfaceSample support),
                Is.True);
            Assert.That(support.Handle.Kind, Is.EqualTo(EarthSurfaceKind.Platform));
            Assert.That(landing.TryResolveLandingSurface(
                new Vector3(0f, 20f, 0f), new Vector3(0f, 10f, 0f), out EarthSurfaceSample landingSurface),
                Is.True);
            Assert.That(landingSurface.Handle.Kind, Is.EqualTo(EarthSurfaceKind.Platform));
            Assert.That(landingSurface.Handle, Is.EqualTo(support.Handle));

            Object.Destroy(actor);
            Object.Destroy(platform.gameObject);
            Object.Destroy(planet);
            Object.Destroy(serviceObject);
            yield return null;
        }

        private static EarthPlatform CreatePlatform(EarthSurfaceQueryService service, float radius)
        {
            GameObject go = new GameObject("Ability Platform");
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            go.AddComponent<MeshCollider>();
            Rigidbody body = go.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            EarthPlatform platform = go.AddComponent<EarthPlatform>();
            platform.Configure(null, null);
            EarthPlatformSurfaceProvider provider = go.AddComponent<EarthPlatformSurfaceProvider>();
            provider.Configure(platform, service);
            var geometry = new EarthPlatformGeometry(
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
            platform.Initialize(91u, in geometry, 1f, 0.25f);
            return platform;
        }
    }
}
