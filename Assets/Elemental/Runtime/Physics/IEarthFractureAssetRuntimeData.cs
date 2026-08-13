using Elemental.Simulation.Structures;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    /// <summary>
    /// Runtime-readable boundary implemented by the authoring fracture asset.
    /// Runtime code receives copied pure definitions and never mutates the asset.
    /// </summary>
    public interface IEarthFractureAssetRuntimeData
    {
        int SchemaVersion { get; }
        Mesh IntactRenderMesh { get; }
        Mesh IntactColliderMesh { get; }
        int PieceCount { get; }
        int BondCount { get; }
        Mesh GetPieceRenderMesh(int index);
        Mesh GetPieceColliderMesh(int index);
        EarthPieceFaceMetadata GetPieceFaceMetadata(int index);
        bool CopyDefinitions(
            EarthPieceDefinition[] pieceDestination,
            EarthBondDefinition[] bondDestination);
    }
}
