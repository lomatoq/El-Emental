using System.Collections;
using System.Linq;
using Elemental.Input.Actions;
using Elemental.Input.Gestures;
using Elemental.Presentation.Animation;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class ArmorJumpAimAnimationRuntimeTests
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";

        [UnityTest]
        public IEnumerator PhysicalArmorHoldReturnsToWeightedOrdinaryPoseAndShortSpaceUsesJumpLane()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            Mouse mouse = null;
            Keyboard keyboard = null;
            AsyncOperation unload = null;
            try
            {
                EarthSceneReadinessGate gate = All<EarthSceneReadinessGate>(scene).First();
                double readinessDeadline = Time.realtimeSinceStartupAsDouble + 130d;
                while (!gate.IsReady && !gate.Failed && Time.realtimeSinceStartupAsDouble < readinessDeadline)
                    yield return null;
                Assert.That(gate.IsReady, Is.True, gate.Status);
                foreach (EarthMvpBotController bot in All<EarthMvpBotController>(scene)) bot.enabled = false;

                MagicInputController input = All<MagicInputController>(scene)
                    .First(value => value.name == "Planet Character");
                PlanetMotor motor = input.GetComponent<PlanetMotor>();
                HumanoidCharacterPresentation presentation =
                    input.GetComponentInChildren<HumanoidCharacterPresentation>(true);
                EarthCharacterPoseController pose = presentation.PoseController;
                EarthAnimationDriver driver = presentation.GetComponent<EarthAnimationDriver>();
                Animator animator = presentation.Animator;
                PlayerInput player = input.GetComponent<PlayerInput>();
                Assert.That(motor, Is.Not.Null);
                Assert.That(pose, Is.Not.Null);
                Assert.That(driver, Is.Not.Null);
                Assert.That(animator != null && animator.isHuman, Is.True);
                int magicLayer = animator.GetLayerIndex("Earth Magic Upper Body");
                Assert.That(magicLayer, Is.GreaterThanOrEqualTo(0));

                mouse = InputSystem.AddDevice<Mouse>("Armor ordinary-pose mouse");
                keyboard = InputSystem.AddDevice<Keyboard>("Armor ordinary-pose keyboard");
                player.neverAutoSwitchControlSchemes = true;
                player.ActivateInput();
                player.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
                player.currentActionMap.Enable();

                Vector2 center = new(Screen.width * .5f, Screen.height * .5f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
                QueueMiddle(mouse, center, true);
                yield return null;
                yield return null;
                Assert.That(input.IsArmorActive, Is.True, "The physical Shift+MMB route did not equip armor.");

                double armorDeadline = Time.realtimeSinceStartupAsDouble + 2.0d;
                while (Time.realtimeSinceStartupAsDouble < armorDeadline)
                {
                    QueueMiddle(mouse, center, true);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
                    yield return null;
                }
                yield return new WaitForEndOfFrame();

                Assert.That(input.IsArmorActive, Is.True);
                Assert.That(motor.ArmorEncumbrance01,
                    Is.InRange(EarthPersistentAnimationPolicy.MinimumArmorEncumbrance - .01f, 1f),
                    "Equipped armor did not reach the motor's weighted movement policy.");
                Assert.That(motor.Telemetry.Brace01, Is.LessThan(.01f),
                    "Persistent armor incorrectly entered the cast-brace state instead of ordinary locomotion.");
                Assert.That(pose.CurrentRequest.Technique, Is.Not.EqualTo(EarthTechniqueId.Armor),
                    "Equipped armor still owns a permanent cast pose.");
                Assert.That(pose.CurrentRequest.Technique, Is.Not.EqualTo(EarthTechniqueId.ArmorDome));
                Assert.That(pose.CurrentRequest.Technique, Is.Not.EqualTo(EarthTechniqueId.ArmorOrbit));
                Assert.That(driver.GetLayerWeight(magicLayer), Is.LessThan(.12f),
                    "Armor remained in the raised-hands magic layer after its finite presentation window.");

                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                float leftBelowHead = Vector3.Dot(head.position - leftHand.position, motor.LocalUp);
                float rightBelowHead = Vector3.Dot(head.position - rightHand.position, motor.LocalUp);
                Assert.That(leftBelowHead, Is.GreaterThan(.18f));
                Assert.That(rightBelowHead, Is.GreaterThan(.18f));

                QueueMiddle(mouse, center, false);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                yield return null;
                Assert.That(input.IsArmorActive, Is.False);

                // A short Space press is routed through the real Pillar-vs-jump
                // disambiguation path. Release before the pillar charge threshold.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;

                double jumpDeadline = Time.realtimeSinceStartupAsDouble + 1.2d;
                while (motor.HasStableSupport && Time.realtimeSinceStartupAsDouble < jumpDeadline)
                    yield return null;
                Assert.That(motor.HasStableSupport, Is.False, "Short Space did not reach the ordinary motor jump.");
                yield return new WaitForEndOfFrame();
                Assert.That(pose.CurrentRequest.Technique, Is.Not.EqualTo(EarthTechniqueId.PillarJump));
                Assert.That(driver.GetLayerWeight(magicLayer), Is.LessThan(.12f),
                    "The ordinary jump retained a stale hand-cast overlay.");
            }
            finally
            {
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (scene.IsValid() && scene.isLoaded) unload = SceneManager.UnloadSceneAsync(scene);
            }
            if (unload != null) while (!unload.isDone) yield return null;
        }

        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static void QueueMiddle(Mouse mouse, Vector2 position, bool held)
        {
            var state = new MouseState { position = position };
            state.WithButton(MouseButton.Middle, held);
            InputSystem.QueueStateEvent(mouse, state);
        }
    }
}
