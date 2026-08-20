using Elemental.Authoring.Fracture;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.VFX;
using Elemental.Simulation.Magic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthMaterialPresentationTests
    {
        private const string FractureAssetPath =
            "Assets/Elemental/Content/Fracture/EarthWallFracture.asset";
        private const string HighProfilePath =
            "Assets/Elemental/Content/Profiles/EarthMaterialProfile.asset";
        private const string LowProfilePath =
            "Assets/Elemental/Content/Profiles/EarthMaterialProfile-NativeLow.asset";

        [Test]
        public void EarthMasterShaderExposesStableLocalDetailContract()
        {
            Shader shader = Shader.Find("Elemental/SG Earth Master");

            Assert.That(shader, Is.Not.Null);
            using var materialScope = new MaterialScope(shader);
            Material material = materialScope.Material;
            Assert.That(material.HasProperty("_ExteriorColor"), Is.True);
            Assert.That(material.HasProperty("_InteriorColor"), Is.True);
            Assert.That(material.HasProperty("_MicroFadeStart"), Is.True);
            Assert.That(material.HasProperty("_CavityStrength"), Is.True);
            Assert.That(material.HasProperty("_MagicAmount"), Is.True);
            Assert.That(material.HasProperty("_UsePlanetFrame"), Is.True);
        }

        [Test]
        public void MaterialProfileAppliesDistinctInteriorAndQualityVariant()
        {
            Shader shader = Shader.Find("Elemental/SG Earth Master");
            Assert.That(shader, Is.Not.Null);
            EarthMaterialProfile highProfile = AssetDatabase.LoadAssetAtPath<EarthMaterialProfile>(HighProfilePath);
            EarthMaterialProfile profile = AssetDatabase.LoadAssetAtPath<EarthMaterialProfile>(LowProfilePath);
            using var materialScope = new MaterialScope(shader);
            Material material = materialScope.Material;

            Assert.That(highProfile, Is.Not.Null);
            Assert.That(profile, Is.Not.Null);
            Assert.That(highProfile.Quality, Is.EqualTo(EarthMaterialQuality.High));
            Assert.That(profile.Quality, Is.EqualTo(EarthMaterialQuality.Low));
            profile.Apply(material, true);

            Assert.That(material.GetFloat("_InteriorAmount"), Is.EqualTo(1f));
            Assert.That(material.GetColor("_ExteriorColor"), Is.Not.EqualTo(material.GetColor("_InteriorColor")));
            Assert.That(material.IsKeywordEnabled("_EARTH_DETAIL_LOW"), Is.True);
        }

        [Test]
        public void FeedbackProfileIsDeterministicAndBoundedByImpactStrength()
        {
            EarthFeedbackProfile profile = ScriptableObject.CreateInstance<EarthFeedbackProfile>();
            var weak = new EarthImpactEvent(
                1u, 7u, 50f, 80f, 4f, 6f,
                float3.zero, new float3(0f, 1f, 0f), EarthImpactMaterialKind.LooseStone);
            var strong = new EarthImpactEvent(
                2u, 9u, 2200f, 480000f, 500f, 44f,
                float3.zero, new float3(0f, 1f, 0f), EarthImpactMaterialKind.Structure);

            EarthFeedbackSample first = profile.Evaluate(in strong);
            EarthFeedbackSample repeated = profile.Evaluate(in strong);
            EarthFeedbackSample low = profile.Evaluate(in weak);

            Assert.That(first.DustCount, Is.EqualTo(repeated.DustCount).And.InRange(0, 52));
            Assert.That(first.ChipCount, Is.EqualTo(repeated.ChipCount).And.InRange(0, 14));
            Assert.That(first.ScarRadius, Is.EqualTo(repeated.ScarRadius).Within(0.0001f));
            Assert.That(first.DustCount, Is.GreaterThan(low.DustCount));
            Assert.That(first.ScarRadius, Is.GreaterThan(low.ScarRadius));
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void ImpactBatchIsDeterministicFrameCappedAndAllocationFree()
        {
            var sample = new EarthFeedbackSample(12, 4, 0.4f, 8f);
            EarthFeedbackBatchAccumulator batch = default;
            var warm = new EarthImpactEvent(
                1u, 2u, 80f, 1200f, 20f, 11f,
                new float3(2f, 24f, 1f), new float3(0f, 1f, 0f),
                EarthImpactMaterialKind.Structure);
            batch.Add(in warm, in sample, 72, 20);
            Assert.That(batch.TryFlush(out _), Is.True);

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (uint index = 0; index < 128u; index++)
            {
                var impact = new EarthImpactEvent(
                    index + 1u, 900u - index, 90f + index, 2400f + index * 10f, 35f, 12f,
                    new float3(index * 0.01f, 24f, 0f), new float3(0f, 1f, 0f),
                    EarthImpactMaterialKind.Structure);
                batch.Add(in impact, in sample, 72, 20);
            }
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(batch.TryFlush(out EarthFeedbackBatchResult result), Is.True);
            Assert.That(allocated, Is.Zero);
            Assert.That(result.EventCount, Is.EqualTo(128));
            Assert.That(result.DustCount, Is.EqualTo(72));
            Assert.That(result.ChipCount, Is.EqualTo(20));
            Assert.That(result.MaximumKineticEnergy, Is.EqualTo(3670f));
            Assert.That(math.all(math.isfinite(result.Point)), Is.True);
            Assert.That(math.length(result.Normal), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(batch.PendingCount, Is.Zero);
        }

        [Test]
        public void ProductionFractureUsesSeparateMaskedRenderAndConvexMeshes()
        {
            EarthFractureAsset asset = AssetDatabase.LoadAssetAtPath<EarthFractureAsset>(FractureAssetPath);

            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.SchemaVersion, Is.EqualTo(EarthFractureAsset.CurrentSchemaVersion));
            Assert.That(asset.PieceCount, Is.EqualTo(40));
            Assert.That(EarthFractureValidator.Validate(asset).IsValid, Is.True);
            var physicalAspects = new float[asset.PieceCount];
            for (int pieceIndex = 0; pieceIndex < asset.PieceCount; pieceIndex++)
            {
                Mesh render = asset.GetPieceRenderMesh(pieceIndex);
                Mesh collider = asset.GetPieceColliderMesh(pieceIndex);
                Assert.That(render, Is.Not.SameAs(collider));
                Assert.That(render.subMeshCount, Is.EqualTo(2));
                Assert.That(collider.vertexCount, Is.LessThanOrEqualTo(255));
                Assert.That(collider.bounds.size.z, Is.GreaterThan(0.035f),
                    $"Piece {pieceIndex} collapsed to a zero-volume depth sheet.");
                Vector3 physicalSize = Vector3.Scale(collider.bounds.size, new Vector3(8f, 4f, 0.55f));
                float smallest = Mathf.Max(0.001f, Mathf.Min(physicalSize.x, physicalSize.y, physicalSize.z));
                physicalAspects[pieceIndex] = Mathf.Max(physicalSize.x, physicalSize.y, physicalSize.z) / smallest;
                Assert.That(render.colors32, Has.Length.EqualTo(render.vertexCount));

                bool hasExterior = false;
                bool hasInterior = false;
                foreach (Color32 color in render.colors32)
                {
                    hasExterior |= color.r >= 192 && color.g <= 64;
                    hasInterior |= color.g >= 192 && color.r <= 64;
                }
                Assert.That(hasExterior, Is.True, $"Piece {pieceIndex} is missing exterior classification.");
                Assert.That(hasInterior, Is.True, $"Piece {pieceIndex} is missing interior classification.");
            }
            System.Array.Sort(physicalAspects);
            Assert.That(physicalAspects[physicalAspects.Length / 2], Is.LessThanOrEqualTo(3.5f));
            Assert.That(physicalAspects[physicalAspects.Length - 1], Is.LessThanOrEqualTo(6f));
        }

        private sealed class MaterialScope : System.IDisposable
        {
            public MaterialScope(Shader shader) => Material = new Material(shader);
            public Material Material { get; }
            public void Dispose() => Object.DestroyImmediate(Material);
        }
    }
}
