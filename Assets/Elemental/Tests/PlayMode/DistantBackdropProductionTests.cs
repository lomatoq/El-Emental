#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Elemental.Presentation.Camera;
using Elemental.Presentation.DistantScenery;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.UI;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using Elemental.Simulation.Time;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class DistantBackdropProductionTests
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const string Folder = "BuildReports/DistantBackdrop";
        private Scene scene, previous;
        private ProductionCaptureResolution captureResolution;
        private float timeScale;
        private bool environmentWasPaused;
        private CelestialSystemBehaviour celestial;
        private CelestialLightingAuthorityMode lightingMode;
        private float phase;
        private FrontendFlowController flow;
        private bool reduced;
        private DistantBackdrop backdrop;
        private UnityEngine.Camera camera;
        private Vector3 cameraPosition;
        private Quaternion cameraRotation;
        private bool[] rendererStates;
        private Renderer[] renderers;
        private Report report;
        [Serializable] private sealed class Report
        {
            public string utc, graphicsApi, gpu, cpu;
            public int generated, lodGroups, frustumRenderers, changedPixels, motionSamples;
            public long applyTimeManagedBytes;
            public double applyTimeMeanMilliseconds;
            public bool preferenceBound, pausedStable, reducedStable, restored, unscaledMotionObserved;
        }
        [UnitySetUp] public IEnumerator Load()
        {
            captureResolution=new ProductionCaptureResolution();
            previous = SceneManager.GetActiveScene(); timeScale = Time.timeScale;
            Directory.CreateDirectory(Folder);
            report = new Report { utc = DateTime.UtcNow.ToString("O"), graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                gpu = SystemInfo.graphicsDeviceName, cpu = SystemInfo.processorType };
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1); SceneManager.SetActiveScene(scene);
            Assert.That(scene.path, Is.EqualTo(ScenePath));
            var gate = All<EarthSceneReadinessGate>().Single();
            double deadline = Time.realtimeSinceStartupAsDouble + 140;
            while (!gate.IsReady && !gate.Failed && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(gate.IsReady, Is.True, gate.Status);
            flow = All<FrontendFlowController>().Single();
            while (flow.State == FrontendState.Loading && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            reduced = flow.Preferences.ReducedMotion;
            Assert.That(flow.BeginBot(), Is.True);
            deadline = Time.realtimeSinceStartupAsDouble + 15;
            while (flow.State != FrontendState.Combat && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(flow.State, Is.EqualTo(FrontendState.Combat));
            foreach (var bot in All<EarthMvpBotController>()) bot.enabled = false;
            backdrop = All<DistantBackdrop>().Single();
            celestial = All<CelestialSystemBehaviour>().Single();
            lightingMode = celestial.LightingAuthority; phase = celestial.Snapshot.TimeOfDay01;
            var director = new SerializedObject(flow).FindProperty("cameraDirector").objectReferenceValue as EarthCameraDirector;
            Assert.That(director, Is.Not.Null);
            camera = celestial.TargetCamera; Assert.That(camera, Is.Not.Null);
            yield return captureResolution.WaitForRenderedSize(camera);
            ProductionCaptureResolution.SaveScreen(Folder+"/00-native1080-combat-ui.png");
            cameraPosition = camera.transform.position; cameraRotation = camera.transform.rotation;
            renderers = backdrop.GetComponentsInChildren<Renderer>(true);
            rendererStates = renderers.Select(item => item.enabled).ToArray();
            report.preferenceBound = backdrop.SettingsSource == flow;
            Assert.That(report.preferenceBound, Is.True, "Installer must bind the actual existing preference owner.");
        }
        [UnityTest] public IEnumerator HardPolishGradingVariants()
        {
            const string evidence = "BuildReports/HardPolish/G02/Grading";
            Directory.CreateDirectory(evidence);
            var volumes = All<Volume>().Where(v => v.enabled && v.weight > 0 && v.isGlobal &&
                (camera.GetUniversalAdditionalCameraData().volumeLayerMask.value & (1 << v.gameObject.layer)) != 0)
                .OrderByDescending(v => v.priority).ToArray();
            Assert.That(volumes, Is.Not.Empty);
            File.WriteAllText(evidence + "/owners.json", string.Join("\n", volumes.Select(v =>
                v.name + " priority=" + v.priority + " profile=" + v.profile.name + " shared=" + AssetDatabase.GetAssetPath(v.sharedProfile) +
                "\n" + string.Join("\n", v.profile.components.Select(c => JsonUtility.ToJson(c, true))))));
            VolumeProfile grading = volumes.First(v => v.profile.TryGet(out ColorAdjustments _)).profile;
            grading.TryGet(out ColorAdjustments color); grading.TryGet(out Tonemapping tone);
            float contrast = color.contrast.value; TonemappingMode mode = tone.mode.value;
            bool hadShadows = grading.TryGet(out ShadowsMidtonesHighlights shadows);
            string oldShadows = hadShadows ? JsonUtility.ToJson(shadows) : null;
            if (!hadShadows) shadows = grading.Add<ShadowsMidtonesHighlights>(true);
            celestial.SetLightingAuthorityForQa(CelestialLightingAuthorityMode.AnimatedEphemeris);
            try
            {
                foreach (var sample in new[] { ("day", .25f), ("sunset", .50f), ("night", .75f) })
                {
                    celestial.SetTimeOfDayForQa(sample.Item2); celestial.EvaluatePresentationForQa();
                    color.contrast.value = contrast; tone.mode.value = mode; shadows.active = false;
                    yield return null; Capture(sample.Item1 + "-original", evidence);
                    color.contrast.value = 0;
                    yield return null; Capture(sample.Item1 + "-contrast-zero", evidence);
                    shadows.active = true;
                    shadows.shadowsStart.Override(0); shadows.shadowsEnd.Override(.3f);
                    foreach (float gain in new[] { .2f, .4f, .6f })
                    {
                        shadows.shadows.Override(new Vector4(1, 1, 1, gain * celestial.Snapshot.Night01));
                        yield return null; Capture(sample.Item1 + "-shadows-" + gain.ToString("F1", System.Globalization.CultureInfo.InvariantCulture), evidence);
                    }
                }
            }
            finally
            {
                color.contrast.value = contrast; tone.mode.value = mode;
                if (hadShadows) JsonUtility.FromJsonOverwrite(oldShadows, shadows);
                else { grading.Remove<ShadowsMidtonesHighlights>(); Object.Destroy(shadows); }
            }
        }

        [UnityTest] public IEnumerator HardPolishDayNightMaterialAndMeshEvidence()
        {
            const string evidence = "BuildReports/HardPolish/Lighting";
            Directory.CreateDirectory(evidence);
            var provenance = new System.Text.StringBuilder("renderer\tmesh\tmeshPath\tmeshGuid\tmaterial\tshader\tmaterialGuid\ttriangles\tdistance\n");
            foreach (Renderer renderer in All<Renderer>())
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh :
                    renderer.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
                if (mesh == null) continue;
                string path = AssetDatabase.GetAssetPath(mesh);
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    provenance.AppendLine($"{AnimationUtility.CalculateTransformPath(renderer.transform, null)}\t{mesh.name}\t{path}\t{AssetDatabase.AssetPathToGUID(path)}\t{material.name}\t{material.shader.name}\t{AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(material))}\t{Enumerable.Range(0, mesh.subMeshCount).Sum(submesh => (long)mesh.GetIndexCount(submesh)) / 3}\t{Vector3.Distance(camera.transform.position, renderer.bounds.center):F2}");
                }
            }
            File.WriteAllText(evidence + "/renderer-provenance.tsv", provenance.ToString());
            var data = camera.GetUniversalAdditionalCameraData();
            bool post = data.renderPostProcessing;
            celestial.SetLightingAuthorityForQa(CelestialLightingAuthorityMode.AnimatedEphemeris);
            try
            {
                foreach (var sample in new[] { ("day", .25f), ("sunset", .50f), ("night", .75f) })
                {
                    celestial.SetTimeOfDayForQa(sample.Item2);
                    celestial.EvaluatePresentationForQa();
                    yield return null;
                    data.renderPostProcessing = false;
                    Capture(sample.Item1 + "-no-post", evidence);
                    data.renderPostProcessing = post;
                    Capture(sample.Item1 + "-final", evidence);
                }
            }
            finally { data.renderPostProcessing = post; }
        }

        [UnityTest] public IEnumerator SavedBackdropRendersInGameplayAndUsesExistingLightingPreferences()
        {
            Assert.That(backdrop.GeneratedCount, Is.GreaterThan(0)); report.generated = backdrop.GeneratedCount;
            Assert.That(backdrop.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(backdrop.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            foreach (Component component in backdrop.GetComponentsInChildren<Component>(true))
            {
                string ns = component != null ? component.GetType().Namespace ?? "" : "";
                Assert.That(ns.StartsWith("Unity.Netcode", StringComparison.Ordinal) || ns.StartsWith("Elemental.Online", StringComparison.Ordinal), Is.False);
            }
            var lods = backdrop.GetComponentsInChildren<LODGroup>(true); report.lodGroups = lods.Length;
            Assert.That(lods.Length, Is.EqualTo(backdrop.GeneratedCount));
            foreach (var group in lods)
            {
                LOD[] levels = group.GetLODs(); Assert.That(levels.Length, Is.EqualTo(2));
                Assert.That(levels[0].renderers.Length, Is.GreaterThan(0)); Assert.That(levels[1].renderers.Length, Is.GreaterThan(0));
                Mesh high = levels[0].renderers[0].GetComponent<MeshFilter>().sharedMesh;
                Mesh low = levels[1].renderers[0].GetComponent<MeshFilter>().sharedMesh;
                Assert.That(low.vertexCount, Is.LessThan(high.vertexCount));
            }
            SetPreference(false); yield return null;
            celestial.SetLightingAuthorityForQa(CelestialLightingAuthorityMode.AnimatedEphemeris);
            Time.timeScale = 0; yield return null;
            celestial.SetTimeOfDayForQa(.25f); celestial.EvaluatePresentationForQa();
            camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            report.frustumRenderers = renderers.Count(r => GeometryUtility.TestPlanesAABB(planes, r.bounds));
            Assert.That(report.frustumRenderers, Is.GreaterThan(0));
            Color32[] day = Capture("01-day-gameplay");
            foreach (var renderer in renderers) renderer.enabled = false;
            Color32[] absent = Capture("02-day-backdrop-disabled");
            RestoreRenderers();
            for (int i = 0; i < day.Length; i++)
                if (Math.Abs(day[i].r - absent[i].r) + Math.Abs(day[i].g - absent[i].g) + Math.Abs(day[i].b - absent[i].b) > 3) report.changedPixels++;
            Assert.That(report.changedPixels, Is.GreaterThan(128), "Backdrop must affect actual gameplay-camera pixels.");
            for (int turn = 1; turn <= 3; turn++)
            {
                camera.transform.rotation = Quaternion.AngleAxis(turn * 90, backdrop.stagingUp) * cameraRotation;
                celestial.EvaluatePresentationForQa(); Capture("03-day-sweep-" + turn * 90);
            }
            camera.transform.rotation = cameraRotation;
            celestial.SetTimeOfDayForQa(.75f); celestial.EvaluatePresentationForQa(); Capture("04-night-gameplay");
            yield return new WaitForEndOfFrame();
            ProductionCaptureResolution.SaveScreen(Folder+"/04-native1080-night-ui.png");
            Assert.That(Shader.GetGlobalFloat("_ElementalNight01"), Is.GreaterThan(.9f));
            Assert.That(RenderSettings.fog, Is.False, "Backdrop must not add legacy fog over the existing atmosphere.");
            Transform floating = backdrop.GetComponentsInChildren<Transform>(true).First(t => t.name.StartsWith("Island_", StringComparison.Ordinal));
            environmentWasPaused=backdrop.environmentPaused;
            if(backdrop.profile.proceduralValley)
            {
                // V2 spec explicitly separates menu environment time from gameplay timeScale.
                backdrop.environmentPaused=false;
                Assert.That(backdrop.animateWhenPaused,Is.True);
                Vector3 unscaledStart=floating.position;
                yield return new WaitForSecondsRealtime(.25f);
                report.unscaledMotionObserved=floating.position!=unscaledStart;
                Assert.That(report.unscaledMotionObserved,Is.True,"V2 menu levitation must advance with gameplay timeScale zero.");
                backdrop.environmentPaused=true;
            }
            yield return null; Vector3 paused = floating.position;
            yield return null; yield return null;
            report.pausedStable = floating.position == paused; Assert.That(report.pausedStable, Is.True);
            Capture("05-paused");
            backdrop.environmentPaused=false;
            SetPreference(true); yield return null; yield return null;
            Assert.That(backdrop.reducedMotion, Is.True);
            Vector3 still = floating.position; backdrop.ApplyTime(123456);
            report.reducedStable = floating.position == still; Assert.That(report.reducedStable, Is.True);
            Capture("06-reduced-motion");
            SetPreference(false); yield return null;
            for (int i = 0; i < 8; i++) backdrop.ApplyTime(500 + i * .016);
            long before = GC.GetAllocatedBytesForCurrentThread();
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int i = 0; i < 256; i++) backdrop.ApplyTime(510 + i * .016);
            long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - start;
            report.applyTimeManagedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            report.applyTimeMeanMilliseconds = elapsed * 1000d / System.Diagnostics.Stopwatch.Frequency / 256;
            report.motionSamples = 256;
            Assert.That(report.applyTimeManagedBytes, Is.Zero, "Only the warmed ApplyTime loop is measured; this is not whole-frame GC evidence.");
            File.WriteAllText(Folder + "/evidence.json", JsonUtility.ToJson(report, true));
        }
        [UnityTearDown] public IEnumerator Restore()
        {
            captureResolution?.Dispose();captureResolution=null;
            RestoreRenderers();
            if (flow != null) SetPreference(reduced);
            if (backdrop != null) { backdrop.SetReducedMotion(reduced);backdrop.environmentPaused=environmentWasPaused; }
            if (camera != null) camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            if (celestial != null)
            {
                celestial.SetLightingAuthorityForQa(lightingMode); celestial.SetTimeOfDayForQa(phase); celestial.EvaluatePresentationForQa();
            }
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            Time.timeScale = timeScale;
            if (previous.IsValid() && previous.isLoaded)
                foreach (var root in previous.GetRootGameObjects())
                    foreach (var system in root.GetComponentsInChildren<CelestialSystemBehaviour>(true))
                        if (system.isActiveAndEnabled) system.EvaluatePresentationForQa();
            if (report != null) { report.restored = true; File.WriteAllText(Folder + "/last-progress.json", JsonUtility.ToJson(report, true)); }
        }
        private void SetPreference(bool value)
        { var p = flow.Preferences; p.Set(p.MasterVolume, p.UIVolume, p.Sensitivity, value); }
        private void RestoreRenderers()
        {
            if (renderers == null || rendererStates == null) return;
            for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = rendererStates[i];
        }
        private T[] All<T>() where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        private Color32[] Capture(string name, string folder = Folder)
        {
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            RenderTexture active = RenderTexture.active;
            var data = camera.GetUniversalAdditionalCameraData(); bool dither = data.dithering;
            try
            {
                data.dithering = false;
                var request = new RenderPipeline.StandardRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); texture.Apply(false, false);
                File.WriteAllBytes(folder + "/" + name + ".png", texture.EncodeToPNG()); return texture.GetPixels32();
            }
            finally
            { data.dithering = dither; RenderTexture.active = active; Object.Destroy(texture); target.Release(); Object.Destroy(target); }
        }
    }
}
#endif
