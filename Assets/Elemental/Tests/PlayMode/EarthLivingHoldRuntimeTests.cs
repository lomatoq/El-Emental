using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Elemental.Presentation.Animation;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [Serializable] private struct LivingHoldFrame
        {
            public float seconds, weight, loopTime, chestStep, leftHandStep, rightHandStep;
            public Vector3 chest, leftHand, rightHand;
        }
        [Serializable] private sealed class LivingHoldReport
        {
            public string utc;
            public float maximumChestStep, chestTravel, leftHandTravel, rightHandTravel;
            public List<LivingHoldFrame> frames = new();
        }

        [UnityTest]
        public IEnumerator AuthoredLivingHoldMovesForTenSecondsAndCancels()
        {
            Actor actor = _actors.Find(a => a.Presentation.PoseController != null);
            Assert.That(actor, Is.Not.Null);
            var presentation = actor.Presentation;
            var executor = GetPrivate<MagicExecutor>(presentation, "executor");
            var motor = presentation.GetComponentInParent<PlanetMotor>();
            var animator = presentation.Animator;
            var driver = presentation.GetComponent<EarthAnimationDriver>();
            int layer = animator.GetLayerIndex(EarthLivingHoldPolicy.LayerName);
            Assert.That(layer, Is.GreaterThanOrEqualTo(0), "Install authored Living Hold first.");
            var chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            var left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            SceneManager.MoveGameObjectToScene(stone, _scene);
            stone.name = "Living Hold Runtime Stone";
            stone.transform.position = chest.position + motor.FacingForward * 3f;
            stone.transform.localScale = Vector3.one * .3f;
            var body = stone.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = 12f;
            stone.AddComponent<PhysicalImpactTarget>().Configure(body);
            var report = new LivingHoldReport { utc = DateTime.UtcNow.ToString("O") };
            string folder = "BuildReports/LivingHold";
            Directory.CreateDirectory(folder);
            try
            {
                Physics.SyncTransforms();
                Assert.That(executor.TryBeginGravityWell(stone.GetComponent<Collider>(),
                    stone.transform.position, motor.LocalUp, true), Is.True);
                double deadline = Time.realtimeSinceStartupAsDouble + 3d;
                while (presentation.LivingHoldWeight < .2f && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return _frame;
                Assert.That(presentation.LivingHoldWeight, Is.GreaterThan(.2f));
                Quaternion previousChest = chest.localRotation;
                Vector3 previousLeft = animator.transform.InverseTransformPoint(left.position);
                Vector3 previousRight = animator.transform.InverseTransformPoint(right.position);
                double start = Time.realtimeSinceStartupAsDouble;
                int nextCapture = 0;
                while (Time.realtimeSinceStartupAsDouble - start < 10d)
                {
                    yield return _frame;
                    float elapsed = (float)(Time.realtimeSinceStartupAsDouble - start);
                    Vector3 localLeft = animator.transform.InverseTransformPoint(left.position);
                    Vector3 localRight = animator.transform.InverseTransformPoint(right.position);
                    float chestStep = Quaternion.Angle(previousChest, chest.localRotation);
                    float leftStep = Vector3.Distance(previousLeft, localLeft);
                    float rightStep = Vector3.Distance(previousRight, localRight);
                    report.maximumChestStep = Mathf.Max(report.maximumChestStep, chestStep);
                    report.chestTravel += chestStep;
                    report.leftHandTravel += leftStep;
                    report.rightHandTravel += rightStep;
                    report.frames.Add(new LivingHoldFrame { seconds = elapsed,
                        weight = presentation.LivingHoldWeight,
                        loopTime = driver.GetCurrentAnimatorStateInfo(layer).normalizedTime,
                        chestStep = chestStep, leftHandStep = leftStep, rightHandStep = rightStep,
                        chest = chest.localEulerAngles, leftHand = localLeft, rightHand = localRight });
                    previousChest = chest.localRotation;
                    previousLeft = localLeft;
                    previousRight = localRight;
                    if (elapsed >= nextCapture)
                    {
                        ScreenCapture.CaptureScreenshot(Path.Combine(folder, $"hold-{nextCapture:00}.png"));
                        nextCapture += 2;
                    }
                }
                Assert.That(report.chestTravel, Is.GreaterThan(.5f), "Rendered torso stayed frozen during held magic.");
                Assert.That(report.maximumChestStep, Is.LessThan(15f), "Visible torso jumped during loop playback.");
                Assert.That(report.frames[report.frames.Count - 1].loopTime - report.frames[0].loopTime,
                    Is.GreaterThan(1f), "Authored loop did not complete a full cycle.");
                executor.CancelGravityWell();
                yield return new WaitForSeconds(.8f);
                Assert.That(presentation.LivingHoldWeight, Is.LessThan(.005f));
                presentation.ResetMagicIK();
                Assert.That(driver.GetLayerWeight(layer), Is.Zero);
            }
            finally
            {
                executor.CancelGravityWell();
                UnityEngine.Object.Destroy(stone);
                File.WriteAllText(Path.Combine(folder, "hold-trace.json"), JsonUtility.ToJson(report, true));
            }
        }
    }
}
