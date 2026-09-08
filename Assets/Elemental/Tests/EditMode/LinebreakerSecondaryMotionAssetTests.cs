using System;
using System.Reflection;
using Elemental.Presentation.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class LinebreakerSecondaryMotionAssetTests
    {
        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void ImportedCharacterHasMovingBoundedChainsAndRigidHairDuringAnchorTurns(int fps)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Elemental/Content/Characters/Linebreaker/Linebreaker.fbx");
            Assert.That(prefab, Is.Not.Null);
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>();
                Assert.That(animator != null && animator.isHuman, Is.True);
                var motion = instance.AddComponent<HumanoidSecondaryMotion>();
                motion.ConfigureFromHierarchy(animator);
                Assert.That(motion.HasValidSecondaryHierarchy, Is.True);
                Assert.That(motion.HasHelmetHairLock, Is.True);
                Assert.That(motion.HasCollisionProxies, Is.True);
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                Quaternion hipsBind = hips.localRotation;
                Quaternion headBind = head.localRotation;
                Transform[] all = instance.GetComponentsInChildren<Transform>();
                Transform tail = Array.Find(all, t => t.name == "Secondary_Tail_03");
                Transform belt = Array.Find(all, t => t.name == "Secondary_Belt_L_02");
                Transform hair = Array.Find(all, t => t.name == "Secondary_HairLock");
                Quaternion tailBind = tail.localRotation;
                Quaternion beltBind = belt.localRotation;
                Quaternion hairBind = hair.localRotation;
                Vector3[] positions = Array.ConvertAll(all, t => t.localPosition);
                float peakTail = 0f, peakBelt = 0f;
                for (int i = 0; i < fps * 3; i++)
                {
                    float phase = i * 2f * Mathf.PI * 1.6f / fps;
                    hips.localRotation = hipsBind * Quaternion.Euler(Mathf.Sin(phase) * 7f, Mathf.Sin(phase * .7f) * 15f, 0f);
                    head.localRotation = headBind * Quaternion.Euler(0f, Mathf.Sin(phase) * 35f, 0f);
                    InvokeChain(motion, "tailBones", "_tail", true, 1f / fps);
                    InvokeChain(motion, "leftBeltBones", "_leftBelt", false, 1f / fps);
                    InvokeChain(motion, "rightBeltBones", "_rightBelt", false, 1f / fps);
                    peakTail = Mathf.Max(peakTail, Quaternion.Angle(tailBind, tail.localRotation));
                    peakBelt = Mathf.Max(peakBelt, Quaternion.Angle(beltBind, belt.localRotation));
                    Assert.That(Quaternion.Angle(hairBind, hair.localRotation), Is.LessThan(.001f));
                    for (int j = 0; j < all.Length; j++)
                        Assert.That(Vector3.Distance(positions[j], all[j].localPosition), Is.LessThan(.00001f), all[j].name);
                }
                Assert.That(peakTail, Is.InRange(.25f, 60f), "Tail must respond to head turning.");
                Assert.That(peakBelt, Is.InRange(.25f, 60f), "Belt must respond to animated hips.");
                motion.ResetAfterDiscontinuity();
                Assert.That(Quaternion.Angle(tailBind, tail.localRotation), Is.LessThan(.001f));
                Assert.That(Quaternion.Angle(beltBind, belt.localRotation), Is.LessThan(.001f));
                Debug.Log($"[CharacterRig] {fps} Hz: tail={peakTail:F3} belt={peakBelt:F3}, lengths preserved, rigid hair, reset accepted.");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        private static void InvokeChain(HumanoidSecondaryMotion motion, string bonesName, string stateName, bool tail, float dt)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Type type = typeof(HumanoidSecondaryMotion);
            type.GetMethod("ApplyChain", flags).Invoke(motion, new[]
            {
                type.GetField(bonesName, flags).GetValue(motion),
                type.GetField(stateName, flags).GetValue(motion),
                (object)Vector2.zero, tail ? 18f : 22f, dt, tail
            });
        }
    }
}
