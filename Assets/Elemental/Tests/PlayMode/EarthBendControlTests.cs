using System.Collections;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthBendControlTests
    {
        [UnityTest]
        public IEnumerator HeldFragmentRemainsDynamicAndMovesOnlyThroughForce()
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Rigidbody body = rock.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = 40f;
            EarthFragment fragment = rock.AddComponent<EarthFragment>();
            fragment.Initialize(1u, null, Vector3.zero, 0.5f, body.mass);
            fragment.BeginBendControl(new Vector3(3f, 0f, 0f), Vector3.zero, 0f, BendTuning.Default);

            Vector3 start = rock.transform.position;
            yield return new WaitForFixedUpdate();

            Assert.That(body.isKinematic, Is.False);
            Assert.That(fragment.IsHeld, Is.True);
            Assert.That(rock.transform.position.x, Is.GreaterThanOrEqualTo(start.x));
            Assert.That(body.linearVelocity.x, Is.GreaterThan(0f));
            Assert.That(fragment.LastAppliedControlForce.x, Is.GreaterThan(0f));

            Object.Destroy(rock);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HeavyRockFallsFurtherBehindSameMovingTarget()
        {
            EarthFragment light = CreateFragment("Light Bend Rock", 20f, new Vector3(-2f, 0f, 0f));
            EarthFragment heavy = CreateFragment("Heavy Bend Rock", 400f, new Vector3(-2f, 0f, 1f));
            BendTuning tuning = new BendTuning(maximumControlForce: 2400f);
            light.BeginBendControl(new Vector3(4f, 0f, 0f), Vector3.zero, 0f, tuning);
            heavy.BeginBendControl(new Vector3(4f, 0f, 1f), Vector3.zero, 0f, tuning);

            for (int index = 0; index < 20; index++) yield return new WaitForFixedUpdate();

            float lightError = light.LastControlError.magnitude;
            float heavyError = heavy.LastControlError.magnitude;
            Assert.That(heavyError, Is.GreaterThan(lightError + 0.25f));

            Object.Destroy(light.gameObject);
            Object.Destroy(heavy.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReleaseKeepsExistingMotionAndAddsGestureTrajectory()
        {
            EarthFragment fragment = CreateFragment("Release Bend Rock", 30f, Vector3.zero);
            fragment.BeginBendControl(Vector3.zero, Vector3.zero, 0f, BendTuning.Default);
            fragment.Body.linearVelocity = new Vector3(2f, 0f, 0f);

            Vector3 released = fragment.ReleaseBend(Vector3.forward, new Vector3(3f, 2f, 0f), 0.5f);
            yield return new WaitForFixedUpdate();

            Assert.That(fragment.IsHeld, Is.False);
            Assert.That(fragment.Body.isKinematic, Is.False);
            Assert.That(released.x, Is.GreaterThan(2f));
            Assert.That(released.y, Is.GreaterThan(0f));
            Assert.That(released.z, Is.GreaterThan(0f));

            Object.Destroy(fragment.gameObject);
            yield return null;
        }

        private static EarthFragment CreateFragment(string name, float mass, Vector3 position)
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = name;
            Rigidbody body = rock.AddComponent<Rigidbody>();
            body.useGravity = false;
            EarthFragment fragment = rock.AddComponent<EarthFragment>();
            fragment.Initialize(1u, null, position, 0.5f, mass);
            return fragment;
        }
    }
}
