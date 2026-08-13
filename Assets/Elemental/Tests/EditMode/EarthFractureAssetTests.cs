using Elemental.Authoring.Fracture;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthFractureAssetTests
    {
        [Test]
        public void ValidAssetCopiesStableDefinitionsAndFaceMetadata()
        {
            Mesh mesh = CreateClosedCube();
            EarthFractureAsset asset = CreateAsset(mesh, connected: true, includeInterior: true);
            var pieces = new EarthPieceDefinition[asset.PieceCount];
            var bonds = new EarthBondDefinition[asset.BondCount];

            bool copied = asset.CopyDefinitions(pieces, bonds);
            EarthFractureValidationResult validation = EarthFractureValidator.Validate(asset);

            Assert.That(copied, Is.True);
            Assert.That(validation.IsValid, Is.True);
            Assert.That(pieces[0].Id, Is.EqualTo(new EarthPieceId(1)));
            Assert.That(pieces[1].RestLocalPosition.x, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(bonds[0].PieceB, Is.EqualTo(EarthBondGraph.WorldPieceIndex));
            Assert.That(asset.GetPieceFaceMetadata(0).InteriorSubmesh, Is.EqualTo(1));

            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void ValidatorRejectsMissingInteriorClassification()
        {
            Mesh mesh = CreateClosedCube();
            EarthFractureAsset asset = CreateAsset(mesh, connected: true, includeInterior: false);

            EarthFractureValidationResult validation = EarthFractureValidator.Validate(asset);

            Assert.That(validation.Error, Is.EqualTo(EarthFractureValidationError.MissingFaceMetadata));
            Assert.That(validation.Index, Is.Zero);
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void ValidatorRejectsDisconnectedIntactGraph()
        {
            Mesh mesh = CreateClosedCube();
            EarthFractureAsset asset = CreateAsset(mesh, connected: false, includeInterior: true);

            EarthFractureValidationResult validation = EarthFractureValidator.Validate(asset);

            Assert.That(validation.Error, Is.EqualTo(EarthFractureValidationError.DisconnectedIntactGraph));
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void ValidatorRejectsImpossibleHierarchy()
        {
            Mesh mesh = CreateClosedCube();
            EarthFractureAsset asset = CreateAsset(mesh, connected: true, includeInterior: true);
            EarthFracturePieceRecord first = asset.PieceRecords[0];
            EarthFracturePieceRecord second = asset.PieceRecords[1];
            first.parentPieceIndex = 1;
            first.hierarchyLevel = 1;
            second.parentPieceIndex = 0;
            second.hierarchyLevel = 1;
            asset.PieceRecords[0] = first;
            asset.PieceRecords[1] = second;

            EarthFractureValidationResult validation = EarthFractureValidator.Validate(asset);

            Assert.That(validation.Error, Is.EqualTo(EarthFractureValidationError.ImpossibleHierarchy));
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void ValidatorRejectsBondCentroidOutsideRestSeam()
        {
            Mesh mesh = CreateClosedCube();
            EarthFractureAsset asset = CreateAsset(mesh, connected: true, includeInterior: true);
            EarthFractureBondRecord bond = asset.BondRecords[1];
            bond.localCentroid = new Vector3(100f, 100f, 100f);
            asset.BondRecords[1] = bond;

            EarthFractureValidationResult validation = EarthFractureValidator.Validate(asset);

            Assert.That(validation.Error, Is.EqualTo(EarthFractureValidationError.MismatchedRestSeam));
            Assert.That(validation.Index, Is.EqualTo(1));
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
        }

        private static EarthFractureAsset CreateAsset(
            Mesh mesh,
            bool connected,
            bool includeInterior)
        {
            EarthPieceFaceFlags faces = EarthPieceFaceFlags.HasExterior;
            if (includeInterior) faces |= EarthPieceFaceFlags.HasInterior;
            var pieces = new EarthFracturePieceRecord[2];
            for (int index = 0; index < pieces.Length; index++)
            {
                pieces[index] = new EarthFracturePieceRecord
                {
                    id = (ushort)(index + 1),
                    parentPieceIndex = EarthBondGraph.WorldPieceIndex,
                    flags = EarthPieceFlags.Structural | EarthPieceFlags.Repairable,
                    restLocalPosition = new Vector3(index - 0.5f, 0f, 0f),
                    restLocalRotation = Quaternion.identity,
                    restLocalScale = Vector3.one,
                    mass = 2f,
                    volume = 1f,
                    materialId = 1,
                    renderMesh = mesh,
                    colliderMesh = mesh,
                    faceFlags = faces,
                    exteriorSubmesh = 0,
                    interiorSubmesh = 1
                };
            }

            EarthFractureBondRecord foundation = CreateBond(
                1, 0, EarthBondGraph.WorldPieceIndex, Vector3.down, EarthBondFlags.Foundation);
            EarthFractureBondRecord[] bonds = connected
                ? new[] { foundation, CreateBond(2, 0, 1, Vector3.right, EarthBondFlags.Repairable) }
                : new[] { foundation };
            EarthFractureAsset asset = ScriptableObject.CreateInstance<EarthFractureAsset>();
            asset.SetBakedData(mesh, mesh, pieces, bonds);
            return asset;
        }

        private static EarthFractureBondRecord CreateBond(
            ushort id,
            short a,
            short b,
            Vector3 normal,
            EarthBondFlags flags)
        {
            return new EarthFractureBondRecord
            {
                id = id,
                pieceA = a,
                pieceB = b,
                flags = flags,
                localCentroid = Vector3.zero,
                localNormalA = normal,
                contactArea = 1f,
                tensileStrength = 10f,
                shearStrength = 12f,
                compressionStrength = 35f
            };
        }

        private static Mesh CreateClosedCube()
        {
            var mesh = new Mesh { name = "Fracture Validator Cube" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)
            };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[]
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7
            }, 0);
            mesh.SetTriangles(new[]
            {
                0, 1, 5, 0, 5, 4,
                1, 2, 6, 1, 6, 5,
                2, 3, 7, 2, 7, 6,
                3, 0, 4, 3, 4, 7
            }, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
