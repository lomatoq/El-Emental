using System.Collections.Generic;
using Elemental.Simulation.Bending;
using Elemental.Runtime.World;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public class EarthWaveContactTests
    {
        [Test]
        public void HeadSeamStonesCoverTwoStaggeredRings()
        {
            var directions = new HashSet<float3>();
            for (int i = 0; i < EarthArmorHeadShell.FillerCount; i++)
            {
                float3 direction = EarthArmorHeadShell.FillerDirection(i);
                Assert.That(math.length(direction), Is.EqualTo(1f).Within(.00001f));
                Assert.That(direction.y, i < 8 ? Is.GreaterThan(.8f) : Is.InRange(0f,.1f));
                Assert.That(directions.Add(direction), Is.True);
            }
        }

        [Test]
        public void HeadPlatePlanesEncloseMeasuredOffCentreHeadInsteadOfNeckRadius()
        {
            float3 center = new float3(2, 3.4f, -1);
            var points = new float3[8];
            for (int i = 0; i < 8; i++)
                points[i] = center + new float3((i & 1) == 0 ? -.29f : .29f,
                    (i & 2) == 0 ? -.37f : .37f, (i & 4) == 0 ? -.32f : .32f);
            foreach (var direction in new[] {new float3(0,1,0),new float3(0,0,1),
                new float3(1,0,0),new float3(-1,0,0),new float3(0,0,-1),new float3(1,1,1)})
            {
                var normal = math.normalize(direction);
                var point = EarthArmorHeadShell.SurfacePoint(points, center, direction);
                foreach (var vertex in points)
                    Assert.That(math.dot(vertex - point, normal), Is.LessThan(.00001f));
                Assert.That(math.distance(point, center), Is.GreaterThan(.28f));
            }
        }

        [Test]
        public void WavePlacementNeverRecentresToAChangingLowestVertex()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                Mesh mesh = cube.GetComponent<MeshFilter>().sharedMesh;
                Vector3 up = new Vector3(.2f, 1f, -.3f).normalized;
                Vector3 surface = new Vector3(200, 60, -130);
                foreach (float tilt in new[] {-12f, -1f, 0f, 1f, 12f})
                {
                    Quaternion rotation = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0, 0, tilt);
                    var placement = Elemental.Runtime.Physics.EarthPillarWaveColumn.ResolveFullRisePlacement(
                        mesh, surface, up, rotation, Vector3.one, .061f);
                    Assert.That(Vector3.ProjectOnPlane(placement.RootPosition - surface, up).magnitude, Is.LessThan(.00003f),
                        "A changing support corner moved the entire fracture into a neighbour.");
                    float error = Elemental.Runtime.Geometry.EarthSurfacePlacementSolver.MeasureSupportError(
                        mesh, placement.RootPosition, surface, up, rotation, Vector3.one);
                    Assert.That(error, Is.EqualTo(-.071f).Within(.00003f));
                }
            }
            finally { Object.DestroyImmediate(cube); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AcuteWaveBevelStaysInsideItsAssignedFootprint(bool reversed)
        {
            var source = new Mesh(); Mesh render = null;
            var footprint = new[] {new float2(-.05f,-1f),new float2(.05f,-1f),new float2(0,2f)};
            if (reversed) System.Array.Reverse(footprint);
            try
            {
                Elemental.Runtime.Physics.EarthWebWaveCellMeshFactory.ConfigureSharedBoundaryCell(source, footprint, 7, 1.5f, true);
                render = Elemental.Runtime.Geometry.EarthFractureBevelMeshBuilder.Create(source, .08f, .2f);
                Elemental.Runtime.Physics.EarthWebWaveCellMeshFactory.ContainRenderFootprint(render, footprint);
                var vertices = source.vertices; var triangles = source.triangles;
                foreach (var point in render.vertices)
                    for (int t = 0; t < triangles.Length; t += 3)
                    {
                        Vector3 a = vertices[triangles[t]];
                        Vector3 normal = Vector3.Cross(vertices[triangles[t+1]] - a, vertices[triangles[t+2]] - a).normalized;
                        if (Mathf.Abs(normal.y) < .0001f)
                            Assert.That(Vector3.Dot(point - a, normal), Is.LessThan(.0001f));
                    }
            }
            finally { if (render != null && render != source) Object.DestroyImmediate(render); Object.DestroyImmediate(source); }
        }

        [TestCase(.36f, .14f, .1f, .46f)]
        [TestCase(3f, 1f, .5f, 3f)]
        public void TravellingPulseOverlapsRisingRowsWithoutChangingAuthoredSpeed(float rise, float settle, float hold, float retreat)
        {
            var timing = new EarthWaveAnimationTiming(.055f, rise, settle, hold, retreat);
            var travel = new EarthWaveTravelSchedule(2f, 9f, 6.04f, in timing);
            for (int row = 1; row < 8; row++)
            {
                float time = travel.Delay(2f + row) + .05f;
                Assert.That(timing.Locate(time - travel.Delay(2f + row), out float nextProgress), Is.EqualTo(1));
                Assert.That(timing.Locate(time - travel.Delay(1f + row), out float previousProgress), Is.EqualTo(1));
                Assert.That(previousProgress, Is.GreaterThan(nextProgress));
                Assert.That(travel.Delay(2f + row), Is.EqualTo(row / 6.04f).Within(.00001f));
            }
            Assert.That(travel.EffectiveSpeed, Is.EqualTo(6.04f).Within(.00001f));
            Assert.That(travel.Duration, Is.EqualTo(travel.Delay(9f) + timing.TotalDuration).Within(.0001f));
        }

        [Test]
        public void ComplexSmallStoneColliderKeepsExtremaBelowPhysXFaceLimit()
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh result = null;
            try
            {
                Mesh source = sphere.GetComponent<MeshFilter>().sharedMesh;
                result = Elemental.Runtime.Geometry.EarthStoneColliderMesh.Create(source);
                Assert.That(result.triangles.Length / 3, Is.LessThanOrEqualTo(124));
                Assert.That(Vector3.Distance(result.bounds.size,source.bounds.size), Is.LessThan(.0001f));
                Assert.That(Vector3.Distance(result.bounds.center,source.bounds.center), Is.LessThan(.0001f));
            }
            finally { Object.DestroyImmediate(sphere); if (result != null) Object.DestroyImmediate(result); }
        }

        [Test]
        public void WaveFractureOutlineIsTheSameAtDifferentEmergenceDepths()
        {
            var mesh = new Mesh();
            try
            {
                Elemental.Runtime.Physics.EarthWebWaveCellMeshFactory.ConfigureSharedBoundaryCell(mesh,
                    new[] {new float2(-1,-1),new float2(1,-1),new float2(1,1),new float2(-1,1)},7,2f,true);
                var vertices = System.Array.ConvertAll(mesh.vertices, x => (float3)x);
                var points = new float3[64];
                foreach (float depth in new[] {.15f,.45f,1f,1.5f,1.85f})
                {
                    int count = EarthWaveSurfaceContactSolver.Slice(vertices,mesh.triangles,new float4(0,1,0,depth),points,out _,out _);
                    float2 min = new float2(10), max = new float2(-10);
                    for (int i = 0; i < count; i++) { min = math.min(min,points[i].xz); max = math.max(max,points[i].xz); }
                    Assert.That(math.cmax(math.abs(min + new float2(.992f))), Is.LessThan(.0001f));
                    Assert.That(math.cmax(math.abs(max - new float2(.992f))), Is.LessThan(.0001f));
                }
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void TiltedContactSectionStaysOnTheActualSurfaceAndNeverAppearsInAir()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var mesh = cube.GetComponent<MeshFilter>().sharedMesh;
                var vertices = System.Array.ConvertAll(mesh.vertices, x => (float3)x);
                var points = new float3[64];
                var plane = new float4(.2f, 1f, .3f, .1f);
                int count = EarthWaveSurfaceContactSolver.Slice(vertices, mesh.triangles, plane, points, out float min, out float max);
                Assert.That(count, Is.GreaterThanOrEqualTo(4)); Assert.That(min, Is.LessThan(0)); Assert.That(max, Is.GreaterThan(0));
                for (int i = 0; i < count; i++)
                {
                    Assert.That(math.abs(math.dot(plane.xyz, points[i]) + plane.w), Is.LessThan(.00001f));
                    Assert.That(math.cmax(math.abs(points[i])), Is.LessThanOrEqualTo(.50001f));
                }
                Assert.That(EarthWaveSurfaceContactSolver.Slice(vertices, mesh.triangles, new float4(0,1,0,2), points, out _, out _), Is.Zero);
            }
            finally { Object.DestroyImmediate(cube); }
        }

        [Test]
        public void NinetySixNearbyWaveCellsRetainTheirContactsWithinTheSharedBudget()
        {
            var host = new GameObject("Wave contact budget");
            try
            {
                var hub = host.AddComponent<EarthMaterialFeedbackHub>();
                var sources = new HashSet<uint>(); int dust = 0, chips = 0;
                hub.Presented += c => { sources.Add(c.SourceId); dust += c.DustCount; chips += c.ChipCount; };
                for (uint i = 1; i <= 96; i++) hub.Emit(EarthMaterialFeedbackKind.WaveSurfaceContact,
                    new Vector3(i * .005f,0,0), Vector3.up,1,.1f,i,1,20,5);
                hub.FlushPending();
                Assert.That(sources.Count, Is.EqualTo(96)); Assert.That(dust, Is.LessThanOrEqualTo(256));
                Assert.That(chips, Is.LessThanOrEqualTo(64)); Assert.That(hub.CoalescedEvents, Is.Zero);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void TinyStoneClusterUsesGeometrySizedOrbit()
        {
            float tiny = EarthGravityGripSolver.CompactOrbitRadius(1.35f, 8 * .1f * .1f * .1f);
            float large = EarthGravityGripSolver.CompactOrbitRadius(1.35f, 8f);
            Assert.That(tiny, Is.EqualTo(.24f).Within(.0001f));
            Assert.That(large, Is.EqualTo(1.35f));
        }
    }
}
