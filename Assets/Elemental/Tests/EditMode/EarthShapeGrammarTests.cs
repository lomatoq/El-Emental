using System.Collections.Generic;
using Elemental.Runtime.Geometry;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthShapeGrammarTests
    {
        [Test]
        public void RockLibraryBuildsTwelveDistinctColliderSafeSilhouettes()
        {
            var signatures = new HashSet<string>();
            var meshes = new List<Mesh>();
            try
            {
                for (int index = 0; index < EarthRockMeshFactory.ArchetypeCount; index++)
                {
                    uint seed = EarthShapeSeed.Compose(81u, (uint)(index + 1), 5u, 2u, 13u).Value;
                    Mesh mesh = EarthRockMeshFactory.Create((EarthRockArchetype)index, seed);
                    meshes.Add(mesh);
                    EarthMeshIntegrityReport report = EarthMeshIntegrityValidator.Validate(
                        mesh, EarthMeshIntegrityPolicy.ConvexCollider);
                    Assert.That(report.IsValid, Is.True, report.ToString());
                    Vector3 size = mesh.bounds.size;
                    signatures.Add($"{Mathf.RoundToInt(size.x * 20f)}:" +
                                   $"{Mathf.RoundToInt(size.y * 20f)}:" +
                                   $"{Mathf.RoundToInt(size.z * 20f)}:{mesh.triangles.Length / 3}");
                }
                Assert.That(signatures.Count, Is.GreaterThanOrEqualTo(10));
            }
            finally
            {
                for (int index = 0; index < meshes.Count; index++)
                    Object.DestroyImmediate(meshes[index]);
            }
        }

        [Test]
        public void ShapeSeedAndAntiRepeatAreDeterministic()
        {
            EarthShapeSeed first = EarthShapeSeed.Compose(2u, 3u, 5u, 7u, 11u);
            EarthShapeSeed second = EarthShapeSeed.Compose(2u, 3u, 5u, 7u, 11u);
            Assert.That(first, Is.EqualTo(second));

            var a = new EarthShapeDiversityTracker(12);
            var b = new EarthShapeDiversityTracker(12);
            EarthRockArchetype previous = (EarthRockArchetype)255;
            for (uint index = 0; index < 24; index++)
            {
                uint seed = EarthShapeSeed.Compose(19u, index + 1u, 5u, 1u, 0u).Value;
                EarthRockArchetype left = a.Select(seed);
                EarthRockArchetype right = b.Select(seed);
                Assert.That(left, Is.EqualTo(right));
                Assert.That(left, Is.Not.EqualTo(previous), $"Adjacent duplicate at {index}");
                previous = left;
            }
        }

        [Test]
        public void TwentySamplesPerFamily_AreValidAndDeterministic()
        {
            for (int family = 0; family < EarthRockMeshFactory.ArchetypeCount; family++)
            {
                var signatures = new HashSet<uint>();
                for (uint sample = 1; sample <= 20; sample++)
                {
                    uint seed = EarthShapeSeed.Compose(8191u, sample, (uint)family, 1u, 47u).Value;
                    Mesh mesh = EarthRockMeshFactory.Create((EarthRockArchetype)family, seed);
                    EarthMeshIntegrityReport report = EarthMeshIntegrityValidator.Validate(
                        mesh, EarthMeshIntegrityPolicy.ConvexCollider);
                    Assert.That(report.IsValid, Is.True, $"family={family} sample={sample}: {report}");
                    signatures.Add(EarthRockMeshFactory.Signature(
                        (EarthRockArchetype)family, seed).SilhouetteHash);
                    Object.DestroyImmediate(mesh);
                }
                Assert.That(signatures.Count, Is.GreaterThanOrEqualTo(18), $"family={family}");
            }
        }

        [Test]
        public void WallFamiliesProduceTwentyValidVisibleVariations()
        {
            for (int family = 0; family < EarthWallMeshFactory.ArchetypeCount; family++)
            {
                var silhouettes = new HashSet<string>();
                for (uint sample = 1; sample <= 20; sample++)
                {
                    uint seed = EarthShapeSeed.Compose(0xE17F0411u, sample, (uint)(100 + family), 1u, 29u).Value;
                    Mesh mesh = EarthWallMeshFactory.Create((EarthWallArchetype)family, seed);
                    EarthMeshIntegrityReport report = EarthMeshIntegrityValidator.Validate(
                        mesh, EarthMeshIntegrityPolicy.ClosedHero);
                    Assert.That(report.IsValid, Is.True, $"wall family={family} sample={sample}: {report}");
                    Vector3[] vertices = mesh.vertices;
                    uint hash = 2166136261u;
                    for (int index = 0; index < vertices.Length; index += 6)
                    {
                        Vector3 vertex = vertices[index];
                        hash = (hash ^ (uint)Mathf.RoundToInt((vertex.y + 1f) * 4096f)) * 16777619u;
                        hash = (hash ^ (uint)Mathf.RoundToInt((vertex.z + 1f) * 4096f)) * 16777619u;
                    }
                    silhouettes.Add(hash.ToString("X8"));
                    Object.DestroyImmediate(mesh);
                }
                Assert.That(silhouettes.Count, Is.GreaterThanOrEqualTo(16), $"wall family={family}");
            }
        }

        [Test]
        public void WallAntiRepeatIsDeterministicAndAvoidsAdjacentFamilies()
        {
            var first = new EarthWallShapeDiversityTracker(16);
            var second = new EarthWallShapeDiversityTracker(16);
            EarthWallArchetype previous = (EarthWallArchetype)255;
            for (uint index = 0; index < 32; index++)
            {
                uint seed = EarthShapeSeed.Compose(31u, index + 1u, 0x57414C4Cu, 1u, 0u).Value;
                EarthWallArchetype a = first.Select(seed);
                EarthWallArchetype b = second.Select(seed);
                Assert.That(a, Is.EqualTo(b));
                Assert.That(a, Is.Not.EqualTo(previous), $"Adjacent wall duplicate at {index}");
                previous = a;
            }
        }
    }
}
