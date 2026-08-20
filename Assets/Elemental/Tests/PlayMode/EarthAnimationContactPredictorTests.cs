using System.Collections;
using Elemental.Presentation.Animation;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthAnimationContactPredictorTests
    {
        [UnityTest]
        public IEnumerator PredictorRejectsFreeDebrisAndNeverMutatesCanonicalBody()
        {
            CreatePlayer(out GameObject player, out Rigidbody playerBody, out PlanetMotor motor,
                out EarthAnimationContactPredictor predictor);
            GameObject debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debris.name = "Free dynamic landing debris";
            debris.transform.SetPositionAndRotation(new Vector3(10000f, 0f, 0f), Quaternion.identity);
            debris.transform.localScale = new Vector3(4f, 0.5f, 4f);
            Rigidbody debrisBody = debris.AddComponent<Rigidbody>();
            debrisBody.useGravity = false;
            debrisBody.isKinematic = false;
            try
            {
                playerBody.linearVelocity = Vector3.down * 8f;
                Physics.SyncTransforms();
                Vector3 beforePosition = playerBody.position;
                Vector3 beforeVelocity = playerBody.linearVelocity;
                EarthLandingCandidateSnapshot candidate = predictor.Predict(0.65f, 6, 0f, 0.02f);
                Assert.That(candidate.IsValid, Is.False);
                Assert.That(playerBody.position, Is.EqualTo(beforePosition));
                Assert.That(playerBody.linearVelocity, Is.EqualTo(beforeVelocity));
            }
            finally
            {
                Object.Destroy(player);
                Object.Destroy(debris);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PredictorUsesMovingSupportPointVelocity()
        {
            CreatePlayer(out GameObject player, out Rigidbody playerBody, out PlanetMotor motor,
                out EarthAnimationContactPredictor predictor);
            GameObject supportObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            supportObject.name = "Predicted moving support";
            supportObject.transform.SetPositionAndRotation(new Vector3(10000f, 0f, 0f), Quaternion.identity);
            supportObject.transform.localScale = new Vector3(4f, 0.5f, 4f);
            Rigidbody supportBody = supportObject.AddComponent<Rigidbody>();
            supportBody.useGravity = false;
            supportBody.isKinematic = false;
            TestMovingAnimationSupport support = supportObject.AddComponent<TestMovingAnimationSupport>();
            support.SetFrame(new SupportFrameSnapshot(
                83u, 4u, float3.zero, quaternion.identity,
                new float3(0f, -2f, 0f), float3.zero,
                new float3(0f, -2f, 0f), new float3(0f, 1f, 0f), false));
            try
            {
                playerBody.linearVelocity = Vector3.down * 10f;
                Physics.SyncTransforms();
                EarthLandingCandidateSnapshot candidate = predictor.Predict(0.65f, 6, 0f, 0.02f);
                Assert.That(candidate.IsValid, Is.True);
                Assert.That(candidate.MovingSupport, Is.True);
                Assert.That(candidate.SurfaceId, Is.EqualTo(83u));
                Assert.That(candidate.Generation, Is.EqualTo(4u));
                Assert.That(candidate.SurfacePointVelocity.y, Is.EqualTo(-2f).Within(0.001f));
                Assert.That(candidate.ImpactSpeed, Is.EqualTo(8f).Within(0.15f));
                Assert.That(candidate.TimeToContact, Is.LessThan(0.06f),
                    "Prediction must target PlanetMotor support acquisition, not late collider contact.");
            }
            finally
            {
                Object.Destroy(player);
                Object.Destroy(supportObject);
            }
            yield return null;
        }

        private static void CreatePlayer(
            out GameObject player,
            out Rigidbody body,
            out PlanetMotor motor,
            out EarthAnimationContactPredictor predictor)
        {
            player = new GameObject("Animation predictor player");
            player.transform.position = new Vector3(10000f, 2f, 0f);
            body = player.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            CapsuleCollider capsule = player.AddComponent<CapsuleCollider>();
            capsule.radius = 0.5f;
            capsule.height = 2f;
            motor = player.AddComponent<PlanetMotor>();
            motor.Configure(null, body, capsule, null, null);
            predictor = player.AddComponent<EarthAnimationContactPredictor>();
            predictor.Configure(motor);
            body.isKinematic = false;
        }
    }

    public sealed class TestMovingAnimationSupport : MonoBehaviour, IMovingSurface
    {
        private SupportFrameSnapshot _frame;
        public uint SurfaceId => _frame.SurfaceId;
        public Vector3 SurfaceVelocity => new Vector3(
            _frame.LinearVelocity.x, _frame.LinearVelocity.y, _frame.LinearVelocity.z);
        public Vector3 SurfaceUp => new Vector3(_frame.Up.x, _frame.Up.y, _frame.Up.z);
        public bool IsEmerging => _frame.Emerging;
        public SupportFrameSnapshot SupportFrame => _frame;
        public MovingSupportSnapshot Snapshot => new MovingSupportSnapshot(in _frame);
        public void SetFrame(in SupportFrameSnapshot frame) => _frame = frame;
    }
}
