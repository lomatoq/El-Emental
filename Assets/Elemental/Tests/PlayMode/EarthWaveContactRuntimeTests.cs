using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public class EarthWaveContactRuntimeTests
    {
        [System.Serializable] private class Report
        {
            public bool passed;
            public int immutableChecks, contactEvents, burstEvents, contactSources, extractionDust, extractionChips, matchedColliders;
            public double maximumContactMilliseconds;
            public float maximumContactPlaneError;
            public float maximumProjectedVertexDrift;
            public int projectedVertexChecks;
            public float travelSeconds, effectiveSpeed;
            public float crestArrivalSpan, maximumCrestPhaseError;
            public float maximumRenderSampleInterval;
            public int maximumSimultaneousRisingCells;
            public int descendingChecks, partialReuseRejections;
            public int collisionPairChecks;
            public float maximumCellPenetration;
        }

        [UnityTest]
        public IEnumerator LongWaveKeepsItsMeshesAndEmitsOnSurfaceThroughRiseAndRetreat()
        {
            const string path = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(path);
            var report = new Report();
            AsyncOperation unload = null;
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Elemental.Wave.SurfaceContact", 64);
            try
            {
                foreach (var bot in All<EarthMvpBotController>(scene)) bot.enabled = false;
                yield return null;
                var pool = All<EarthPillarWavePool>(scene).First();
                var player = All<PlanetMotor>(scene).First(x => x.name == "Planet Character");
                var body = player.GetComponent<Rigidbody>();
                var hub = All<EarthMaterialFeedbackHub>(scene).First();
                var columns = pool.GetComponentsInChildren<EarthPillarWaveColumn>(true);
                var sources = new HashSet<uint>();
                float start = Time.time, lastBurst = 0;
                hub.Presented += cue =>
                {
                    if (cue.Kind == EarthMaterialFeedbackKind.ExtractionSurfaceContact)
                    { report.extractionDust += cue.DustCount; report.extractionChips += cue.ChipCount; return; }
                    if (cue.Kind != EarthMaterialFeedbackKind.WaveSurfaceContact && cue.Kind != EarthMaterialFeedbackKind.WaveSurfaceBurst) return;
                    sources.Add(cue.SourceId);
                    if (cue.Kind == EarthMaterialFeedbackKind.WaveSurfaceContact) report.contactEvents++;
                    else { report.burstEvents++; lastBurst = Time.time - start; }
                    var column = columns.FirstOrDefault(x => x.StableEarthId == cue.SourceId);
                    if (column != null && column.TryGetVisiblePlacementDiagnostic(out _,out _,out var surface,out var up,out _,out _))
                        report.maximumContactPlaneError = Mathf.Max(report.maximumContactPlaneError,
                            Mathf.Abs(Vector3.Dot((Vector3)cue.Point - surface, up)));
                };
                Vector3 origin = body.worldCenterOfMass - player.LocalUp * 1.25f;
                int launched = pool.Launch(origin,player.LocalUp,player.FacingForward,.95f,.8f,body);
                Assert.That(launched, Is.GreaterThan(60));
                var ids = columns.Select(x => x.CastGeneration).ToArray();
                var meshes = columns.Select(x => x.GetComponent<MeshFilter>().sharedMesh).ToArray();
                var shapes = meshes.Select(x => x.vertices).ToArray();
                var projected = new Vector3[columns.Length][];
                var previousHeights = new float[columns.Length];
                Assert.That(pool.Launch(origin,player.LocalUp,player.FacingForward,.95f,.8f,body), Is.Zero);
                Assert.That(pool.LaunchCrest(origin,player.LocalUp,player.FacingForward,5,body), Is.Zero);
                float duration = pool.LastWaveDuration + 2f;
                report.travelSeconds = pool.LastWaveTravelSeconds;
                report.effectiveSpeed = pool.LastWaveEffectiveSpeed;
                var crestArrival = Enumerable.Repeat(-1f, columns.Length).ToArray();
                float minimumCrestPhase = float.MaxValue, maximumCrestPhase = float.MinValue;
                float previousSampleTime = start;
                bool capturedRise = false, capturedFall = false;
                float nextCheck = start;
                while (Time.time - start < duration)
                {
                    report.maximumRenderSampleInterval = Mathf.Max(report.maximumRenderSampleInterval, Time.time - previousSampleTime);
                    previousSampleTime = Time.time;
                    if (Time.time >= nextCheck)
                    {
                        for (int i = 0; i < columns.Length; i++)
                        {
                            Assert.That(columns[i].CastGeneration, Is.EqualTo(ids[i]));
                            Assert.That(columns[i].GetComponent<MeshFilter>().sharedMesh, Is.SameAs(meshes[i]));
                            CollectionAssert.AreEqual(shapes[i], meshes[i].vertices);
                            report.immutableChecks++;
                            // Mesh identity alone misses rotating/scaling fragments. Compare
                            // the actual rendered vertices projected onto their ground plane.
                            if (columns[i].TryGetVisiblePlacementDiagnostic(out var renderedMesh, out var pose,
                                out var ground, out var normal, out float currentHeight, out bool polygon) && polygon)
                            {
                                Assert.That(renderedMesh, Is.SameAs(meshes[i]), "Visible proxy substituted another fracture mesh.");
                                if (currentHeight < previousHeights[i] - .01f) report.descendingChecks++;
                                previousHeights[i] = currentHeight;
                                bool first = projected[i] == null;
                                if (first) projected[i] = new Vector3[shapes[i].Length];
                                for (int v = 0; v < shapes[i].Length; v++)
                                {
                                    Vector3 point = Vector3.ProjectOnPlane(pose.MultiplyPoint3x4(shapes[i][v]) - ground, normal);
                                    if (first) projected[i][v] = point;
                                    else report.maximumProjectedVertexDrift = Mathf.Max(report.maximumProjectedVertexDrift,
                                        Vector3.Distance(point, projected[i][v]));
                                    report.projectedVertexChecks++;
                                }
                                if (currentHeight > .1f)
                                    for (int j = 0; j < i; j++)
                                    {
                                        if (!columns[j].TryGetVisiblePlacementDiagnostic(out _, out var otherPose,
                                            out _, out _, out float otherHeight, out _) || otherHeight <= .1f) continue;
                                        report.collisionPairChecks++;
                                        if (UnityEngine.Physics.ComputePenetration(columns[i].GetComponent<MeshCollider>(),
                                            pose.GetPosition(), pose.rotation, columns[j].GetComponent<MeshCollider>(),
                                            otherPose.GetPosition(), otherPose.rotation, out _, out float penetration))
                                            report.maximumCellPenetration = Mathf.Max(report.maximumCellPenetration, penetration);
                                    }
                            }
                        }
                        nextCheck += .4f;
                    }
                    if (pool.AvailableColumns > 0 && pool.AvailableColumns < columns.Length && report.partialReuseRejections == 0)
                    {
                        Assert.That(pool.LaunchCrest(origin, player.LocalUp, player.FacingForward, 1, body), Is.Zero,
                            "A new crest occupied an early-retired cell while its original wave was still running.");
                        Assert.That(pool.Launch(origin, player.LocalUp, player.FacingForward, 0f, .1f, body), Is.Zero);
                        report.partialReuseRejections++;
                    }
                    int risingCells = 0;
                    for (int i = 0; i < columns.Length; i++)
                    {
                        if (!columns[i].TryGetVisiblePlacementDiagnostic(out _, out _, out _, out _, out float height, out _)) continue;
                        if (crestArrival[i] < 0f && height > .05f && height < 1f) risingCells++;
                        if (crestArrival[i] >= 0f || height < 1f) continue;
                        crestArrival[i] = Time.time - start;
                        float phase = crestArrival[i] - columns[i].ScheduledDelay;
                        minimumCrestPhase = Mathf.Min(minimumCrestPhase, phase);
                        maximumCrestPhase = Mathf.Max(maximumCrestPhase, phase);
                    }
                    report.maximumSimultaneousRisingCells = Mathf.Max(report.maximumSimultaneousRisingCells, risingCells);
                    if (recorder.Valid) report.maximumContactMilliseconds = System.Math.Max(report.maximumContactMilliseconds, recorder.LastValue / 1000000d);
                    if (!capturedRise && Time.time - start > pool.Profile.AnimationTiming.Rise * .7f)
                    { Directory.CreateDirectory("BuildReports/WaveContact"); ScreenCapture.CaptureScreenshot("BuildReports/WaveContact/Rise.png"); capturedRise = true; }
                    if (!capturedFall && Time.time - start > pool.Profile.AnimationTiming.Duration * .8f + pool.LastWaveTravelSeconds * .5f)
                    { ScreenCapture.CaptureScreenshot("BuildReports/WaveContact/Retreat.png"); capturedFall = true; }
                    yield return new WaitForEndOfFrame();
                }
                Assert.That(pool.AvailableColumns, Is.EqualTo(columns.Length));
                Assert.That(report.collisionPairChecks, Is.GreaterThan(100));
                Assert.That(report.maximumCellPenetration, Is.LessThan(.003f), "Neighbouring animated wave volumes pass through each other.");
                Assert.That(report.partialReuseRejections, Is.GreaterThan(0));
                Assert.That(report.descendingChecks, Is.GreaterThan(50));
                Assert.That(crestArrival.Count(x => x >= 0f), Is.EqualTo(launched));
                report.crestArrivalSpan = crestArrival.Max() - crestArrival.Where(x => x >= 0f).Min();
                report.maximumCrestPhaseError = maximumCrestPhase - minimumCrestPhase;
                Assert.That(report.effectiveSpeed, Is.EqualTo(pool.Profile.Tuning.WaveSpeed).Within(.0001f));
                Assert.That(report.maximumSimultaneousRisingCells, Is.GreaterThan(launched / 2), "Rows wait for their neighbours instead of sharing a travelling pulse.");
                // Threshold arrivals are quantized by rendered frames; the expensive
                // pairwise Editor audit may take longer than 150 ms per frame.
                Assert.That(report.crestArrivalSpan, Is.EqualTo(report.travelSeconds).Within(report.maximumRenderSampleInterval + .02f));
                Assert.That(report.maximumCrestPhaseError, Is.LessThan(report.maximumRenderSampleInterval + .02f));
                Assert.That(report.projectedVertexChecks, Is.GreaterThan(1000));
                Assert.That(report.maximumProjectedVertexDrift, Is.LessThan(pool.Profile.TremorDistance * 2f + .0001f),
                    "Rendered fracture boundaries slide across the ground although the mesh itself is unchanged.");
                Assert.That(report.contactEvents, Is.GreaterThan(100)); Assert.That(report.burstEvents, Is.GreaterThan(20));
                Assert.That(lastBurst, Is.GreaterThan(pool.Profile.AnimationTiming.Rise + pool.Profile.AnimationTiming.Settle));
                Assert.That(sources.Count, Is.GreaterThan(launched * .8f));
                Assert.That(report.maximumContactPlaneError, Is.LessThan(.001f));
                report.contactSources = sources.Count;

                // Extracted-rock rim feedback continues while the body intersects ground.
                var fragments = All<EarthFragmentPool>(scene).First();
                var fragment = fragments.Acquire(null,origin + player.LocalUp * .08f,.6f,100f);
                Assert.That(fragment, Is.Not.Null);
                fragment.Body.isKinematic = true;
                fragment.BeginSurfaceEmergence(null,origin,player.LocalUp,.6f);
                yield return new WaitForSeconds(.4f);
                Assert.That(report.extractionDust, Is.GreaterThan(200)); Assert.That(report.extractionChips, Is.GreaterThan(40));

                foreach (var piece in All<EarthWallPiece>(scene))
                {
                    var filter = piece.GetComponent<MeshFilter>(); var collider = piece.GetComponent<MeshCollider>();
                    if (filter?.sharedMesh == null || !filter.sharedMesh.name.Contains("Natural Fracture")) continue;
                    Assert.That(collider.sharedMesh.triangles.Length / 3, Is.LessThan(255));
                    Assert.That(Vector3.Distance(collider.sharedMesh.bounds.center,filter.sharedMesh.bounds.center), Is.LessThan(.0001f));
                    Assert.That(Vector3.Distance(collider.sharedMesh.bounds.size,filter.sharedMesh.bounds.size), Is.LessThan(.0001f), "Wall stone kept its larger old cell collider.");
                    report.matchedColliders++;
                }
                Assert.That(report.matchedColliders, Is.GreaterThan(1)); report.passed = true;
            }
            finally
            {
                Directory.CreateDirectory("BuildReports/WaveContact");
                File.WriteAllText("BuildReports/WaveContact/Latest.json",JsonUtility.ToJson(report,true));
                if (scene.IsValid() && scene.isLoaded) unload = SceneManager.UnloadSceneAsync(scene);
            }
            if (unload != null) yield return unload;
        }
        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true)).ToArray();
    }
}
