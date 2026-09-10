using System;
using System.Collections.Generic;
using Elemental.Presentation.Rendering;
using Elemental.Runtime.Geometry;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class RumbleRockMeshFactoryTests
    {
        [TestCase(RumbleRockFamily.Boulder)]
        [TestCase(RumbleRockFamily.Slab)]
        [TestCase(RumbleRockFamily.Wedge)]
        [TestCase(RumbleRockFamily.Pebble)]
        [TestCase(RumbleRockFamily.Pillar)]
        public void ThousandRecipeCourt_IsClosedPlanarAndDeterministic(RumbleRockFamily family)
        {
            float maximumAdjustment = 0, minimumAreaMargin = float.PositiveInfinity;
            int reducedBevels = 0, retainedEdges = 0, rejectedShapeCuts = 0;
            var policy = new EarthMeshIntegrityPolicy(true, true, false, 4096,
                weldTolerance: 0.000001f, strictFlatNormals: true);
            for (int sample = 0; sample < 200; sample++)
            {
                int seed = unchecked(51803 + sample * 7919 + (int)family * 104729);
                RumbleRockRecipe original = RumbleRockMeshFactory.CreateDefaultRecipe(seed, family,
                    0.4f + (sample % 9) * 0.2f);
                Vector3 anisotropy = new Vector3(0.5f + (sample % 5) * 0.45f,
                    0.6f + (sample % 7) * 0.3f, 0.45f + (sample % 3) * 0.65f);
                var recipe = new RumbleRockRecipe(seed, family, Vector3.Scale(original.Size, anisotropy),
                    original.CutCount, original.BevelWidth, original.SilhouetteBias);
                Mesh mesh = RumbleRockMeshFactory.Build(recipe, out RumbleRockMeshFactory.BuildDiagnostics diagnostics);
                maximumAdjustment = Mathf.Max(maximumAdjustment, diagnostics.MaximumCanonicalAdjustment);
                minimumAreaMargin = Mathf.Min(minimumAreaMargin, diagnostics.MinimumCross / diagnostics.RequiredCross);
                reducedBevels += diagnostics.ReducedBevelCuts;
                retainedEdges += diagnostics.RetainedEdges;
                rejectedShapeCuts += diagnostics.RejectedShapeCuts;
                try
                {
                    EarthMeshIntegrityReport report = EarthMeshIntegrityValidator.Validate(mesh, policy,
                        Matrix4x4.TRS(new Vector3(7, -2, 11), Quaternion.Euler(13, sample, 27), anisotropy));
                    Assert.That(report.IsValid, Is.True, $"{family}/{seed}: {report}");
                    Assert.That(report.ComponentCount, Is.EqualTo(1));
                    Assert.That(report.SignedVolume, Is.GreaterThan(0));
                    Assert.That(mesh.bounds.min.y, Is.EqualTo(0).Within(0.00001f));
                    if (sample % 40 != 0) continue;
                    Mesh repeated = RumbleRockMeshFactory.Build(recipe);
                    try
                    {
                        Assert.That(Signature(repeated), Is.EqualTo(Signature(mesh)));
                        Assert.That(repeated.normals, Is.EqualTo(mesh.normals));
                        Assert.That(repeated.colors, Is.EqualTo(mesh.colors));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(repeated); }
                }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
            }
            TestContext.WriteLine($"{family}: recipes=200, maxCanonicalDelta={maximumAdjustment:G9}, " +
                $"minCrossMargin={minimumAreaMargin:G9}, reducedBevelCuts={reducedBevels}, " +
                $"retainedEdges={retainedEdges}, rejectedShapeCuts={rejectedShapeCuts}");
        }

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

        [TestCase("V5_Boulder_00")]
        [TestCase("V5_Boulder_01")]
        [TestCase("V5_Boulder_02")]
        [TestCase("V5_Boulder_03")]
        [TestCase("V5_Boulder_04")]
        [TestCase("V5_Boulder_05")]
        [TestCase("V5_Boulder_06")]
        [TestCase("V5_Boulder_07")]
        [TestCase("V5_Pebble_17")]
        [TestCase("V5_Pebble_18")]
        [TestCase("V5_Pebble_19")]
        [TestCase("V5_Wedge_12")]
        public void PhysicsCopy_IsCenteredUnitSizedAndPreservesApprovedTopology(string sourceName)
        {
            Mesh source = AssetDatabase.LoadAssetAtPath<Mesh>(
                $"Assets/Elemental/Content/GraphicsV5/Rocks/{sourceName}.asset");
            string physicsName = sourceName.Replace("V5_", "V5_Physics_") + "_CenteredUnit";
            Mesh physics = AssetDatabase.LoadAssetAtPath<Mesh>(
                $"Assets/Elemental/Content/GraphicsV5/Physics/{physicsName}.asset");

            Assert.That(source, Is.Not.Null);
            Assert.That(physics, Is.Not.Null);
            Assert.That(source.bounds.min.y, Is.EqualTo(0f).Within(0.0005f),
                "The original lookdev rock must remain base-centered.");
            Assert.That(physics.bounds.center.x, Is.EqualTo(0f).Within(0.0005f));
            Assert.That(physics.bounds.center.y, Is.EqualTo(0f).Within(0.0005f));
            Assert.That(physics.bounds.center.z, Is.EqualTo(0f).Within(0.0005f));
            float maximumAxis = Mathf.Max(
                physics.bounds.size.x,
                Mathf.Max(physics.bounds.size.y, physics.bounds.size.z));
            Assert.That(maximumAxis, Is.EqualTo(1f).Within(0.0005f));
            Assert.That(physics.vertexCount, Is.EqualTo(source.vertexCount));
            Assert.That(physics.triangles.Length, Is.EqualTo(source.triangles.Length));
            Assert.That(physics.subMeshCount, Is.EqualTo(source.subMeshCount));
            AssertNormalAndTangentPreservation(source, physics);
            AssertNormalsFollowTriangleWinding(physics);
            Assert.That(physics.colors, Is.EqualTo(source.colors));
            EarthMeshIntegrityReport integrity = EarthMeshIntegrityValidator.Validate(
                physics,
                EarthMeshIntegrityPolicy.ConvexCollider);
            Assert.That(integrity.IsValid, Is.True, integrity.ToString());
        }

        [Test]
        public void RumbleStoneVariation_StaysInsideTheAuthoredPaletteFamily()
        {
            Shader shader = Shader.Find("Elemental/Graphics V5/Rumble Rock Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            Color authored = new Color(0.56f, 0.35f, 0.20f, 1f);
            material.SetColor("_BaseColor", authored);
            material.SetColor("_ShadowColor", new Color(0.22f, 0.15f, 0.11f, 1f));
            material.SetColor("_EdgeColor", new Color(0.72f, 0.51f, 0.33f, 1f));
            material.SetFloat("_MacroScale", 3.2f);
            material.SetFloat("_MacroStrength", 0.1f);
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                Renderer renderer = go.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                var properties = new MaterialPropertyBlock();
                for (uint stableId = 1; stableId <= 64; stableId++)
                {
                    EarthStoneVisualVariant.Apply(renderer, stableId, properties);
                    renderer.GetPropertyBlock(properties);
                    Color varied = properties.GetColor("_BaseColor");
                    float distance = Vector3.Distance(
                        new Vector3(authored.r, authored.g, authored.b),
                        new Vector3(varied.r, varied.g, varied.b));
                    Assert.That(distance, Is.LessThan(0.055f),
                        $"Stable rock {stableId} escaped the authored Rumble palette.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static void AssertNormalAndTangentPreservation(Mesh source, Mesh physics)
        {
            Vector3[] sourceNormals = source.normals;
            Vector3[] physicsNormals = physics.normals;
            Assert.That(sourceNormals, Has.Length.EqualTo(source.vertexCount));
            Assert.That(physicsNormals, Has.Length.EqualTo(sourceNormals.Length));

            bool preserved = true;
            bool globallyFlipped = true;
            for (int index = 0; index < sourceNormals.Length; index++)
            {
                preserved &= (physicsNormals[index] - sourceNormals[index]).sqrMagnitude <= 0.0000000001f;
                globallyFlipped &= (physicsNormals[index] + sourceNormals[index]).sqrMagnitude <= 0.0000000001f;
            }
            Assert.That(preserved || globallyFlipped, Is.True,
                "Centering must preserve every authored normal; only a full closed-mesh winding repair may negate all normals together.");

            Vector4[] sourceTangents = source.tangents;
            Vector4[] physicsTangents = physics.tangents;
            Assert.That(physicsTangents, Has.Length.EqualTo(sourceTangents.Length));
            for (int index = 0; index < sourceTangents.Length; index++)
            {
                Vector4 expected = sourceTangents[index];
                if (globallyFlipped) expected.w = -expected.w;
                Assert.That((physicsTangents[index] - expected).sqrMagnitude, Is.LessThanOrEqualTo(0.0000000001f),
                    $"Physics tangent {index} must preserve its direction and update handedness only for a global winding flip.");
            }
        }

        [TestCase(.1f)]
        [TestCase(1f)]
        [TestCase(10f)]
        public void PhysicsWindingAcceptance_IsInvariantUnderUniformUnitConversion(float units)
        {
            Mesh source=AssetDatabase.LoadAssetAtPath<Mesh>(
                "Assets/Elemental/Content/GraphicsV5/Physics/V5_Physics_Boulder_05_CenteredUnit.asset");
            Assert.That(source,Is.Not.Null);
            Mesh copy=UnityEngine.Object.Instantiate(source);
            try
            {
                Vector3[] positions=copy.vertices;
                for(int i=0;i<positions.Length;i++)positions[i]*=units;
                copy.vertices=positions;copy.RecalculateBounds();
                AssertNormalsFollowTriangleWinding(copy);
                Assert.That(copy.triangles,Is.EqualTo(source.triangles),"Unit conversion must preserve every closed face.");
                Assert.That(copy.normals,Is.EqualTo(source.normals));
            }
            finally{UnityEngine.Object.DestroyImmediate(copy);}
        }

        private static void AssertNormalsFollowTriangleWinding(Mesh mesh)
        {
            // Use the same scale-invariant degeneracy/manifold/flat-normal contract
            // as publication. An absolute cross-squared cutoff changes under scale^4.
            Assert.That(RumbleRockMeshFactory.Validate(mesh,out string reason),Is.True,reason);
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                int[] indices = mesh.GetIndices(submesh, true);
                for (int offset = 0; offset + 2 < indices.Length; offset += 3)
                {
                    int a = indices[offset];
                    int b = indices[offset + 1];
                    int c = indices[offset + 2];
                    Vector3 geometric = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                    Vector3 authored = normals[a] + normals[b] + normals[c];
                    // Vector3.normalized returns zero below magnitude1e-5 even for
                    // a valid small face. Strict validation above established nonzero area.
                    float alignment=Vector3.Dot(geometric/Mathf.Sqrt(geometric.sqrMagnitude),
                        authored/Mathf.Sqrt(authored.sqrMagnitude));
                    Assert.That(alignment, Is.GreaterThanOrEqualTo(0.999f),
                        $"Physics triangle {offset / 3} in submesh {submesh} has normals opposed to its winding.");
                }
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
