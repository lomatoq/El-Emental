using System.Collections;
using System.Linq;
using Elemental.Runtime.Matter;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthContainedFractureRuntimeTests
    {
        [System.Serializable]
        private struct SplitMeasurement
        {
            public uint seed;
            public int depth;
            public bool succeeded;
            public double milliseconds;
            public long managedBytes;
        }
        [System.Serializable]
        private sealed class PerformanceReport
        {
            public string utc;
            public string scope = "Unity Editor main thread; Stopwatch and GC.GetAllocatedBytesForCurrentThread around complete TryEmitBreak, including containment, canonical split, activation and feedback; assertions/report writing and source preparation/cooking excluded; preparation is measured separately. First sample can include first-use binding overhead. Not a player-build performance certification.";
            public int measuredCalls;
            public float minimumCollisionFill = 1f;
            public float minimumRenderFill = 1f;
            public double coldPreparationMilliseconds;
            public int preparedPlanCount;
            public int preparedNativeMeshCount;
            public double maximumMilliseconds;
            public long maximumManagedBytes;
            public SplitMeasurement[] samples = new SplitMeasurement[4];
        }

        [UnityTest]
        public IEnumerator RotatedThinConvexAndRecursiveChildrenNeverGrowOutsideTheirParent()
        {
            var host = new GameObject("Contained fracture test kernel");
            var poolHost = new GameObject("Contained fracture pool");
            poolHost.SetActive(false);
            var parent = new GameObject("Thin tetrahedron parent");
            var template = GameObject.CreatePrimitive(PrimitiveType.Cube);
            template.SetActive(false);
            var mesh = new Mesh { name = "Thin tetrahedron convex" };
            mesh.vertices = new[] { Vector3.zero, new Vector3(4,0,0), new Vector3(0,.4f,0), new Vector3(0,0,1) };
            mesh.triangles = new[] { 0,2,1, 0,1,3, 0,3,2, 1,2,3 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var collider = parent.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh; collider.convex = true;
            var body = parent.AddComponent<Rigidbody>();
            body.useGravity = false; body.mass = 96f;
            var identity = parent.AddComponent<EarthMatterIdentity>();
            var kernel = host.AddComponent<EarthMatterKernelBehaviour>();
            var pool = poolHost.AddComponent<EarthRockDebrisPool>();
            pool.Configure(16, null, template.GetComponent<MeshFilter>().sharedMesh, null, null);
            pool.ConfigureMatterKernel(kernel);
            poolHost.SetActive(true);
            var performance = new PerformanceReport();
            try
            {
                for (uint seed = 1; seed <= 3; seed++)
                {
                    parent.SetActive(true);
                    Assert.That(identity.ReleaseRetiredRepresentation(), Is.True);
                    parent.transform.SetPositionAndRotation(new Vector3(seed * 100, 1000, 30),
                        Quaternion.Euler(seed * 37, seed * 61, seed * 23));
                    parent.transform.localScale = new Vector3(.8f, 1.4f, .7f);
                    Physics.SyncTransforms();
                    long preparationStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    pool.PrepareFracture(collider, 3);
                    performance.coldPreparationMilliseconds += (System.Diagnostics.Stopwatch.GetTimestamp() - preparationStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    // Deliberately inflated legacy radius and remote impact: geometry must still
                    // come from the actual parent convex, never the nominal sphere or hit point.
                    var decision = EarthRockBreakPolicy.Resolve(6f, 96f, 10000f, false);
                    Assert.That(MeasureBreak(pool, Vector3.zero, 6f, 96f, seed, decision, 0, identity, performance),
                        Is.True, $"Initial seed {seed}: {pool.LastBreakRejection}");
                    var children = pool.GetComponentsInChildren<EarthRockDebris>(false).Where(x =>
                        x.MatterIdentity.TryRead(out var record) && record.Source.SourceStableId == seed).ToArray();
                    Assert.That(children.Length, Is.EqualTo(3));
                    Assert.That(children.Sum(x => x.EarthMass), Is.EqualTo(96f).Within(.001f));
                    AssertFill(children, mesh, parent.transform, performance);
                    foreach (var child in children)
                    {
                        foreach (var vertex in Vertices(child))
                        {
                            Vector3 local = parent.transform.InverseTransformPoint(vertex);
                            Assert.That(local.x, Is.GreaterThanOrEqualTo(-.0001f));
                            Assert.That(local.y, Is.GreaterThanOrEqualTo(-.0001f));
                            Assert.That(local.z, Is.GreaterThanOrEqualTo(-.0001f));
                            Assert.That(local.x / 4f + local.y / .4f + local.z, Is.LessThanOrEqualTo(1.0001f),
                                "Independent tetrahedron half-space check: a child escaped the true parent convex.");
                        }
                        child.Body.detectCollisions = false;
                    }
                    parent.SetActive(false);
                    if (seed != 1) continue;
                    var splitAgain = children[0];
                    var oldIds = children.Select(x => x.MatterIdentity.MatterId).ToArray();
                    var childCollider = splitAgain.GetComponent<Collider>();
                    splitAgain.Body.detectCollisions = true;
                    Physics.SyncTransforms();
                    long recursivePreparationStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    pool.PrepareFracture(childCollider, 3);
                    performance.coldPreparationMilliseconds += (System.Diagnostics.Stopwatch.GetTimestamp() - recursivePreparationStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    var secondDecision = EarthRockBreakPolicy.Resolve(3f, splitAgain.EarthMass, 10000f, false, 1);
                    Assert.That(MeasureBreak(pool, Vector3.one * -1000f, 3f, splitAgain.EarthMass,
                        444, secondDecision, 1, splitAgain.MatterIdentity, performance), Is.True,
                        $"Recursive seed {seed}: {pool.LastBreakRejection}");
                    var grandchildren = pool.GetComponentsInChildren<EarthRockDebris>(false).Where(x =>
                        !oldIds.Contains(x.MatterIdentity.MatterId)).ToArray();
                    Assert.That(grandchildren.Length, Is.EqualTo(3));
                    AssertFill(grandchildren, ((MeshCollider)childCollider).sharedMesh, splitAgain.transform, performance);
                    foreach (var child in grandchildren)
                    {
                        foreach (var vertex in Vertices(child))
                            Assert.That(Vector3.Distance(childCollider.ClosestPoint(vertex), vertex), Is.LessThan(.0001f));
                        child.Body.detectCollisions = false;
                    }
                    Assert.That(grandchildren.Sum(x => x.EarthMass), Is.EqualTo(splitAgain.EarthMass).Within(.001f));
                    splitAgain.gameObject.SetActive(false);
                }
            }
            finally
            {
                performance.utc = System.DateTime.UtcNow.ToString("O");
                performance.preparedPlanCount = pool.PreparedFracturePlanCount;
                performance.preparedNativeMeshCount = pool.PreparedFractureMeshCount;
                System.IO.Directory.CreateDirectory("BuildReports");
                System.IO.File.WriteAllText("BuildReports/ContainedFracturePerformance.json", JsonUtility.ToJson(performance, true));
                Debug.Log($"[ContainedFracture] Editor full split calls={performance.measuredCalls}, maximum={performance.maximumMilliseconds:F4} ms, maximum managed allocation={performance.maximumManagedBytes} bytes. See BuildReports/ContainedFracturePerformance.json.");
                Object.Destroy(parent); Object.Destroy(template); Object.Destroy(poolHost);
                Object.Destroy(host); Object.Destroy(mesh);
            }
            yield return null;
        }

        private static bool MeasureBreak(EarthRockDebrisPool pool, Vector3 point, float radius, float mass,
            uint seed, EarthRockBreakDecision decision, int depth, EarthMatterIdentity identity, PerformanceReport report)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            bool succeeded = pool.TryEmitBreak(point, Vector3.up, Vector3.zero, radius, mass, seed, decision, depth, identity);
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            double elapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            report.samples[report.measuredCalls++] = new SplitMeasurement
                { seed = seed, depth = depth, succeeded = succeeded, milliseconds = elapsed, managedBytes = allocated };
            report.maximumMilliseconds = System.Math.Max(report.maximumMilliseconds, elapsed);
            report.maximumManagedBytes = System.Math.Max(report.maximumManagedBytes, allocated);
            return succeeded;
        }

        private static void AssertFill(EarthRockDebris[] children, Mesh source, Transform parent, PerformanceReport report)
        {
            float parentVolume=Volume(source)*Mathf.Abs(parent.localToWorldMatrix.determinant);
            float collision=0,render=0;
            foreach(var child in children)
            {
                float determinant=Mathf.Abs(child.transform.localToWorldMatrix.determinant);
                collision+=Volume(child.GetComponent<MeshCollider>().sharedMesh)*determinant;
                render+=Volume(child.GetComponent<MeshFilter>().sharedMesh)*determinant;
            }
            report.minimumCollisionFill=Mathf.Min(report.minimumCollisionFill,collision/parentVolume);
            report.minimumRenderFill=Mathf.Min(report.minimumRenderFill,render/parentVolume);
            Assert.That(collision/parentVolume,Is.InRange(.95f,1.001f),"Child colliders must fill the original convex, not sparsely inscribe unrelated templates.");
            Assert.That(render/parentVolume,Is.InRange(.85f,1.001f),"Visible bevels must retain most of the parent volume.");
        }
        private static float Volume(Mesh mesh) => EarthConvexPartitionSolver.MeshVolume(
            mesh.vertices.Select(v=>(Unity.Mathematics.float3)v).ToArray(),mesh.triangles);

        private static Vector3[] Vertices(EarthRockDebris child) =>
            child.GetComponent<MeshFilter>().sharedMesh.vertices
                .Concat(child.GetComponent<MeshCollider>().sharedMesh.vertices)
                .Select(child.transform.TransformPoint).ToArray();
    }
}
