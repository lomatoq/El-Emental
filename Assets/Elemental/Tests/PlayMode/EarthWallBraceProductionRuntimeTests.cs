using System.Collections;
using System.IO;
using Elemental.Input.Gestures;
using Elemental.Presentation.Animation;
using Elemental.Runtime.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.Profiling;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [UnityTest]
        public IEnumerator BlockedForwardMovementBracesRealWallAndReleasesHands()
        {
            Actor actor = _actors.Find(value => value.Presentation.GetComponent<EarthCharacterPoseController>() != null);
            Assert.That(actor, Is.Not.Null);
            PlanetMotor motor = actor.Presentation.GetComponentInParent<PlanetMotor>();
            Animator animator = actor.Presentation.Animator;
            Camera proofCamera = actor.Presentation.GetComponentInParent<MagicInputController>().CastCamera;
            Assert.That(proofCamera, Is.Not.Null);
            string reportDirectory = Path.GetFullPath("BuildReports/WallBrace");
            Directory.CreateDirectory(reportDirectory);
            var wallMarker = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Elemental.Character.WallBrace", 64);
            var input = motor.gameObject.AddComponent<AirborneMantleProofInput>();
            motor.ConfigureInputSource(input);
            Vector3 up = motor.LocalUp.normalized;
            Vector3 forward = Vector3.ProjectOnPlane(motor.FacingForward, up).normalized;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall brace physical contact proof";
            SceneManager.MoveGameObjectToScene(wall, _scene);
            wall.transform.SetPositionAndRotation(motor.SupportFeetPoint(up) + forward * .8f + up * 2.5f,
                Quaternion.LookRotation(forward, up));
            wall.transform.localScale = new Vector3(4f, 5f, .5f);
            for (int layer = 0; layer < 32; layer++)
                if ((motor.GroundMask.value & (1 << layer)) != 0) { wall.layer = layer; break; }
            Collider shape = wall.GetComponent<Collider>();
            try
            {
                Physics.SyncTransforms();
                input.Move = new float2(0f, 1f);
                double until = Time.realtimeSinceStartupAsDouble + 4d;
                while (actor.Presentation.WallBraceWeight < .7f && Time.realtimeSinceStartupAsDouble < until)
                    yield return null;
                Assert.That(motor.IsMantling, Is.False, "A five-meter wall must remain a brace, not a mantle.");
                Transform leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                Transform rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                string admission = $"weight={actor.Presentation.WallBraceWeight}, reach={actor.Presentation.WallBraceArmReach}, action={actor.Presentation.CurrentAuthoredAction}, support={motor.HasStableSupport}, velocity={motor.Body.linearVelocity}, intent={motor.LastCommand.Move}, shoulderWallDistance={Vector3.Distance(leftShoulder.position, shape.ClosestPoint(leftShoulder.position)):F4}/{Vector3.Distance(rightShoulder.position, shape.ClosestPoint(rightShoulder.position)):F4}, leftShoulder={leftShoulder.position}, rightShoulder={rightShoulder.position}, facing={motor.FacingForward}, up={motor.LocalUp}, capsuleRadius={motor.Capsule.radius}, scale={motor.transform.lossyScale}, wall={wall.transform.position}";
                Assert.That(actor.Presentation.WallBraceWeight, Is.GreaterThan(.7f), admission);
                Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                double totalNanoseconds = 0d;
                long maximumNanoseconds = 0L;
                int measuredSamples = 0;
                for (int i = 0; i < 30; i++)
                {
                    yield return null;
                    long elapsed = wallMarker.LastValue;
                    if (wallMarker.Valid && elapsed > 0L)
                    {
                        totalNanoseconds += elapsed;
                        maximumNanoseconds = System.Math.Max(maximumNanoseconds, elapsed);
                        measuredSamples++;
                    }
                }
                string contacts = $"brace={actor.Presentation.WallBraceWeight:F3}, submitted={actor.Presentation.WallBraceSubmittedHands}, reach={actor.Presentation.WallBraceArmReach}, contactDistance={actor.Presentation.WallBraceContactDistance}, action={actor.Presentation.CurrentAuthoredAction}, support={motor.HasStableSupport}, localLeftShoulder={motor.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position)}, localRightShoulder={motor.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position)}, localHands={motor.transform.InverseTransformPoint(left.position)}/{motor.transform.InverseTransformPoint(rightHand.position)}, facing={motor.FacingForward}, wall={wall.transform.position}";
                Assert.That(Vector3.Distance(left.position, shape.ClosestPoint(left.position)), Is.LessThan(.25f), contacts);
                Assert.That(Vector3.Distance(rightHand.position, shape.ClosestPoint(rightHand.position)), Is.LessThan(.25f), contacts);
                CaptureComboCamera(proofCamera, Path.Combine(reportDirectory, "Braced.png"));
                File.WriteAllText(Path.Combine(reportDirectory, "ContactAndProfilerLatest.json"), JsonUtility.ToJson(new WallBraceProofReport
                {
                    leftHandDistance = Vector3.Distance(left.position, shape.ClosestPoint(left.position)),
                    rightHandDistance = Vector3.Distance(rightHand.position, shape.ClosestPoint(rightHand.position)),
                    braceWeight = actor.Presentation.WallBraceWeight,
                    measuredMarkerSamples = measuredSamples,
                    meanMarkerMicroseconds = measuredSamples > 0 ? totalNanoseconds / measuredSamples / 1000d : 0d,
                    maximumMarkerMicroseconds = maximumNanoseconds / 1000d
                }, true));
                wall.transform.position += right * .06f;
                Physics.SyncTransforms();
                for (int i = 0; i < 12; i++) yield return null;
                Assert.That(Vector3.Distance(left.position, shape.ClosestPoint(left.position)), Is.LessThan(.25f));
                input.Move = float2.zero;
                until = Time.realtimeSinceStartupAsDouble + 1d;
                while (actor.Presentation.WallBraceWeight > .001f && Time.realtimeSinceStartupAsDouble < until)
                    yield return null;
                Assert.That(actor.Presentation.WallBraceWeight, Is.LessThanOrEqualTo(.001f));
                CaptureComboCamera(proofCamera, Path.Combine(reportDirectory, "Released.png"));
            }
            finally
            {
                input.Move = float2.zero;
                wallMarker.Dispose();
                Object.Destroy(wall);
            }
        }

        [System.Serializable]
        private sealed class WallBraceProofReport
        {
            public float leftHandDistance, rightHandDistance, braceWeight;
            public int measuredMarkerSamples;
            public double meanMarkerMicroseconds, maximumMarkerMicroseconds;
        }
    }
}
