using Elemental.Simulation.Structures;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthBondGraphTests
    {
        [Test]
        public void StableIdentifiersReserveZeroAsInvalid()
        {
            Assert.That(new EarthStructureId(0).IsValid, Is.False);
            Assert.That(new EarthPieceId(0).IsValid, Is.False);
            Assert.That(new EarthBondId(0).IsValid, Is.False);
            Assert.That(new EarthStructureId(7), Is.EqualTo(new EarthStructureId(7)));
            Assert.That(new EarthPieceId(7), Is.Not.EqualTo(new EarthPieceId(8)));
            Assert.That(new EarthBondId(7).GetHashCode(), Is.EqualTo(7));
        }

        [Test]
        public void ValidGraphAcceptsPieceAndFoundationBonds()
        {
            EarthPieceDefinition[] pieces = CreatePieces(2);
            EarthBondDefinition[] bonds =
            {
                CreateBond(1, 0, 1),
                CreateBond(2, 0, EarthBondGraph.WorldPieceIndex, EarthBondFlags.Foundation)
            };

            EarthGraphValidationResult result = EarthBondGraph.Validate(pieces, 2, bonds, 2);

            Assert.That(result.IsValid, Is.True);
        }

        [TestCase(EarthGraphValidationError.InvalidPieceId)]
        [TestCase(EarthGraphValidationError.DuplicatePieceId)]
        [TestCase(EarthGraphValidationError.InvalidParent)]
        [TestCase(EarthGraphValidationError.InvalidPieceGeometry)]
        public void ValidatorRejectsInvalidPieceData(EarthGraphValidationError expected)
        {
            EarthPieceDefinition[] pieces = CreatePieces(2);
            switch (expected)
            {
                case EarthGraphValidationError.InvalidPieceId:
                    pieces[1].Id = default;
                    break;
                case EarthGraphValidationError.DuplicatePieceId:
                    pieces[1].Id = pieces[0].Id;
                    break;
                case EarthGraphValidationError.InvalidParent:
                    pieces[1].ParentPieceIndex = 1;
                    break;
                case EarthGraphValidationError.InvalidPieceGeometry:
                    pieces[1].Mass = 0f;
                    break;
            }

            EarthGraphValidationResult result = EarthBondGraph.Validate(
                pieces, pieces.Length, new EarthBondDefinition[0], 0);

            Assert.That(result.Error, Is.EqualTo(expected));
            Assert.That(result.Index, Is.EqualTo(1));
        }

        [TestCase(EarthGraphValidationError.InvalidBondId)]
        [TestCase(EarthGraphValidationError.DuplicateBondId)]
        [TestCase(EarthGraphValidationError.InvalidBondEndpoint)]
        [TestCase(EarthGraphValidationError.SelfBond)]
        [TestCase(EarthGraphValidationError.InvalidBondGeometry)]
        [TestCase(EarthGraphValidationError.InvalidBondStrength)]
        public void ValidatorRejectsInvalidBondData(EarthGraphValidationError expected)
        {
            EarthPieceDefinition[] pieces = CreatePieces(3);
            EarthBondDefinition[] bonds =
            {
                CreateBond(1, 0, 1),
                CreateBond(2, 1, 2)
            };
            switch (expected)
            {
                case EarthGraphValidationError.InvalidBondId:
                    bonds[1].Id = default;
                    break;
                case EarthGraphValidationError.DuplicateBondId:
                    bonds[1].Id = bonds[0].Id;
                    break;
                case EarthGraphValidationError.InvalidBondEndpoint:
                    bonds[1].PieceB = 7;
                    break;
                case EarthGraphValidationError.SelfBond:
                    bonds[1].PieceB = bonds[1].PieceA;
                    break;
                case EarthGraphValidationError.InvalidBondGeometry:
                    bonds[1].LocalNormalA = float3.zero;
                    break;
                case EarthGraphValidationError.InvalidBondStrength:
                    bonds[1].ShearStrength = 0f;
                    break;
            }

            EarthGraphValidationResult result = EarthBondGraph.Validate(
                pieces, pieces.Length, bonds, bonds.Length);

            Assert.That(result.Error, Is.EqualTo(expected));
            Assert.That(result.Index, Is.EqualTo(1));
        }

        [Test]
        public void ValidatorRejectsMissingAndOversizedStorageWithoutThrowing()
        {
            EarthGraphValidationResult missing = EarthBondGraph.Validate(null, 0, null, 0);
            EarthGraphValidationResult oversized = EarthBondGraph.Validate(
                new EarthPieceDefinition[1], 2, new EarthBondDefinition[0], 0);

            Assert.That(missing.Error, Is.EqualTo(EarthGraphValidationError.MissingStorage));
            Assert.That(oversized.Error, Is.EqualTo(EarthGraphValidationError.CapacityExceeded));
        }

        internal static EarthPieceDefinition[] CreatePieces(int count)
        {
            var pieces = new EarthPieceDefinition[count];
            for (int index = 0; index < count; index++)
            {
                pieces[index] = new EarthPieceDefinition
                {
                    Id = new EarthPieceId((ushort)(index + 1)),
                    ParentPieceIndex = EarthBondGraph.WorldPieceIndex,
                    Flags = EarthPieceFlags.Structural | EarthPieceFlags.Repairable,
                    RestLocalPosition = new float3(index, 0f, 0f),
                    RestLocalRotation = quaternion.identity,
                    RestLocalScale = new float3(1f),
                    Mass = 2f,
                    Volume = 1f,
                    LocalCenterOfMass = float3.zero,
                    MaterialId = 1
                };
            }
            return pieces;
        }

        internal static EarthBondDefinition CreateBond(
            ushort id,
            short pieceA,
            short pieceB,
            EarthBondFlags flags = EarthBondFlags.Repairable)
        {
            return new EarthBondDefinition
            {
                Id = new EarthBondId(id),
                PieceA = pieceA,
                PieceB = pieceB,
                Flags = flags,
                LocalCentroid = new float3(pieceA, 0f, 0f),
                LocalNormalA = new float3(1f, 0f, 0f),
                ContactArea = 1f,
                TensileStrength = 10f,
                ShearStrength = 10f,
                CompressionStrength = 40f
            };
        }
    }
}
