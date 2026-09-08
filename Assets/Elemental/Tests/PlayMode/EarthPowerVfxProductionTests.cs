using System;
using System.Collections;
using System.IO;
using System.Linq;
using Elemental.Input.Gestures;
using Elemental.Presentation.Camera;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.UI;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Time;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Elemental.Tests.PlayMode
{
    /// <summary>Real saved arena, actual gameplay entry points and production camera/materials.</summary>
    public sealed class EarthPowerVfxProductionTests
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const string Folder = "BuildReports/PowerVfx/Production";
        [Serializable] private sealed class Evidence
        {
            public string utc, decor, arena;
            public int capturedTargets, releasedPieces, captureFrames;
            public float fullCharge, chargedVignette, releasedVignette, daylightPixelDifference, nightPixelDifference;
            public float nightGlobal, nightSolarAltitude;
            public float restingCharge, edgeBlurDifference, centreBlurDifference, edgeDarkening;
            public bool particlesRecorderValid, clarityRecorderValid;
            public double materialPeakMs, clarityPeakMs, materialMeanMs, clarityMeanMs;
            public string scope = "Actual gameplay API gravity grab/disassembly, routed Space charge, saved scene and production camera. Sunlight pair renders in one frame at identical camera/time; no asset save. CPU marker samples cover 90 held-gravity frames; no GPU measurement claim.";
        }
        [UnityTest] public IEnumerator CaptureGrabExtractionChargeAndSunlightInSavedArena()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Production asset visual fixture runs in Editor Play Mode.");
            yield break;
#else
            Scene scene = default, previous = SceneManager.GetActiveScene();
            Keyboard keyboard = null; Mouse mouse = null;
            Material atmosphere = null; float savedShaft = 0f, savedTimeScale = Time.timeScale;
            var report = new Evidence { utc = DateTime.UtcNow.ToString("O") };
            Directory.CreateDirectory(Folder);
            try
            {
                yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
                scene = SceneManager.GetSceneByPath(ScenePath); SceneManager.SetActiveScene(scene);
                var gate = All<EarthSceneReadinessGate>(scene).Single();
                double deadline = Time.realtimeSinceStartupAsDouble + 130;
                while (!gate.IsReady && !gate.Failed && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(gate.IsReady, Is.True, gate.Status);
                var flow = All<FrontendFlowController>(scene).Single();
                while (flow.State == FrontendState.Loading && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(flow.BeginBot(), Is.True);
                deadline = Time.realtimeSinceStartupAsDouble + 12;
                while (flow.State != FrontendState.Combat && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(flow.State, Is.EqualTo(FrontendState.Combat));
                foreach (var bot in All<EarthMvpBotController>(scene)) bot.enabled = false;
                // The offline scene may also contain an inactive online camera graph.
                // Follow the frontend's current binding, never select by list order.
                var frontendBinding = new SerializedObject(flow);
                var director = frontendBinding.FindProperty("cameraDirector").objectReferenceValue as EarthCameraDirector;
                Assert.That(director, Is.Not.Null, "Frontend must explicitly bind its current camera director.");
                Assert.That(director.isActiveAndEnabled, Is.True, "Offline round must use its active production camera.");
                var camera = director.GetComponent<UnityEngine.Camera>();
                var look = director.GetComponent<EarthChargeCameraLookdevV2>();
                var actor = director.Player;
                var motor = actor.GetComponent<PlanetMotor>();
                var actorMagic = actor.GetComponent<MagicInputController>();
                Assert.That(actorMagic, Is.Not.Null, "Directed player must own its authored magic input controller.");
                // The executor lives on the scene's magic host, not on the actor root.
                // Follow the same explicit binding used by the real input route.
                var executor = actorMagic.EarthExecutor;
                Assert.That(executor, Is.Not.Null, "Player magic input must reference its production executor.");
                var playerInput = actor.GetComponent<PlayerInput>();
                var sky = All<CelestialSystemBehaviour>(scene).Single();
                sky.SetLightingAuthorityForQa(CelestialLightingAuthorityMode.AnimatedEphemeris);
                sky.SetTimeOfDayForQa(.25f); sky.EvaluatePresentationForQa();
                for (int i = 0; i < 180 && !motor.HasStableSupport; i++) yield return new WaitForFixedUpdate();
                Assert.That(motor.HasStableSupport, Is.True);
                keyboard = InputSystem.AddDevice<Keyboard>("Power VFX proof keyboard");
                mouse = InputSystem.AddDevice<Mouse>("Power VFX proof mouse");
                playerInput.neverAutoSwitchControlSchemes = true; playerInput.ActivateInput();
                playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse); playerInput.currentActionMap?.Enable();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return null;
                Vector3 actorFocus = actor.position + actor.up;
                Vector3 view = Vector3.ProjectOnPlane(camera.transform.forward, actor.up).normalized;
                // Same production camera and render frame, at rest: permanent
                // softness must work without any charge and preserve the centre.
                var edgeMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Elemental/Content/Materials/AtmosphereFullscreen.mat");
                float savedBlur = edgeMaterial.GetFloat("_GameplayEdgeBlur");
                float savedDark = edgeMaterial.GetFloat("_GameplayEdgeDarkness");
                report.restingCharge = look.Charge01;
                Assert.That(report.restingCharge, Is.LessThan(.01f));
                try
                {
                    edgeMaterial.SetFloat("_GameplayEdgeBlur", 0f); edgeMaterial.SetFloat("_GameplayEdgeDarkness", 0f);
                    var sharp = Capture(camera, actorFocus, actor.up, view, "14-gameplay-original", true);
                    edgeMaterial.SetFloat("_GameplayEdgeBlur", savedBlur);
                    var soft = Capture(camera, actorFocus, actor.up, view, "15-gameplay-blur-only", true);
                    report.edgeBlurDifference = RegionDifference(sharp, soft, false, false);
                    report.centreBlurDifference = RegionDifference(sharp, soft, true, false);
                    Assert.That(report.edgeBlurDifference, Is.GreaterThan(.00001f), "Resting camera must actually blur screen edges.");
                    Assert.That(report.centreBlurDifference, Is.LessThan(.0002f), "Central gameplay region must remain sharp.");
                    edgeMaterial.SetFloat("_GameplayEdgeDarkness", savedDark);
                    var final = Capture(camera, actorFocus, actor.up, view, "16-gameplay-soft-vignette", true);
                    report.edgeDarkening = RegionDifference(soft, final, false, true);
                    Assert.That(report.edgeDarkening, Is.GreaterThan(.002f), "Permanent vignette must gently darken edges at zero charge.");
                }
                finally { edgeMaterial.SetFloat("_GameplayEdgeBlur", savedBlur); edgeMaterial.SetFloat("_GameplayEdgeDarkness", savedDark); }
                Capture(camera, actorFocus, actor.up, view, "01-charge-neutral");
                deadline = Time.realtimeSinceStartupAsDouble + 2.2;
                while (Time.realtimeSinceStartupAsDouble < deadline)
                { InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); yield return null; }
                yield return new WaitForEndOfFrame();
                report.fullCharge = look.Charge01; report.chargedVignette = look.ChargeVignetteIntensity;
                Assert.That(report.fullCharge, Is.GreaterThan(.8f));
                Assert.That(report.chargedVignette, Is.GreaterThan(.35f));
                Capture(camera, actorFocus, actor.up, view, "02-charge-full");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(1.6f);
                report.releasedVignette = look.ChargeVignetteIntensity;
                Assert.That(look.Charge01, Is.LessThan(.01f));
                Assert.That(report.releasedVignette, Is.LessThan(.25f));
                Capture(camera, actorFocus, actor.up, view, "03-charge-release");

                var decor = All<EarthDestructibleDecorRock>(scene)
                    .Where(value => value.IsEarthTargetValid && value.IsAnchored)
                    .OrderBy(value => Vector3.SqrMagnitude(value.transform.position - actor.position)).FirstOrDefault();
                Assert.That(decor, Is.Not.Null, "Production needs an anchored decor stone to prove extraction.");
                report.decor = decor.name;
                Vector3 focus = decor.transform.position, up = focus.normalized;
                view = Vector3.ProjectOnPlane(camera.transform.forward, up).normalized;
                Capture(camera, focus, up, view, "04-decor-before");
                Assert.That(executor.TryBeginGravityWell(decor.GetComponent<Collider>(), focus + up * .8f, up, true), Is.True);
                yield return new WaitForSeconds(.12f);
                Assert.That(decor.IsAnchored, Is.False, "Actual gravity grip must detach the selected decor.");
                Assert.That(executor.IsGravityWellActive, Is.True);
                report.capturedTargets = executor.GravityWellCapturedCount;
                Capture(camera, focus, up, view, "05-gravity-decor-extraction");
                using (var materialCpu = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Elemental.Earth.MaterialParticles", 120))
                using (var clarityCpu = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Elemental.Presentation.Clarity", 120))
                {
                    for (int i = 0; i < 90; i++)
                    {
                        yield return null;
                        report.captureFrames++;
                        double materialMs = materialCpu.LastValue / 1000000d, clarityMs = clarityCpu.LastValue / 1000000d;
                        report.materialPeakMs = Math.Max(report.materialPeakMs, materialMs);
                        report.clarityPeakMs = Math.Max(report.clarityPeakMs, clarityMs);
                        report.materialMeanMs += materialMs; report.clarityMeanMs += clarityMs;
                        if (i == 12 || i == 38) Capture(camera, focus, up, view, "06-gravity-hold-" + i);
                    }
                    report.particlesRecorderValid = materialCpu.Valid; report.clarityRecorderValid = clarityCpu.Valid;
                }
                report.materialMeanMs /= report.captureFrames; report.clarityMeanMs /= report.captureFrames;
                executor.CancelGravityWell();
                yield return new WaitForSeconds(.2f);
                var arena = All<EarthArenaStructure>(scene)
                    .Where(value => value.OrdinaryDamageEnabled && !value.IsFractured && value.PieceCount > 1)
                    .OrderBy(value => Vector3.SqrMagnitude(value.transform.position - actor.position)).FirstOrDefault();
                Assert.That(arena, Is.Not.Null);
                report.arena = arena.name;
                var collider = arena.GetComponentsInChildren<Collider>().First(value => value.enabled && value.gameObject.activeInHierarchy);
                focus = collider.bounds.center; up = focus.normalized;
                view = Vector3.ProjectOnPlane(camera.transform.forward, up).normalized;
                Capture(camera, focus, up, view, "07-arena-before");
                Assert.That(executor.TryBeginGravityWell(collider, focus + up * .8f, up, true), Is.True);
                executor.SetGravityStructureGesture(EarthGravityStructureIntent.Disassemble, .18f);
                yield return new WaitForSeconds(.12f);
                report.releasedPieces = arena.ReleasedPieceCount;
                Assert.That(report.releasedPieces, Is.GreaterThan(0), "Actual disassembly must release arena pieces.");
                Capture(camera, focus, up, view, "08-arena-extraction");
                yield return new WaitForSeconds(.25f);
                Capture(camera, focus, up, view, "09-arena-extraction-cloud");
                executor.CancelGravityWell();
                yield return new WaitForSeconds(2.3f);

                atmosphere = AssetDatabase.LoadAssetAtPath<Material>("Assets/Elemental/Content/Materials/AtmosphereFullscreen.mat");
                Assert.That(atmosphere, Is.Not.Null); savedShaft = atmosphere.GetFloat("_SunDustStrength");
                sky.SetTimeOfDayForQa(.25f); sky.EvaluatePresentationForQa();
                // One render frame, immutable view/time: restore the runtime-only material
                // value in finally, never mark the material dirty or save assets.
                atmosphere.SetFloat("_SunDustStrength", 0f);
                var without = Capture(camera, focus, up, view, "10-sun-shafts-off");
                atmosphere.SetFloat("_SunDustStrength", savedShaft);
                var with = Capture(camera, focus, up, view, "11-sun-shafts-on");
                report.daylightPixelDifference = Difference(without, with);
                Assert.That(report.daylightPixelDifference, Is.GreaterThan(.00001f));
                sky.SetTimeOfDayForQa(.75f); sky.EvaluatePresentationForQa();
                report.nightGlobal = Shader.GetGlobalFloat("_ElementalNight01");
                report.nightSolarAltitude = Shader.GetGlobalFloat("_ElementalSolarAltitude");
                Assert.That(report.nightGlobal, Is.GreaterThan(.999f), "Night fixture must reach actual full-night shader state.");
                Assert.That(report.nightSolarAltitude, Is.LessThan(0f));
                atmosphere.SetFloat("_SunDustStrength", 0f);
                without = Capture(camera, focus, up, view, "12-night-shafts-off");
                atmosphere.SetFloat("_SunDustStrength", savedShaft);
                with = Capture(camera, focus, up, view, "13-night-shafts-on");
                report.nightPixelDifference = Difference(without, with);
                Assert.That(report.nightPixelDifference, Is.LessThan(.0001f));
                File.WriteAllText(Folder + "/evidence.json", JsonUtility.ToJson(report, true));
            }
            finally
            {
                if (atmosphere != null) atmosphere.SetFloat("_SunDustStrength", savedShaft);
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                Time.timeScale = savedTimeScale;
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                File.WriteAllText(Folder + "/last-progress.json", JsonUtility.ToJson(report, true));
            }
#endif
        }

        private static Color32[] Capture(UnityEngine.Camera camera, Vector3 focus, Vector3 up, Vector3 view, string name, bool keepGameplayView = false)
        {
            Vector3 savedPosition = camera.transform.position; Quaternion savedRotation = camera.transform.rotation;
            var target = new RenderTexture(1280, 800, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            bool savedDithering = cameraData.dithering;
            try
            {
                // URP final-output dithering chooses a new noise pattern for each
                // render request, even within one frame. Disable only that quantization
                // noise for image subtraction, preserving every authored post effect.
                cameraData.dithering = false;
                Vector3 position = focus + up * 5f - view * 8f;
                if (!keepGameplayView) camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(focus - position, up));
                var request = new RenderPipeline.StandardRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); pixels.Apply(false, false);
                File.WriteAllBytes(Folder + "/" + name + ".png", pixels.EncodeToPNG());
                return pixels.GetPixels32();
            }
            finally
            {
                camera.transform.SetPositionAndRotation(savedPosition, savedRotation);
                cameraData.dithering = savedDithering;
                RenderTexture.active = previous; UnityEngine.Object.Destroy(pixels); target.Release(); UnityEngine.Object.Destroy(target);
            }
        }
        private static float Difference(Color32[] a, Color32[] b)
        {
            double sum = 0;
            for (int i = 0; i < a.Length; i++) sum += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
            return (float)(sum / (a.Length * 3d * 255d));
        }
        private static float RegionDifference(Color32[] a, Color32[] b, bool centre, bool signedDarkening)
        {
            double sum = 0; int count = 0;
            for (int y = 0; y < 800; y++) for (int x = 0; x < 1280; x++)
            {
                float u = (x + .5f) / 1280f, v = (y + .5f) / 800f;
                bool included = centre ? Mathf.Abs(u - .5f) < .08f && Mathf.Abs(v - .5f) < .08f
                    : u < .12f || u > .88f || v < .12f || v > .88f;
                if (!included) continue;
                int i = y * 1280 + x;
                int r = a[i].r - b[i].r, g = a[i].g - b[i].g, blue = a[i].b - b[i].b;
                sum += signedDarkening ? r + g + blue : Math.Abs(r) + Math.Abs(g) + Math.Abs(blue); count++;
            }
            return (float)(sum / (count * 3d * 255d));
        }
        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
