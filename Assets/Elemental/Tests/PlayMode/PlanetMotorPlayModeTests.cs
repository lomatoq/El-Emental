using System.Collections;
using Elemental.Input.Actions;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Gravity;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class PlanetMotorPlayModeTests
    {
        private sealed class ManualInputSource : MonoBehaviour, IPlanetMotorInputSource
        {
            public float2 Move;
            public bool JumpQueued;

            public PlanetMotorCommand SampleCommand(uint tick)
            {
                PlanetMotorCommand command = new PlanetMotorCommand(tick, Move, JumpQueued);
                JumpQueued = false;
                return command;
            }
        }

        private sealed class Fixture
        {
            public GameObject Root;
            public GameObject Planet;
            public GameObject Character;
            public GameObject CameraFrame;
            public Rigidbody Body;
            public PlanetMotor Motor;
            public GravityBody GravityBody;
            public ManualInputSource Input;
            public PlanetInputReader BufferedJumpInput;
            public ActiveRagdollPuppet Puppet;
            public Vector3 RadialUp;
            public Vector3 Center;
            public bool OwnsCameraFrame;
        }

        [UnityTest]
        public IEnumerator Motor_GroundsMovesAndJumpsAtNorthPole()
        {
            Fixture fixture = CreateFixture(Vector3.up);

            for (int tick = 0; tick < 50; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(fixture.Motor.IsGrounded, Is.True);
            AssertFinite(fixture.Body.position);
            AssertFinite(fixture.Motor.LocalUp);

            fixture.Input.Move = new float2(0f, 1f);
            for (int tick = 0; tick < 12; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Vector3 tangentVelocity = Vector3.ProjectOnPlane(fixture.Body.linearVelocity, fixture.RadialUp);
            Assert.That(tangentVelocity.magnitude, Is.GreaterThan(0.1f));

            fixture.Input.Move = float2.zero;
            fixture.Input.JumpQueued = true;
            yield return new WaitForFixedUpdate();

            Assert.That(Vector3.Dot(fixture.Body.linearVelocity, fixture.Motor.LocalUp), Is.GreaterThan(0f));
            Assert.That(fixture.Motor.IsGrounded, Is.False);
            DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator Motor_GroundsAtAntipodeWithoutNaNOrOrientationFlip()
        {
            Fixture fixture = CreateFixture(Vector3.down);

            for (int tick = 0; tick < 60; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(fixture.Motor.IsGrounded, Is.True);
            Assert.That(Vector3.Dot(fixture.Motor.LocalUp, Vector3.down), Is.GreaterThan(0.99f));
            AssertFinite(fixture.Body.position);
            AssertFinite(fixture.Body.linearVelocity);
            AssertFinite(fixture.Body.angularVelocity);
            AssertFinite(fixture.Character.transform.up);
            DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator Motor_RejectsCloserDynamicDebrisAsSupport()
        {
            Fixture fixture = CreateFixture(Vector3.up, new Vector3(420f, 0f, 0f), true);
            var debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debris.name = "Closer Dynamic Debris";
            debris.transform.position = fixture.Center + fixture.RadialUp * 10.01f;
            debris.transform.localScale = new Vector3(0.45f, 0.02f, 0.45f);
            Rigidbody debrisBody = debris.AddComponent<Rigidbody>();
            debrisBody.useGravity = false;
            debrisBody.constraints = RigidbodyConstraints.FreezeAll;

            for (int tick = 0; tick < 50; tick++)
                yield return new WaitForFixedUpdate();

            Assert.That(fixture.Motor.IsGrounded, Is.True);
            Assert.That(fixture.Motor.GroundSupport.HasSupport, Is.True);
            Assert.That(
                fixture.Motor.GroundSupport.Candidate.Kind,
                Is.EqualTo(CharacterSupportKind.PlanetGround),
                "A closer dynamic collider may not steal canonical support from static ground.");

            Object.Destroy(debris);
            DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator TankSteering_ADTurnsInPlaceThenWMovesAlongNewHeading()
        {
            Fixture fixture = CreateFixture(Vector3.up);
            fixture.Motor.ConfigureTankSteering(true, 180f);
            for (int tick = 0; tick < 50; tick++) yield return new WaitForFixedUpdate();

            Vector3 start = fixture.Body.position;
            Vector3 initialForward = Vector3.ProjectOnPlane(
                fixture.Character.transform.forward, fixture.RadialUp).normalized;
            fixture.Input.Move = new float2(1f, 0f);
            for (int tick = 0; tick < 30; tick++) yield return new WaitForFixedUpdate();

            Vector3 turnedForward = Vector3.ProjectOnPlane(
                fixture.Character.transform.forward, fixture.RadialUp).normalized;
            Assert.That(Vector3.Angle(initialForward, turnedForward), Is.GreaterThan(30f));
            Assert.That(Vector3.Distance(start, fixture.Body.position), Is.LessThan(0.8f));

            Vector3 beforeForwardMove = fixture.Body.position;
            fixture.Input.Move = new float2(0f, 1f);
            for (int tick = 0; tick < 18; tick++) yield return new WaitForFixedUpdate();
            Vector3 displacement = Vector3.ProjectOnPlane(
                fixture.Body.position - beforeForwardMove, fixture.RadialUp);
            Assert.That(displacement.magnitude, Is.GreaterThan(0.25f));
            Assert.That(Vector3.Dot(displacement.normalized, turnedForward), Is.GreaterThan(0.45f));

            DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator TankSteering_MovesAndTurnsOnSphericalGroundAwayFromArenaPole()
        {
            Fixture fixture = CreateFixture(Vector3.right);
            fixture.Motor.ConfigureTankSteering(true, 170f);
            for (int tick = 0; tick < 60; tick++) yield return new WaitForFixedUpdate();

            Assert.That(fixture.Motor.IsGrounded, Is.True);
            Assert.That(Vector3.Dot(fixture.Motor.LocalUp, Vector3.right), Is.GreaterThan(0.99f));
            Vector3 start = fixture.Body.position;
            Vector3 initialForward = Vector3.ProjectOnPlane(
                fixture.Character.transform.forward, fixture.Motor.LocalUp).normalized;

            fixture.Input.Move = new float2(0.55f, 1f);
            for (int tick = 0; tick < 36; tick++) yield return new WaitForFixedUpdate();

            Vector3 displacement = fixture.Body.position - start;
            Vector3 turnedForward = Vector3.ProjectOnPlane(
                fixture.Character.transform.forward, fixture.Motor.LocalUp).normalized;
            Assert.That(displacement.magnitude, Is.GreaterThan(0.5f),
                "Tank locomotion must remain active on the spherical planet, not only on the arena floor.");
            Assert.That(Vector3.Angle(initialForward, turnedForward), Is.GreaterThan(20f),
                "The canonical body must turn with the locomotion command on curved ground.");
            Assert.That(Vector3.Dot(fixture.GravityBody.LastAcceleration, -fixture.Motor.LocalUp),
                Is.GreaterThan(12f));

            DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator ProfiledJump_UsesShortBallisticArcUnderSurfaceGravity()
        {
            Fixture fixture = CreateFixture(Vector3.up);
            PlanetMotorFeelProfile profile = ScriptableObject.CreateInstance<PlanetMotorFeelProfile>();
            JsonUtility.FromJsonOverwrite("{\"jumpSpeed\":3.2}", profile);
            fixture.Motor.ConfigureFeel(profile);
            for (int tick = 0; tick < 60; tick++) yield return new WaitForFixedUpdate();

            Assert.That(fixture.Motor.IsGrounded, Is.True);
            float launchRadius = Vector3.Distance(fixture.Body.worldCenterOfMass, fixture.Center);
            float maximumRise = 0f;
            bool airborne = false;
            fixture.Input.JumpQueued = true;
            for (int tick = 0; tick < 90; tick++)
            {
                yield return new WaitForFixedUpdate();
                float rise = Vector3.Distance(fixture.Body.worldCenterOfMass, fixture.Center) - launchRadius;
                maximumRise = Mathf.Max(maximumRise, rise);
                airborne |= !fixture.Motor.IsGrounded;
                if (airborne && tick > 12 && fixture.Motor.IsGrounded) break;
            }

            Assert.That(airborne, Is.True);
            Assert.That(maximumRise, Is.InRange(0.18f, 0.75f),
                "The tuned jump must read as a compact hop; a multi-metre float indicates lost gravity or a stale jump default.");
            Assert.That(fixture.Motor.IsGrounded, Is.True,
                "The tuned jump must return to spherical support within the bounded test window.");

            Object.Destroy(profile);
            DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator SameUpdateJumpTap_LaunchesRadiallyWithoutDestabilizingPuppet()
        {
            Fixture fixture = CreateFixture(Vector3.up, withBufferedJumpInput: true);
            for (int tick = 0; tick < 60; tick++) yield return new WaitForFixedUpdate();

            Assert.That(fixture.Motor.IsGrounded, Is.True);
            Assert.That(fixture.BufferedJumpInput, Is.Not.Null);
            Assert.That(fixture.Puppet, Is.Not.Null);
            fixture.Puppet.SuppressImpacts(2f);

            var router = new EarthActionRouter();
            EarthActionRoute begin = router.Step(new EarthActionRouterFrame(
                Time.unscaledTime,
                grounded: true,
                stableSupport: true,
                jumpPressed: true,
                jumpHeld: false,
                jumpReleased: true));
            if (begin.Phase == EarthActionRoutePhase.Begin)
                fixture.BufferedJumpInput.RouteJumpStarted();

            EarthActionRoute commit = router.Step(new EarthActionRouterFrame(
                Time.unscaledTime + 0.016f,
                grounded: true,
                stableSupport: true,
                jumpHeld: false));
            if (commit.Phase == EarthActionRoutePhase.Commit)
                fixture.BufferedJumpInput.RouteJumpCanceled();

            yield return new WaitForFixedUpdate();

            float radialSpeed = Vector3.Dot(
                fixture.Body.linearVelocity,
                fixture.Motor.LocalUp);
            Assert.That(begin.Phase, Is.EqualTo(EarthActionRoutePhase.Begin));
            Assert.That(commit.Phase, Is.EqualTo(EarthActionRoutePhase.Commit));
            Assert.That(radialSpeed, Is.GreaterThan(0.5f),
                "A short Space press/release must reach PlanetMotor's buffered jump on the next physics tick.");
            Assert.That(fixture.Puppet.CurrentState.Mode, Is.Not.EqualTo(CharacterPhysicalMode.Stagger));
            Assert.That(fixture.Puppet.CurrentState.Mode, Is.Not.EqualTo(CharacterPhysicalMode.FullRagdoll));

            DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator FixedCommandReplay_CircumnavigatesAndMatchesIsolatedWorld()
        {
            // Keep deterministic fixture physics outside any production planet that
            // may already be open while the focused PlayMode suite runs.
            Fixture first = CreateFixture(Vector3.up, new Vector3(-240f, 0f, 0f), true);
            Fixture second = CreateFixture(Vector3.up, new Vector3(240f, 0f, 0f), true);
            WaitForFixedUpdate fixedUpdate = new WaitForFixedUpdate();

            for (int tick = 0; tick < 50; tick++)
            {
                yield return fixedUpdate;
            }

            first.Input.Move = new float2(0f, 1f);
            second.Input.Move = new float2(0f, 1f);
            Vector3 previousDirection = (first.Body.position - first.Center).normalized;
            float accumulatedAngle = 0f;

            for (int tick = 0; tick < 460; tick++)
            {
                yield return fixedUpdate;

                Vector3 firstLocal = first.Body.position - first.Center;
                Vector3 secondLocal = second.Body.position - second.Center;
                AssertFinite(firstLocal);
                AssertFinite(secondLocal);
                Assert.That(Vector3.Distance(firstLocal, secondLocal), Is.LessThan(0.05f));

                Vector3 direction = firstLocal.normalized;
                accumulatedAngle += Vector3.Angle(previousDirection, direction);
                previousDirection = direction;
            }

            Assert.That(accumulatedAngle, Is.GreaterThan(300f));
            Assert.That(first.Motor.IsGrounded, Is.True);
            Assert.That(second.Motor.IsGrounded, Is.True);
            DestroyFixture(first);
            DestroyFixture(second);
        }

        private static Fixture CreateFixture(
            Vector3 radialUp,
            Vector3 center = default,
            bool useCharacterAsCameraFrame = false,
            bool withBufferedJumpInput = false)
        {
            if (center == default) center = new Vector3(240f, 0f, 0f);
            Fixture fixture = new Fixture
            {
                RadialUp = radialUp.normalized,
                Center = center
            };

            fixture.Root = new GameObject("Planet Motor Test World");
            fixture.Root.SetActive(false);
            fixture.Root.transform.position = center;
            PointPlanetGravitySource source = fixture.Root.AddComponent<PointPlanetGravitySource>();
            source.Configure(new GravityFieldId(1u), 10f, 14f, 1f, 50f);
            GravityWorldBehaviour world = fixture.Root.AddComponent<GravityWorldBehaviour>();
            world.Configure(new[] { source });

            fixture.Planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fixture.Planet.name = "Test Planet";
            fixture.Planet.transform.position = center;
            fixture.Planet.transform.localScale = Vector3.one * 20f;

            fixture.Character = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            fixture.Character.name = "Test Planet Character";
            fixture.Character.SetActive(false);
            fixture.Character.transform.rotation = Quaternion.FromToRotation(Vector3.up, fixture.RadialUp);
            fixture.Character.transform.position = center + (fixture.RadialUp * 11.05f);
            fixture.Body = fixture.Character.AddComponent<Rigidbody>();
            fixture.Body.mass = 20f;
            fixture.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            CapsuleCollider capsule = fixture.Character.GetComponent<CapsuleCollider>();
            fixture.GravityBody = fixture.Character.AddComponent<GravityBody>();
            fixture.GravityBody.Configure(world, fixture.Body);
            fixture.Input = fixture.Character.AddComponent<ManualInputSource>();
            fixture.Motor = fixture.Character.AddComponent<PlanetMotor>();
            Transform cameraFrame;
            if (useCharacterAsCameraFrame)
            {
                fixture.CameraFrame = fixture.Character;
                fixture.OwnsCameraFrame = false;
                cameraFrame = fixture.Character.transform;
            }
            else
            {
                fixture.CameraFrame = new GameObject("Test Camera Frame");
                fixture.OwnsCameraFrame = true;
                fixture.CameraFrame.transform.rotation = Quaternion.LookRotation(Vector3.forward, fixture.RadialUp);
                cameraFrame = fixture.CameraFrame.transform;
            }

            fixture.Motor.Configure(world, fixture.Body, capsule, fixture.Input, cameraFrame);

            if (withBufferedJumpInput)
            {
                EarthPillarMobility pillar = fixture.Character.AddComponent<EarthPillarMobility>();
                pillar.Configure(fixture.Body, fixture.Motor);
                EarthInputAdapter inputAdapter = fixture.Character.AddComponent<EarthInputAdapter>();
                inputAdapter.enabled = false;
                fixture.BufferedJumpInput = fixture.Character.AddComponent<PlanetInputReader>();
                fixture.BufferedJumpInput.enabled = false;
                fixture.BufferedJumpInput.Configure(inputAdapter, pillar);
                fixture.Motor.ConfigureInputSource(fixture.BufferedJumpInput);
                fixture.Puppet = fixture.Character.AddComponent<ActiveRagdollPuppet>();
                fixture.Puppet.Configure(
                    1u,
                    world,
                    fixture.Body,
                    fixture.Motor,
                    null,
                    fixture.Character.transform,
                    System.Array.Empty<ActiveRagdollJoint>(),
                    new Collider[] { capsule });
            }

            fixture.Root.SetActive(true);
            fixture.Character.SetActive(true);
            return fixture;
        }

        private static void DestroyFixture(Fixture fixture)
        {
            Object.Destroy(fixture.Character);
            if (fixture.OwnsCameraFrame)
            {
                Object.Destroy(fixture.CameraFrame);
            }
            Object.Destroy(fixture.Planet);
            Object.Destroy(fixture.Root);
        }

        private static void AssertFinite(Vector3 value)
        {
            Assert.That(float.IsFinite(value.x), Is.True);
            Assert.That(float.IsFinite(value.y), Is.True);
            Assert.That(float.IsFinite(value.z), Is.True);
        }
    }
}
