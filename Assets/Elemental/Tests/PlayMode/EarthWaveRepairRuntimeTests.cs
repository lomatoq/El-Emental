using System.Collections;
using System.IO;
using System.Linq;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthWaveRepairRuntimeTests
    {
        [System.Serializable] private sealed class Report
        {
            public bool passed;
            public int waveCasts, crestCasts, visibleSamples;
            public float maximumPolygonSpan, maximumCrestMeshSpan;
            public double maximumScheduleMilliseconds;
            public string utc;
        }

        [UnityTest]
        public IEnumerator RepeatedProductionWavesAndCrestsNeverReuseOversizedGeometry()
        {
            const string path = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            var scene = SceneManager.GetSceneByPath(path);
            var report = new Report { utc = System.DateTime.UtcNow.ToString("O") };
            AsyncOperation unload = null;
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Elemental.Wave.Schedule", 32);
            try
            {
                foreach (var bot in All<EarthMvpBotController>(scene)) bot.enabled = false;
                yield return null;
                var pool = All<EarthPillarWavePool>(scene).First();
                var player = All<PlanetMotor>(scene).First(x => x.name == "Planet Character");
                var columns = pool.GetComponentsInChildren<EarthPillarWaveColumn>(true);
                var body = player.GetComponent<Rigidbody>();
                float limit = EarthWaveFootprintSolver.Radius(pool.Profile.Tuning.MaximumWidth) * 2f * 1.08f + .03f;
                float start = Time.time;
                bool captured = false;
                float cycle = pool.Profile.AnimationTiming.TotalDuration * (pool.Profile.Tuning.MaximumRows + 1) + 2f;
                while (Time.time - start < cycle * 13f && (report.waveCasts < 6 || report.crestCasts < 6 || pool.AvailableColumns < columns.Length))
                {
                    if (report.waveCasts < 6 && report.waveCasts == report.crestCasts && pool.AvailableColumns == columns.Length)
                    {
                        var surface = body.worldCenterOfMass - player.LocalUp * 1.25f;
                        Assert.That(pool.Launch(surface, player.LocalUp, player.FacingForward,
                            .95f, .8f, body), Is.GreaterThan(0));
                        report.waveCasts++;
                        // A busy pool must leave the live fracture intact.
                        Assert.That(pool.LaunchCrest(surface, player.LocalUp, player.FacingForward, 5, body), Is.Zero);
                    }
                    else if (report.crestCasts < report.waveCasts && pool.AvailableColumns == columns.Length)
                    {
                        var surface = body.worldCenterOfMass - player.LocalUp * 1.25f;
                        Assert.That(pool.LaunchCrest(surface, player.LocalUp, player.FacingForward, 5, body), Is.EqualTo(5));
                        report.crestCasts++;
                    }
                    foreach (var cell in columns)
                    {
                        if (!cell.TryGetVisiblePlacementDiagnostic(out var mesh, out var matrix,
                            out _, out _, out _, out bool polygon)) continue;
                        report.visibleSamples++;
                        float span = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z);
                        if (polygon)
                        {
                            span *= Mathf.Max(matrix.lossyScale.x, matrix.lossyScale.z);
                            report.maximumPolygonSpan = Mathf.Max(report.maximumPolygonSpan, span);
                            Assert.That(span, Is.LessThanOrEqualTo(limit), "Oversized polygon after a repeated cast");
                        }
                        else
                        {
                            report.maximumCrestMeshSpan = Mathf.Max(report.maximumCrestMeshSpan, span);
                            Assert.That(span, Is.LessThan(1.3f), "Crest reused a metre-sized fracture mesh instead of its unit mesh");
                        }
                    }
                    if (recorder.Valid) report.maximumScheduleMilliseconds = System.Math.Max(
                        report.maximumScheduleMilliseconds, recorder.LastValue / 1000000d);
                    if (!captured && Time.time - start > 3.6f)
                    {
                        Directory.CreateDirectory("BuildReports/WaveRepair");
                        ScreenCapture.CaptureScreenshot("BuildReports/WaveRepair/RepeatedWave.png");
                        captured = true;
                    }
                    yield return null;
                }
                Assert.That(report.waveCasts, Is.EqualTo(6));
                Assert.That(report.visibleSamples, Is.GreaterThan(100));
                Assert.That(report.maximumPolygonSpan, Is.GreaterThan(.2f));
                Assert.That(report.maximumCrestMeshSpan, Is.GreaterThan(.2f));
                report.passed = true;
            }
            finally
            {
                Directory.CreateDirectory("BuildReports/WaveRepair");
                File.WriteAllText("BuildReports/WaveRepair/Latest.json", JsonUtility.ToJson(report, true));
                if (scene.IsValid() && scene.isLoaded) unload = SceneManager.UnloadSceneAsync(scene);
            }
            if (unload != null) yield return unload;
        }
        private static T[] All<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true)).ToArray();
    }
}
