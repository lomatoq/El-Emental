using System.Collections;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class PlanetMotorPlayModeTests
    {
        [UnityTest]
        public IEnumerator MotorUsesActualAnchoredRockTopInsteadOfFloorBelow()
        {
            Fixture fixture = CreateFixture(Vector3.up, new Vector3(460f, 0f, 0f), true);
            var rockObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                rockObject.name = "Actual decor support above terrain";
                rockObject.transform.position = fixture.Center + Vector3.up * 10.2f;
                rockObject.transform.localScale = new Vector3(3f, .4f, 3f);
                var rockBody = rockObject.AddComponent<Rigidbody>();
                rockBody.useGravity = false;
                var rock = rockObject.AddComponent<EarthDestructibleDecorRock>();
                // This fixture exercises support, not fracture. The actual rock
                // contract remains bound; omit its unrelated pool-start work.
                rock.enabled = false;
                rock.Configure(0xD3ABC001, rockBody, rockObject.GetComponent<Collider>(), null,
                    null, 1f, 100000f, true);
                fixture.Body.position += Vector3.up * .5f;
                Physics.SyncTransforms();
                for (int i = 0; i < 80; i++) yield return new WaitForFixedUpdate();
                Assert.That(fixture.Motor.IsGrounded, Is.True);
                Assert.That(fixture.Motor.GroundSupport.Candidate.Kind, Is.EqualTo(CharacterSupportKind.SettledMatter));
                Assert.That(fixture.Motor.GroundSupport.Candidate.SurfaceId, Is.EqualTo(rock.StableEarthId));
                Assert.That(Vector3.Dot(fixture.Motor.SupportFeetPoint(Vector3.up) -
                    (rockObject.transform.position + Vector3.up * .2f), Vector3.up), Is.InRange(-.03f, .05f));
            }
            finally { Object.Destroy(rockObject); DestroyFixture(fixture); }
        }

        [UnityTest]
        public IEnumerator ReleasedEarthRockSupportsAfterSleepAndKeepsIdentityAcrossContactWake()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                go.transform.position = new Vector3(480, 20, 0);
                var body = go.AddComponent<Rigidbody>(); body.useGravity = false;
                var shape = go.GetComponent<Collider>();
                var rock = go.AddComponent<EarthDestructibleDecorRock>();
                rock.enabled = false;
                rock.Configure(0xD3ABC002, body, shape, null, null, .5f, 100000, false);
                body.Sleep();
                var sleeping = CharacterSupportRuntimeAdapter.Classify(shape, .01f, 1f);
                Assert.That(sleeping.Kind, Is.EqualTo(CharacterSupportKind.SettledMatter));
                var prior = new CharacterSupportSelection(true, sleeping, false);
                body.WakeUp(); body.linearVelocity = Vector3.right * .05f;
                var awake = CharacterSupportRuntimeAdapter.Classify(shape, .01f, 1f, prior);
                Assert.That(awake.Kind, Is.EqualTo(CharacterSupportKind.SettledMatter));
                Assert.That(awake.Generation, Is.EqualTo(rock.TargetHandle.Generation));
                body.linearVelocity = Vector3.right * 3f;
                var thrown = CharacterSupportRuntimeAdapter.Classify(shape, .01f, 1f, prior);
                Assert.That(thrown.IsWalkable, Is.False);
                body.linearVelocity = Vector3.zero;
                var apex = CharacterSupportRuntimeAdapter.Classify(shape, .01f, 1f);
                Assert.That(apex.IsWalkable, Is.False);
                yield return null;
            }
            finally { Object.Destroy(go); }
        }
    }
}
