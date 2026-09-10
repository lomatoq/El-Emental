using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Rendering;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Fire;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
 public sealed partial class HardPolishFireStreamBindingRuntimeTests
 {
  [UnityTest,Timeout(300000)] public IEnumerator CollisionDrivenFireActualLinebreakerHeadOnObliqueCornerOrbitAimAndMovingCapture()
  {
   const string directory="BuildReports/HardPolish/G05/FluidDemonstration";Directory.CreateDirectory(directory);
   var camera=All<CelestialSystemBehaviour>().Single().TargetCamera;var resolution=new ProductionCaptureResolution();
   FireVisualCaptureCamera pose=null;GameObject wall=null,corner=null;DirectFireInputLease lease=null;
   PlanetMotor motor=null;MonoBehaviour originalInput=null;AirborneMantleProofInput movement=null;
   int oldCaptureRate=Time.captureFramerate;Vector3 oldPosition=camera.transform.position;Quaternion oldRotation=camera.transform.rotation;
   var csv=new StringBuilder("phase,frame,time,parcels,collisions,queries,budgetStops,cpuMs,rootTravel,muzzleX,muzzleY,muzzleZ,coolingParcels,minimumTemperature,maximumSoot\n");
   try
   {
    yield return resolution.WaitForRenderedSize(camera);yield return EnterCombat();
    var actor=duel.PlayerTransform.GetComponentInChildren<HumanoidCharacterPresentation>();Assert.That(actor,Is.Not.Null);
    Assert.That(actor.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.sharedMaterials.Any(m=>m!=null&&m.shader!=null&&m.shader.name.Contains("Rumble"))),Is.True,"Use actual saved Linebreaker and original Rumble character material.");
    var input=duel.PlayerTransform.GetComponentInChildren<Elemental.Input.Gestures.MagicInputController>(true);
    Assert.That(input.TrySelectElement(Elemental.Simulation.Magic.ElementId.Fire),Is.True);lease=new DirectFireInputLease(input);
    motor=duel.PlayerTransform.GetComponent<PlanetMotor>();Assert.That(motor,Is.Not.Null);originalInput=motor.ConfiguredInputSource;
    movement=motor.gameObject.AddComponent<AirborneMantleProofInput>();motor.ConfigureInputSource(movement);
    var session=binding.PlayerSession;Vector3 direction=FindClearFireDirection(duel.PlayerTransform,session.MuzzlePosition);
    Vector3 up=duel.PlayerTransform.up;Vector3 side=Vector3.Cross(up,direction).normalized;if(side.sqrMagnitude<.1f)side=duel.PlayerTransform.right;
    Vector3 source=session.MuzzlePosition;Vector3 subject=source+direction*2;
    pose=camera.gameObject.AddComponent<FireVisualCaptureCamera>();pose.Place(subject+side*7+up*1.2f,subject);
    wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Owned flow head-on stone wall";
    wall.transform.SetPositionAndRotation(source+direction*3.4f,Quaternion.LookRotation(direction,up));wall.transform.localScale=new Vector3(7,7,.35f);
    corner=GameObject.CreatePrimitive(PrimitiveType.Cube);corner.name="Owned flow inside corner";
    corner.transform.SetPositionAndRotation(source+direction*2+side*1.1f,Quaternion.LookRotation(side,up));corner.transform.localScale=new Vector3(5,7,.35f);corner.SetActive(false);
    #if UNITY_EDITOR
    var stone=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Elemental/Content/GraphicsV5/Materials/RumbleArenaSandstone.mat");
    Assert.That(stone,Is.Not.Null,"Use the saved stone material for collision capture.");wall.GetComponent<Renderer>().sharedMaterial=stone;corner.GetComponent<Renderer>().sharedMaterial=stone;
    #endif
    Physics.SyncTransforms();Time.captureFramerate=30;
    Assert.That(session.TryBegin(source+direction*8),Is.True);int slot=session.Group.Slot;
    var gas=binding.Presenter(slot).CpuDiagnostics.FlowDiagnostics;Assert.That(gas,Is.Not.Null,"Active backend must be real transported flow.");
    Vector3 startRoot=duel.PlayerTransform.position;float movingTravel=0;int collisionBaseline=0;bool sawCoolingGas=false;
    for(int phase=0;phase<6;phase++)
    {
     string label=new[]{"head-on","oblique","corner","orbit","moving-aim","release"}[phase];
     wall.SetActive(phase<3);corner.SetActive(phase==2);Physics.SyncTransforms();
     if(phase==2)
     {
      // The side wall is at +side: inspect its inner face from the open -side half-space.
      // Keep both physical test walls visible; do not hide scenery to manufacture a clear shot.
      Vector3 cornerTarget=subject+direction*.65f;
      Vector3 cornerEye=subject-side*7-direction*1.5f+up*2.2f;
      pose.Place(cornerEye,cornerTarget);
      Vector3 sight=cornerTarget-cornerEye;
      Ray viewRay=new Ray(cornerEye,sight.normalized);
      Assert.That(wall.GetComponent<Collider>().Raycast(viewRay,out _,sight.magnitude),Is.False,"Head-on fixture wall obscures the corner interaction.");
      Assert.That(corner.GetComponent<Collider>().Raycast(viewRay,out _,sight.magnitude),Is.False,"Side fixture wall obscures the corner interaction.");
     }
     if(phase==5)session.Stop();
     for(int frame=0;frame<36;frame++)
     {
      movement.Move=phase==4?new float2(.25f,.1f):float2.zero;
      if(phase<5)session.SetAim(session.MuzzlePosition+(direction+side*(phase==1?.5f:phase==4?Mathf.Sin(frame*.13f)*.5f:0)).normalized*8);
      if(phase==3)
      {
       if(frame<6&&gas.Solver.Count>0){Vector3 inside=gas.Solver.Particles[gas.Solver.Count/2].Position;pose.Place(inside,inside+direction*3);}
       else pose.Place(subject+(Quaternion.AngleAxis(frame*4,up)*side)*7+up,subject);
      }
      if(phase==4)pose.Place(duel.PlayerTransform.position+up*1.2f+direction*2+side*8,duel.PlayerTransform.position+up*1.2f+direction*2);
      yield return new WaitForEndOfFrame();
      float travel=Vector3.Distance(startRoot,duel.PlayerTransform.position);if(phase==4)movingTravel=Mathf.Max(movingTravel,travel);
      if(frame%3==0)ProductionCaptureResolution.SaveScreen(Path.Combine(directory,(phase==3&&frame<6?"camera-inside":label)+"-"+(frame/3).ToString("D3")+".png"));
      int cooling=0;float minimumTemperature=1,maximumSoot=0;
      for(int parcel=0;parcel<gas.Solver.Count;parcel++)
      {
       var particle=gas.Solver.Particles[parcel];if(particle.Spark)continue;
       minimumTemperature=Mathf.Min(minimumTemperature,particle.Temperature);maximumSoot=Mathf.Max(maximumSoot,particle.Soot);
       if(particle.Temperature<.45f&&particle.Soot>.15f)cooling++;
      }
      sawCoolingGas|=cooling>0;
      Vector3 muzzle=session.MuzzlePosition;
      csv.AppendLine(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14}",label,frame,Time.time,gas.Solver.Count,gas.Solver.Collisions,gas.Solver.QueryCount,gas.Solver.BudgetStops,gas.LastStepMilliseconds,travel,muzzle.x,muzzle.y,muzzle.z,cooling,minimumTemperature,maximumSoot));
     }
     if(phase==0){Assert.That(gas.Solver.Collisions,Is.GreaterThan(20));collisionBaseline=gas.Solver.Collisions;}
     if(phase==2)Assert.That(gas.Solver.Collisions,Is.GreaterThan(collisionBaseline+20));
    }
    Assert.That(sawCoolingGas,Is.True,"Actual transported stream must enter its cooling-smoke phase.");
    Assert.That(movingTravel,Is.GreaterThan(.1f),"Actual motor must move during aimed flow, not only camera or transform teleport.");
    Assert.That(gas.Solver.Count,Is.Zero,"Release drains transported gas.");
   }
   finally
   {
    binding.PlayerSession.Stop();if(motor!=null)motor.ConfigureInputSource(originalInput);if(movement!=null)UnityEngine.Object.Destroy(movement);
    lease?.Dispose();if(wall!=null)UnityEngine.Object.Destroy(wall);if(corner!=null)UnityEngine.Object.Destroy(corner);
    if(pose!=null)UnityEngine.Object.Destroy(pose);camera.transform.SetPositionAndRotation(oldPosition,oldRotation);Time.captureFramerate=oldCaptureRate;resolution.Dispose();
    File.WriteAllText(Path.Combine(directory,"ActualCharacter.csv"),csv.ToString());
    File.WriteAllText(Path.Combine(directory,"SCOPE.txt"),"Saved Linebreaker, actual motor/pose and Fire stream owner. 1920x1080 motion sequence every 3 simulation frames at 30fps (10 captured fps). Particle motion uses real swept PhysX collisions; damage remains original finite FireStreamSession authority and does not follow cosmetic fans. Visual acceptance and GPU timing require review; these assertions do not establish beauty.");
   }
  }
 }
}
