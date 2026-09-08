using System;
using System.Collections;
using System.IO;
using System.Linq;
using Elemental.Presentation.Camera;
using Elemental.Presentation.UI;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace Elemental.Tests.PlayMode
{
    public sealed class EarthSurfaceWindDustProductionTests
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const string Folder = "BuildReports/SurfaceWindDust";
        [Serializable] private sealed class Report
        {
            public string utc;
            public Vector3 anchor;
            public int particles, stoneCandidates, groundEmitted, stoneEmitted, changedPixels, movingSamples;
            public float meanTravelMetres;
            public double markerMeanMs, markerPeakMs;
            public bool markerValid;
        }
        [UnityTest] public IEnumerator SavedArenaHasPersistentSurfaceDriftAndDenserStoneWakes()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Saved-asset visual acceptance requires Editor Play Mode."); yield break;
#else
            Scene scene = default, previous = SceneManager.GetActiveScene();
            Directory.CreateDirectory(Folder); var report = new Report { utc = DateTime.UtcNow.ToString("O") };
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
                var director = new SerializedObject(flow).FindProperty("cameraDirector").objectReferenceValue as EarthCameraDirector;
                Assert.That(director, Is.Not.Null);
                var camera = director.GetComponent<UnityEngine.Camera>();
                var dust = All<EarthSurfaceWindDust>(scene).Single();
                Assert.That(dust.isActiveAndEnabled, Is.True);
                yield return new WaitForSeconds(4f);
                report.anchor = dust.ArenaAnchor.position; report.particles = dust.LiveParticles;
                report.stoneCandidates = dust.SettledStoneCandidates; report.groundEmitted = dust.GroundEmitted; report.stoneEmitted = dust.StoneEmitted;
                Assert.That(report.particles, Is.InRange(12, dust.Profile.maximumParticles));
                Assert.That(report.stoneCandidates, Is.GreaterThan(0));
                Assert.That(report.stoneEmitted, Is.GreaterThan(report.groundEmitted), "Settled stones must receive a denser persistent wind layer.");
                Assert.That(dust.GetComponentsInChildren<Rigidbody>().Length, Is.Zero);
                var before = new ParticleSystem.Particle[256]; int beforeCount = dust.System.GetParticles(before);
                Capture(camera, "01-surface-wind-drift");
                yield return new WaitForSeconds(.8f);
                var after = new ParticleSystem.Particle[256]; int afterCount = dust.System.GetParticles(after);
                for (int i = 0; i < afterCount; i++)
                    for (int j = 0; j < beforeCount; j++)
                        if (after[i].randomSeed == before[j].randomSeed && after[i].remainingLifetime < before[j].remainingLifetime)
                        { report.meanTravelMetres += Vector3.Distance(after[i].position,before[j].position); report.movingSamples++; break; }
                Assert.That(report.movingSamples, Is.GreaterThan(5));
                report.meanTravelMetres /= report.movingSamples;
                Assert.That(report.meanTravelMetres, Is.GreaterThan(.25f));
                Capture(camera, "02-surface-wind-after-drift");
                using (var cpu = ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"Elemental.VFX.SurfaceWindDust",64))
                {
                    for (int i = 0; i < 60; i++) { yield return null; double ms = cpu.LastValue / 1000000d; report.markerMeanMs += ms; report.markerPeakMs = Math.Max(report.markerPeakMs,ms); }
                    report.markerValid = cpu.Valid; report.markerMeanMs /= 60d;
                }
                var renderer = dust.System.GetComponent<ParticleSystemRenderer>();
                renderer.enabled = false;
                var off = Capture(camera,"03-surface-wind-off");
                renderer.enabled = true;
                var on = Capture(camera,"04-surface-wind-on");
                for (int i = 0; i < off.Length; i++) if (Math.Abs(off[i].r-on[i].r)+Math.Abs(off[i].g-on[i].g)+Math.Abs(off[i].b-on[i].b)>2) report.changedPixels++;
                Assert.That(report.changedPixels, Is.GreaterThan(128), "Wind dust must affect rendered production pixels.");
                File.WriteAllText(Folder + "/evidence.json",JsonUtility.ToJson(report,true));
                // Inspect the actual prepared wall partition in the saved arena,
                // before and after a real pluck. This is visual QA, not hit acceptance.
                var motor = All<PlanetMotor>(scene).First();
                Vector3 up = motor.transform.up;
                Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, up).normalized;
                Vector3 right = Vector3.Cross(up, forward).normalized;
                Vector3 foundation = motor.SupportFeetPoint(up) + forward + right * 4f;
                if (UnityEngine.Physics.Raycast(foundation + up * 3f, -up, out var groundHit, 8f,
                    motor.GroundMask, QueryTriggerInteraction.Ignore)) foundation = groundHit.point;
                Transform planet = new SerializedObject(dust).FindProperty("planet").objectReferenceValue as Transform;
                var wall = All<EarthWallPool>(scene).First().Acquire(foundation - right * 3f,
                    foundation + right * 3f, planet.position, 2.6f, .7f, supportNormal: up, foundationEmbed: .25f);
                Assert.That(wall, Is.Not.Null);
                Assert.That(wall.UsesBakedFracture, Is.True);
                while (!wall.IsEmergenceComplete) yield return null;
                Vector3 focus = foundation + up * 1.1f;
                Vector3 reviewPosition = focus - forward * 7f + right * 4f + up * 2f;
                Quaternion reviewRotation = Quaternion.LookRotation(focus - reviewPosition, up);
                camera.transform.SetPositionAndRotation(reviewPosition, reviewRotation);
                Capture(camera, "05-wall-sealed-intact");
                Assert.That(wall.TryPluckCell(focus - forward * .35f, out var piece), Is.True);
                piece.Body.position += -forward * 2f + right * 2f;
                piece.Body.linearVelocity = Vector3.zero;
                UnityEngine.Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                yield return null;
                camera.transform.SetPositionAndRotation(reviewPosition, reviewRotation);
                Capture(camera, "06-wall-interior-and-volume");
            }
            finally
            {
                File.WriteAllText(Folder + "/last-progress.json",JsonUtility.ToJson(report,true));
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            }
#endif
        }
        private static Color32[] Capture(UnityEngine.Camera camera,string name)
        {
            var target = new RenderTexture(1280,800,24,RenderTextureFormat.ARGB32);
            var image = new Texture2D(1280,800,TextureFormat.RGB24,false);
            var previous = RenderTexture.active;
            var data = camera.GetUniversalAdditionalCameraData(); bool dither = data.dithering;
            try
            {
                data.dithering = false;
                var request = new RenderPipeline.StandardRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera,request),Is.True);
                RenderPipeline.SubmitRenderRequest(camera,request);
                RenderTexture.active = target; image.ReadPixels(new Rect(0,0,1280,800),0,0); image.Apply(false,false);
                File.WriteAllBytes(Folder + "/" + name + ".png",image.EncodeToPNG()); return image.GetPixels32();
            }
            finally { data.dithering=dither; RenderTexture.active=previous; UnityEngine.Object.Destroy(image); target.Release(); UnityEngine.Object.Destroy(target); }
        }
        private static T[] All<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
