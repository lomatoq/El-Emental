#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.UI;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Magic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [Serializable] private sealed class HeavyEvidenceFrame {public float elapsed,sourceTime,handTravel;public int pass;public bool casting;}
        [Serializable] private sealed class HeavyEvidence {public string scope="Actual saved actor and normal presentation owner, real-time 1x; actual released-body presentation event handler only, not proof of input recognition or launch/damage timestamps.";public List<HeavyEvidenceFrame> frames=new();}
        [UnityTest] public IEnumerator HeavyNativeWindowReturnsToProductionLocomotionAtOneTimesSpeed()
        {
            const string folder="BuildReports/HardPolish/G04/ProductionHeavy";Directory.CreateDirectory(folder);
            var flow=_scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FrontendFlowController>(true)).Single();
            var player=_actors.Single(a=>a.Presentation.transform.IsChildOf(flow.MatchController.PlayerTransform));
            var pose=player.Presentation.GetComponent<EarthCharacterPoseController>();
            var begin=typeof(EarthCharacterPoseController).GetMethod("OnBodyReleased",BindingFlags.Instance|BindingFlags.NonPublic);
            var camera=_scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CelestialSystemBehaviour>(true)).Single().TargetCamera;
            var animator=player.Presentation.GetComponent<Animator>();var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            var output=new ProductionCaptureResolution();var report=new HeavyEvidence();
            float oldScale=Time.timeScale,oldCapture=Time.captureDeltaTime;
            try
            {
                Time.timeScale=1;Time.captureDeltaTime=0;yield return output.WaitForRenderedSize(camera);
                for(int pass=0;pass<2;pass++)
                {
                    player.Input.Move=pass==0?float2.zero:new float2(0,.25f);
                    yield return new WaitForSeconds(.3f);
                    Vector3 origin=player.Presentation.transform.InverseTransformPoint(hand.position);
                    Vector3 velocity=player.Presentation.transform.forward*4f;
                    begin.Invoke(pose,new object[]{new EarthBodyReleasedEvent(pose.PresentationTick,0u,80f,new float3(velocity.x,velocity.y,velocity.z))});
                    Assert.That(pose.AuthoritativeStartsAtContact,Is.True,"The real released-body event handler must own the release entry.");
                    float start=Time.time,nextCapture=0,maxTravel=0,maxSource=0;bool saw=false;int image=0;
                    while(Time.time-start<2.2f)
                    {
                        yield return _frame;var sample=player.Probe.Latest;
                        bool casting=player.Presentation.CurrentAuthoredAction==EarthAuthoredActionId.MagicCast;
                        float travel=Vector3.Distance(origin,player.Presentation.transform.InverseTransformPoint(hand.position));
                        if(casting){saw=true;maxTravel=Mathf.Max(maxTravel,travel);maxSource=Mathf.Max(maxSource,sample.magicSampleTime);}
                        report.frames.Add(new HeavyEvidenceFrame{elapsed=Time.time-start,sourceTime=sample.magicSampleTime,handTravel=travel,pass=pass,casting=casting});
                        if(Time.time-start>=nextCapture&&image<16){ProductionCaptureResolution.SaveScreen(folder+"/"+(pass==0?"standing":"moving")+"-"+(image++).ToString("D2")+".png");nextCapture+=.12f;}
                    }
                    Assert.That(saw,Is.True);Assert.That(maxTravel,Is.GreaterThan(.025f));
                    Assert.That(maxSource,Is.GreaterThanOrEqualTo(.23f),"The measured two-hand contact interval never became visible.");
                    Assert.That(player.Presentation.CurrentAuthoredAction,Is.Not.EqualTo(EarthAuthoredActionId.MagicCast),"Heavy failed to return through the existing recovery owner.");
                }
            }
            finally{player.Input.Move=float2.zero;Time.timeScale=oldScale;Time.captureDeltaTime=oldCapture;output.Dispose();File.WriteAllText(folder+"/ProductionHeavy.json",JsonUtility.ToJson(report,true));}
        }
    }
}
#endif
