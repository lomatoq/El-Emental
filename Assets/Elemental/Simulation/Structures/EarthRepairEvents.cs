namespace Elemental.Simulation.Structures
{
    public readonly struct EarthRepairStartedEvent
    {
        public EarthRepairStartedEvent(uint tick, EarthStructureId structureId, int pieceCount, float selectedMass)
        { Tick = tick; StructureId = structureId; PieceCount = pieceCount; SelectedMass = selectedMass; }
        public uint Tick { get; }
        public EarthStructureId StructureId { get; }
        public int PieceCount { get; }
        public float SelectedMass { get; }
    }

    public readonly struct EarthPieceCapturedEvent
    {
        public EarthPieceCapturedEvent(uint tick, EarthStructureId structureId, EarthPieceId pieceId, int order)
        { Tick = tick; StructureId = structureId; PieceId = pieceId; Order = order; }
        public uint Tick { get; }
        public EarthStructureId StructureId { get; }
        public EarthPieceId PieceId { get; }
        public int Order { get; }
    }

    public readonly struct EarthPieceStagedEvent
    {
        public EarthPieceStagedEvent(uint tick, EarthStructureId structureId, EarthPieceId pieceId, byte attempt)
        { Tick = tick; StructureId = structureId; PieceId = pieceId; Attempt = attempt; }
        public uint Tick { get; }
        public EarthStructureId StructureId { get; }
        public EarthPieceId PieceId { get; }
        public byte Attempt { get; }
    }

    public readonly struct EarthBondReformingEvent
    {
        public EarthBondReformingEvent(uint tick, EarthStructureId structureId, EarthBondId bondId, float progress)
        { Tick = tick; StructureId = structureId; BondId = bondId; Progress = progress; }
        public uint Tick { get; }
        public EarthStructureId StructureId { get; }
        public EarthBondId BondId { get; }
        public float Progress { get; }
    }

    public readonly struct EarthBondRepairedEvent
    {
        public EarthBondRepairedEvent(uint tick, EarthStructureId structureId, EarthBondId bondId)
        { Tick = tick; StructureId = structureId; BondId = bondId; }
        public uint Tick { get; }
        public EarthStructureId StructureId { get; }
        public EarthBondId BondId { get; }
    }

    public readonly struct EarthRepairInterruptedEvent
    {
        public EarthRepairInterruptedEvent(uint tick, EarthStructureId structureId, EarthRepairInterruptReason reason, int repairedPieces)
        { Tick = tick; StructureId = structureId; Reason = reason; RepairedPieces = repairedPieces; }
        public uint Tick { get; }
        public EarthStructureId StructureId { get; }
        public EarthRepairInterruptReason Reason { get; }
        public int RepairedPieces { get; }
    }

    public readonly struct EarthStructureRebuiltEvent
    {
        public EarthStructureRebuiltEvent(uint tick, EarthStructureId structureId, uint revision)
        { Tick = tick; StructureId = structureId; Revision = revision; }
        public uint Tick { get; }
        public EarthStructureId StructureId { get; }
        public uint Revision { get; }
    }

    public readonly struct EarthRepairRejectedEvent
    {
        public EarthRepairRejectedEvent(uint tick, EarthStructureId structureId, EarthRepairRejectReason reason)
        { Tick = tick; StructureId = structureId; Reason = reason; }
        public uint Tick { get; }
        public EarthStructureId StructureId { get; }
        public EarthRepairRejectReason Reason { get; }
    }
}
