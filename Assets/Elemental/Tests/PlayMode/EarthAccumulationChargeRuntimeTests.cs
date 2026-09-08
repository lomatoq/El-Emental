using System.Collections;
using System.IO;
using System.Linq;
using Elemental.Input.Gestures;
using Elemental.Presentation.Camera;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthAccumulationChargeRuntimeTests
    {
        [UnityTest]
        public IEnumerator PhysicalLmbWallAccumulationDrivesCameraWithoutRmbCharge()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            Scene scene = default;
            Keyboard keyboard = null;
            Mouse mouse = null;
            try
            {
                yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
                scene = SceneManager.GetSceneByPath(scenePath);
                var gate = All<EarthSceneReadinessGate>(scene).Single();
                double timeout = Time.realtimeSinceStartupAsDouble + 130d;
                while (!gate.IsReady && !gate.Failed && Time.realtimeSinceStartupAsDouble < timeout) yield return null;
                Assert.That(gate.IsReady, Is.True, gate.Status);
                foreach (var bot in All<EarthMvpBotController>(scene)) bot.enabled = false;
                var director = All<EarthCameraDirector>(scene).Single();
                var camera = director.GetComponent<Camera>();
                var look = director.GetComponent<EarthChargeCameraLookdevV2>();
                var input = director.Player.GetComponent<MagicInputController>();
                var playerInput = director.Player.GetComponent<PlayerInput>();
                var queries = All<EarthSurfaceQueryService>(scene).Single();
                keyboard = InputSystem.AddDevice<Keyboard>("Accumulation proof keyboard");
                mouse = InputSystem.AddDevice<Mouse>("Accumulation proof mouse");
                playerInput.neverAutoSwitchControlSchemes = true;
                playerInput.ActivateInput();
                playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
                playerInput.currentActionMap?.Enable();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return new WaitForSeconds(.4f);

                Vector2 pointer = default;
                bool found = false;
                for (int row = 0; row < 4 && !found; row++)
                for (int column = 0; column < 4 && !found; column++)
                {
                    Vector2 candidate = new Vector2(camera.pixelWidth * (.3f + column * .1f),
                        camera.pixelHeight * (.25f + row * .1f));
                    Ray ray = camera.ScreenPointToRay(candidate);
                    var query = new EarthSurfaceQuery(new float3(ray.origin.x, ray.origin.y, ray.origin.z),
                        new float3(ray.direction.x, ray.direction.y, ray.direction.z), 80f, EarthSurfaceCapabilities.Draw, .05f);
                    if (!queries.TrySample(in query, out _)) continue;
                    pointer = candidate; found = true;
                }
                Assert.That(found, Is.True, "Production camera needs an actual drawable earth surface.");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer, buttons = 1 });
                yield return null;
                Vector2 end = pointer + new Vector2(camera.pixelWidth * .16f, 0f);
                timeout = Time.realtimeSinceStartupAsDouble + 1.2d;
                while (Time.realtimeSinceStartupAsDouble < timeout)
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = end, buttons = 1 });
                    yield return null;
                }
                yield return new WaitForEndOfFrame();
                Assert.That(input.CurrentBendPhase, Is.Not.EqualTo(BendPhase.Charging), "Only LMB was used.");
                Assert.That(input.AccumulationCharge01, Is.GreaterThan(.7f));
                Assert.That(look.SampleChargeSources().Accumulation, Is.GreaterThan(.7f));
                Assert.That(look.ChargeFeedback.FieldOfViewDelta, Is.GreaterThan(7f));
                Directory.CreateDirectory("BuildReports/ChargeFeedback");
                ScreenCapture.CaptureScreenshot("BuildReports/ChargeFeedback/04-lmb-wall-accumulation.png");
                float peak = look.ChargeFeedback.FieldOfViewDelta;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = end });
                yield return new WaitForSeconds(.9f);
                Assert.That(input.AccumulationCharge01, Is.Zero);
                Assert.That(look.SampleChargeSources().Accumulation, Is.Zero);
                Assert.That(look.ChargeFeedback.FieldOfViewDelta, Is.LessThan(.1f));
                File.WriteAllText("BuildReports/ChargeFeedback/lmb-accumulation.txt",
                    $"physicalLmbOnly=true; drawnSurface=true; maximumFovDelta={peak:F3}; releaseCleared=true");
            }
            finally
            {
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            }
        }

        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
