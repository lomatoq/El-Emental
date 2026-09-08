using Elemental.Simulation.Bending;
using System.Collections;
using System.IO;
using System.Text;
using Elemental.Input.Actions;
using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class LandingSlamKeyboardProductionTests
    {
        private Scene _previous, _scene;
        private Keyboard _keyboard;
        private Mouse _mouse;

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            Time.timeScale = 1f;
            if (_previous.IsValid() && _previous.isLoaded) SceneManager.SetActiveScene(_previous);
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        }

        [UnityTest] public IEnumerator RealAirborneShiftSpaceRoutesExactlyOneCraterAndRadialPulse()
        {
            _previous = SceneManager.GetActiveScene();
            const string path = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByPath(path); SceneManager.SetActiveScene(_scene);
            var gate = Find<EarthSceneReadinessGate>();
            float deadline = Time.realtimeSinceStartup + 130f;
            while (!gate.IsReady && !gate.Failed && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(gate.IsReady, Is.True, gate.Status);
            yield return ProductionCombatTestFlow.BeginBotAfterReadiness(_scene);
            var duel = Find<EarthMvpDuelController>();
            var bot = duel.BotTransform.GetComponent<EarthMvpBotController>();
            if (bot != null) bot.enabled = false;
            var player = duel.PlayerTransform;
            var input = player.GetComponent<MagicInputController>();
            var router = player.GetComponent<EarthActionRouterBehaviour>();
            var slam = player.GetComponent<EarthLandingSlam>();
            var motor = player.GetComponent<PlanetMotor>();
            var body = player.GetComponent<Rigidbody>();
            Assert.That(input.enabled && router.enabled, Is.True);
            Assert.That(slam, Is.Not.Null, "Shipping router must install the slam component.");
            var playerInput = player.GetComponent<PlayerInput>();
            _keyboard = InputSystem.AddDevice<Keyboard>("Landing slam regression keyboard");
            _mouse = InputSystem.AddDevice<Mouse>("Landing slam regression mouse");
            playerInput.neverAutoSwitchControlSchemes = true; playerInput.ActivateInput();
            if (!playerInput.user.valid)
            {
                playerInput.enabled = false; playerInput.enabled = true; playerInput.neverAutoSwitchControlSchemes = true; playerInput.ActivateInput();
            }
            Assert.That(playerInput.user.valid, Is.True, "The production test requires actual paired devices.");
            playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", _keyboard, _mouse);
            playerInput.currentActionMap.Enable();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(_mouse, new MouseState());
            yield return null; yield return null;
            deadline = Time.realtimeSinceStartup + 10f;
            while (!motor.HasStableSupport && Time.realtimeSinceStartup < deadline) yield return new WaitForFixedUpdate();
            Assert.That(motor.HasStableSupport, Is.True);
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            Vector3 up = motor.LocalUp;
            var planet = Find<VoxelPlanetBehaviour>();
            int editsBefore = planet.State.EditCount;
            int commitsBefore = slam.CommitCount;
            motor.BeginExternalLaunch(8);
            body.linearVelocity = up * 14f;
            deadline = Time.realtimeSinceStartup + 2f;
            while (motor.HasStableSupport && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(motor.HasStableSupport, Is.False, "The real physics launch must leave the ground before the keyboard chord.");
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.LeftShift, Key.Space));
            bool routed = false, held = false;
            var trace = new StringBuilder();
            deadline = Time.realtimeSinceStartup + 12f;
            while ((slam.CommitCount == commitsBefore || slam.HasPendingTerrain) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                routed |= router.Owner == EarthActionOwner.LandingSlam;
                held |= slam.IsHeld;
                trace.AppendLine($"owner={router.Owner}; held={slam.IsHeld}; armed={slam.IsArmed}; grounded={motor.HasStableSupport}; speed={Vector3.Dot(body.linearVelocity, up):F2}; commits={slam.CommitCount}; reject={slam.LastRejection}");
            }
            Directory.CreateDirectory("BuildReports/LandingSlamKeyboard");
            File.WriteAllText("BuildReports/LandingSlamKeyboard/routing.txt", trace.ToString());
            Assert.That(routed && held, Is.True, "Paired Shift+Space must reach the real LandingSlam owner and ability.");
            Assert.That(slam.CommitCount, Is.EqualTo(commitsBefore + 1), slam.LastRejection);
            Assert.That(planet.State.EditCount, Is.GreaterThan(editsBefore));
            Assert.That(slam.LastReleasedFloorPieces, Is.GreaterThan(0));
            Assert.That(slam.LastWaveColumnCount, Is.EqualTo(36));
            for (int i = 0; i < 35; i++) yield return new WaitForFixedUpdate();
            Assert.That(slam.CommitCount, Is.EqualTo(commitsBefore + 1), "The held chord cannot repeat on the same contact.");
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null; yield return null;
            Assert.That(slam.IsHeld, Is.False, "Keyboard release must reach the ability.");
        }

        private T Find<T>() where T : Component
        {
            foreach (var root in _scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            Assert.Fail(typeof(T).Name + " missing from production scene"); return null;
        }
    }
}
