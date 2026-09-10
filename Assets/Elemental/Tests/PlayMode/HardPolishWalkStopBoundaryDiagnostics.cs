using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Elemental.Presentation.Animation;
using Elemental.Presentation.MotionMatching;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [Serializable] private sealed class KneeBoundaryFrame
        {
            public string actor,bone;
            public int frame,pass,sourceFrame,requestedSerial,appliedSerial,evaluations;
            public bool movingQuery,authoredIdle;
            public float dt,elapsed,ikWeight,kneeAngle,pelvisOffset,eammWeight,master,halfLife,inertiaEnabled;
            public Vector4 target,previousTarget,output,offset,renderedLocal;
            public Vector3 velocity,offsetVelocity;
            public int awaitingDerivative, graphEvaluationAtContact;
            public bool swingFloor,swingStride;
            public float floorCorrection,floorBoneClearance,floorGoalClearance;
            public Vector3 swingTarget;
        }
        [Serializable] private sealed class KneeBoundaryReport
        {
            public string utc;
            public List<KneeBoundaryFrame> frames=new();
        }
        private sealed class KneeBoundaryGraph
        {
            public Actor actor;
            public NativeArray<Quaternion> targets;
            public NativeArray<float> weights,master,halfLife,enabled;
            public NativeArray<int> serial,applied,counters;
            public NativeArray<EarthRotationInertializationState> states;
            public HumanBodyBones[] bones;
        }
        [UnityTest]
        public IEnumerator WalkStopRecordsActualInertializationAtIdleOwnershipBoundary()
        {
            var report=new KneeBoundaryReport { utc=DateTime.UtcNow.ToString("O") };
            var records=new List<KneeBoundaryGraph>();
            foreach(Actor actor in _actors)
            {
                var driver=actor.Presentation.GetComponent<EarthAnimationDriver>();
                object graph=typeof(EarthAnimationDriver).GetField("_graph",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(driver);
                records.Add(new KneeBoundaryGraph { actor=actor,
                    targets=BoundaryField<NativeArray<Quaternion>>(graph,"_eammLocalRotations"),
                    weights=BoundaryField<NativeArray<float>>(graph,"_eammWeights"),
                    master=BoundaryField<NativeArray<float>>(graph,"_masterWeight"),
                    halfLife=BoundaryField<NativeArray<float>>(graph,"_halfLife"),
                    enabled=BoundaryField<NativeArray<float>>(graph,"_inertializationEnabled"),
                    serial=BoundaryField<NativeArray<int>>(graph,"_transitionSerial"),
                    applied=BoundaryField<NativeArray<int>>(graph,"_appliedTransitionSerial"),
                    counters=BoundaryField<NativeArray<int>>(graph,"_finalCounters"),
                    states=BoundaryField<NativeArray<EarthRotationInertializationState>>(graph,"_rotationStates"),
                    bones=(HumanBodyBones[])typeof(EAMMBasePoseBridge).GetField("Bones",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null) });
            }
            string folder="BuildReports/HardPolish/G03"; Directory.CreateDirectory(folder);
            try
            {
                for(int pass=0;pass<3;pass++)
                {
                    foreach(Actor actor in _actors) actor.Input.Move=new float2(0,1);
                    yield return new WaitForSeconds(.55f);
                    double start=Time.realtimeSinceStartupAsDouble;
                    while(Time.realtimeSinceStartupAsDouble-start<1.3)
                    {
                        float elapsed=(float)(Time.realtimeSinceStartupAsDouble-start);
                        if(elapsed>=.2f) foreach(Actor actor in _actors) actor.Input.Move=float2.zero;
                        yield return _frame;
                        foreach(KneeBoundaryGraph record in records)
                        {
                            var bridge=record.actor.Bridge;
                            var feet=record.actor.Presentation.FootContactController;
                            for(int index=0;index<record.bones.Length;index++)
                            {
                                HumanBodyBones bone=record.bones[index];
                                bool left=bone is HumanBodyBones.LeftUpperLeg or HumanBodyBones.LeftLowerLeg;
                                if(!left && bone is not (HumanBodyBones.RightUpperLeg or HumanBodyBones.RightLowerLeg)) continue;
                                EarthRotationInertializationState state=record.states[index];
                                Quaternion actual=record.actor.Presentation.Animator.GetBoneTransform(bone).localRotation;
                                report.frames.Add(new KneeBoundaryFrame { actor=record.actor.Presentation.name,bone=bone.ToString(),
                                    frame=Time.frameCount,pass=pass,sourceFrame=bridge.SourcePoseFrame,
                                    requestedSerial=record.serial[0],appliedSerial=record.applied[0],evaluations=record.counters[0],
                                    movingQuery=bridge.IsLocomotionQuery,authoredIdle=bridge.UsesAuthoredIdleKnees,
                                    dt=Time.deltaTime,elapsed=elapsed,ikWeight=left?feet.LeftFootIkWeight:feet.RightFootIkWeight,
                                    kneeAngle=left?feet.LeftKneeAngleDegrees:feet.RightKneeAngleDegrees,pelvisOffset=feet.PelvisOffsetMeters,
                                    eammWeight=record.weights[index],master=record.master[0],halfLife=record.halfLife[0],inertiaEnabled=record.enabled[0],
                                    target=BoundaryQuaternion(record.targets[index]),previousTarget=BoundaryQuaternion(state.PreviousTarget),
                                    output=BoundaryQuaternion(state.PreviousOutput),offset=BoundaryQuaternion(state.OffsetRotation),
                                    renderedLocal=BoundaryQuaternion(actual),velocity=(Vector3)state.PreviousOutputAngularVelocity,
                                    offsetVelocity=(Vector3)state.OffsetAngularVelocity,awaitingDerivative=state.AwaitingIncomingDerivative,
                                    graphEvaluationAtContact=BoundaryField<int>(record.actor.Presentation.GetComponent<EarthAnimationDriver>(),"_lastContactGraphEvaluation"),
                                    swingFloor=BoundaryField<bool>(feet,left?"_leftSwingFloorActive":"_rightSwingFloorActive"),
                                    swingStride=BoundaryField<bool>(feet,left?"_leftSwingStrideActive":"_rightSwingStrideActive"),
                                    floorCorrection=left?feet.LeftSwingFloorCorrectionMeters:feet.RightSwingFloorCorrectionMeters,
                                    floorBoneClearance=left?feet.LeftFloorBoneClearance:feet.RightFloorBoneClearance,
                                    floorGoalClearance=left?feet.LeftFloorGoalClearance:feet.RightFloorGoalClearance,
                                    swingTarget=BoundaryField<Vector3>(feet,left?"_leftSwingFloorTarget":"_rightSwingFloorTarget") });
                            }
                        }
                    }
                }
                Assert.That(report.frames.Count,Is.GreaterThan(100),"Rendered trace did not execute.");
                // Diagnostic capture deliberately does not weaken or replace the existing35degree acceptance test.
            }
            finally
            {
                foreach(Actor actor in _actors) actor.Input.Move=float2.zero;
                File.WriteAllText(Path.Combine(folder,"WalkStopInertialization.json"),JsonUtility.ToJson(report,true));
            }
        }
        private static T BoundaryField<T>(object owner,string field) => (T)owner.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
        private static Vector4 BoundaryQuaternion(Quaternion value) => new Vector4(value.x,value.y,value.z,value.w);
        private static Vector4 BoundaryQuaternion(quaternion value) => (Vector4)value.value;
    }
}
