using System;
using System.Collections;
using System.IO;
using System.Linq;
using Elemental.Input.Gestures;
using Elemental.Presentation.Camera;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthChargeFeedbackRuntimeTests
    {
        [UnityTest]
        public IEnumerator PhysicalSpaceChargeWidensProductionLensAndReleasesEveryEffect()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            Scene scene = default;
            Keyboard keyboard = null;
            Mouse mouse = null;
            string folder = Path.GetFullPath(Path.Combine("BuildReports", "ChargeFeedback"));
            Directory.CreateDirectory(folder);
            try
            {
                yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
                scene = SceneManager.GetSceneByPath(scenePath);
                var gate = All<EarthSceneReadinessGate>(scene).Single();
                double deadline = Time.realtimeSinceStartupAsDouble + 130d;
                while (!gate.IsReady && !gate.Failed && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
                Assert.That(gate.IsReady, Is.True, gate.Status);
                foreach (var bot in All<EarthMvpBotController>(scene)) bot.enabled = false;
                var director = All<EarthCameraDirector>(scene).Single();
                var camera = director.GetComponent<Camera>();
                var look = director.GetComponent<EarthChargeCameraLookdevV2>();
                Assert.That(look, Is.Not.Null, "Shipping camera must own the charge adapter.");
                var actor = director.Player;
                var motor = actor.GetComponent<PlanetMotor>();
                var pillar = actor.GetComponent<EarthPillarMobility>();
                var input = actor.GetComponent<MagicInputController>();
                var playerInput = actor.GetComponent<PlayerInput>();
                for (int frame = 0; frame < 180 && !motor.HasStableSupport; frame++)
                    yield return new WaitForFixedUpdate();
                Assert.That(motor.HasStableSupport, Is.True);
                keyboard = InputSystem.AddDevice<Keyboard>("Charge feedback proof keyboard");
                mouse = InputSystem.AddDevice<Mouse>("Charge feedback proof mouse");
                playerInput.neverAutoSwitchControlSchemes = true;
                playerInput.ActivateInput();
                playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
                playerInput.currentActionMap?.Enable();
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                yield return new WaitForEndOfFrame();
                float neutralFov = camera.fieldOfView;
                Capture(camera, folder, "01-neutral");

                deadline = Time.realtimeSinceStartupAsDouble + 2.1d;
                while (Time.realtimeSinceStartupAsDouble < deadline)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                    yield return null;
                }
                yield return new WaitForEndOfFrame();
                Assert.That(pillar.IsCharging, Is.True, "Real routed Space must own a charge.");
                Assert.That(look.SampleChargeSources().Pillar, Is.GreaterThan(.8f));
                Assert.That(look.Charge01, Is.GreaterThan(.8f));
                Assert.That(camera.fieldOfView - neutralFov, Is.InRange(8f, 10.1f));
                Assert.That(look.ChargeFeedback.PositionShake, Is.GreaterThan(.003f));
                Assert.That(look.ChargeFeedback.RotationShake, Is.GreaterThan(.07f));
                var volume = look.GetComponentsInChildren<Volume>(true)
                    .Single(value => value.name == "Earth Runtime Lookdev Volume V2" ||
                        value.profile.Has<ChromaticAberration>());
                Assert.That(volume.profile.TryGet(out ChromaticAberration chromatic), Is.True);
                Assert.That(chromatic.intensity.value, Is.InRange(.25f, .301f));
                Vector3 unshakenPosition = camera.transform.position;
                Quaternion unshakenRotation = camera.transform.rotation;
                Capture(camera, folder, "02-full-charge");
                var cameraData = camera.GetUniversalAdditionalCameraData();
                Assert.That(cameraData.renderPostProcessing, Is.True);
                Assert.That((cameraData.volumeLayerMask.value & (1 << volume.gameObject.layer)) != 0, Is.True,
                    "Charge volume must belong to the camera's rendered volume mask.");
                var renderedStack = cameraData.volumeStack ?? VolumeManager.instance.stack;
                Assert.That(renderedStack.GetComponent<ChromaticAberration>().intensity.value,
                    Is.GreaterThan(.2f), "Actual URP volume stack must consume the charge effect.");
                float renderedFov = 2f * Mathf.Atan(1f / camera.projectionMatrix.m11) * Mathf.Rad2Deg;
                Assert.That(renderedFov, Is.EqualTo(camera.fieldOfView).Within(.1f),
                    "Rendered projection must use the charged lens.");
                Assert.That(Vector3.Distance(camera.transform.position, unshakenPosition), Is.LessThan(.00001f));
                Assert.That(Quaternion.Angle(camera.transform.rotation, unshakenRotation), Is.LessThan(.001f),
                    "SRP render shake must restore the camera pose after rendering.");

                float chargedFov = camera.fieldOfView;
                float chargedBaseFov = look.BaseFieldOfView;
                look.enabled = false;
                Assert.That(look.Charge01, Is.Zero);
                Assert.That(chromatic.intensity.value, Is.Zero);
                Assert.That(camera.fieldOfView, Is.EqualTo(chargedBaseFov).Within(.05f));
                look.enabled = true;
                deadline = Time.realtimeSinceStartupAsDouble + .4d;
                while (Time.realtimeSinceStartupAsDouble < deadline)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                    yield return null;
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                deadline = Time.realtimeSinceStartupAsDouble + 1.6d;
                while (Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                yield return new WaitForEndOfFrame();
                Assert.That(pillar.IsCharging, Is.False);
                Assert.That(look.Charge01, Is.LessThan(.001f));
                Assert.That(chromatic.intensity.value, Is.LessThan(.001f));
                Assert.That(look.ChargeFeedback.FieldOfViewDelta, Is.LessThan(.001f));
                // Releasing Space launches the real pillar, so Cinemachine legitimately
                // switches from Explore (60) to Airborne (64). Charge must return to
                // that current authored lens instead of overwriting it with old idle FOV.
                float releasedBaseFov = director.GetComponent<EarthCinemachineCameraController>().FieldOfView;
                Assert.That(camera.fieldOfView, Is.EqualTo(releasedBaseFov).Within(.05f));
                Assert.That(look.BaseFieldOfView, Is.EqualTo(releasedBaseFov).Within(.05f));
                Capture(camera, folder, "03-released");

                // Validate the disabled reader synchronously: the physical puppet
                // owns its enabled flag and restores allowed controls on its next tick.
                input.enabled = false;
                Assert.That(look.SampleChargeSources().Allowed, Is.False);
                input.enabled = true;
                // Suspend through the actual match owner, whose state remains
                // authoritative even when another control owner updates component flags.
                var duel = All<EarthMvpDuelController>(scene).Single();
                duel.SetRoundReady(false);
                yield return null;
                Assert.That(duel.CombatAllowed, Is.False);
                Assert.That(look.SampleChargeSources().Allowed, Is.False,
                    "Saved charge camera must be bound to the suspended scene duel.");
                Assert.That(look.Charge01, Is.Zero);
                duel.SetRoundReady(true);
                var clock = System.Diagnostics.Stopwatch.StartNew();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int sample = 0; sample < 10000; sample++) look.SampleChargeSources();
                long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                clock.Stop();
                Assert.That(bytes, Is.Zero, "Read-only charge sampling is a steady-state camera hot loop.");
                File.WriteAllText(Path.Combine(folder, "runtime-evidence.json"),
                    "{\"utc\":\"" + DateTime.UtcNow.ToString("O") + "\",\"neutralFov\":" +
                    neutralFov.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    ",\"chargedFov\":" + chargedFov.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    ",\"releasedAuthoredFov\":" + releasedBaseFov.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    ",\"sampleCount\":10000,\"allocatedBytes\":" + bytes +
                    ",\"samplingMilliseconds\":" + clock.Elapsed.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}");
            }
            finally
            {
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            }
        }

        private static void Capture(Camera camera, string folder, string name)
        {
            int width = 1280, height = Mathf.RoundToInt(1280f / camera.aspect);
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                pixels.Apply(false, false);
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.Destroy(pixels);
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
        }

        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
