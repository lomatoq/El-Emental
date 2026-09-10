using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Elemental.Presentation.Animation;
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
        [Serializable] private struct FireChannelPoseFrame
        {
            public float seconds, loopTime, loopWeight, chestStep, rootTravel;
            public uint presentationGeneration;
            public Vector3 leftHand,rightHand,leftKnee,rightKnee;
        }
        [Serializable] private sealed class FireChannelPoseReport
        {
            public string utc;
            public List<FireChannelPoseFrame> frames=new();
        }

        [UnityTest]
        public IEnumerator FireChannelUsesOneHandCastAndLivingLoopThroughTenSecondMovementAndCancel()
        {
            Actor actor=_actors.Find(a=>a.Presentation.PoseController!=null);
            Assert.That(actor,Is.Not.Null);
            var presentation=actor.Presentation;
            var pose=presentation.PoseController;
            var driver=presentation.GetComponent<EarthAnimationDriver>();
            var motor=presentation.GetComponentInParent<PlanetMotor>();
            var animator=presentation.Animator;
            int layer=animator.GetLayerIndex(EarthLivingHoldPolicy.LayerName);
            Assert.That(layer,Is.GreaterThanOrEqualTo(0));
            var chest=animator.GetBoneTransform(HumanBodyBones.Chest);
            var left=animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var right=animator.GetBoneTransform(HumanBodyBones.RightHand);
            var leftKnee=animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            var rightKnee=animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            var report=new FireChannelPoseReport { utc=DateTime.UtcNow.ToString("O") };
            string folder="BuildReports/HardPolish/G04"; Directory.CreateDirectory(folder);
            uint session=1;
            try
            {
                Vector3 focus=chest.position+motor.FacingForward*4;
                Assert.That(pose.BeginFireChannelPresentation(session,focus),Is.True);
                uint generation=pose.AuthoritativePresentationGeneration;
                Assert.That(EarthHumanoidMotionResolver.Resolve(pose.CurrentRequest.Technique),
                    Is.EqualTo(EarthHumanoidPoseSlot.GravityRepair),"Fire must not adopt GenericCast's punch.");
                Assert.That(pose.BeginFireChannelPresentation(session,focus),Is.True);
                Assert.That(pose.AuthoritativePresentationGeneration,Is.EqualTo(generation));
                double deadline=Time.realtimeSinceStartupAsDouble+3;
                while(presentation.LivingHoldWeight<.2f && Time.realtimeSinceStartupAsDouble<deadline) yield return _frame;
                Assert.That(presentation.LivingHoldWeight,Is.GreaterThan(.2f));
                double start=Time.realtimeSinceStartupAsDouble;
                float initialLoop=driver.GetCurrentAnimatorStateInfo(layer).normalizedTime;
                Vector3 initialRoot=motor.transform.position;
                Quaternion priorChest=chest.localRotation;
                float maxChestStep=0, maxTravel=0, chestTravel=0;
                while(Time.realtimeSinceStartupAsDouble-start<10)
                {
                    float elapsed=(float)(Time.realtimeSinceStartupAsDouble-start);
                    actor.Input.Move=elapsed<2 ? float2.zero : elapsed<4 ? new float2(0,.25f) :
                        elapsed<6 ? new float2(.25f,0) : elapsed<8 ? new float2(-.25f,0) : float2.zero;
                    focus=chest.position+motor.FacingForward*4;
                    Assert.That(pose.UpdateFireChannelPresentation(session,focus),Is.True);
                    yield return _frame;
                    float step=Quaternion.Angle(priorChest,chest.localRotation); priorChest=chest.localRotation;
                    maxChestStep=Mathf.Max(maxChestStep,step); chestTravel+=step;
                    float travel=Vector3.Distance(initialRoot,motor.transform.position); maxTravel=Mathf.Max(maxTravel,travel);
                    Assert.That(pose.AuthoritativePresentationGeneration,Is.EqualTo(generation),"Target updates restarted the cast buffer.");
                    foreach(var bone in new[]{left,right,leftKnee,rightKnee})
                        Assert.That(math.all(math.isfinite((float3)bone.position)),Is.True,"Non-finite final pose.");
                    report.frames.Add(new FireChannelPoseFrame { seconds=elapsed,
                        loopTime=driver.GetCurrentAnimatorStateInfo(layer).normalizedTime,
                        loopWeight=presentation.LivingHoldWeight,chestStep=step,rootTravel=travel,
                        presentationGeneration=generation,leftHand=left.position,rightHand=right.position,
                        leftKnee=leftKnee.position,rightKnee=rightKnee.position });
                }
                Assert.That(maxTravel,Is.GreaterThan(.1f),"Fixture did not exercise locomotion during channel.");
                Assert.That(chestTravel,Is.GreaterThan(.5f));
                Assert.That(maxChestStep,Is.LessThan(15f),"Rendered chest jumped at native speed.");
                Assert.That(driver.GetCurrentAnimatorStateInfo(layer).normalizedTime-initialLoop,Is.GreaterThan(1));
                actor.Input.Move=float2.zero;
                Assert.That(pose.EndFireChannelPresentation(session),Is.True);
                Assert.That(pose.UpdateFireChannelPresentation(session,focus),Is.False);
                yield return new WaitForSeconds(.8f);
                Assert.That(presentation.LivingHoldWeight,Is.LessThan(.005f));
                session++;
                Assert.That(pose.BeginFireChannelPresentation(session,focus),Is.True);
                yield return _frame;
                // Same ownership boundary used by ragdoll/knockdown; no test manipulates live bones.
                pose.SetPresentationSuppressed(true);
                Assert.That(pose.FireChannelPresentationActive,Is.False);
                Assert.That(pose.UpdateFireChannelPresentation(session,focus),Is.False);
                pose.SetPresentationSuppressed(false);
                Assert.That(pose.BeginFireChannelPresentation(session,focus),Is.False,
                    "A late update/begin for the cancelled generation must not resurrect the channel.");
                yield return new WaitForSeconds(.8f);
                Assert.That(presentation.LivingHoldWeight,Is.LessThan(.005f));
            }
            finally
            {
                actor.Input.Move=float2.zero; pose.EndFireChannelPresentation(session);
                pose.SetPresentationSuppressed(false); presentation.ResetMagicIK();
                File.WriteAllText(Path.Combine(folder,"FireChannelPose.json"),JsonUtility.ToJson(report,true));
            }
        }
    }
}
