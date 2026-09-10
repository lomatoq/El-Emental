using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Elemental.Presentation.UI;
using Elemental.Runtime.World;
using Elemental.Simulation.Rendering;
using NUnit.Framework;
using Unity.Cinemachine;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class MenuCameraOcclusionProductionTests
    {
        private Scene scene, previous;
        private static readonly int FadeId = Shader.PropertyToID("_MenuOcclusionFade");
        [UnityTearDown] public IEnumerator Restore()
        {
            Time.timeScale = 1;
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
        [UnityTest] public IEnumerator StartupOrbitRetainsStoneGeometryAndFadesWithoutRendererPops()
        {
            string folder = "BuildReports/MenuCamera/OrbitOcclusion-" + System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(folder);
            previous = SceneManager.GetActiveScene();
            const string path = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByPath(path); SceneManager.SetActiveScene(scene);
            var gate = Find<EarthSceneReadinessGate>(); var flow = Find<FrontendFlowController>();
            double deadline = Time.realtimeSinceStartupAsDouble + 130;
            while ((!gate.IsReady || flow.State == FrontendState.Loading) && !gate.Failed && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(gate.IsReady, Is.True, gate.Status);
            Assert.That(flow.State, Is.EqualTo(FrontendState.Main));
            var menu = Find<CinematicMenuCamera>();
            var camera = Find<CinemachineBrain>().GetComponent<Camera>();
            var renderers = (Renderer[])typeof(CinematicMenuCamera).GetField("_sceneRenderers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu);
            var wasHidden = (bool[])typeof(CinematicMenuCamera).GetField("_rendererWasHidden", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu);
            var stones = new List<Renderer>(); var meshes = new List<Mesh>();
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i]; var mf = r != null ? r.GetComponent<MeshFilter>() : null;
                if (r == null || wasHidden[i] || mf == null || mf.sharedMesh == null || r.sharedMaterial == null ||
                    r.sharedMaterial.shader.name != "Elemental/Graphics V5/Rumble Rock Lit") continue;
                Assert.That(r.sharedMaterial.HasProperty(FadeId), Is.True, "The native stone material needs the independent camera fade.");
                stones.Add(r); meshes.Add(mf.sharedMesh);
            }
            Assert.That(stones.Count, Is.GreaterThan(20));
            var fade = new float[stones.Count]; var block = new MaterialPropertyBlock();
            yield return new WaitForEndOfFrame(); Save(folder, "00-main");
            for (int i = 0; i < stones.Count; i++) { stones[i].GetPropertyBlock(block); fade[i] = block.HasFloat(FadeId) ? block.GetFloat(FadeId) : 1; }
            Assert.That(flow.BeginBot(), Is.True);
            int frames = 0, transitions = 0, partiallyVisible = 0; float nextCapture = 0;
            float started = Time.unscaledTime; long peakCpu = 0;
            var trace = new StringBuilder("frame,time,state,progress,near,x,y,z,partial,fadeChanges\n");
            var changes = new StringBuilder("frame,renderer,previous,next\n");
            using var cpu = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Elemental.MenuCamera.OcclusionFade", 64);
            deadline = Time.realtimeSinceStartupAsDouble + 20;
            try
            {
                while ((flow.State == FrontendState.Starting || menu.OwnsPresentation) && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    yield return new WaitForEndOfFrame();
                    int partial = 0, changed = 0;
                    for (int i = 0; i < stones.Count; i++)
                    {
                        var r = stones[i]; if (r == null || !r.gameObject.activeInHierarchy || !r.enabled) continue;
                        Assert.That(r.forceRenderingOff, Is.False, r.name + ": whole stone was toggled off during the rendered orbit.");
                        Assert.That(r.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(meshes[i]), r.name + ": camera must not simplify stone geometry.");
                        r.GetPropertyBlock(block); float value = block.HasFloat(FadeId) ? block.GetFloat(FadeId) : 1;
                        if (value > .001f && value < .999f) partial++;
                        float delta = Mathf.Abs(value - fade[i]);
                        Assert.That(delta, Is.LessThanOrEqualTo(Time.unscaledDeltaTime / MenuOcclusionFade.FadeOutSeconds + .005f), r.name + ": opacity jumped at an occlusion boundary.");
                        if (delta > .001f) { changed++; changes.AppendLine($"{frames},{r.name},{fade[i]:F4},{value:F4}"); }
                        if (r.name == "Arena_FloorBase_INTACT") Assert.That(value, Is.EqualTo(1), "Keep the supporting arena floor.");
                        fade[i] = value;
                    }
                    partiallyVisible += partial; transitions += changed; frames++;
                    peakCpu = System.Math.Max(peakCpu, cpu.LastValue);
                    Vector3 p = camera.transform.position;
                    trace.AppendLine($"{frames},{Time.unscaledTime-started:F4},{flow.State},{menu.DepartureProgress:F4},{camera.nearClipPlane:F4},{p.x:F4},{p.y:F4},{p.z:F4},{partial},{changed}");
                    Assert.That(camera.nearClipPlane, Is.EqualTo(.1f).Within(.001f), "Do not hide the defect by shrinking the authored near clip.");
                    if (Time.unscaledTime - started >= nextCapture) { Save(folder, $"orbit-{frames:D3}"); nextCapture += .35f; }
                    yield return null;
                }
                Assert.That(flow.State, Is.EqualTo(FrontendState.Combat));
                yield return new WaitForSecondsRealtime(.3f);
                yield return new WaitForEndOfFrame(); Save(folder, "99-combat");
                Assert.That(frames, Is.GreaterThan(12));
                Assert.That(partiallyVisible, Is.GreaterThan(0), "The actual saved route must exercise smooth occluders.");
                Assert.That(transitions, Is.GreaterThan(4));
                for (int i = 0; i < stones.Count; i++)
                {
                    if (stones[i] == null) continue;
                    stones[i].GetPropertyBlock(block);
                    Assert.That(block.HasFloat(FadeId) ? block.GetFloat(FadeId) : 1, Is.EqualTo(1).Within(.001f), stones[i].name + ": fade lease must restore after combat handoff.");
                }
            }
            finally
            {
                File.WriteAllText(folder + "/motion.csv", trace.ToString());
                File.WriteAllText(folder + "/fade-changes.csv", changes.ToString());
                File.WriteAllText(folder + "/metrics.txt", $"stones={stones.Count}\nframes={frames}\npartialSamples={partiallyVisible}\nfadeChanges={transitions}\npeakOcclusionCpuNs={peakCpu}\n");
            }
        }
        private static void Save(string folder, string name)
        {
            var frame = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(folder + "/" + name + ".png", frame.EncodeToPNG()); Object.Destroy(frame);
        }
        private T Find<T>() where T : Component
        {
            foreach (var root in scene.GetRootGameObjects()) { var found = root.GetComponentInChildren<T>(true); if (found != null) return found; }
            return null;
        }
    }
}
