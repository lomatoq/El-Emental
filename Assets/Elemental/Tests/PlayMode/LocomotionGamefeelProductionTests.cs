using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Rendering;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [Serializable] private sealed class GamefeelFrame
        {
            public string scenario;
            public int fps, frame;
            public float time, speed, desiredSpeed, yaw, phase, leftWeight, rightWeight, eamm;
            public bool leftLocked, rightLocked, step, leftRelease, rightRelease;
            public string shortTransition;
            public Vector3 root, left, right, leftTarget, rightTarget, leftToes, rightToes, leftProjectedToes, rightProjectedToes;
            public Quaternion leftRotation,rightRotation;
            public float leftFloorCorrection,rightFloorCorrection,leftPredictedClearance,rightPredictedClearance,leftCaptureToeLift,rightCaptureToeLift;
        }
        [Serializable] private sealed class GamefeelReport
        {
            public string scope = "Actual saved Linebreaker actor; deterministic 60/120Hz motion clock, real motor/graph/IK. Native 1080 sequence at 30 sampled frames/s for 60Hz pass. Capture timing is not performance evidence. Visual inspection remains required.";
            public List<GamefeelFrame> frames = new List<GamefeelFrame>();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator ActualSavedFighterRunStartsStopsAndTurnReleaseStayContinuous()
        {
            Actor actor = ShortPlayer();
            var presentation = actor.Presentation;
            var motor = presentation.GetComponentInParent<PlanetMotor>();
            var animator = presentation.GetComponent<Animator>();
            var driver = presentation.GetComponent<EarthAnimationDriver>();
            var feet = presentation.FootContactController;
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform leftToes=animator.GetBoneTransform(HumanBodyBones.LeftToes),rightToes=animator.GetBoneTransform(HumanBodyBones.RightToes);
            var celestial = _scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CelestialSystemBehaviour>(true)).Single();
            Camera camera = celestial.TargetCamera;
            var output = new ProductionCaptureResolution();
            var report = new GamefeelReport();
            string folder = "BuildReports/HardPolish/G03/LocomotionGamefeel";
            Directory.CreateDirectory(folder);
            float previousCaptureDelta = Time.captureDeltaTime;
            Vector3 cameraPosition = camera.transform.position;
            Quaternion cameraRotation = camera.transform.rotation;
            FireVisualCaptureCamera capture = camera.gameObject.AddComponent<FireVisualCaptureCamera>();
            var violations = new List<string>();
            try
            {
                yield return output.WaitForRenderedSize(camera);
                foreach (int fps in new[] { 60, 120 })
                {
                    Time.captureDeltaTime = 1f / fps;
                    foreach (string scenario in new[] { "run-stop-reverse", "left-turn", "right-turn" })
                    {
                        actor.Input.Move = float2.zero;
                        yield return new WaitForSeconds(1f);
                        Assert.That(motor.HasStableSupport, Is.True, "Actual arena actor has no stable starting support.");
                        Vector3 previousForward = motor.FacingForward;
                        Vector3 initialForward = previousForward;
                        Vector3 previousLeft = motor.transform.InverseTransformPoint(left.position);
                        Vector3 previousRight = motor.transform.InverseTransformPoint(right.position);
                        Quaternion previousLeftRotation=Quaternion.Inverse(motor.transform.rotation)*left.rotation;
                        Quaternion previousRightRotation=Quaternion.Inverse(motor.transform.rotation)*right.rotation;
                        bool previousLeftLocked = feet.LeftFootLocked, previousRightLocked = feet.RightFootLocked;
                        float yaw = 0f; int leftReleases = 0, rightReleases = 0;
                        bool turning = scenario != "run-stop-reverse";
                        int frames = fps * 3;
                        for (int frame = 0; frame < frames; frame++)
                        {
                            float t = frame / (float)fps;
                            actor.Input.Move = turning
                                ? new float2(t < 1.3f ? (scenario == "left-turn" ? -1f : 1f) : 0f, 0f)
                                : new float2(0f, t < .65f ? 1f : t < 1.15f ? 0f : t < 1.65f ? -1f : 0f);
                            Vector3 focus = motor.transform.position;
                            capture.Place(focus + initialForward * 4.8f + Vector3.Cross(motor.LocalUp, initialForward) * 3f + motor.LocalUp * .4f, focus);
                            yield return _frame;
                            Vector3 localLeft = motor.transform.InverseTransformPoint(left.position);
                            Vector3 localRight = motor.transform.InverseTransformPoint(right.position);
                            yaw += Mathf.Abs(Vector3.SignedAngle(previousForward, motor.FacingForward, motor.LocalUp));
                            bool leftRelease = previousLeftLocked && !feet.LeftFootLocked;
                            bool rightRelease = previousRightLocked && !feet.RightFootLocked;
                            if (leftRelease) leftReleases++;
                            if (rightRelease) rightReleases++;
                            // This measures the exact release frame that old whole-cycle
                            // height-span checks missed. No floor tolerance is relaxed.
                            if (turning && leftRelease && Vector3.Distance(localLeft, previousLeft) * fps / 60f > .08f)
                                violations.Add(scenario + " " + fps + "Hz left release discontinuity at " + frame);
                            if (turning && rightRelease && Vector3.Distance(localRight, previousRight) * fps / 60f > .08f)
                                violations.Add(scenario + " " + fps + "Hz right release discontinuity at " + frame);
                            if(turning&&leftRelease&&!feet.LeftReleaseRotationBasisReady)
                                violations.Add(scenario+" "+fps+"Hz left release lacks completed-solve rotation calibration at "+frame);
                            if(turning&&rightRelease&&!feet.RightReleaseRotationBasisReady)
                                violations.Add(scenario+" "+fps+"Hz right release lacks completed-solve rotation calibration at "+frame);
                            Quaternion localLeftRotation=Quaternion.Inverse(motor.transform.rotation)*left.rotation;
                            Quaternion localRightRotation=Quaternion.Inverse(motor.transform.rotation)*right.rotation;
                            // 720 deg/s leaves ample room over the 170 deg/s tank
                            // yaw plus authored ankle articulation, while rejecting
                            // the observed 56.7 degree single-frame contact handoff.
                            if(turning && leftRelease && Quaternion.Angle(previousLeftRotation,localLeftRotation)*fps/60f>12f)
                                violations.Add(scenario+" "+fps+"Hz left release rotation discontinuity at "+frame);
                            if(turning && rightRelease && Quaternion.Angle(previousRightRotation,localRightRotation)*fps/60f>12f)
                                violations.Add(scenario+" "+fps+"Hz right release rotation discontinuity at "+frame);
                            if (!turning && presentation.ShortTransition == EarthShortTransition.StartWalk)
                                violations.Add("Full-speed run incorrectly entered the slow walking fragment at " + frame);
                            report.frames.Add(new GamefeelFrame { scenario = scenario, fps = fps, frame = frame, time = t,
                                speed = motor.LocomotionMotion.Speed, desiredSpeed = math.length(motor.LocomotionMotion.DesiredVelocity),
                                yaw = yaw, phase = driver.GetCurrentAnimatorStateInfo(0).normalizedTime,
                                leftWeight = feet.LeftFootIkWeight, rightWeight = feet.RightFootIkWeight,
                                leftLocked = feet.LeftFootLocked, rightLocked = feet.RightFootLocked,
                                leftRelease = feet.LeftReleaseTransitionActive, rightRelease = feet.RightReleaseTransitionActive,
                                step = presentation.AuthoredTurnStepActive, shortTransition = presentation.ShortTransition.ToString(),
                                eamm = actor.Bridge != null ? actor.Bridge.AppliedEammMasterWeight : 0f,
                                root = motor.transform.position, left = left.position, right = right.position,
                                leftTarget = feet.LeftTargetWorld, rightTarget = feet.RightTargetWorld,
                                leftToes=leftToes!=null?leftToes.position:left.position,rightToes=rightToes!=null?rightToes.position:right.position,
                                leftRotation=left.rotation,rightRotation=right.rotation,
                                leftProjectedToes=feet.LeftFloorProjectedToesWorld,rightProjectedToes=feet.RightFloorProjectedToesWorld,
                                leftFloorCorrection=feet.LeftSwingFloorCorrectionMeters,rightFloorCorrection=feet.RightSwingFloorCorrectionMeters,
                                leftPredictedClearance=feet.LeftFloorPredictedClearance,rightPredictedClearance=feet.RightFloorPredictedClearance,
                                leftCaptureToeLift=feet.LeftPivotCaptureToeLift,rightCaptureToeLift=feet.RightPivotCaptureToeLift });
                            if (fps == 60 && frame % 2 == 0)
                                ProductionCaptureResolution.SaveScreen(Path.Combine(folder, scenario + "-" + (frame / 2).ToString("D3") + ".png"));
                            previousLeftRotation=localLeftRotation;previousRightRotation=localRightRotation;
                            previousForward = motor.FacingForward;
                            previousLeft = localLeft; previousRight = localRight;
                            previousLeftLocked = feet.LeftFootLocked; previousRightLocked = feet.RightFootLocked;
                        }
                        if (turning)
                        {
                            if (yaw < 150f) violations.Add(scenario + " did not exercise a substantial actual motor turn.");
                            if (leftReleases == 0 || rightReleases == 0) violations.Add(scenario + " did not show both feet taking and releasing stance.");
                        }
                    }
                }
            }
            finally
            {
                actor.Input.Move = float2.zero;
                Time.captureDeltaTime = previousCaptureDelta;
                if (capture != null) UnityEngine.Object.DestroyImmediate(capture);
                camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
                output.Dispose();
                File.WriteAllText(Path.Combine(folder, "frames.json"), JsonUtility.ToJson(report, true));
                File.WriteAllLines(Path.Combine(folder, "violations.txt"), violations);
            }
            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }
    }
}
