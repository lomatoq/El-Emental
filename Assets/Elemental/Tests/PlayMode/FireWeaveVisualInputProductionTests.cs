using System.Collections;
using System.IO;
using System.Reflection;
using Elemental.Presentation.Fire;
using Elemental.Simulation.Fire;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
 public sealed partial class HardPolishSchoolInputTests
 {
  [UnityTest,Timeout(240000)]public IEnumerator ActualWheelHandStreamEndsWhileItsOldGasDrainsIntoFieldTransition()
  {
   FireVisualCaptureCamera cameraRig=null;string folder="BuildReports/HardPolish/G05/WeaveInputHandoff-"+System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
   try
   {
    yield return KeyTap(Key.Digit1);yield return null;yield return new WaitForEndOfFrame();
    Assert.That(magic.SelectedElement,Is.EqualTo(Elemental.Simulation.Magic.ElementId.Fire),InputStateEvidence);
    var actor=duel.PlayerTransform.GetComponent<Elemental.Runtime.Characters.PlanetMotor>();var ability=binding.PlayerAbilities;
    var camera=All<Elemental.Presentation.Rendering.CelestialSystemBehaviour>()[0].TargetCamera;
    cameraRig=camera.gameObject.AddComponent<FireVisualCaptureCamera>();Vector3 up=actor.LocalUp,side=Vector3.Cross(up,actor.FacingForward).normalized;
    cameraRig.Place(actor.Body.position+side*10+up*3,actor.Body.position+actor.FacingForward*2+up*2);
    yield return null;yield return new WaitForEndOfFrame();
    Vector2 pointer=new Vector2(Screen.width*.52f,Screen.height*.83f);
    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift));yield return null;yield return new WaitForEndOfFrame();
    InputSystem.QueueStateEvent(mouse,new MouseState{position=pointer}.WithButton(MouseButton.Middle));yield return null;yield return new WaitForEndOfFrame();yield return new WaitForFixedUpdate();yield return new WaitForEndOfFrame();
    Assert.That(binding.PlayerSession.IsActive,Is.True,InputStateEvidence);
    var oldSolver=binding.Presenter(binding.PlayerSession.Group.Slot).CpuDiagnostics.FlowDiagnostics.Solver;
    var effects=binding.GetComponent<FireAbilityEffects>();
    var snapshots=(FirePresentationSnapshot[])typeof(FireAbilityEffects).GetField("snapshots",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(effects);
    int start=(int)typeof(FireAbilityEffects).GetField("WeaveStart",BindingFlags.NonPublic|BindingFlags.Static).GetRawConstantValue();
    int end=(int)typeof(FireAbilityEffects).GetField("LowFlightHandStart",BindingFlags.NonPublic|BindingFlags.Static).GetRawConstantValue();
    var serialField=typeof(FireFlowParticleSolver).GetField("serial",BindingFlags.NonPublic|BindingFlags.Instance);
    Directory.CreateDirectory(folder);var log=new System.Text.StringBuilder();
    void Capture(string name)
    {
     float minimum=float.PositiveInfinity;for(int i=start;i<end;i++)if(snapshots[i].Emits)minimum=Mathf.Min(minimum,Vector3.Distance(snapshots[i].Origin,binding.PlayerMuzzle.position));
     log.AppendLine($"{name}: level={magic.FirePower01*12} stream={binding.PlayerSession.IsActive} oldParcels={oldSolver.Count} oldBirthSerial={serialField.GetValue(oldSolver)} nozzleMinimumDistance={minimum} morph={effects.WeaveMorphPower} coverage={effects.WeaveVerticalCoverage}");
     var texture=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(folder+"/"+name+".png",texture.EncodeToPNG());}finally{Object.Destroy(texture);}
    }
    for(int i=0;i<2;i++){InputSystem.QueueStateEvent(mouse,new MouseState{position=pointer,scroll=new Vector2(0,120)}.WithButton(MouseButton.Middle));yield return null;yield return new WaitForEndOfFrame();}
    for(int i=0;i<20;i++)yield return null;yield return new WaitForEndOfFrame();
    Assert.That(magic.FirePower01*12,Is.EqualTo(6).Within(.01));Assert.That(binding.PlayerSession.IsActive,Is.True);Capture("level6-stream");
    uint serialBefore=(uint)serialField.GetValue(oldSolver);int begins=binding.PoseBegins;
    InputSystem.QueueStateEvent(mouse,new MouseState{position=pointer,scroll=new Vector2(0,120)}.WithButton(MouseButton.Middle));yield return null;yield return new WaitForEndOfFrame();
    Assert.That(ability.WeaveForm,Is.EqualTo(FireWeaveForm.Orbit));Assert.That(binding.PlayerSession.IsActive,Is.False);
    for(int frame=0;frame<49;frame++)
    {
     yield return null;yield return new WaitForEndOfFrame();
     Assert.That((uint)serialField.GetValue(oldSolver),Is.LessThanOrEqualTo(serialBefore),"Stopped hand stream generated a new particle birth.");
     Assert.That(binding.PoseBegins,Is.EqualTo(begins));
     for(int i=start;i<end;i++)if(snapshots[i].Emits)
      Assert.That(Vector3.Distance(snapshots[i].Origin,binding.PlayerMuzzle.position),Is.GreaterThan(.7f),"Weave source is still injecting beside the hand after semantic Orbit.");
     if(frame==5)Capture("level7-after-0.1s");if(frame==47)Capture("level7-after-0.8s");
    }
    for(int i=0;i<3;i++){InputSystem.QueueStateEvent(mouse,new MouseState{position=pointer,scroll=new Vector2(0,120)}.WithButton(MouseButton.Middle));yield return null;yield return new WaitForEndOfFrame();}
    for(int i=0;i<30;i++)yield return null;yield return new WaitForEndOfFrame();
    Assert.That(magic.FirePower01*12,Is.EqualTo(10).Within(.01));Assert.That(binding.PlayerSession.IsActive,Is.False);Capture("level10-sphere");
    for(int i=0;i<2;i++){InputSystem.QueueStateEvent(mouse,new MouseState{position=pointer,scroll=new Vector2(0,120)}.WithButton(MouseButton.Middle));yield return null;yield return new WaitForEndOfFrame();}
    for(int i=0;i<36;i++)yield return null;yield return new WaitForEndOfFrame();
    Assert.That(magic.FirePower01*12,Is.EqualTo(12).Within(.01));Assert.That(binding.PlayerSession.IsActive,Is.False);Capture("level12-sphere");
    File.WriteAllText(folder+"/measurements.txt",log.ToString());
   }
   finally{InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState());if(cameraRig!=null)Object.Destroy(cameraRig);}
  }
 }
}
