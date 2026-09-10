using System.Collections;
using Elemental.Input.Actions;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class HardPolishSchoolInputTests
    {
        [UnityTest]
        public IEnumerator ActualControlSpaceConsumesJumpRetainsMovementAndStopsOnPauseOrSchoolChange()
        {
            var settings=InputSystem.settings;
            var background=settings.backgroundBehavior;var editor=settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            try
            {
                yield return KeyTap(Key.Digit2);
                var router=magic.GetComponent<EarthActionRouterBehaviour>();
                var reader=magic.GetComponent<PlanetInputReader>();
                foreach(var ctrl in new[]{Key.LeftCtrl,Key.RightCtrl})
                {
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(ctrl,Key.Space,Key.W));
                    yield return null;yield return null;
                    Assert.That(router.Owner,Is.EqualTo(EarthActionOwner.StoneCounter));
                    var guard=magic.GetComponent<EarthStoneCounterGuard>();
                    Assert.That(guard.IsGuarding,Is.True);
                    var command=reader.SampleCommand(891);
                    Assert.That(command.Move.y,Is.GreaterThan(.5f));
                    Assert.That(command.JumpPressed,Is.False);
                    Assert.That(router.Consumes(EarthInputConsumption.Primary),Is.True);
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(ctrl));
                    yield return null;yield return null;
                    Assert.That(guard.IsGuarding,Is.False);
                }
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftCtrl,Key.Space));
                yield return null;yield return null;
                flow.Pause();yield return null;
                Assert.That(magic.GetComponent<EarthStoneCounterGuard>().IsGuarding,Is.False);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                flow.Resume();yield return null;yield return null;
                yield return KeyTap(Key.Digit1);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftCtrl,Key.Space));
                yield return null;yield return null;
                Assert.That(magic.GetComponent<EarthStoneCounterGuard>().IsGuarding,Is.False,"Earth counter stole Fire input ownership.");
            }
            finally
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                settings.backgroundBehavior=background;settings.editorInputBehaviorInPlayMode=editor;
            }
        }
    }
}
