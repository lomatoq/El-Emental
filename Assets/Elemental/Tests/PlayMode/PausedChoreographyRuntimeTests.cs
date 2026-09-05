using System.Collections;
using Elemental.Presentation.Animation;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        private static readonly HumanBodyBones[] PausedPoseBones =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.UpperChest,
            HumanBodyBones.Head,
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.RightShoulder,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightFoot
        };

        [UnityTest]
        public IEnumerator PausedGameTimeDoesNotReapplyActiveChoreographyToFrozenBones()
        {
            Actor actor = _actors.Find(value => value.Presentation.PoseController != null);
            Assert.That(actor, Is.Not.Null);
            EarthCharacterPoseController pose = actor.Presentation.PoseController;
            EarthChoreographyDirector choreography =
                actor.Presentation.GetComponent<EarthChoreographyDirector>();
            Animator animator = actor.Presentation.Animator;
            Assert.That(choreography, Is.Not.Null);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.updateMode, Is.EqualTo(AnimatorUpdateMode.Normal),
                "The pause contract must be updated if the production Animator leaves scaled GameTime.");

            Transform[] bones =
            {
                animator.GetBoneTransform(HumanBodyBones.UpperChest) ??
                    animator.GetBoneTransform(HumanBodyBones.Chest),
                animator.GetBoneTransform(HumanBodyBones.Head),
                animator.GetBoneTransform(HumanBodyBones.LeftShoulder),
                animator.GetBoneTransform(HumanBodyBones.RightShoulder)
            };
            for (int index = 0; index < bones.Length; index++)
                Assert.That(bones[index], Is.Not.Null, $"Required choreography bone {index} is missing.");

            HumanoidOrganicIdle organic = actor.Presentation.GetComponent<HumanoidOrganicIdle>();
            HumanoidProceduralBodyResponse body =
                actor.Presentation.GetComponent<HumanoidProceduralBodyResponse>();
            bool organicEnabled = organic != null && organic.enabled;
            bool bodyEnabled = body != null && body.enabled;
            float oldScale = Time.timeScale;
            try
            {
                // Isolate the director from the two other documented additive
                // chest/head passes; this test identifies its own write authority.
                if (organic != null) organic.enabled = false;
                if (body != null) body.enabled = false;
                yield return _frame;

                Vector3 target = actor.Presentation.transform.position +
                                 actor.Presentation.transform.forward * 3f +
                                 actor.Presentation.transform.up * .5f;
                pose.RequestSemanticPresentation(
                    EarthTechniqueKind.Wall,
                    EarthTechniqueId.RaiseWall,
                    0xfa115e01u,
                    target,
                    80f,
                    4f);
                double deadline = Time.realtimeSinceStartupAsDouble + 2d;
                while ((!choreography.CurrentRequest.IsActive ||
                        choreography.AppliedVisualPose.MaximumAbsDegrees < .25f) &&
                       Time.realtimeSinceStartupAsDouble < deadline)
                    yield return _frame;
                Assert.That(choreography.CurrentRequest.IsActive, Is.True,
                    "Fixture never reached an active production choreography request.");
                Assert.That(choreography.AppliedVisualPose.MaximumAbsDegrees, Is.GreaterThan(.25f),
                    "Fixture has no additive pose and cannot expose repeated multiplication.");

                Time.timeScale = 0f;
                yield return _frame;
                var frozen = new Quaternion[bones.Length];
                for (int index = 0; index < bones.Length; index++)
                    frozen[index] = bones[index].localRotation;

                for (int renderFrame = 0; renderFrame < 12; renderFrame++)
                {
                    yield return _frame;
                    Assert.That(choreography.CurrentRequest.IsActive, Is.True,
                        $"Active request disappeared during paused render frame {renderFrame}.");
                    for (int index = 0; index < bones.Length; index++)
                        Assert.That(Quaternion.Angle(frozen[index], bones[index].localRotation),
                            Is.LessThan(.05f),
                            $"Bone {bones[index].name} accumulated an additive offset on paused render frame {renderFrame}.");
                }
            }
            finally
            {
                Time.timeScale = oldScale;
                if (organic != null) organic.enabled = organicEnabled;
                if (body != null) body.enabled = bodyEnabled;
            }
            yield return _frame;
        }

        [UnityTest]
        public IEnumerator PausedGameTimeKeepsTheCompleteProductionPoseChainStable()
        {
            Actor actor = _actors.Find(value => value.Presentation.PoseController != null);
            Assert.That(actor, Is.Not.Null);
            EarthCharacterPoseController pose = actor.Presentation.PoseController;
            EarthChoreographyDirector choreography =
                actor.Presentation.GetComponent<EarthChoreographyDirector>();
            Animator animator = actor.Presentation.Animator;
            Assert.That(choreography, Is.Not.Null);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.updateMode, Is.EqualTo(AnimatorUpdateMode.Normal));

            var bones = new Transform[PausedPoseBones.Length];
            for (int index = 0; index < bones.Length; index++)
                bones[index] = animator.GetBoneTransform(PausedPoseBones[index]);

            float oldScale = Time.timeScale;
            try
            {
                Vector3 target = actor.Presentation.transform.position +
                                 actor.Presentation.transform.forward * 3f +
                                 actor.Presentation.transform.up * .5f;
                pose.RequestSemanticPresentation(
                    EarthTechniqueKind.Wall,
                    EarthTechniqueId.RaiseWall,
                    0xfa115e02u,
                    target,
                    80f,
                    4f);
                double deadline = Time.realtimeSinceStartupAsDouble + 2d;
                while ((!choreography.CurrentRequest.IsActive ||
                        choreography.AppliedVisualPose.MaximumAbsDegrees < .25f) &&
                       Time.realtimeSinceStartupAsDouble < deadline)
                    yield return _frame;
                Assert.That(choreography.CurrentRequest.IsActive, Is.True);
                Assert.That(choreography.AppliedVisualPose.MaximumAbsDegrees, Is.GreaterThan(.25f));

                Time.timeScale = 0f;
                yield return _frame;
                var localRotations = new Quaternion[bones.Length];
                var localPositions = new Vector3[bones.Length];
                for (int index = 0; index < bones.Length; index++)
                {
                    if (bones[index] == null) continue;
                    localRotations[index] = bones[index].localRotation;
                    localPositions[index] = bones[index].localPosition;
                }

                for (int renderFrame = 0; renderFrame < 12; renderFrame++)
                {
                    yield return _frame;
                    Assert.That(choreography.CurrentRequest.IsActive, Is.True,
                        $"Active request disappeared during full-chain paused frame {renderFrame}.");
                    for (int index = 0; index < bones.Length; index++)
                    {
                        Transform bone = bones[index];
                        if (bone == null) continue;
                        Assert.That(Quaternion.Angle(localRotations[index], bone.localRotation),
                            Is.LessThan(.05f),
                            $"Production owner changed {PausedPoseBones[index]} rotation on paused frame {renderFrame}.");
                        Assert.That(Vector3.Distance(localPositions[index], bone.localPosition),
                            Is.LessThan(.0001f),
                            $"Production owner changed {PausedPoseBones[index]} position on paused frame {renderFrame}.");
                    }
                }
            }
            finally
            {
                Time.timeScale = oldScale;
            }
            yield return _frame;
        }
    }
}
