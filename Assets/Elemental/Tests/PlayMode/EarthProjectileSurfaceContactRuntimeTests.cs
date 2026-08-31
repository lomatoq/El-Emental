using System.Collections;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthProjectileSurfaceContactRuntimeTests
    {
        [UnityTest]
        public IEnumerator NearSurfaceTangentFlightStaysArmedAndDirectWallImpactSpendsProjectile()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Projectile Graze Floor";
            floor.transform.SetPositionAndRotation(new Vector3(0f, -0.5f, 0f), Quaternion.identity);
            floor.transform.localScale = new Vector3(12f, 1f, 4f);

            CreateProjectile(
                "Tangent Projectile",
                new Vector3(-2f, 0.248f, 0f),
                Vector3.right,
                12f,
                out GameObject tangentObject,
                out EarthFragment tangentFragment,
                out EarthMvpMagicProjectile tangentProjectile);
            int tangentImpacts = 0;
            tangentFragment.SurfaceImpactAccepted += _ => tangentImpacts++;
            float tangentStartX = tangentObject.transform.position.x;

            for (int index = 0; index < 4; index++)
                yield return new WaitForFixedUpdate();

            Assert.That(tangentProjectile.State, Is.EqualTo(EarthMvpProjectileState.Armed),
                "A shallow physical graze must not consume the projectile's combat effect.");
            Assert.That(tangentImpacts, Is.Zero,
                "A tangent contact is physical motion, not a gameplay impact episode.");
            Assert.That(tangentObject.transform.position.x, Is.GreaterThan(tangentStartX + 0.25f),
                "The rejected graze must preserve useful travel along the surface.");

            Object.Destroy(tangentObject);
            Object.Destroy(floor);
            yield return null;

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Projectile Direct Impact Wall";
            wall.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            wall.transform.localScale = new Vector3(0.3f, 4f, 4f);

            CreateProjectile(
                "Direct Projectile",
                new Vector3(-2f, 0.75f, 0f),
                Vector3.right,
                16f,
                out GameObject directObject,
                out EarthFragment directFragment,
                out EarthMvpMagicProjectile directProjectile);
            int directImpacts = 0;
            directFragment.SurfaceImpactAccepted += _ => directImpacts++;
            float deadline = Time.time + 0.5f;
            while (directProjectile.State == EarthMvpProjectileState.Armed && Time.time < deadline)
                yield return new WaitForFixedUpdate();

            Assert.That(directProjectile.State,
                Is.EqualTo(EarthMvpProjectileState.SpentDynamic),
                "A genuine normal wall hit must still resolve the projectile.");
            Assert.That(directImpacts, Is.EqualTo(1),
                "One wall contact episode must publish exactly one gameplay impact.");

            Object.Destroy(directObject);
            Object.Destroy(wall);
            yield return null;
        }

        private static void CreateProjectile(
            string name,
            Vector3 position,
            Vector3 direction,
            float speed,
            out GameObject projectileObject,
            out EarthFragment fragment,
            out EarthMvpMagicProjectile projectile)
        {
            projectileObject = new GameObject(name);
            Rigidbody body = projectileObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = 18f;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            SphereCollider shape = projectileObject.AddComponent<SphereCollider>();
            shape.radius = 0.5f;
            fragment = projectileObject.AddComponent<EarthFragment>();
            projectile = projectileObject.AddComponent<EarthMvpMagicProjectile>();
            fragment.Initialize(1u, null, position, 0.25f, body.mass);
            projectileObject.transform.localScale = Vector3.one * 0.5f;
            projectile.Configure(fragment, null, null, null, direction, 11.8f, 2.1f);
            fragment.LaunchProjectile(direction, speed, null, 0.1f);
            Physics.SyncTransforms();
        }
    }
}
