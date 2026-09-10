using System.Collections;
using Elemental.Input.Actions;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Magic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode {
 public sealed partial class HardPolishSchoolInputTests {
  [UnityTest,Timeout(240000)] public IEnumerator RealFireKeysConsumeJumpAndRetainHeldRingWithNewMouseGrammar(){
   var settings=InputSystem.settings;var background=settings.backgroundBehavior;var editor=settings.editorInputBehaviorInPlayMode;
   settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
   try{yield return KeyTap(Key.Digit1);Assert.That(magic.SelectedElement,Is.EqualTo(ElementId.Fire));var ability=binding.PlayerAbilities;Assert.That(ability,Is.Not.Null);
    var motor=duel.PlayerTransform.GetComponent<PlanetMotor>();var reader=duel.PlayerTransform.GetComponent<PlanetInputReader>();
    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));yield return null;yield return new WaitForFixedUpdate();
    Assert.That(magic.ConsumesFireJump,Is.True);Assert.That(reader.SampleCommand(920).JumpPressed,Is.False);Assert.That(ability.IsLifting,Is.True);
    InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return new WaitForFixedUpdate();Assert.That(motor.FireLiftActive,Is.False);
    int rings=ability.Rings;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift,Key.Space));for(int i=0;i<5;i++)yield return null;
    Assert.That(ability.Rings,Is.EqualTo(rings+1));Assert.That(ability.IsLifting,Is.False);Assert.That(reader.SampleCommand(921).JumpPressed,Is.False);
    InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
    Assert.That(ability.IsRingCharging,Is.False,"Chord release releases held ring");
    int shots=ability.Shots;
    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift));yield return null;
    var pointer=new Vector2(Screen.width*.5f,Screen.height*.6f);
    InputSystem.QueueStateEvent(mouse,new MouseState{position=pointer}.WithButton(MouseButton.Middle));yield return null;
    for(int tap=0;tap<3;tap++){
      InputSystem.QueueStateEvent(mouse,new MouseState{position=pointer}.WithButton(MouseButton.Middle).WithButton(MouseButton.Left));yield return null;
      InputSystem.QueueStateEvent(mouse,new MouseState{position=pointer}.WithButton(MouseButton.Middle));yield return new WaitForSeconds(.15f);
    }
    Assert.That(ability.Shots,Is.EqualTo(shots+3),"Each fresh LMB edge while MMB fires one rapid shot");
    InputSystem.QueueStateEvent(mouse,new MouseState{position=pointer});InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));yield return null;flow.Pause();yield return null;Assert.That(ability.IsLifting,Is.False);
    InputSystem.QueueStateEvent(keyboard,new KeyboardState());flow.Resume();yield return null;yield return KeyTap(Key.Digit2);Assert.That(ability.IsAvailable,Is.False);
   }finally{InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState());settings.backgroundBehavior=background;settings.editorInputBehaviorInPlayMode=editor;}
  }
 }
}
