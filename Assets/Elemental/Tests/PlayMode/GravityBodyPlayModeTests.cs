using System.Collections;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Gravity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class GravityBodyPlayModeTests
    {
        private sealed class CollisionCounter : MonoBehaviour
        {
            public int Count { get; private set; }
            private void OnCollisionEnter(Collision collision) => Count++;
        }

        [UnityTest]
        public IEnumerator BodiesOnAllAxesFallTowardPlanetWithoutGlobalGravity()
        {
            GameObject worldObject = new GameObject("Gravity Test World");
            worldObject.SetActive(false);

            PointPlanetGravitySource source = worldObject.AddComponent<PointPlanetGravitySource>();
            source.Configure(new GravityFieldId(1u), 10f, 12f, 1f, 50f);
            GravityWorldBehaviour world = worldObject.AddComponent<GravityWorldBehaviour>();
            world.Configure(new[] { source });
            worldObject.SetActive(true);

            Vector3[] directions =
            {
                Vector3.right,
                Vector3.left,
                Vector3.up,
                Vector3.down,
                Vector3.forward,
                Vector3.back
            };
            Rigidbody[] bodies = new Rigidbody[directions.Length];
            float[] startingDistances = new float[directions.Length];

            for (int index = 0; index < directions.Length; index++)
            {
                GameObject bodyObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bodyObject.name = $"Gravity Body {index}";
                bodyObject.SetActive(false);
                bodyObject.transform.position = directions[index] * 20f;
                Rigidbody body = bodyObject.AddComponent<Rigidbody>();
                GravityBody gravityBody = bodyObject.AddComponent<GravityBody>();
                gravityBody.Configure(world, body);
                bodyObject.SetActive(true);
                bodies[index] = body;
                startingDistances[index] = body.position.magnitude;
            }

            for (int tick = 0; tick < 20; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            for (int index = 0; index < bodies.Length; index++)
            {
                Assert.That(bodies[index].useGravity, Is.False);
                Assert.That(bodies[index].position.magnitude, Is.LessThan(startingDistances[index]));
                Object.Destroy(bodies[index].gameObject);
            }

            Object.Destroy(worldObject);
        }

        [UnityTest]
        public IEnumerator HighSpeedContinuousBodyDoesNotTunnelThroughPlanet()
        {
            GameObject planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet.name = "Tunneling Regression Planet";
            planet.transform.localScale = Vector3.one * 20f;

            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "Tunneling Regression Body";
            projectile.transform.position = new Vector3(24f, 0f, 0f);
            projectile.transform.localScale = Vector3.one * 0.5f;
            Rigidbody body = projectile.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = new Vector3(-120f, 0f, 0f);
            CollisionCounter counter = projectile.AddComponent<CollisionCounter>();

            for (int tick = 0; tick < 8; tick++)
                yield return new WaitForFixedUpdate();

            Assert.That(counter.Count, Is.GreaterThan(0));
            Assert.That(body.position.magnitude, Is.GreaterThanOrEqualTo(9.5f));
            Assert.That(float.IsFinite(body.position.x), Is.True);

            Object.Destroy(projectile);
            Object.Destroy(planet);
            yield return null;
        }
    }
}
