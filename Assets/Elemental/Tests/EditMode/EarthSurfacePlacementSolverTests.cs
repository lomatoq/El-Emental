using System.Collections.Generic;
using Elemental.Runtime.Geometry;
using Elemental.Runtime.Physics;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthSurfacePlacementSolverTests
    {
        [TestCase(0f, 1f, 0f)]
        [TestCase(0.7f, 0.5f, -0.3f)]
        [TestCase(0f, -1f, 0f)]
        public void WaveFoundationLowersTwentyPercentAlongArenaOrPlanetUp(float x, float y, float z)
        {
            Mesh mesh = EarthWebWaveCellMeshFactory.Create(4);
            try
            {
                Vector3 up = new Vector3(x, y, z).normalized;
                Vector3 surface = up * 18f;
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, up);
                Vector3 scale = new Vector3(0.8f, 2f, 0.7f);
                var original = EarthPillarWaveColumn.ResolveFullRisePlacement(mesh, surface, up, rotation, scale);
                var buried = EarthPillarWaveColumn.ResolveFullRisePlacement(mesh, surface, up, rotation, scale, 0.20f);
                float depth = mesh.bounds.size.y * scale.y * 0.20f;
                Assert.That(buried.IsValid, Is.True);
                Assert.That(Vector3.Distance(buried.RootPosition, original.RootPosition - up * depth), Is.LessThan(0.0001f));
                float measured = EarthSurfacePlacementSolver.MeasureSupportError(mesh, buried.RootPosition, surface, up, rotation, scale);
                Assert.That(measured, Is.EqualTo(-0.01f - depth).Within(0.0001f));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void PolygonAndLegacyPillarsUseExactOneCentimetreFullRiseSeat()
        {
            var polygon = new Mesh { name = "Pillar Support Polygon Test" };
            Mesh legacy = null;
            try
            {
                var footprint = new[]
                {
                    new float2(-0.65f, -0.45f),
                    new float2(0.55f, -0.52f),
                    new float2(0.72f, 0.30f),
                    new float2(-0.42f, 0.61f)
                };
                EarthWebWaveCellMeshFactory.ConfigureSharedBoundaryCell(
                    polygon,
                    footprint,
                    0xA511u,
                    1.21f);
                legacy = EarthWebWaveCellMeshFactory.Create(4);
                Vector3 up = new Vector3(0.18f, 0.96f, -0.21f).normalized;
                Vector3 surface = up * 18f;
                Quaternion rotation = Quaternion.LookRotation(
                    Vector3.ProjectOnPlane(Vector3.forward, up).normalized,
                    up) * Quaternion.Euler(2.5f, 17f, 1.3f);

                EarthSurfacePlacementResult polygonSeat =
                    EarthPillarWaveColumn.ResolveFullRisePlacement(
                        polygon, surface, up, rotation, Vector3.one);
                EarthSurfacePlacementResult legacySeat =
                    EarthPillarWaveColumn.ResolveFullRisePlacement(
                        legacy,
                        surface,
                        up,
                        rotation,
                        new Vector3(0.75f, 1.2f, 0.58f));

                Assert.That(polygonSeat.IsValid, Is.True);
                Assert.That(legacySeat.IsValid, Is.True);
                Assert.That(polygonSeat.SupportError, Is.EqualTo(-0.01f).Within(0.0001f));
                Assert.That(legacySeat.SupportError, Is.EqualTo(-0.01f).Within(0.0001f));
                Assert.That(polygonSeat.SupportError, Is.InRange(-0.0101f, 0.015f));
                Assert.That(legacySeat.SupportError, Is.InRange(-0.0101f, 0.015f));
            }
            finally
            {
                Object.DestroyImmediate(polygon);
                if (legacy != null) Object.DestroyImmediate(legacy);
            }
        }

        [Test]
        public void FiveHundredTransformedRocksTouchTheirCanonicalSupportPlane()
        {
            var meshes = new List<Mesh>();
            try
            {
                for (int family = 0; family < EarthRockMeshFactory.ArchetypeCount; family++)
                {
                    Mesh mesh = EarthRockMeshFactory.Create(
                        (EarthRockArchetype)family,
                        0xA511u + (uint)family * 977u);
                    meshes.Add(mesh);
                }

                for (int index = 0; index < 504; index++)
                {
                    Mesh mesh = meshes[index % meshes.Count];
                    Vector3 normal = new Vector3(
                        Mathf.Sin(index * 0.73f),
                        0.35f + Mathf.Repeat(index * 0.137f, 0.65f),
                        Mathf.Cos(index * 0.51f)).normalized;
                    Quaternion rotation = Quaternion.LookRotation(
                        Vector3.ProjectOnPlane(Vector3.forward + Vector3.right * 0.17f, normal).normalized,
                        normal) * Quaternion.Euler(0f, Mathf.Repeat(index * 47f, 360f), 0f);
                    Vector3 scale = new Vector3(
                        Mathf.Lerp(0.35f, 2.4f, Mathf.Repeat(index * 0.173f, 1f)),
                        Mathf.Lerp(0.25f, 2.1f, Mathf.Repeat(index * 0.217f, 1f)),
                        Mathf.Lerp(0.4f, 2.7f, Mathf.Repeat(index * 0.311f, 1f)));
                    Vector3 surface = normal * 18f;
                    float embed = Mathf.Lerp(0.02f, 0.05f, Mathf.Repeat(index * 0.117f, 1f));

                    EarthSurfacePlacementResult result = EarthSurfacePlacementSolver.Solve(
                        mesh,
                        surface,
                        normal,
                        rotation,
                        scale,
                        embed);

                    Assert.That(result.IsValid, Is.True, $"placement {index}");
                    float measured = EarthSurfacePlacementSolver.MeasureSupportError(
                        mesh,
                        result.RootPosition,
                        surface,
                        normal,
                        rotation,
                        scale);
                    Assert.That(measured, Is.InRange(-0.06f, 0.015f), $"placement {index}");
                    Assert.That(measured, Is.EqualTo(-embed).Within(0.0001f), $"placement {index}");
                }
            }
            finally
            {
                for (int index = 0; index < meshes.Count; index++)
                    Object.DestroyImmediate(meshes[index]);
            }
        }
    }
}
