using System;
using System.Collections;
using System.IO;
using Elemental.Presentation.Animation;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [UnityTest]
        public IEnumerator ProductionBriefTurnTapArticulatesLegs()
        {
            foreach (Actor actor in _actors)
            {
                Animator animator = actor.Presentation.GetComponent<Animator>();
                Transform leg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                Quaternion initial = leg.localRotation;
                float peak = 0f;
                float start = Time.time;
                actor.Input.Move = new float2(-1f, 0f);
                while (Time.time < start + .65f)
                {
                    if (Time.time >= start + .08f) actor.Input.Move = float2.zero;
                    yield return _frame;
                    peak = Mathf.Max(peak, Quaternion.Angle(initial, leg.localRotation));
                }
                Debug.Log($"[CharacterReadability] brief80ms turn leg articulation={peak:F3}");
                Assert.That(peak, Is.GreaterThan(8f), "A brief real turn must finish a visible stepping response.");
            }
        }

        [UnityTest]
        public IEnumerator ProductionTurnsMoveLegsAndOrdinaryGaitMovesAccessories()
        {
            foreach (Actor actor in _actors)
            {
                Animator animator = actor.Presentation.GetComponent<Animator>();
                Transform leg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                Transform[] bones = animator.GetComponentsInChildren<Transform>();
                Transform tail = Array.Find(bones, b => b.name == "Secondary_Tail_03");
                Transform belt = Array.Find(bones, b => b.name == "Secondary_Belt_L_02");
                Assert.That(tail != null && belt != null, Is.True);
                Quaternion legStart = leg.localRotation, tailStart = tail.localRotation, beltStart = belt.localRotation;
                float legPeak = 0f, tailPeak = 0f, beltPeak = 0f;
                actor.Input.Move = new float2(1f, 0f);
                float until = Time.time + 1.4f;
                while (Time.time < until)
                {
                    yield return _frame;
                    legPeak = Mathf.Max(legPeak, Quaternion.Angle(legStart, leg.localRotation));
                }
                actor.Input.Move = new float2(0f, 1f);
                until = Time.time + 1.6f;
                while (Time.time < until)
                {
                    yield return _frame;
                    tailPeak = Mathf.Max(tailPeak, Quaternion.Angle(tailStart, tail.localRotation));
                    beltPeak = Mathf.Max(beltPeak, Quaternion.Angle(beltStart, belt.localRotation));
                }
                actor.Input.Move = float2.zero;
                Directory.CreateDirectory("BuildReports/CharacterReadability");
                string actorName = actor.Presentation.GetComponentInParent<Elemental.Runtime.Characters.PlanetMotor>().name;
                File.WriteAllText("BuildReports/CharacterReadability/" + actorName + ".txt",
                    $"turnLegPeak={legPeak:F3}; ordinaryTailPeak={tailPeak:F3}; ordinaryBeltPeak={beltPeak:F3}");
                Debug.Log($"[CharacterReadability] {actorName}: turnLeg={legPeak:F3}, tail={tailPeak:F3}, belt={beltPeak:F3}");
                ScreenCapture.CaptureScreenshot("BuildReports/CharacterReadability/" + actorName + "-gait.png");
                AssertAccessoriesDeformVisibleMesh(actor, actorName);
                Assert.That(legPeak, Is.GreaterThan(8f), "Turn state must visibly articulate the legs, not only change its state hash.");
                Assert.That(tailPeak, Is.GreaterThan(.6f), "Ordinary gait must visibly excite the plume.");
                Assert.That(beltPeak, Is.GreaterThan(.6f), "Ordinary gait must visibly excite the belt strips.");
            }
        }

        private static void AssertAccessoriesDeformVisibleMesh(Actor actor, string actorName)
        {
            var motion = actor.Presentation.GetComponent<HumanoidSecondaryMotion>();
            Assert.That(motion, Is.Not.Null);
            var skin = actor.Presentation.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(skin, Is.Not.Null);
            Transform[] transforms = skin.bones;
            Quaternion[] rotations = Array.ConvertAll(transforms, t => t.localRotation);
            var posed = new Mesh(); var neutral = new Mesh();
            try
            {
                skin.BakeMesh(posed);
                motion.ResetAfterDiscontinuity();
                skin.BakeMesh(neutral);
                Vector3[] a = posed.vertices, b = neutral.vertices;
                BoneWeight[] weights = skin.sharedMesh.boneWeights;
                float tailDistance = 0f, beltDistance = 0f;
                for (int i = 0; i < a.Length; i++)
                {
                    float distance = skin.transform.TransformVector(a[i] - b[i]).magnitude;
                    BoneWeight weight = weights[i];
                    int[] indices = { weight.boneIndex0, weight.boneIndex1, weight.boneIndex2, weight.boneIndex3 };
                    float[] values = { weight.weight0, weight.weight1, weight.weight2, weight.weight3 };
                    for (int j = 0; j < 4; j++)
                    {
                        if (values[j] < .05f) continue;
                        string name = transforms[indices[j]].name;
                        if (name.StartsWith("Secondary_Tail_")) tailDistance = Mathf.Max(tailDistance, distance);
                        if (name.StartsWith("Secondary_Belt_")) beltDistance = Mathf.Max(beltDistance, distance);
                    }
                }
                File.AppendAllText("BuildReports/CharacterReadability/" + actorName + ".txt",
                    $"; actualSkinnedTailDisplacement={tailDistance:F5}m; actualSkinnedBeltDisplacement={beltDistance:F5}m");
                Assert.That(tailDistance, Is.GreaterThan(.002f), "Plume vertices must follow the simulated bones.");
                Assert.That(beltDistance, Is.GreaterThan(.002f), "Belt vertices must follow the simulated bones.");
            }
            finally
            {
                for (int i = 0; i < transforms.Length; i++) transforms[i].localRotation = rotations[i];
                UnityEngine.Object.Destroy(posed); UnityEngine.Object.Destroy(neutral);
            }
        }
    }
}
