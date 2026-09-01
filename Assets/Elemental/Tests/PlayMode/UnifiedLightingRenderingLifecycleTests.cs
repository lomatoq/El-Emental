using System.Collections;
using Elemental.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class UnifiedLightingRenderingLifecycleTests
    {
        [UnityTest]
        public IEnumerator BinderPreservesPropertyBlocksAndStagesCapturedFractureFrame()
        {
            Shader shader = Shader.Find(UnifiedLightingMigrationProfile.UnifiedShaderName);
            Assert.That(shader, Is.Not.Null);
            Material character = CreateMaterial(shader, UnifiedLightingMaterialFamily.Character);
            Material exterior = CreateMaterial(shader, UnifiedLightingMaterialFamily.SandstoneExterior);
            Material interior = CreateMaterial(shader, UnifiedLightingMaterialFamily.SandstoneInterior);
            Material ground = CreateMaterial(shader, UnifiedLightingMaterialFamily.PlanetGround);
            Material magic = CreateMaterial(shader, UnifiedLightingMaterialFamily.MagicConstruct);
            UnifiedLightingMigrationProfile profile =
                ScriptableObject.CreateInstance<UnifiedLightingMigrationProfile>();
            GameObject intact = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject fracture = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                Assert.That(profile.TryConfigureRuntime(new[]
                {
                    new UnifiedLightingMigrationEntry(UnifiedLightingMaterialRole.Character, character),
                    new UnifiedLightingMigrationEntry(UnifiedLightingMaterialRole.IntactSandstone, exterior),
                    new UnifiedLightingMigrationEntry(UnifiedLightingMaterialRole.LooseRock, exterior),
                    new UnifiedLightingMigrationEntry(UnifiedLightingMaterialRole.FractureExterior, exterior),
                    new UnifiedLightingMigrationEntry(UnifiedLightingMaterialRole.FractureInterior, interior),
                    new UnifiedLightingMigrationEntry(UnifiedLightingMaterialRole.PlanetGround, ground),
                    new UnifiedLightingMigrationEntry(UnifiedLightingMaterialRole.MagicConstruct, magic)
                }, out string failure), Is.True, failure);

                Renderer intactRenderer = intact.GetComponent<Renderer>();
                Renderer fractureRenderer = fracture.GetComponent<Renderer>();
                intactRenderer.sharedMaterial = exterior;
                fractureRenderer.sharedMaterial = exterior;
                var sourceBlock = new MaterialPropertyBlock();
                sourceBlock.SetFloat("_Fade", 0.63f);
                sourceBlock.SetColor("_BaseColor", new Color(0.41f, 0.32f, 0.24f, 1f));
                intactRenderer.SetPropertyBlock(sourceBlock);
                fractureRenderer.SetPropertyBlock(sourceBlock);

                UnifiedLightingMaterialBinder intactBinder =
                    intact.AddComponent<UnifiedLightingMaterialBinder>();
                UnifiedLightingMaterialBinder fractureBinder =
                    fracture.AddComponent<UnifiedLightingMaterialBinder>();
                intactBinder.Configure(profile);
                fractureBinder.Configure(profile);
                var intactFrame = new UnifiedLightingProjectionFrame(
                    UnifiedLightingProjectionMode.CapturedStructureLocal,
                    Vector3.zero,
                    Matrix4x4.identity);
                Matrix4x4 capturedFrame = Matrix4x4.TRS(
                    new Vector3(2.4f, -0.7f, 0.9f),
                    Quaternion.Euler(0f, 24f, 0f),
                    Vector3.one);
                var fractureFrame = new UnifiedLightingProjectionFrame(
                    UnifiedLightingProjectionMode.CapturedStructureLocal,
                    Vector3.zero,
                    capturedFrame);

                Assert.That(intactBinder.Bind(
                    intactRenderer, 0, UnifiedLightingMaterialRole.IntactSandstone, intactFrame),
                    Is.True);
                Assert.That(fractureBinder.Bind(
                    fractureRenderer, 0, UnifiedLightingMaterialRole.FractureExterior, fractureFrame),
                    Is.True);
                yield return null;

                var intactResult = new MaterialPropertyBlock();
                var fractureResult = new MaterialPropertyBlock();
                intactRenderer.GetPropertyBlock(intactResult);
                fractureRenderer.GetPropertyBlock(fractureResult);
                Assert.That(intactRenderer.sharedMaterial, Is.SameAs(fractureRenderer.sharedMaterial));
                Assert.That(intactResult.GetFloat("_Fade"), Is.EqualTo(0.63f));
                Assert.That(fractureResult.GetFloat("_Fade"), Is.EqualTo(0.63f));
                Assert.That(fractureResult.GetColor("_BaseColor"),
                    Is.EqualTo(new Color(0.41f, 0.32f, 0.24f, 1f)));
                Assert.That(fractureResult.GetFloat("_FractureMappingEnabled"), Is.EqualTo(1f));
                Matrix4x4 boundFrame = fractureResult.GetMatrix("_FractureLocalToStructure");
                Assert.That(MaximumMatrixElementDelta(boundFrame, capturedFrame),
                    Is.LessThan(0.000001f));
            }
            finally
            {
                Object.Destroy(intact);
                Object.Destroy(fracture);
                Object.Destroy(profile);
                Object.Destroy(character);
                Object.Destroy(exterior);
                Object.Destroy(interior);
                Object.Destroy(ground);
                Object.Destroy(magic);
            }
        }

        private static Material CreateMaterial(
            Shader shader,
            UnifiedLightingMaterialFamily family)
        {
            var material = new Material(shader);
            material.SetFloat("_MaterialFamily", (float)family);
            return material;
        }

        private static float MaximumMatrixElementDelta(Matrix4x4 a, Matrix4x4 b)
        {
            float maximum = 0f;
            for (int index = 0; index < 16; index++)
                maximum = Mathf.Max(maximum, Mathf.Abs(a[index] - b[index]));
            return maximum;
        }
    }
}
