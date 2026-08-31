using System.IO;
using Elemental.Presentation.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class DuelShadowRenderingTests
    {
        private static readonly DuelShadowClassificationSettings Classification =
            new DuelShadowClassificationSettings(0.45f, 0.8f);

        private static DuelShadowStabilizationSettings Stabilization(
            float centerHysteresis = 0.5f,
            float contractionHysteresis = 2f)
        {
            return new DuelShadowStabilizationSettings(
                4f,
                200f,
                0f,
                2f,
                0.5f,
                1f,
                centerHysteresis,
                contractionHysteresis,
                2f);
        }

        [Test]
        public void TexelSnapping_ProducesExactGridCoordinates()
        {
            const float diameter = 20f;
            const int resolution = 1024;
            float texel = diameter / resolution;

            Vector2 snapped = DuelShadowMath.SnapCenterToTexels(
                new Vector2(texel * 2.49f, texel * -3.51f),
                diameter,
                resolution);

            Assert.That(snapped.x, Is.EqualTo(texel * 2f).Within(0.000001f));
            Assert.That(snapped.y, Is.EqualTo(texel * -4f).Within(0.000001f));
        }

        [Test]
        public void BoundsHysteresis_HoldsSmallMotionAndSmallContraction()
        {
            DuelShadowBoundsState state = default;
            DuelShadowStabilizationSettings settings = Stabilization();
            Assert.That(DuelShadowMath.TryBuildFrame(
                new Bounds(Vector3.zero, new Vector3(20f, 8f, 12f)),
                new Vector3(0.4f, -0.8f, 0.3f),
                Vector3.up,
                settings,
                2048,
                ref state,
                out DuelShadowFrame initial), Is.True);
            float initialMinimumDepth = state.MinimumDepth;
            float initialMaximumDepth = state.MaximumDepth;

            Bounds smallMove = new Bounds(
                new Vector3(0.08f, 0.03f, -0.05f),
                new Vector3(18f, 8f, 12f));
            Assert.That(DuelShadowMath.TryBuildFrame(
                smallMove,
                new Vector3(0.4f, -0.8f, 0.3f),
                Vector3.up,
                settings,
                2048,
                ref state,
                out DuelShadowFrame held), Is.True);

            Assert.That(held.SnappedCenter, Is.EqualTo(initial.SnappedCenter));
            Assert.That(held.HalfExtent, Is.EqualTo(initial.HalfExtent));
            Assert.That(state.MinimumDepth, Is.EqualTo(initialMinimumDepth));
            Assert.That(state.MaximumDepth, Is.EqualTo(initialMaximumDepth));

            Assert.That(DuelShadowMath.TryBuildFrame(
                new Bounds(Vector3.zero, new Vector3(12f, 6f, 8f)),
                new Vector3(0.4f, -0.8f, 0.3f),
                Vector3.up,
                settings,
                2048,
                ref state,
                out DuelShadowFrame contracted), Is.True);
            Assert.That(contracted.HalfExtent, Is.LessThan(initial.HalfExtent));
        }

        [Test]
        public void QualityTiers_ResolveToBoundedContractValues()
        {
            DuelShadowQuality low = DuelShadowQuality.Resolve(DuelShadowQualityTier.Low);
            DuelShadowQuality balanced = DuelShadowQuality.Resolve(
                DuelShadowQualityTier.Balanced);
            DuelShadowQuality cinematic = DuelShadowQuality.Resolve(
                DuelShadowQualityTier.Cinematic);

            Assert.That(low.Resolution, Is.EqualTo(1024));
            Assert.That(low.PcfKernelWidth, Is.EqualTo(3));
            Assert.That(balanced.Resolution, Is.EqualTo(2048));
            Assert.That(balanced.PcfKernelWidth, Is.EqualTo(5));
            Assert.That(cinematic.Resolution, Is.EqualTo(4096));
            Assert.That(cinematic.PcfKernelWidth, Is.EqualTo(7));
        }

        [Test]
        public void CasterClassification_IsDeterministicAndRejectsDebrisAndVfx()
        {
            for (int repeat = 0; repeat < 32; repeat++)
            {
                Assert.That(DuelShadowCasterPolicy.IsIncluded(
                    DuelShadowCasterClass.Player, 0.1f, Classification), Is.True);
                Assert.That(DuelShadowCasterPolicy.IsIncluded(
                    DuelShadowCasterClass.HeroRock, 0.449f, Classification), Is.False);
                Assert.That(DuelShadowCasterPolicy.IsIncluded(
                    DuelShadowCasterClass.HeroRock, 0.45f, Classification), Is.True);
                Assert.That(DuelShadowCasterPolicy.IsIncluded(
                    DuelShadowCasterClass.ActiveFragment, 0.799f, Classification), Is.False);
                Assert.That(DuelShadowCasterPolicy.IsIncluded(
                    DuelShadowCasterClass.ActiveFragment, 0.8f, Classification), Is.True);
                Assert.That(DuelShadowCasterPolicy.IsIncluded(
                    DuelShadowCasterClass.TinyDebris, 100f, Classification), Is.False);
                Assert.That(DuelShadowCasterPolicy.IsIncluded(
                    DuelShadowCasterClass.Vfx, 100f, Classification), Is.False);
            }
        }

        [TestCase(0f, -1f, 0f)]
        [TestCase(1f, -1f, 1f)]
        [TestCase(0f, 1f, 0f)]
        public void LightMatrices_AreFiniteForRepresentativeAndDegenerateUpCases(
            float x,
            float y,
            float z)
        {
            DuelShadowBoundsState state = default;
            Assert.That(DuelShadowMath.TryBuildFrame(
                new Bounds(new Vector3(0f, 55f, 0f), new Vector3(30f, 12f, 30f)),
                new Vector3(x, y, z),
                Vector3.up,
                Stabilization(),
                2048,
                ref state,
                out DuelShadowFrame frame), Is.True);
            Assert.That(DuelShadowMath.IsFinite(frame.ViewMatrix), Is.True);
            Assert.That(DuelShadowMath.IsFinite(frame.ProjectionMatrix), Is.True);
            Assert.That(DuelShadowMath.IsFinite(frame.WorldToShadowMatrix), Is.True);
        }

        [Test]
        public void GenerationCommit_InvalidatesPreviousRepresentationAtomically()
        {
            DuelShadowCasterRegistry registry = new DuelShadowCasterRegistry(4, 2);
            Assert.That(registry.TryRegister(
                Record(19u, 0u), out DuelShadowRegistrationHandle intact), Is.True);
            Assert.That(registry.TryRegister(
                Record(19u, 1u), out DuelShadowRegistrationHandle fractured), Is.True);
            Assert.That(registry.IsGenerationActive(intact), Is.True);
            Assert.That(registry.IsGenerationActive(fractured), Is.False);

            Assert.That(registry.TryCommitGeneration(19u, 1u), Is.True);

            Assert.That(registry.IsGenerationActive(intact), Is.False);
            Assert.That(registry.IsGenerationActive(fractured), Is.True);
            Assert.That(registry.CountActiveRegistrations(Classification), Is.EqualTo(1));
        }

        [Test]
        public void EmptyPoolInterval_DoesNotReactivateStaleGeneration()
        {
            DuelShadowCasterRegistry registry = new DuelShadowCasterRegistry(4, 2);
            Assert.That(registry.TryRegister(
                Record(23u, 0u), out DuelShadowRegistrationHandle initial), Is.True);
            Assert.That(registry.TryRegister(
                Record(23u, 1u), out DuelShadowRegistrationHandle next), Is.True);
            Assert.That(registry.TryCommitGeneration(23u, 1u), Is.True);
            Assert.That(registry.Unregister(initial), Is.True);
            Assert.That(registry.Unregister(next), Is.True);

            Assert.That(registry.TryRegister(
                Record(23u, 0u), out DuelShadowRegistrationHandle stale), Is.True);
            Assert.That(registry.IsGenerationActive(stale), Is.False);
            Assert.That(registry.TryRegister(
                Record(23u, 1u), out DuelShadowRegistrationHandle current), Is.True);
            Assert.That(registry.IsGenerationActive(current), Is.True);
        }

        [Test]
        public void CapacityRejection_DoesNotEraseCommittedGeneration()
        {
            DuelShadowCasterRegistry registry = new DuelShadowCasterRegistry(2, 2);
            Assert.That(registry.TryRegister(
                Record(27u, 0u), out DuelShadowRegistrationHandle old), Is.True);
            Assert.That(registry.TryRegister(
                Record(27u, 1u), out DuelShadowRegistrationHandle current), Is.True);
            Assert.That(registry.TryCommitGeneration(27u, 1u), Is.True);
            Assert.That(registry.Unregister(old), Is.True);
            Assert.That(registry.Unregister(current), Is.True);
            Assert.That(registry.TryRegister(
                Record(28u, 0u), out DuelShadowRegistrationHandle fillerA), Is.True);
            Assert.That(registry.TryRegister(
                Record(28u, 0u), out DuelShadowRegistrationHandle fillerB), Is.True);

            Assert.That(registry.TryRegister(
                Record(27u, 0u), out _), Is.False);
            Assert.That(registry.Unregister(fillerA), Is.True);
            Assert.That(registry.TryRegister(
                Record(27u, 0u), out DuelShadowRegistrationHandle stale), Is.True);
            Assert.That(registry.IsGenerationActive(stale), Is.False);
            Assert.That(registry.IsRegistrationCurrent(fillerB), Is.True);
        }

        [Test]
        public void PooledUnregister_StaleHandleCannotRemoveReusedSlot()
        {
            DuelShadowCasterRegistry registry = new DuelShadowCasterRegistry(1, 2);
            Assert.That(registry.TryRegister(
                Record(31u, 0u), out DuelShadowRegistrationHandle first), Is.True);
            Assert.That(registry.Unregister(first), Is.True);
            Assert.That(registry.TryRegister(
                Record(32u, 0u), out DuelShadowRegistrationHandle reused), Is.True);

            Assert.That(registry.Unregister(first), Is.False);
            Assert.That(registry.IsRegistrationCurrent(reused), Is.True);
            Assert.That(registry.Count, Is.EqualTo(1));
        }

        [Test]
        public void HighBitGroupAndGenerationRemainCanonicalUnsignedValues()
        {
            const uint groupId = 0xF1234567u;
            const uint generation = 0xE2345678u;
            DuelShadowCasterRegistry registry = new DuelShadowCasterRegistry(2, 2);

            Assert.That(registry.TryRegister(
                Record(groupId, generation),
                out DuelShadowRegistrationHandle handle), Is.True);
            Assert.That(registry.IsGenerationActive(handle), Is.True);
            Assert.That(registry.TryCommitGeneration(groupId, generation), Is.True);

            var commands = new DuelShadowDrawCommand[1];
            Assert.That(registry.CopyActiveDrawCommands(
                commands,
                Classification,
                1,
                out _,
                out _), Is.Zero,
                "A renderer-less pure record is active but intentionally not drawable.");
        }

        [Test]
        public void OwnedShadersImportAndDebugReceiverCallsBoundedShadowSampler()
        {
            const string casterPath =
                "Assets/Elemental/Content/GraphicsVNext/Rendering/DuelShadowCaster.shader";
            const string debugPath =
                "Assets/Elemental/Content/GraphicsVNext/Rendering/DuelShadowDebug.shader";
            Assert.That(AssetDatabase.LoadAssetAtPath<Shader>(casterPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<Shader>(debugPath), Is.Not.Null);

            string renderingPath = Path.Combine(
                Application.dataPath,
                "Elemental/Content/GraphicsVNext/Rendering");
            string debugSource = File.ReadAllText(Path.Combine(
                renderingPath,
                "DuelShadowDebug.shader"));
            string includeSource = File.ReadAllText(Path.Combine(
                renderingPath,
                "ElementalDuelShadow.hlsl"));
            StringAssert.Contains("ElementalDuelShadow.hlsl", debugSource);
            StringAssert.Contains("ElementalSampleDuelShadow(positionWS)", debugSource);
            StringAssert.Contains("clamp((int)round(_ElementalDuelShadowParams.w), 1, 3)",
                includeSource);
            StringAssert.Contains("for (int y = -3; y <= 3; y++)", includeSource);
            StringAssert.Contains("for (int x = -3; x <= 3; x++)", includeSource);
        }

        private static DuelShadowCasterRecord Record(uint groupId, uint generation)
        {
            return new DuelShadowCasterRecord(
                null,
                new Bounds(Vector3.zero, Vector3.one * 2f),
                groupId,
                generation,
                DuelShadowCasterClass.ActiveFragment,
                1);
        }
    }
}
