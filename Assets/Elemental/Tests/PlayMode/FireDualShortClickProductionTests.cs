using System.Collections;
using Elemental.Simulation.Magic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode { public sealed partial class HardPolishSchoolInputTests {
[UnityTest,Timeout(240000)] public IEnumerator MiddleContextDoesNotReinterpretAlreadyHeldMouseButtons(){
 var settings=InputSystem.settings;var bg=settings.backgroundBehavior;var ed=settings.editorInputBehaviorInPlayMode;
 settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
 try{yield return KeyTap(Key.Digit1);Assert.That(magic.SelectedElement,Is.EqualTo(ElementId.Fire),InputStateEvidence);var a=binding.PlayerAbilities;var cam=All<Elemental.Presentation.Rendering.CelestialSystemBehaviour>()[0].TargetCamera;var motor=duel.PlayerTransform.GetComponent<Elemental.Runtime.Characters.PlanetMotor>();Vector2 p=cam.WorldToScreenPoint(cam.transform.position+motor.LocalUp*30+motor.FacingForward*15);
  for(int order=0;order<2;order++){
   InputSystem.QueueStateEvent(mouse,new MouseState{position=p});InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSeconds(.3f);
   var first=order==0?MouseButton.Left:MouseButton.Right;int shots=a.Shots;
   InputSystem.QueueStateEvent(mouse,new MouseState{position=p}.WithButton(first));yield return null;yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
   InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift));
   InputSystem.QueueStateEvent(mouse,new MouseState{position=p}.WithButton(first).WithButton(MouseButton.Middle));yield return null;yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
   Assert.That(a.Shots,Is.EqualTo(shots));Assert.That(a.IsBoltCharging,Is.False,"Middle cancels an earlier normal charge");
   InputSystem.QueueStateEvent(mouse,new MouseState{position=p}.WithButton(MouseButton.Middle));yield return null;yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
   Assert.That(a.Shots,Is.EqualTo(shots),"Releasing prior held button cannot shoot in new context");
   InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift));
   InputSystem.QueueStateEvent(mouse,new MouseState{position=p}.WithButton(first).WithButton(MouseButton.Middle));yield return null;yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
   Assert.That(a.Shots,Is.EqualTo(shots+1),"Fresh modifier edge is admitted; "+InputStateEvidence);
  }
 }finally{InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState());settings.backgroundBehavior=bg;settings.editorInputBehaviorInPlayMode=ed;}
}
}}
