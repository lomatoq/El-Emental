using System.Collections;
using System.IO;
using Elemental.Input.Gestures;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [UnityTest]
        public IEnumerator RunningKeepsSeismicReadabilityWithoutDensePulseSpam()
        {
            Actor actor = _actors.Find(a => a.Presentation.GetComponentInParent<MagicInputController>() != null);
            Assert.That(actor, Is.Not.Null, "Select the local input owner, not whichever presentation has a pose controller.");
            var motor = actor.Presentation.GetComponentInParent<PlanetMotor>();
            var vision = motor.GetComponent<EarthSeismicVision>();
            var camera = motor.GetComponent<MagicInputController>().CastCamera;
            Assert.That(camera, Is.Not.Null);
            Assert.That(camera.gameObject.scene, Is.EqualTo(_scene));
            vision.SetActive(true);
            yield return new WaitForSeconds(1.2f);
            int startPulses = vision.EmittedPulseCount;
            int frames = 0, readable = 0;
            float traveled = 0f;
            Vector3 previous = motor.Body.position;
            actor.Input.Move = new Unity.Mathematics.float2(0f, 1f);
            float end = Time.time + 2f;
            try
            {
                while (Time.time < end)
                {
                    yield return _frame;
                    traveled += Vector3.Distance(previous, motor.Body.position);
                    previous = motor.Body.position;
                    frames++;
                    if (Shader.GetGlobalFloat("_EarthSeismicVision") > .95f) readable++;
                    if (motor.HasStableSupport && motor.AcceptsMovingSupport && !motor.IsMantling)
                        Assert.That(Shader.GetGlobalFloat("_EarthSeismicVision"), Is.GreaterThan(.95f),
                            "Running support must never restart the toggle fade.");
                }
                Assert.That(traveled, Is.GreaterThan(2f));
                Assert.That(readable / (float)frames, Is.GreaterThan(.75f));
                Assert.That(vision.EmittedPulseCount - startPulses,
                    Is.LessThanOrEqualTo(Mathf.CeilToInt(traveled / 1.5f) + 3));
                string folder = Path.GetFullPath("BuildReports/EnvironmentAnimationRescue/SeismicVision");
                Directory.CreateDirectory(folder);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "RunningGameView.png"));
                AssertRunningSeismicPixels(camera, motor, folder);
                Debug.Log($"[Seismic running] travel={traveled:F2}m, readable={readable}/{frames}, pulses={vision.EmittedPulseCount-startPulses}");
                yield return _frame;
            }
            finally { actor.Input.Move = Unity.Mathematics.float2.zero; vision.SetActive(false); }
        }

        private static void AssertRunningSeismicPixels(Camera camera, PlanetMotor motor, string folder)
        {
            const int width = 960, height = 540;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture oldActive = RenderTexture.active, oldTarget = camera.targetTexture;
            float oldAspect = camera.aspect;
            float[] strengths = Shader.GetGlobalFloatArray("_EarthSeismicStrengths16");
            Vector4[] waves = Shader.GetGlobalVectorArray("_EarthSeismicWaves16");
            try
            {
                Assert.That(waves.Length, Is.EqualTo(EarthSeismicVision.WaveCount), "Legacy shader array capacity truncated new pulses.");
                Assert.That(strengths.Length, Is.EqualTo(EarthSeismicVision.WaveCount));
                Assert.That(Shader.GetGlobalFloatArray("_EarthSeismicRadiusTravels16").Length,
                    Is.EqualTo(EarthSeismicVision.WaveCount));
                float youngestRadius = float.PositiveInfinity;
                for (int i = 0; i < waves.Length; i++)
                    if (strengths[i] > 0f) youngestRadius = Mathf.Min(youngestRadius, waves[i].w);
                Assert.That(youngestRadius, Is.LessThan(8f),
                    "A grounded running player must receive the current automatic pulse, including slots beyond the legacy five.");
                target.Create();
                camera.targetTexture = target;
                camera.aspect = width / (float)height;
                Color32[] Capture(string filename)
                {
                    // Render the actual input owner's camera; additive fixtures may
                    // also contain another camera composited into the Game View.
                    camera.Render();
                    RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    readback.Apply();
                    File.WriteAllBytes(Path.Combine(folder, filename), readback.EncodeToPNG());
                    return readback.GetPixels32();
                }
                Color32[] visible = Capture("RunningReadability.png");
                Shader.SetGlobalFloatArray("_EarthSeismicStrengths16", new float[EarthSeismicVision.WaveCount]);
                Color32[] darkReference = Capture("RunningWithoutWavesReference.png");
                int revealed = 0;
                for (int i = 0; i < visible.Length; i++)
                    if (visible[i].r > darkReference[i].r + 8) revealed++;
                Vector3 viewport = camera.WorldToViewportPoint(motor.Body.position + motor.LocalUp);
                var evidence = new System.Text.StringBuilder();
                evidence.AppendLine($"camera={camera.name}, scene={camera.gameObject.scene.name}, position={camera.transform.position:F3}, forward={camera.transform.forward:F3}");
                evidence.AppendLine($"player={motor.Body.position:F3}, playerViewport={viewport:F3}, cameraDistance={Vector3.Distance(camera.transform.position, motor.Body.position):F3}, revealedPixels={revealed}/{visible.Length}");
                for (int i = 0; i < Mathf.Min(waves.Length, strengths.Length); i++)
                    if (strengths[i] > 0f) evidence.AppendLine($"wave[{i}]={waves[i]:F3}, strength={strengths[i]:F3}");
                File.WriteAllText(Path.Combine(folder, "RunningReadability.txt"), evidence.ToString());
                Debug.Log("[Seismic pixel QA] " + evidence);
                Assert.That(revealed, Is.GreaterThan(visible.Length / 100),
                    "The running player's actual camera must reveal geometry; an active shader global alone is insufficient.");
            }
            finally
            {
                Shader.SetGlobalFloatArray("_EarthSeismicStrengths16", strengths);
                camera.targetTexture = oldTarget;
                camera.aspect = oldAspect;
                RenderTexture.active = oldActive;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(readback);
            }
        }

        [UnityTest]
        public IEnumerator SeismicVisionRevealsNightGeometryAndImmediatelyLosesAirborneSupport()
        {
            Actor actor = _actors.Find(a => a.Presentation.PoseController != null);
            Assert.That(actor, Is.Not.Null);
            PlanetMotor motor = actor.Presentation.GetComponentInParent<PlanetMotor>();
            MagicInputController input = motor.GetComponent<MagicInputController>();
            EarthSeismicVision vision = motor.GetComponent<EarthSeismicVision>();
            Assert.That(input, Is.Not.Null);
            Assert.That(vision, Is.Not.Null);
            CelestialSystemBehaviour celestial = null;
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<CelestialSystemBehaviour>();
                if (found != null) celestial = found;
            }
            Assert.That(celestial, Is.Not.Null);
            float oldPhase = celestial.Snapshot.TimeOfDay01;
            bool oldRequested = vision.Requested;
            using var publishTiming = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Elemental.SeismicVision.Publish", 128);
            try
            {
                Assert.That(motor.HasStableSupport, Is.True);
                celestial.SetTimeOfDayForQa(.75f);
                vision.SetActive(true);
                Assert.That(Shader.GetGlobalFloat("_EarthSeismicVision"), Is.Zero, "Toggle must not snap on.");
                yield return new WaitForSeconds(.12f);
                Assert.That(Shader.GetGlobalFloat("_EarthSeismicVision"), Is.InRange(.01f, .99f));
                yield return new WaitForSeconds(1.1f);
                yield return _frame;
                Assert.That(vision.IsActive, Is.True);
                Assert.That(vision.VisiblePulseCount, Is.GreaterThan(0));
                Assert.That(Shader.GetGlobalFloat("_EarthSeismicVision"), Is.EqualTo(1f));
                string folder = Path.GetFullPath("BuildReports/EnvironmentAnimationRescue/SeismicVision");
                Directory.CreateDirectory(folder);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "NightGrounded.png"));
                yield return _frame;
                actor.Input.Move = new Unity.Mathematics.float2(0f, 1f);
                yield return new WaitForSeconds(.35f);
                Assert.That(Shader.GetGlobalFloat("_EarthSeismicMotion01"), Is.GreaterThan(.5f), "Walking must soften wave peaks.");
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "NightWalking.png"));
                yield return _frame;
                actor.Input.Move = Unity.Mathematics.float2.zero;
                yield return new WaitForSeconds(.3f);
                vision.SetActive(false);
                Assert.That(Shader.GetGlobalFloat("_EarthSeismicVision"), Is.EqualTo(1f), "Toggle must not snap off.");
                yield return new WaitForSeconds(.5f);
                Assert.That(Shader.GetGlobalFloat("_EarthSeismicVision"), Is.InRange(.01f, .99f));
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "NightFadeOut.png"));
                yield return _frame;
                float partial = Shader.GetGlobalFloat("_EarthSeismicVision");
                vision.SetActive(true);
                Assert.That(Shader.GetGlobalFloat("_EarthSeismicVision"), Is.EqualTo(partial), "Reversing must preserve the current blend.");
                yield return new WaitForSeconds(1.1f);
                yield return _frame; // Launch after presentation; observe the next rendered update.
                motor.BeginExternalLaunch(12);
                motor.Body.linearVelocity += motor.LocalUp * 5f;
                yield return _frame;
                Assert.That(vision.IsActive, Is.False, "Airborne perception must stop on the first rendered frame.");
                Assert.That(vision.VisiblePulseCount, Is.Zero, "Old waves survived loss of support.");
                Assert.That(Shader.GetGlobalFloat("_EarthSeismicVision"), Is.Zero);
                Assert.That(vision.Requested, Is.True, "Jumping suspends the ability without erasing the player's toggle.");
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "NightAirborne.png"));
                double deadline = Time.realtimeSinceStartupAsDouble + 5d;
                while (!vision.IsActive && Time.realtimeSinceStartupAsDouble < deadline) yield return _frame;
                Assert.That(vision.IsActive, Is.True, "Ground contact must resume fresh waves.");
                Assert.That(Shader.GetGlobalFloat("_EarthSeismicVision"), Is.GreaterThan(.95f),
                    "Support reacquisition must restore the toggled mode immediately, not restart its fade.");
                yield return new WaitForSeconds(1.1f);
                vision.SetActive(false);
                yield return new WaitForSeconds(1.1f);
                yield return _frame;
                Assert.That(vision.IsActive, Is.False);
                Assert.That(Shader.GetGlobalFloat("_EarthSeismicVision"), Is.Zero);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "NightNormal.png"));
                yield return _frame;
                Assert.That(publishTiming.Valid, Is.True);
                Debug.Log($"[Seismic fade QA] Publish CPU last frame: {publishTiming.LastValue} ns (all publishers, not GPU time).");
            }
            finally
            {
                actor.Input.Move = Unity.Mathematics.float2.zero;
                vision.SetActive(oldRequested);
                celestial.SetTimeOfDayForQa(oldPhase);
            }
        }
    }
}
