using System.IO;
using Elemental.Presentation.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class UnifiedLightingRenderingTests
    {
        private const string RenderingRoot =
            "Assets/Elemental/Content/GraphicsVNext/Rendering/";

        [Test]
        public void MigrationTableIsCompleteDeterministicAndSharesExteriorFamily()
        {
            UnifiedLightingMigrationProfile profile =
                AssetDatabase.LoadAssetAtPath<UnifiedLightingMigrationProfile>(
                    RenderingRoot + "UnifiedLightingMigrationProfile.asset");
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.EntryCount, Is.EqualTo(7));
            Assert.That(profile.IsComplete(), Is.True);

            Assert.That(profile.TryResolve(
                UnifiedLightingMaterialRole.IntactSandstone,
                out Material intact,
                out UnifiedLightingRoleContract intactContract), Is.True);
            Assert.That(profile.TryResolve(
                UnifiedLightingMaterialRole.LooseRock,
                out Material loose,
                out _), Is.True);
            Assert.That(profile.TryResolve(
                UnifiedLightingMaterialRole.FractureExterior,
                out Material fractureExterior,
                out UnifiedLightingRoleContract fractureContract), Is.True);
            Assert.That(profile.TryResolve(
                UnifiedLightingMaterialRole.FractureInterior,
                out Material fractureInterior,
                out UnifiedLightingRoleContract interiorContract), Is.True);

            Assert.That(loose, Is.SameAs(intact));
            Assert.That(fractureExterior, Is.SameAs(intact));
            Assert.That(fractureInterior, Is.Not.SameAs(intact));
            Assert.That(intactContract.Family,
                Is.EqualTo(UnifiedLightingMaterialFamily.SandstoneExterior));
            Assert.That(fractureContract.Family, Is.EqualTo(intactContract.Family));
            Assert.That(intactContract.ProjectionMode,
                Is.EqualTo(UnifiedLightingProjectionMode.CapturedStructureLocal));
            Assert.That(fractureContract.ProjectionMode,
                Is.EqualTo(intactContract.ProjectionMode));
            Assert.That(interiorContract.Family,
                Is.EqualTo(UnifiedLightingMaterialFamily.SandstoneInterior));
        }

        [Test]
        public void CapturedFractureFramePreservesWorldProjectionAndExteriorProperties()
        {
            Vector3 fragmentLocalPoint = new Vector3(0.4f, 0.15f, 0.4f);
            Vector3 fragmentLocalNormal = new Vector3(0.31f, 0.84f, -0.44f).normalized;
            Matrix4x4 intactLocalToStructure = Matrix4x4.TRS(
                new Vector3(1.7f, -0.4f, 2.1f),
                Quaternion.Euler(9.7f, -17.8f, 4.6f),
                new Vector3(1.2f, 0.85f, 1.05f));
            Matrix4x4 fragmentLocalToIntact = Matrix4x4.TRS(
                new Vector3(2.4f, -0.7f, 0.9f),
                Quaternion.Euler(-21f, 38f, 13f),
                new Vector3(0.72f, 1.45f, 1.18f));
            Vector3 intactLocalPoint = fragmentLocalToIntact.MultiplyPoint3x4(
                fragmentLocalPoint);
            Vector3 intactLocalNormal = fragmentLocalToIntact.inverse.transpose
                .MultiplyVector(fragmentLocalNormal).normalized;
            var intactFrame = new UnifiedLightingProjectionFrame(
                UnifiedLightingProjectionMode.CapturedStructureLocal,
                Vector3.zero,
                intactLocalToStructure);
            var fractureFrame = new UnifiedLightingProjectionFrame(
                UnifiedLightingProjectionMode.CapturedStructureLocal,
                Vector3.zero,
                intactLocalToStructure * fragmentLocalToIntact);

            Assert.That(intactFrame.TryResolveMappingPosition(
                intactLocalPoint, Vector3.zero, out Vector3 intactMapping), Is.True);
            Assert.That(fractureFrame.TryResolveMappingPosition(
                fragmentLocalPoint, Vector3.zero, out Vector3 fractureMapping), Is.True);
            Assert.That(Vector3.Distance(intactMapping, fractureMapping),
                Is.LessThan(0.000001f));
            Assert.That(intactFrame.TryResolveMappingNormal(
                intactLocalNormal, Vector3.zero, out Vector3 intactMappingNormal), Is.True);
            Assert.That(fractureFrame.TryResolveMappingNormal(
                fragmentLocalNormal, Vector3.zero, out Vector3 fractureMappingNormal), Is.True);
            Assert.That(Vector3.Distance(intactMappingNormal, fractureMappingNormal),
                Is.LessThan(0.000001f));
            Vector3 intactWeights = UnifiedLightingMath.EvaluateTriplanarWeights(
                intactMappingNormal, 4f);
            Vector3 fractureWeights = UnifiedLightingMath.EvaluateTriplanarWeights(
                fractureMappingNormal, 4f);
            Assert.That(Vector3.Distance(intactWeights, fractureWeights),
                Is.LessThan(0.000001f),
                "Rotated, nonuniform fragments must retain the intact triplanar blend weights.");

            UnifiedLightingMigrationProfile profile =
                AssetDatabase.LoadAssetAtPath<UnifiedLightingMigrationProfile>(
                    RenderingRoot + "UnifiedLightingMigrationProfile.asset");
            Assert.That(profile.TryResolve(
                UnifiedLightingMaterialRole.IntactSandstone,
                out Material intact,
                out _), Is.True);
            Assert.That(profile.TryResolve(
                UnifiedLightingMaterialRole.FractureExterior,
                out Material fractureExterior,
                out _), Is.True);
            Assert.That(fractureExterior, Is.SameAs(intact),
                "The exterior property source must be identical across the handoff.");
            Assert.That(fractureExterior.GetColor("_BaseColor"),
                Is.EqualTo(intact.GetColor("_BaseColor")));
            Assert.That(fractureExterior.GetFloat("_TextureScale"),
                Is.EqualTo(intact.GetFloat("_TextureScale")));
            Assert.That(fractureExterior.GetFloat("_Roughness"),
                Is.EqualTo(intact.GetFloat("_Roughness")));

            var singular = new UnifiedLightingProjectionFrame(
                UnifiedLightingProjectionMode.CapturedStructureLocal,
                Vector3.zero,
                Matrix4x4.zero);
            Assert.That(singular.IsValid, Is.False);
        }

        [Test]
        public void FormRemainsReadableWithAllOptionalOcclusionDisabled()
        {
            float back = UnifiedLightingMath.EvaluateBaseFormLuminance(-1f, 0.82f, 0.58f);
            float side = UnifiedLightingMath.EvaluateBaseFormLuminance(0f, 0.82f, 0.58f);
            float front = UnifiedLightingMath.EvaluateBaseFormLuminance(1f, 0.82f, 0.58f);

            Assert.That(back, Is.GreaterThan(0.5f));
            Assert.That(side, Is.GreaterThan(back));
            Assert.That(front, Is.GreaterThan(side));
            Assert.That(front - back, Is.GreaterThan(0.25f));
            Assert.That(float.IsNaN(back) || float.IsNaN(side) || float.IsNaN(front), Is.False);
        }

        [Test]
        public void UnifiedShaderHasOwnedNormalsAndOptionalShadowSeamsWithoutMacroNoise()
        {
            string absoluteRoot = Path.Combine(
                Application.dataPath,
                "Elemental/Content/GraphicsVNext/Rendering");
            string include = File.ReadAllText(Path.Combine(
                absoluteRoot, "ElementalUnifiedLighting.hlsl"));
            string shader = File.ReadAllText(Path.Combine(
                absoluteRoot, "ElementalUnifiedLit.shader"));

            StringAssert.Contains("ElementalSampleDuelShadow", include);
            StringAssert.Contains("ElementalSampleCapsuleContactShadow", include);
            StringAssert.Contains("#if defined(_SCREEN_SPACE_OCCLUSION)", include);
            StringAssert.DoesNotContain("MainLightRealtimeShadow", include);
            StringAssert.DoesNotContain("TransformWorldToShadowCoord", include);
            StringAssert.Contains("float4 tangentOS : TANGENT", shader);
            StringAssert.Contains("GetVertexNormalInputs", shader);
            StringAssert.Contains("_FractureNormalToStructure", shader);
            StringAssert.Contains("Name \"DepthNormals\"", shader);
            StringAssert.DoesNotContain("ValueNoise", shader);
            StringAssert.DoesNotContain("orange", shader.ToLowerInvariant());
        }

        [Test]
        public void ExplicitMigrationCopiesAuthoredTexturePropertiesOnlyToDestination()
        {
            Shader shader = Shader.Find(UnifiedLightingMigrationProfile.UnifiedShaderName);
            Assert.That(shader, Is.Not.Null);
            var source = new Material(shader);
            var destination = new Material(shader);
            var texture = new Texture2D(2, 2);
            try
            {
                source.SetTexture("_BaseMap", texture);
                source.SetTextureScale("_BaseMap", new Vector2(2.5f, 1.5f));
                source.SetTextureOffset("_BaseMap", new Vector2(0.2f, 0.3f));
                source.SetColor("_BaseColor", new Color(0.2f, 0.3f, 0.4f, 1f));
                source.SetFloat("_NormalStrength", 0.65f);
                source.SetFloat("_TextureScale", 0.37f);
                source.SetFloat("_Roughness", 0.73f);
                source.SetFloat("_Fade", 0.75f);

                Assert.That(UnifiedLightingMaterialMigration.CopyPreservedProperties(
                    source, destination), Is.True);
                Assert.That(destination.GetTexture("_BaseMap"), Is.SameAs(texture));
                Assert.That(destination.GetTextureScale("_BaseMap"),
                    Is.EqualTo(new Vector2(2.5f, 1.5f)));
                Assert.That(destination.GetTextureOffset("_BaseMap"),
                    Is.EqualTo(new Vector2(0.2f, 0.3f)));
                Assert.That(destination.GetColor("_BaseColor"),
                    Is.EqualTo(new Color(0.2f, 0.3f, 0.4f, 1f)));
                Assert.That(destination.GetFloat("_NormalStrength"), Is.EqualTo(0.65f));
                Assert.That(destination.GetFloat("_TextureScale"), Is.EqualTo(0.37f));
                Assert.That(destination.GetFloat("_Roughness"), Is.EqualTo(0.73f));
                Assert.That(destination.GetFloat("_Fade"), Is.EqualTo(0.75f));
                Assert.That(source.GetFloat("_Fade"), Is.EqualTo(0.75f),
                    "Migration must not mutate its source material.");
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(destination);
            }
        }

        [Test]
        public void OffTransitionClearsPriorCapsuleGlobalsAndPublishesZeroStrength()
        {
            Shader.SetGlobalVector(
                CapsuleContactShadowRenderPass.ShadowParamsId,
                new Vector4(1f, 0.8f, 1.25f, 4f));
            Shader.SetGlobalVector(
                CapsuleContactShadowRenderPass.BiasDebugParamsId,
                new Vector4(0.02f, 0.03f, 1f, 0f));

            CapsuleContactShadowFeature.ClearGlobalState();

            Assert.That(Shader.GetGlobalVector(
                CapsuleContactShadowRenderPass.ShadowParamsId), Is.EqualTo(Vector4.zero));
            Assert.That(Shader.GetGlobalVector(
                CapsuleContactShadowRenderPass.BiasDebugParamsId), Is.EqualTo(Vector4.zero));
            Assert.That(CapsuleContactShadowDiagnostics.Current.FeatureRequested, Is.False);
            Assert.That(CapsuleContactShadowDiagnostics.Current.ShadowStrength, Is.Zero);
        }
    }
}
