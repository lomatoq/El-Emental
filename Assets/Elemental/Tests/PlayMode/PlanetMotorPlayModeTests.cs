using System.Collections;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Gravity;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class PlanetMotorPlayModeTests
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
            public ManualInputSource Input;
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
            bool useCharacterAsCameraFrame = false)
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
            GravityBody gravityBody = fixture.Character.AddComponent<GravityBody>();
            gravityBody.Configure(world, fixture.Body);
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
