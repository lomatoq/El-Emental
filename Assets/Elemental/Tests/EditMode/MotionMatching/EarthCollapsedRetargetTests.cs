using System.Collections.Generic;
using Elemental.Authoring.Editor.MotionMatching;
using Elemental.Presentation.MotionMatching;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Elemental.Tests.EditMode.MotionMatching
{
    public sealed class EarthCollapsedRetargetTests
    {
        [TestCase(0f)]
        [TestCase(0.35f)]
        [TestCase(0.7f)]
        public void BackwardRunCollapsedRestRoundTripsOntoActualLinebreakerRig(float phase)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Elemental/Content/Characters/Linebreaker/Linebreaker.fbx");
            Assert.That(prefab, Is.Not.Null);
            GameObject rig = Object.Instantiate(prefab);
            try
            {
                rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Animator animator = rig.GetComponentInChildren<Animator>();
                animator.enabled = false;
                List<EarthRetargetBindBone> rest = MotionLibraryBuilder.CaptureRetargetBindBones(animator);
                var targetRest = new Quaternion[rest.Count];
                for (int index = 0; index < rest.Count; index++)
                    targetRest[index] = animator.GetBoneTransform(rest[index].Bone).localRotation;
                AnimationClip clip = null;
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(
                    "Assets/ThirdParty/Mixamo/X Bot@Running Backward.fbx"))
                    if (asset is AnimationClip candidate && !candidate.name.StartsWith("__preview"))
                    {
                        clip = candidate;
                        break;
                    }
                Assert.That(clip, Is.Not.Null);
                clip.SampleAnimation(rig, phase * clip.length);
                for (int index = 0; index < rest.Count; index++)
                {
                    EarthRetargetBindBone entry = rest[index];
                    Quaternion sampled = MotionLibraryBuilder.GetCollapsedLocalRotation(animator, entry.Bone);
                    Quaternion result = targetRest[index] * Quaternion.Inverse(entry.SourceRestLocalRotation) * sampled;
                    Quaternion expected = animator.GetBoneTransform(entry.Bone).localRotation;
                    Assert.That(Quaternion.Angle(result, expected), Is.LessThan(0.5f),
                        $"{entry.Bone}: collapsed source parent must not reapply FBX conversion/helper rotation.");
                }
            }
            finally { Object.DestroyImmediate(rig); }
        }
    }
}
