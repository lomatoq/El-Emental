using System.Collections.Generic;
using Elemental.Presentation.Camera;
using Elemental.Presentation.MotionMatching;
using Elemental.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthRuntimeRescueContractTests
    {
        [Test]
        public void GameplayCamera_DoesNotCompensateAuthoredDistanceForPhysicalLens()
        {
            Assert.That(
                EarthCinemachineCameraController.ResolveFixedLensDistance(7.65f),
                Is.EqualTo(7.65f).Within(0.0001f));
        }

        [Test]
        public void RetargetBindPose_HashIsDeterministicAndSchemaIsExplicit()
        {
            var entries = new List<EarthRetargetBindBone>
            {
                new(HumanBodyBones.Hips, "Hips", Quaternion.identity),
                new(HumanBodyBones.LeftFoot, "LeftFoot", Quaternion.Euler(2f, 3f, 4f))
            };
            string first = EarthRetargetBindPose.ComputeSkeletonHash(entries);
            string second = EarthRetargetBindPose.ComputeSkeletonHash(entries);
            EarthRetargetBindPose asset = ScriptableObject.CreateInstance<EarthRetargetBindPose>();
            try
            {
                asset.Configure("avatar", first, Vector3.forward, Vector3.up, entries);
                Assert.That(asset.SchemaVersion, Is.EqualTo(EarthRetargetBindPose.CurrentSchemaVersion));
                Assert.That(asset.SourceSkeletonHash, Is.EqualTo(first));
                Assert.That(second, Is.EqualTo(first));
                Assert.That(asset.TryGet(HumanBodyBones.LeftFoot, out EarthRetargetBindBone foot), Is.True);
                Assert.That(foot.SourceJointName, Is.EqualTo("LeftFoot"));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void DepthOfField_MissingOpponentFailsClosedEvenForCaptureOverride()
        {
            GameObject cameraObject = new GameObject("DOF fail closed camera");
            GameObject player = new GameObject("DOF primary");
            try
            {
                cameraObject.AddComponent<Camera>();
                EarthCinematicDepthOfFieldController depthOfField =
                    cameraObject.AddComponent<EarthCinematicDepthOfFieldController>();
                depthOfField.ConfigureSubjects(player.transform, null);
                depthOfField.SetCaptureOverride(true);
                Assert.That(depthOfField.HasRequiredSubjects, Is.False);
                Assert.That(depthOfField.TryGetRenderSettings(out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
