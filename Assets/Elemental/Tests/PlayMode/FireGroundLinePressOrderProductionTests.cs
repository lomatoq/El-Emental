using System.Collections;
using Elemental.Simulation.Magic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode { public sealed partial class HardPolishSchoolInputTests {
[UnityTest,Timeout(240000)] public IEnumerator RealLeftDragDrawsSupportedBurningContourWithoutHandStream(){
 var settings=InputSystem.settings;var bg=settings.backgroundBehavior;var ed=settings.editorInputBehaviorInPlayMode;
 settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
 try{yield return KeyTap(Key.Digit1);Assert.That(magic.SelectedElement,Is.EqualTo(ElementId.Fire),InputStateEvidence);var a=binding.PlayerAbilities;var motor=duel.PlayerTransform.GetComponent<Elemental.Runtime.Characters.PlanetMotor>();
  var camera=All<Elemental.Presentation.Rendering.CelestialSystemBehaviour>()[0].TargetCamera;
  Vector3 up=motor.LocalUp,side=Vector3.Cross(up,motor.FacingForward).normalized,origin=motor.SupportFeetPoint(up);
  int shots=a.Shots;
  for(int i=0;i<6;i++){
   Vector3 point=origin+side*(1+i*.28f)+motor.FacingForward*Mathf.Sin(i*.55f)*.6f;
   Vector2 pixel=camera.WorldToScreenPoint(point);
   InputSystem.QueueStateEvent(mouse,new MouseState{position=pixel}.WithButton(MouseButton.Left));yield return null;yield return new WaitForEndOfFrame();yield return null;yield return new WaitForEndOfFrame();
  }
  Assert.That(a.SourceCount,Is.GreaterThanOrEqualTo(2),InputStateEvidence+" eligible="+magic.WorldPointerEligible);Assert.That(a.Shots,Is.EqualTo(shots));Assert.That(binding.PlayerSession.IsActive,Is.False);
  InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;yield return new WaitForEndOfFrame();Assert.That(a.SourceCount,Is.GreaterThan(0),"Contour remains burning after release");
 }finally{InputSystem.QueueStateEvent(mouse,new MouseState());settings.backgroundBehavior=bg;settings.editorInputBehaviorInPlayMode=ed;}
}
}}
