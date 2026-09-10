using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Elemental.Input.Gestures;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Magic;
using Unity.Mathematics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
 public sealed partial class HardPolishFireStreamBindingRuntimeTests
 {
  [UnityTest,Timeout(300000)] public IEnumerator SavedLinebreakerGathersHoldsMovesAndReleasesActualStonesWithMaterialShedCapture()
  {
   const string folder="BuildReports/HardPolish/HeldMaterialShed";Directory.CreateDirectory(folder);
   var camera=All<CelestialSystemBehaviour>().Single().TargetCamera;
   var resolution=new ProductionCaptureResolution();var stones=new EarthFragment[5];
   FireVisualCaptureCamera pose=null;MagicInputController input=null;MagicExecutor executor=null;
   int oldCapture=Time.captureFramerate;Vector3 oldPosition=camera.transform.position;Quaternion oldRotation=camera.transform.rotation;
   var csv=new StringBuilder("phase,frame,captured,shedEvents,newDust,oldDust,chips,livingHoldWeight,focusX,focusY,focusZ\n");
   try
   {
    yield return resolution.WaitForRenderedSize(camera);yield return EnterCombat();
    var actor=duel.PlayerTransform.GetComponentInChildren<HumanoidCharacterPresentation>();Assert.That(actor,Is.Not.Null);
    Assert.That(actor.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.sharedMaterials.Any(m=>m!=null&&m.shader!=null&&m.shader.name.Contains("Rumble"))),Is.True,"Keep the saved Linebreaker and original character materials.");
    input=duel.PlayerTransform.GetComponentInChildren<MagicInputController>(true);executor=input.EarthExecutor;
    Assert.That(input.TrySelectElement(ElementId.Earth),Is.True);
    var pool=(EarthFragmentPool)typeof(MagicExecutor).GetField("fragmentPool",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(executor);
    Assert.That(pool,Is.Not.Null,"Use the player's existing authored stone pool.");
    Vector3 up=duel.PlayerTransform.up;
    Vector3 direction=FindClearFireDirection(duel.PlayerTransform,binding.PlayerSession.MuzzlePosition);
    Vector3 side=Vector3.Cross(up,direction).normalized;
    Vector3 center=duel.PlayerTransform.position+direction*2.8f;
    for(int i=0;i<stones.Length;i++)
    {
     float radius=.3f+(i%3)*.055f;
     Vector3 probe=center+side*((i%3-1)*.8f)+direction*((i/3)*.75f)+up*4;
     Assert.That(Physics.Raycast(probe,-up,out var ground,9,~0,QueryTriggerInteraction.Ignore),Is.True,"Need actual arena support under the test stones.");
     Assert.That(Vector3.Dot(ground.normal,up),Is.GreaterThan(.35f));
     stones[i]=pool.Acquire(executor,ground.point+up*(radius+.08f),radius,28+i*7);
     Assert.That(stones[i],Is.Not.Null);Assert.That(stones[i].GetComponent<MeshFilter>().sharedMesh,Is.Not.Null);
    }
    Physics.SyncTransforms();
    Vector3 subject=center+up*1.25f;
    pose=camera.gameObject.AddComponent<FireVisualCaptureCamera>();pose.Place(subject+side*6-direction*3+up*.9f,subject);
    yield return null;yield return new WaitForEndOfFrame();
    Time.captureFramerate=30;
    Vector3 screen=camera.WorldToScreenPoint(stones[0].Body.worldCenterOfMass);
    Assert.That(screen.z,Is.GreaterThan(0));
    Assert.That(input.TryBeginGravityWellAtScreenPoint(new float2(screen.x,screen.y)),Is.True,"Acquire through the real MagicInputController screen targeting path.");
    var presenter=All<EarthMaterialFeedbackPresenter>().Single();
    ParticleSystem ReadPool(string name)=>(ParticleSystem)typeof(EarthMaterialFeedbackPresenter).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(presenter);
    var newDust=ReadPool("dust");var oldDust=ReadPool("softDust");var chips=ReadPool("chips");
    int startShed=executor.HeldMaterialShedEvents,releaseShed=0;bool sawNewDust=false,sawChips=false,sawLivingHold=false;float focusTravel=0;
    Vector3 heldFocus=executor.GravityWellFocus;
    for(int phase=0;phase<4;phase++)
    {
     string label=new[]{"gather","hold","move","release"}[phase];
     if(phase==3){input.EndGravityWell();releaseShed=executor.HeldMaterialShedEvents;}
     for(int frame=0;frame<60;frame++)
     {
      if(phase<3)
      {
       float lift=phase==0?Mathf.SmoothStep(0,1,frame/59f)*1.2f:1.2f;
       Vector3 target=heldFocus+up*lift+(phase==2?side*Mathf.Sin(frame*.065f)*1.15f:Vector3.zero);
       Vector3 pointer=camera.WorldToScreenPoint(target);
       Assert.That(input.TryUpdateGravityWellAtScreenPoint(new float2(pointer.x,pointer.y)),Is.True);
       if(phase==2)focusTravel=Mathf.Max(focusTravel,Vector3.Distance(executor.GravityWellFocus,heldFocus+up*1.2f));
      }
      yield return new WaitForEndOfFrame();
      sawNewDust|=newDust.particleCount>0;sawChips|=chips.particleCount>0;sawLivingHold|=actor.LivingHoldWeight>.2f;
      if(frame%6==0)ProductionCaptureResolution.SaveScreen(Path.Combine(folder,label+"-"+(frame/6).ToString("D3")+".png"));
      Vector3 focus=executor.GravityWellFocus;
      csv.AppendLine(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}",label,frame,executor.GravityWellCapturedCount,executor.HeldMaterialShedEvents,newDust.particleCount,oldDust!=null?oldDust.particleCount:0,chips.particleCount,actor.LivingHoldWeight,focus.x,focus.y,focus.z));
      if(phase==1&&frame==30)Assert.That(executor.GravityWellCapturedCount,Is.GreaterThanOrEqualTo(3),"Gather several real physical rocks, not one proxy.");
     }
    }
    Assert.That(executor.HeldMaterialShedEvents-startShed,Is.GreaterThan(12));Assert.That(sawNewDust&&sawChips&&sawLivingHold,Is.True);
    Assert.That(focusTravel,Is.GreaterThan(.4f));Assert.That(executor.IsGravityWellActive,Is.False);
    Assert.That(executor.HeldMaterialShedEvents,Is.EqualTo(releaseShed),"Released owner must stop shedding.");
   }
   finally
   {
    input?.EndGravityWell();foreach(var stone in stones)if(stone!=null)stone.gameObject.SetActive(false);
    if(pose!=null)UnityEngine.Object.Destroy(pose);camera.transform.SetPositionAndRotation(oldPosition,oldRotation);Time.captureFramerate=oldCapture;resolution.Dispose();
    File.WriteAllText(Path.Combine(folder,"ActualLinebreaker.csv"),csv.ToString());
    File.WriteAllText(Path.Combine(folder,"SCOPE.txt"),"Actual saved Linebreaker, actual player MagicInputController screen acquisition/update/release and existing player stone pool. Five physically spawned authored stones on raycast arena support. No material cue/particle injection, teleporting held targets, pool count overrides, or hidden geometry. Camera-only fixture framing,1920x1080 saved pipeline/post/UI.40 screenshots over gather/hold/move/release. Assertions prove route/lifecycle, visual quality still requires review.");
   }
  }
 }
}
