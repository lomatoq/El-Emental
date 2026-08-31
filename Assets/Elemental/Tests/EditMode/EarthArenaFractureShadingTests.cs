using Elemental.Authoring.Editor;
using Elemental.Authoring.Fracture;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthArenaFractureShadingTests
    {
        private const float MatrixTolerance = 0.00001f;

        [Test]
        public void RestPieceFrameContinuesTheIntactMappingAfterCentroidRecentering()
        {
            float4x4 intactLocalToStructure = float4x4.TRS(
                new float3(1.7f, -0.4f, 2.1f),
                quaternion.EulerXYZ(0.17f, -0.31f, 0.08f),
                new float3(1.2f, 0.85f, 1.05f));
            float3 pieceCentroidInIntactSpace = new(2.4f, -0.7f, 0.9f);
            float4x4 pieceRestLocalToStructure = math.mul(
                intactLocalToStructure,
                float4x4.Translate(pieceCentroidInIntactSpace));
            float3 intactLocalPoint = new(2.8f, -0.55f, 1.3f);
            float3 pieceLocalPoint = intactLocalPoint - pieceCentroidInIntactSpace;

            EarthFractureMappingFrame intactFrame =
                EarthFractureMappingFrameSolver.Resolve(intactLocalToStructure);
            EarthFractureMappingFrame pieceFrame =
                EarthFractureMappingFrameSolver.Resolve(pieceRestLocalToStructure);

            Assert.That(intactFrame.IsValid, Is.True);
            Assert.That(pieceFrame.IsValid, Is.True);
            AssertFloat3Approximately(
                pieceFrame.TransformPoint(pieceLocalPoint),
                intactFrame.TransformPoint(intactLocalPoint));

            float4x4 releasedBodyLocalToStructure = float4x4.TRS(
                new float3(-14f, 7f, 21f),
                quaternion.EulerXYZ(1.1f, -0.8f, 0.55f),
                new float3(1f));
            Assert.That(
                math.distance(
                    math.transform(releasedBodyLocalToStructure, pieceLocalPoint),
                    pieceFrame.TransformPoint(pieceLocalPoint)),
                Is.GreaterThan(1f),
                "A released body's current transform must not replace its captured rest mapping frame.");
            AssertFloat3Approximately(
                pieceFrame.TransformPoint(pieceLocalPoint),
                intactFrame.TransformPoint(intactLocalPoint));
        }

        [Test]
        public void InvalidOrSingularMappingFrameIsRejected()
        {
            float4x4 singular = float4x4.Scale(new float3(1f, 0f, 1f));
            EarthFractureMappingFrame singularFrame =
                EarthFractureMappingFrameSolver.Resolve(singular);
            Assert.That(singularFrame.IsValid, Is.False);

            float4x4 nonFinite = float4x4.identity;
            nonFinite.c3.x = float.NaN;
            EarthFractureMappingFrame nonFiniteFrame =
                EarthFractureMappingFrameSolver.Resolve(nonFinite);
            Assert.That(nonFiniteFrame.IsValid, Is.False);
        }

        [Test]
        public void RendererAdapterMergesMappingWithoutClearingExistingOverrides()
        {
            Shader shader = Shader.Find("Elemental/Graphics V5/Rumble Rock Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var renderObject = new GameObject("Fracture mapping renderer");
            var renderer = renderObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            var properties = new MaterialPropertyBlock();
            int unrelatedId = Shader.PropertyToID("_Fade");
            properties.SetFloat(unrelatedId, 0.37f);
            renderer.SetPropertyBlock(properties);

            float4x4 localToStructure = float4x4.TRS(
                new float3(3f, 2f, 1f),
                quaternion.EulerXYZ(0.1f, 0.2f, 0.3f),
                new float3(0.9f, 1.1f, 1.2f));
            EarthFractureMappingFrame frame =
                EarthFractureMappingFrameSolver.Resolve(localToStructure);

            try
            {
                Assert.That(
                    EarthArenaFractureShading.Apply(renderer, in frame, properties),
                    Is.True);
                renderer.GetPropertyBlock(properties);
                Assert.That(properties.GetFloat(unrelatedId), Is.EqualTo(0.37f).Within(0.0001f));
                Assert.That(
                    properties.GetFloat(EarthArenaFractureShading.MappingEnabledId),
                    Is.EqualTo(1f));
                AssertMatrixApproximately(
                    properties.GetMatrix(EarthArenaFractureShading.LocalToStructureId),
                    EarthArenaFractureShading.ToMatrix(localToStructure));
            }
            finally
            {
                Object.DestroyImmediate(renderObject);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void RuntimeCapturesRestFramesAndProxyCyclesPreservePropertyBlocks()
        {
            Shader shader = Shader.Find("Elemental/Graphics V5/Rumble Rock Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var root = new GameObject("Structure root");
            root.transform.SetPositionAndRotation(
                new Vector3(7f, -3f, 2f),
                Quaternion.Euler(11f, 23f, -7f));
            root.transform.localScale = new Vector3(1.1f, 0.95f, 1.05f);
            GameObject intact = GameObject.CreatePrimitive(PrimitiveType.Cube);
            intact.name = "Intact";
            intact.transform.SetParent(root.transform, false);
            intact.transform.localPosition = new Vector3(0.6f, 1.2f, -0.4f);
            intact.transform.localRotation = Quaternion.Euler(4f, -12f, 6f);
            Renderer intactRenderer = intact.GetComponent<Renderer>();
            intactRenderer.sharedMaterial = material;

            var fractureRoot = new GameObject("Fracture root");
            fractureRoot.transform.SetParent(root.transform, false);
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = "Piece";
            piece.transform.SetParent(fractureRoot.transform, false);
            piece.transform.localPosition = new Vector3(-9f, 8f, 7f);
            Renderer pieceRenderer = piece.GetComponent<Renderer>();
            pieceRenderer.sharedMaterial = material;
            var originalProperties = new MaterialPropertyBlock();
            int unrelatedId = Shader.PropertyToID("_Fade");
            originalProperties.SetFloat(unrelatedId, 0.42f);
            pieceRenderer.SetPropertyBlock(originalProperties);

            Mesh pieceMesh = piece.GetComponent<MeshFilter>().sharedMesh;
            var asset = ScriptableObject.CreateInstance<EarthFractureAsset>();
            Vector3 restPosition = new(1.8f, -0.25f, 2.7f);
            Quaternion restRotation = Quaternion.Euler(13f, 29f, -5f);
            Vector3 restScale = new(0.8f, 1.15f, 0.9f);
            asset.SetBakedData(
                intact.GetComponent<MeshFilter>().sharedMesh,
                intact.GetComponent<MeshFilter>().sharedMesh,
                new[]
                {
                    new EarthFracturePieceRecord
                    {
                        id = 1,
                        parentPieceIndex = EarthBondGraph.WorldPieceIndex,
                        flags = EarthPieceFlags.Structural | EarthPieceFlags.Repairable,
                        restLocalPosition = restPosition,
                        restLocalRotation = restRotation,
                        restLocalScale = restScale,
                        mass = 20f,
                        volume = 1f,
                        renderMesh = pieceMesh,
                        colliderMesh = pieceMesh
                    }
                },
                new EarthFractureBondRecord[0]);

            try
            {
                EarthArenaStructure structure = intact.AddComponent<EarthArenaStructure>();
                Assert.That(
                    structure.Configure(
                        asset,
                        root.transform,
                        fractureRoot.transform,
                        intactRenderer,
                        intact.GetComponent<Collider>(),
                        new[] { piece.transform },
                        null,
                        material,
                        null,
                        17u,
                        true,
                        true),
                    Is.True);

                var properties = new MaterialPropertyBlock();
                intactRenderer.GetPropertyBlock(properties);
                Assert.That(
                    properties.GetFloat(EarthArenaFractureShading.MappingEnabledId),
                    Is.EqualTo(1f));
                AssertMatrixApproximately(
                    properties.GetMatrix(EarthArenaFractureShading.LocalToStructureId),
                    root.transform.worldToLocalMatrix * intact.transform.localToWorldMatrix,
                    "intact root-frame mapping");

                Matrix4x4 expectedRestFrame = Matrix4x4.TRS(
                    restPosition,
                    restRotation,
                    restScale);
                pieceRenderer.GetPropertyBlock(properties);
                Assert.That(properties.GetFloat(unrelatedId), Is.EqualTo(0.42f).Within(0.0001f));
                AssertMatrixApproximately(
                    properties.GetMatrix(EarthArenaFractureShading.LocalToStructureId),
                    expectedRestFrame,
                    "piece rest mapping");

                Assert.That(structure.TryAcquirePiece(0), Is.True);
                piece.transform.SetPositionAndRotation(
                    new Vector3(-12f, 4f, 18f),
                    Quaternion.Euler(75f, -20f, 41f));
                pieceRenderer.GetPropertyBlock(properties);
                AssertMatrixApproximately(
                    properties.GetMatrix(EarthArenaFractureShading.LocalToStructureId),
                    expectedRestFrame,
                    "released piece mapping");

                Assert.That(structure.SetMagicRepairProgress(1f), Is.True);
                pieceRenderer.GetPropertyBlock(properties);
                Assert.That(properties.GetFloat(unrelatedId), Is.EqualTo(0.42f).Within(0.0001f));
                Assert.That(
                    properties.GetFloat(EarthArenaFractureShading.MappingEnabledId),
                    Is.EqualTo(1f));
                AssertMatrixApproximately(
                    properties.GetMatrix(EarthArenaFractureShading.LocalToStructureId),
                    expectedRestFrame,
                    "repaired piece mapping");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(asset);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void BrokenCrownPiecesKeepRestFramesFiniteNormalsAndOptionalValidTangents()
        {
            EarthArenaFractureCatalog catalog =
                AssetDatabase.LoadAssetAtPath<EarthArenaFractureCatalog>(
                    BrokenCrownArenaImporter.CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.ImportedModel, Is.Not.Null);

            Transform model = catalog.ImportedModel.transform;
            Transform[] importedTransforms =
                catalog.ImportedModel.GetComponentsInChildren<Transform>(true);
            for (int structureIndex = 0;
                 structureIndex < catalog.Structures.Length;
                 structureIndex++)
            {
                EarthArenaFractureEntry entry = catalog.Structures[structureIndex];
                EarthFractureAsset asset = entry.fractureAsset;
                Assert.That(asset, Is.Not.Null, entry.structureId);
                for (int pieceIndex = 0; pieceIndex < asset.PieceCount; pieceIndex++)
                {
                    string pieceName = $"FR_{entry.structureId}_P{pieceIndex + 1:000}";
                    Transform pieceTransform = FindNamed(importedTransforms, pieceName);
                    Assert.That(pieceTransform, Is.Not.Null, pieceName);
                    EarthFracturePieceRecord record = asset.PieceRecords[pieceIndex];
                    Matrix4x4 authoredLocalToStructure =
                        model.worldToLocalMatrix * pieceTransform.localToWorldMatrix;
                    Matrix4x4 recordedRestLocalToStructure = Matrix4x4.TRS(
                        record.restLocalPosition,
                        record.restLocalRotation,
                        record.restLocalScale);
                    AssertMatrixApproximately(
                        recordedRestLocalToStructure,
                        authoredLocalToStructure,
                        pieceName);

                    Mesh mesh = record.renderMesh;
                    Assert.That(mesh, Is.Not.Null, pieceName);
                    Vector3[] normals = mesh.normals;
                    Assert.That(normals, Has.Length.EqualTo(mesh.vertexCount), pieceName);
                    for (int normalIndex = 0; normalIndex < normals.Length; normalIndex++)
                    {
                        Vector3 normal = normals[normalIndex];
                        Assert.That(IsFinite(normal), Is.True,
                            $"{pieceName} normal {normalIndex} is non-finite.");
                        Assert.That(normal.sqrMagnitude, Is.GreaterThan(0.25f),
                            $"{pieceName} normal {normalIndex} collapsed.");
                    }

                    Vector4[] tangents = mesh.tangents;
                    Assert.That(
                        tangents.Length == 0 || tangents.Length == mesh.vertexCount,
                        Is.True,
                        $"{pieceName} has a partial tangent stream.");
                    for (int tangentIndex = 0; tangentIndex < tangents.Length; tangentIndex++)
                    {
                        Vector4 tangent = tangents[tangentIndex];
                        Assert.That(IsFinite(tangent), Is.True,
                            $"{pieceName} tangent {tangentIndex} is non-finite.");
                        Assert.That(new Vector3(tangent.x, tangent.y, tangent.z).sqrMagnitude,
                            Is.GreaterThan(0.25f),
                            $"{pieceName} tangent {tangentIndex} collapsed.");
                    }

                    Bounds bounds = mesh.bounds;
                    Assert.That(IsFinite(bounds.center) && IsFinite(bounds.extents), Is.True,
                        $"{pieceName} bounds are non-finite.");
                    Assert.That(bounds.size.sqrMagnitude, Is.GreaterThan(0.000001f),
                        $"{pieceName} bounds collapsed.");
                }
            }
        }

        [Test]
        public void ArenaExteriorAndInteriorShareStableReceiverContract()
        {
            Material exterior = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Elemental/Content/GraphicsV5/Materials/RumbleArenaSandstone.mat");
            Material interior = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Elemental/Content/GraphicsV5/Materials/RumbleSandstoneFractureInterior.mat");

            Assert.That(exterior, Is.Not.Null);
            Assert.That(interior, Is.Not.Null);
            Assert.That(interior.shader, Is.EqualTo(exterior.shader));
            Assert.That(exterior.HasProperty(EarthArenaFractureShading.MappingEnabledId), Is.True);
            Assert.That(interior.HasProperty(EarthArenaFractureShading.MappingEnabledId), Is.True);
            AssertSharedFloat(exterior, interior, "_SideShadingSmoothness");
            AssertSharedFloat(exterior, interior, "_AmbientStrength");
            AssertSharedFloat(exterior, interior, "_FacetContrast");
            AssertSharedFloat(exterior, interior, "_MacroStrength");
            AssertSharedFloat(exterior, interior, "_SideShadowFade");
            AssertSharedFloat(exterior, interior, "_StableSideFormOcclusion");
            Vector4 exteriorCenter = exterior.GetVector("_ReceiverPlanetCenter");
            Vector4 interiorCenter = interior.GetVector("_ReceiverPlanetCenter");
            Assert.That(Vector4.Distance(exteriorCenter, interiorCenter),
                Is.LessThanOrEqualTo(0.00001f));
        }

        private static Transform FindNamed(Transform[] transforms, string name)
        {
            for (int index = 0; index < transforms.Length; index++)
                if (transforms[index] != null && transforms[index].name == name)
                    return transforms[index];
            return null;
        }

        private static void AssertSharedFloat(Material first, Material second, string property)
        {
            Assert.That(first.HasProperty(property), Is.True, property);
            Assert.That(second.HasProperty(property), Is.True, property);
            Assert.That(second.GetFloat(property),
                Is.EqualTo(first.GetFloat(property)).Within(0.00001f), property);
        }

        private static void AssertFloat3Approximately(float3 actual, float3 expected)
        {
            Assert.That(math.distance(actual, expected), Is.LessThanOrEqualTo(MatrixTolerance));
        }

        private static void AssertMatrixApproximately(
            Matrix4x4 actual,
            Matrix4x4 expected,
            string context = null)
        {
            for (int row = 0; row < 4; row++)
            for (int column = 0; column < 4; column++)
                Assert.That(actual[row, column],
                    Is.EqualTo(expected[row, column]).Within(MatrixTolerance),
                    $"{context ?? "matrix"} [{row},{column}]");
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private static bool IsFinite(Vector4 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) &&
            float.IsFinite(value.z) && float.IsFinite(value.w);
    }
}
