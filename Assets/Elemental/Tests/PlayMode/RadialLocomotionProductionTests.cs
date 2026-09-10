using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Rendering;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [Serializable] private sealed class RadialRunFrame
        {
            public int normal, frame;
            public Vector3 up, root, left, right, leftHint, rightHint, laneLocalRoot;
            public float sourceFrameAngle, speed, leftKnee, rightKnee, leftWeight, rightWeight;
            public bool grounded, leftLocked, rightLocked;
            public string leftReason, rightReason;
            public float eamm;
            public string sourceStatus;
        }
        [Serializable] private sealed class RadialRunReport
        {
            public string scope = "Saved Linebreaker, real radial gravity and physical tangent lanes at four globe normals; no gameplay root animation writes. Native rendered frames, not a performance benchmark.";
            public List<RadialRunFrame> frames = new List<RadialRunFrame>();
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator ActualSavedFighterRunsWithCoherentLegsAtFourPlanetNormals()
        {
            Actor actor = ShortPlayer();
            var motor = actor.Presentation.GetComponentInParent<PlanetMotor>();
            var puppet = motor.GetComponent<ActiveRagdollPuppet>();
            var animator = actor.Presentation.Animator;
            var feet = actor.Presentation.FootContactController;
            var source = actor.Bridge.SourceController;
            Assert.That(source, Is.Not.Null);
            Assert.That(puppet, Is.Not.Null);
            var planet = _scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PointPlanetGravitySource>(true))
                .OrderByDescending(p => p.Radius).First();
            Vector3 center = planet.transform.position;
            Vector3 originalUp = motor.LocalUp.normalized;
            Vector3 tangent = Vector3.ProjectOnPlane(motor.FacingForward, originalUp).normalized;
            Vector3 axis = Vector3.Cross(originalUp, tangent).normalized;
            Vector3 originalFeet = motor.SupportFeetPoint(originalUp);
            float rootHeight = Vector3.Dot(motor.Body.position - originalFeet, originalUp);
            float radius = Mathf.Max(planet.Radius, Vector3.Distance(center, originalFeet)) + 20f;
            Vector3 originalPosition = motor.Body.position;
            Quaternion originalRotation = motor.Body.rotation;
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var report = new RadialRunReport();
            var violations = new List<string>();
            string folder = "BuildReports/HardPolish/G03/RadialLocomotion-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(folder);
            Camera camera = _scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CelestialSystemBehaviour>(true)).Single().TargetCamera;
            Vector3 cameraPosition = camera.transform.position;
            Quaternion cameraRotation = camera.transform.rotation;
            var capture = camera.gameObject.AddComponent<RadialLocomotionCaptureCamera>();
            var resolution = new ProductionCaptureResolution();
            float previousCapture = Time.captureDeltaTime;
            GameObject lane = null;
            try
            {
                yield return resolution.WaitForRenderedSize(camera);
                Time.captureDeltaTime = 1f / 60f;
                foreach (int angle in new[] { 0, 90, 180, 270 })
                {
                    actor.Input.Move = float2.zero;
                    Quaternion rotation = Quaternion.AngleAxis(angle, axis);
                    Vector3 up = rotation * originalUp, forward = rotation * tangent;
                    Vector3 start = center + up * radius;
                    lane = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    lane.name = "Radial locomotion acceptance lane " + angle;
                    SceneManager.MoveGameObjectToScene(lane, _scene);
                    lane.transform.SetPositionAndRotation(start + forward * 8f - up * .5f, Quaternion.LookRotation(forward, up));
                    lane.transform.localScale = new Vector3(16f, 1f, 32f);
                    for (int layer = 0; layer < 32; layer++)
                        if ((motor.GroundMask.value & (1 << layer)) != 0) { lane.layer = layer; break; }
                    puppet.ResetPhysicalState(start + up * (rootHeight + .04f), Quaternion.LookRotation(forward, up));
                    SynchronizeRadialRelocationRoot(motor.Body);
                    motor.ResetAfterTeleport();
                    Physics.SyncTransforms();
                    Assert.That(Vector3.Angle(motor.FacingForward, forward), Is.LessThan(.1f),
                        "Relocation must seed aim from the new physical/render root, not the last lane turn.");
                    Assert.That(Vector3.Angle(motor.LocalUp, up), Is.LessThan(.1f));
                    double deadline = Time.realtimeSinceStartupAsDouble + 10d;
                    while ((!motor.HasStableSupport || Vector3.Dot(motor.LocalUp, up) < .995f) &&
                        Time.realtimeSinceStartupAsDouble < deadline) yield return _frame;
                    Assert.That(motor.HasStableSupport, Is.True, "Actual radial lane did not establish support at " + angle);
                    for (int settle = 0; settle < 30; settle++) yield return _frame;
                    float peakSpeed = 0f;
                    float minimumLeft = float.MaxValue, maximumLeft = float.MinValue;
                    float minimumRight = float.MaxValue, maximumRight = float.MinValue;
                    int movingFrames = 0, contacts = 0;
                    for (int frame = 0; frame < 240; frame++)
                    {
                        actor.Input.Move = frame < 120 ? new float2(0f, 1f) :
                            frame >= 150 && frame < 210 ? new float2(.7f, 0f) : float2.zero;
                        capture.Place(motor.transform.position + motor.FacingForward * 4.8f +
                            Vector3.Cross(motor.LocalUp, motor.FacingForward) * 3f + motor.LocalUp * .4f,
                            motor.transform.position, motor.LocalUp);
                        yield return _frame;
                        if (frame % 25 == 0) ProductionCaptureResolution.SaveScreen(Path.Combine(folder, angle + "-" + frame.ToString("D3") + ".png"));
                        Vector3 radialUp = motor.LocalUp.normalized;
                        float sourceAngle = Quaternion.Angle(source.SkeletonTransforms[0].rotation, motor.transform.rotation);
                        float leftHeight = Vector3.Dot(left.position - hips.position, radialUp);
                        float rightHeight = Vector3.Dot(right.position - hips.position, radialUp);
                        float speed = Vector3.ProjectOnPlane(motor.Body.linearVelocity, radialUp).magnitude;
                        peakSpeed = Mathf.Max(peakSpeed, speed);
                        if (speed > 1f)
                        {
                            movingFrames++;
                            minimumLeft = Mathf.Min(minimumLeft, leftHeight); maximumLeft = Mathf.Max(maximumLeft, leftHeight);
                            minimumRight = Mathf.Min(minimumRight, rightHeight); maximumRight = Mathf.Max(maximumRight, rightHeight);
                        }
                        if (feet.LeftFootLocked || feet.RightFootLocked) contacts++;
                        if (!motor.HasStableSupport) violations.Add(angle + "/" + frame + " lost physical lane support at " + lane.transform.InverseTransformPoint(motor.Body.position));
                        if (!float.IsFinite(leftHeight) || !float.IsFinite(rightHeight) ||
                            (leftHeight > -.08f && rightHeight > -.08f)) violations.Add(angle + "/" + frame + " collapsed/non-finite legs");
                        if (sourceAngle > .5f) violations.Add(angle + "/" + frame + " hidden source frame drift " + sourceAngle);
                        if (actor.Bridge.PoseRejectionReason == "source-up-diverged") violations.Add(angle + "/" + frame + " valid radial pose rejected");
                        report.frames.Add(new RadialRunFrame { normal = angle, frame = frame, up = radialUp,
                            root = motor.transform.position, left = left.position, right = right.position,
                            laneLocalRoot = lane.transform.InverseTransformPoint(motor.Body.position),
                            leftHint = feet.LeftKneeHintDirectionWorld, rightHint = feet.RightKneeHintDirectionWorld,
                            sourceFrameAngle = sourceAngle, speed = speed, leftKnee = feet.LeftKneeAngleDegrees,
                            rightKnee = feet.RightKneeAngleDegrees, leftWeight = feet.LeftFootIkWeight,
                            rightWeight = feet.RightFootIkWeight, grounded = motor.HasStableSupport,
                            sourceStatus = actor.Bridge.PoseRejectionReason, eamm = actor.Bridge.AppliedEammMasterWeight,
                            leftLocked = feet.LeftFootLocked, rightLocked = feet.RightFootLocked,
                            leftReason = feet.LeftReason.ToString(), rightReason = feet.RightReason.ToString() });
                    }
                    if (peakSpeed < 2f || movingFrames < 30) violations.Add(angle + " failed to exercise actual running");
                    if (maximumLeft - minimumLeft < .04f || maximumRight - minimumRight < .04f)
                        violations.Add(angle + " both authored legs must step through meaningful vertical travel");
                    if (contacts < 5) violations.Add(angle + " no meaningful final contact IK samples");
                    actor.Input.Move = float2.zero;
                    UnityEngine.Object.DestroyImmediate(lane); lane = null;
                }
            }
            finally
            {
                actor.Input.Move = float2.zero;
                Time.captureDeltaTime = previousCapture;
                UnityEngine.Object.DestroyImmediate(capture);
                camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
                resolution.Dispose();
                if (lane != null) UnityEngine.Object.DestroyImmediate(lane);
                puppet.ResetPhysicalState(originalPosition, originalRotation);
                SynchronizeRadialRelocationRoot(motor.Body);
                motor.ResetAfterTeleport();
                File.WriteAllText(Path.Combine(folder, "frames.json"), JsonUtility.ToJson(report, true));
                File.WriteAllLines(Path.Combine(folder, "violations.txt"), violations);
            }
            Assert.That(violations, Is.Empty, string.Join("\n", violations.Take(30)));
        }

        private static void SynchronizeRadialRelocationRoot(Rigidbody body)
        {
            // Same publication sequence as EarthMvpDuelController's canonical
            // respawn: ResetAfterTeleport reads transform.up/forward, while a
            // Rigidbody teleport can still expose the prior interpolated pose.
            Vector3 position = body.position;
            Quaternion rotation = body.rotation;
            RigidbodyInterpolation interpolation = body.interpolation;
            body.interpolation = RigidbodyInterpolation.None;
            body.transform.SetPositionAndRotation(position, rotation);
            body.interpolation = interpolation;
            Physics.SyncTransforms();
        }
    }
    [DefaultExecutionOrder(32000)]
    public sealed class RadialLocomotionCaptureCamera : MonoBehaviour
    {
        private Vector3 position;
        private Quaternion rotation;
        public void Place(Vector3 at, Vector3 subject, Vector3 up)
        { position = at; rotation = Quaternion.LookRotation(subject - at, up); }
        private void LateUpdate() => transform.SetPositionAndRotation(position, rotation);
    }
}
