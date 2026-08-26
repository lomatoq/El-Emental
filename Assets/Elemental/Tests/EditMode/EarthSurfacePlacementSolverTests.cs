using System.Collections.Generic;
using Elemental.Runtime.Geometry;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthSurfacePlacementSolverTests
    {
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
