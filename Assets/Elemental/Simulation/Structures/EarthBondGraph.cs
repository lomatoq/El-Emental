using Unity.Mathematics;

namespace Elemental.Simulation.Structures
{
    public enum EarthGraphValidationError : byte
    {
        None,
        MissingStorage,
        CapacityExceeded,
        InvalidPieceId,
        DuplicatePieceId,
        InvalidParent,
        InvalidPieceGeometry,
        InvalidBondId,
        DuplicateBondId,
        InvalidBondEndpoint,
        SelfBond,
        InvalidBondGeometry,
        InvalidBondStrength
    }

    public readonly struct EarthGraphValidationResult
    {
        public EarthGraphValidationResult(EarthGraphValidationError error, int index)
        {
            Error = error;
            Index = index;
        }

        public EarthGraphValidationError Error { get; }
        public int Index { get; }
        public bool IsValid => Error == EarthGraphValidationError.None;
    }

    public static class EarthBondGraph
    {
        public const short WorldPieceIndex = -1;
        public const int MaxPieceCount = 256;
        public const int MaxBondCount = 1024;
        private const float MinimumNormalLengthSq = 0.000001f;

        public static EarthGraphValidationResult Validate(
            EarthPieceDefinition[] pieces,
            int pieceCount,
            EarthBondDefinition[] bonds,
            int bondCount)
        {
            if (pieces == null || bonds == null)
                return new EarthGraphValidationResult(EarthGraphValidationError.MissingStorage, -1);
            if (pieceCount < 0 || bondCount < 0 || pieceCount > pieces.Length || bondCount > bonds.Length ||
                pieceCount > MaxPieceCount || bondCount > MaxBondCount)
            {
                return new EarthGraphValidationResult(EarthGraphValidationError.CapacityExceeded, -1);
            }

            for (int pieceIndex = 0; pieceIndex < pieceCount; pieceIndex++)
            {
                EarthPieceDefinition piece = pieces[pieceIndex];
                if (!piece.Id.IsValid)
                    return new EarthGraphValidationResult(EarthGraphValidationError.InvalidPieceId, pieceIndex);
                for (int prior = 0; prior < pieceIndex; prior++)
                {
                    if (pieces[prior].Id == piece.Id)
                        return new EarthGraphValidationResult(EarthGraphValidationError.DuplicatePieceId, pieceIndex);
                }

                if (piece.ParentPieceIndex < WorldPieceIndex || piece.ParentPieceIndex >= pieceCount ||
                    piece.ParentPieceIndex == pieceIndex)
                {
                    return new EarthGraphValidationResult(EarthGraphValidationError.InvalidParent, pieceIndex);
                }

                if (!math.all(math.isfinite(piece.RestLocalPosition)) ||
                    !math.all(math.isfinite(piece.RestLocalRotation.value)) ||
                    math.lengthsq(piece.RestLocalRotation.value) < MinimumNormalLengthSq ||
                    !math.all(math.isfinite(piece.RestLocalScale)) ||
                    !math.all(math.isfinite(piece.LocalCenterOfMass)) ||
                    math.any(piece.RestLocalScale <= 0f) ||
                    !math.isfinite(piece.Mass) || piece.Mass <= 0f ||
                    !math.isfinite(piece.Volume) || piece.Volume <= 0f)
                {
                    return new EarthGraphValidationResult(EarthGraphValidationError.InvalidPieceGeometry, pieceIndex);
                }
            }

            for (int bondIndex = 0; bondIndex < bondCount; bondIndex++)
            {
                EarthBondDefinition bond = bonds[bondIndex];
                if (!bond.Id.IsValid)
                    return new EarthGraphValidationResult(EarthGraphValidationError.InvalidBondId, bondIndex);
                for (int prior = 0; prior < bondIndex; prior++)
                {
                    if (bonds[prior].Id == bond.Id)
                        return new EarthGraphValidationResult(EarthGraphValidationError.DuplicateBondId, bondIndex);
                }

                if (bond.PieceA < 0 || bond.PieceA >= pieceCount ||
                    bond.PieceB < WorldPieceIndex || bond.PieceB >= pieceCount)
                {
                    return new EarthGraphValidationResult(EarthGraphValidationError.InvalidBondEndpoint, bondIndex);
                }
                if (bond.PieceA == bond.PieceB)
                    return new EarthGraphValidationResult(EarthGraphValidationError.SelfBond, bondIndex);
                if (!math.all(math.isfinite(bond.LocalCentroid)) ||
                    !math.all(math.isfinite(bond.LocalNormalA)) ||
                    math.lengthsq(bond.LocalNormalA) < MinimumNormalLengthSq ||
                    !math.isfinite(bond.ContactArea) || bond.ContactArea <= 0f)
                {
                    return new EarthGraphValidationResult(EarthGraphValidationError.InvalidBondGeometry, bondIndex);
                }
                if (!math.isfinite(bond.TensileStrength) || bond.TensileStrength <= 0f ||
                    !math.isfinite(bond.ShearStrength) || bond.ShearStrength <= 0f ||
                    !math.isfinite(bond.CompressionStrength) || bond.CompressionStrength <= 0f)
                {
                    return new EarthGraphValidationResult(EarthGraphValidationError.InvalidBondStrength, bondIndex);
                }
            }

            return new EarthGraphValidationResult(EarthGraphValidationError.None, -1);
        }

        public static bool IsStructuralConnection(EarthBondPhase phase)
        {
            return phase == EarthBondPhase.Healthy ||
                   phase == EarthBondPhase.Damaged ||
                   phase == EarthBondPhase.Repaired;
        }
    }
}
