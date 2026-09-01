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
        public IEnumerator BinderPreservesPerSlotBlocksAndStagesCapturedFractureFrame()
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
            var fracture = new GameObject("Two Submesh Fracture");
            Mesh fractureMesh = CreateTwoSubmeshMesh();
            fracture.AddComponent<MeshFilter>().sharedMesh = fractureMesh;
            Renderer fractureRenderer = fracture.AddComponent<MeshRenderer>();
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
                intactRenderer.sharedMaterial = exterior;
                fractureRenderer.sharedMaterials = new[] { exterior, interior };
                var rendererBlock = new MaterialPropertyBlock();
                rendererBlock.SetFloat("_MagicAmount", 0.19f);
                fractureRenderer.SetPropertyBlock(rendererBlock);
                var sourceBlock = new MaterialPropertyBlock();
                sourceBlock.SetFloat("_Fade", 0.63f);
                sourceBlock.SetColor("_BaseColor", new Color(0.41f, 0.32f, 0.24f, 1f));
                intactRenderer.SetPropertyBlock(sourceBlock, 0);
                fractureRenderer.SetPropertyBlock(sourceBlock, 0);
                var interiorBlock = new MaterialPropertyBlock();
                interiorBlock.SetFloat("_Fade", 0.27f);
                interiorBlock.SetColor("_FractureColor", new Color(0.58f, 0.46f, 0.35f, 1f));
                fractureRenderer.SetPropertyBlock(interiorBlock, 1);

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
                Assert.That(fractureBinder.Bind(
                    fractureRenderer, 1, UnifiedLightingMaterialRole.FractureInterior, fractureFrame),
                    Is.True);
                yield return null;

                var intactResult = new MaterialPropertyBlock();
                var rendererResult = new MaterialPropertyBlock();
                var exteriorResult = new MaterialPropertyBlock();
                var interiorResult = new MaterialPropertyBlock();
                intactRenderer.GetPropertyBlock(intactResult, 0);
                fractureRenderer.GetPropertyBlock(rendererResult);
                fractureRenderer.GetPropertyBlock(exteriorResult, 0);
                fractureRenderer.GetPropertyBlock(interiorResult, 1);
                Assert.That(intactRenderer.sharedMaterial,
                    Is.SameAs(fractureRenderer.sharedMaterials[0]));
                Assert.That(intactResult.GetFloat("_Fade"), Is.EqualTo(0.63f));
                Assert.That(rendererResult.GetFloat("_MagicAmount"), Is.EqualTo(0.19f));
                Assert.That(exteriorResult.GetFloat("_Fade"), Is.EqualTo(0.63f));
                Assert.That(exteriorResult.GetColor("_BaseColor"),
                    Is.EqualTo(new Color(0.41f, 0.32f, 0.24f, 1f)));
                Assert.That(interiorResult.GetFloat("_Fade"), Is.EqualTo(0.27f));
                Assert.That(interiorResult.GetColor("_FractureColor"),
                    Is.EqualTo(new Color(0.58f, 0.46f, 0.35f, 1f)));
                Assert.That(exteriorResult.GetFloat("_MaterialFamily"),
                    Is.EqualTo((float)UnifiedLightingMaterialFamily.SandstoneExterior));
                Assert.That(interiorResult.GetFloat("_MaterialFamily"),
                    Is.EqualTo((float)UnifiedLightingMaterialFamily.SandstoneInterior));
                Assert.That(exteriorResult.GetFloat("_FractureMappingEnabled"), Is.EqualTo(1f));
                Assert.That(interiorResult.GetFloat("_FractureMappingEnabled"), Is.EqualTo(1f));
                Matrix4x4 boundFrame = exteriorResult.GetMatrix("_FractureLocalToStructure");
                Assert.That(MaximumMatrixElementDelta(boundFrame, capturedFrame),
                    Is.LessThan(0.000001f));
                Assert.That(MaximumMatrixElementDelta(
                    interiorResult.GetMatrix("_FractureLocalToStructure"),
                    capturedFrame), Is.LessThan(0.000001f));
                Matrix4x4 boundNormalFrame = exteriorResult.GetMatrix(
                    "_FractureNormalToStructure");
                Assert.That(MaximumMatrixElementDelta(
                    boundNormalFrame,
                    capturedFrame.inverse.transpose), Is.LessThan(0.000001f));
            }
            finally
            {
                Object.Destroy(intact);
                Object.Destroy(fracture);
                Object.Destroy(fractureMesh);
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

        private static Mesh CreateTwoSubmeshMesh()
        {
            var mesh = new Mesh { name = "Two Submesh Lighting Test" };
            mesh.vertices = new[]
            {
                new Vector3(-1f, -1f, 0f),
                new Vector3(0f, 1f, 0f),
                new Vector3(1f, -1f, 0f),
                new Vector3(-1f, -1f, 0.1f),
                new Vector3(0f, 1f, 0.1f),
                new Vector3(1f, -1f, 0.1f)
            };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.SetTriangles(new[] { 3, 4, 5 }, 1);
            mesh.RecalculateNormals();
            return mesh;
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
