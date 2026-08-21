using System;
using System.Collections.Generic;
using Elemental.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class RumbleRockMeshFactoryTests
    {
        [Test]
        public void ApprovedTwentyRockCorpus_IsValidAndVisiblyDiverse()
        {
            var signatures = new HashSet<ulong>();
            for (int index = 0; index < 20; index++)
            {
                RumbleRockFamily family = index switch
                {
                    < 8 => RumbleRockFamily.Boulder,
                    < 12 => RumbleRockFamily.Slab,
                    < 16 => RumbleRockFamily.Wedge,
                    _ => RumbleRockFamily.Pebble
                };
                int seed = 51803 + index * 7919;
                float scale = index < 3 ? 1.35f : index < 8 ? 1.1f : index < 16 ? 1.22f : 0.62f;
                RumbleRockRecipe recipe = RumbleRockMeshFactory.CreateDefaultRecipe(seed, family, scale);
                Mesh mesh = RumbleRockMeshFactory.Build(in recipe, $"test_{index}");
                try
                {
                    Assert.That(RumbleRockMeshFactory.Validate(mesh, out string reason), Is.True, reason);
                    Assert.That(mesh.bounds.min.y, Is.EqualTo(0f).Within(0.0005f));
                    Assert.That(mesh.vertexCount, Is.GreaterThan(24));
                    signatures.Add(Signature(mesh));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
            Assert.That(signatures.Count, Is.GreaterThanOrEqualTo(18),
                "The approved corpus must not collapse into repeated silhouettes.");
        }

        [TestCase(RumbleRockFamily.Boulder)]
        [TestCase(RumbleRockFamily.Slab)]
        [TestCase(RumbleRockFamily.Wedge)]
        [TestCase(RumbleRockFamily.Pebble)]
        [TestCase(RumbleRockFamily.Pillar)]
        public void SameRecipe_ProducesIdenticalMesh(RumbleRockFamily family)
        {
            RumbleRockRecipe recipe = RumbleRockMeshFactory.CreateDefaultRecipe(77131, family, 1.15f);
            Mesh first = RumbleRockMeshFactory.Build(in recipe, "first");
            Mesh second = RumbleRockMeshFactory.Build(in recipe, "second");
            try
            {
                Assert.That(Signature(first), Is.EqualTo(Signature(second)));
                Assert.That(first.vertexCount, Is.EqualTo(second.vertexCount));
                Assert.That(first.triangles, Is.EqualTo(second.triangles));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void BevelGeometry_ContainsSeparateFaceAndEdgeClasses()
        {
            RumbleRockRecipe recipe = RumbleRockMeshFactory.CreateDefaultRecipe(
                44819, RumbleRockFamily.Boulder, 1.4f);
            Mesh mesh = RumbleRockMeshFactory.Build(in recipe, "bevel_test");
            try
            {
                Color[] colors = mesh.colors;
                bool hasMainFaces = false;
                bool hasBevelFaces = false;
                for (int index = 0; index < colors.Length; index++)
                {
                    hasMainFaces |= colors[index].a < 0.55f;
                    hasBevelFaces |= colors[index].a > 0.62f;
                }
                Assert.That(hasMainFaces, Is.True);
                Assert.That(hasBevelFaces, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        private static ulong Signature(Mesh mesh)
        {
            unchecked
            {
                ulong hash = 1469598103934665603ul;
                Vector3[] vertices = mesh.vertices;
                for (int index = 0; index < vertices.Length; index++)
                {
                    Vector3 vertex = vertices[index];
                    hash ^= (uint)Mathf.RoundToInt(vertex.x * 10000f);
                    hash *= 1099511628211ul;
                    hash ^= (uint)Mathf.RoundToInt(vertex.y * 10000f);
                    hash *= 1099511628211ul;
                    hash ^= (uint)Mathf.RoundToInt(vertex.z * 10000f);
                    hash *= 1099511628211ul;
                }
                int[] triangles = mesh.triangles;
                for (int index = 0; index < triangles.Length; index++)
                {
                    hash ^= (uint)triangles[index];
                    hash *= 1099511628211ul;
                }
                return hash;
            }
        }
    }
}
