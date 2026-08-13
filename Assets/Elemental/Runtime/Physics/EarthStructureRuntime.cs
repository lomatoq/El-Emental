using Elemental.Simulation.Structures;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    public sealed class EarthStructureRuntime : MonoBehaviour
    {
        private EarthPieceDefinition[] _pieceDefinitions = System.Array.Empty<EarthPieceDefinition>();
        private EarthBondDefinition[] _bondDefinitions = System.Array.Empty<EarthBondDefinition>();
        private EarthPieceState[] _pieceStates = System.Array.Empty<EarthPieceState>();
        private EarthBondState[] _bondStates = System.Array.Empty<EarthBondState>();
        private EarthBondId[] _brokenOutput = System.Array.Empty<EarthBondId>();
        private int[] _islandByPiece = System.Array.Empty<int>();
        private bool[] _islandSupported = System.Array.Empty<bool>();
        private int[] _islandPieceCounts = System.Array.Empty<int>();
        private int[] _traversalQueue = System.Array.Empty<int>();
        private Transform[] _pieceTransforms = System.Array.Empty<Transform>();
        private EarthPieceRuntime[] _pieceRuntimes = System.Array.Empty<EarthPieceRuntime>();
        private EarthBondRuntime[] _bondRuntimes = System.Array.Empty<EarthBondRuntime>();
        private EarthStructureState _state;
        private uint _generation;

        public bool IsConfigured { get; private set; }
        public bool IsFractured => _state.Phase == EarthStructurePhase.Fractured ||
                                    _state.Phase == EarthStructurePhase.Damaged ||
                                    _state.Phase == EarthStructurePhase.Repairing;
        public EarthStructureState State => _state;
        public uint Generation => _generation;
        public int PieceCount => _pieceDefinitions.Length;
        public int BondCount => _bondDefinitions.Length;
        public int RemainingBondCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < _bondStates.Length; index++)
                    if (_bondStates[index].Phase != EarthBondPhase.Broken) count++;
                return count;
            }
        }

        public bool Configure(
            IEarthFractureAssetRuntimeData asset,
            EarthWall wall,
            Transform[] pieceTransforms,
            EarthWallBond[] wallBonds)
        {
            if (asset == null || pieceTransforms == null || wallBonds == null ||
                asset.PieceCount != pieceTransforms.Length || asset.BondCount != wallBonds.Length ||
                asset.PieceCount > EarthBondGraph.MaxPieceCount || asset.BondCount > EarthBondGraph.MaxBondCount)
            {
                IsConfigured = false;
                return false;
            }

            _pieceDefinitions = new EarthPieceDefinition[asset.PieceCount];
            _bondDefinitions = new EarthBondDefinition[asset.BondCount];
            if (!asset.CopyDefinitions(_pieceDefinitions, _bondDefinitions))
            {
                IsConfigured = false;
                return false;
            }

            EarthGraphValidationResult validation = EarthBondGraph.Validate(
                _pieceDefinitions, _pieceDefinitions.Length,
                _bondDefinitions, _bondDefinitions.Length);
            if (!validation.IsValid)
            {
                IsConfigured = false;
                return false;
            }

            _pieceStates = new EarthPieceState[asset.PieceCount];
            _bondStates = new EarthBondState[asset.BondCount];
            _brokenOutput = new EarthBondId[asset.BondCount];
            _islandByPiece = new int[asset.PieceCount];
            _islandSupported = new bool[asset.PieceCount];
            _islandPieceCounts = new int[asset.PieceCount];
            _traversalQueue = new int[asset.PieceCount];
            _pieceTransforms = pieceTransforms;
            _pieceRuntimes = new EarthPieceRuntime[asset.PieceCount];
            _bondRuntimes = new EarthBondRuntime[asset.BondCount];
            for (int index = 0; index < asset.PieceCount; index++)
            {
                EarthWallPiece pieceRuntime = pieceTransforms[index].GetComponent<EarthWallPiece>();
                if (pieceRuntime == null) pieceRuntime = pieceTransforms[index].gameObject.AddComponent<EarthWallPiece>();
                pieceRuntime.ConfigureCanonical(
                    wall,
                    this,
                    index,
                    _pieceDefinitions[index].Id,
                    asset.GetPieceFaceMetadata(index));
                _pieceRuntimes[index] = pieceRuntime;
            }
            for (int index = 0; index < asset.BondCount; index++)
            {
                _bondRuntimes[index] = new EarthBondRuntime(
                    index, _bondDefinitions[index].Id, wallBonds[index].Joint);
            }

            IsConfigured = true;
            ResetExact(default, 0, 0);
            return true;
        }

        public void ResetExact(EarthStructureId structureId, uint generation, uint tick)
        {
            if (!IsConfigured) return;
            _generation = generation;
            for (int index = 0; index < _pieceDefinitions.Length; index++)
            {
                _pieceStates[index] = EarthPieceState.Intact;
                _pieceRuntimes[index]?.ResetExact(
                    _pieceDefinitions[index], structureId, generation);
            }
            for (int index = 0; index < _bondDefinitions.Length; index++)
            {
                _bondStates[index] = EarthBondState.Healthy;
                _bondRuntimes[index].ResetForPool();
            }

            _state = new EarthStructureState
            {
                Id = structureId,
                Phase = EarthStructurePhase.Intact,
                PieceCount = (ushort)_pieceDefinitions.Length,
                BondCount = (ushort)_bondDefinitions.Length,
                IslandCount = 1,
                SupportedIslandCount = 1,
                Revision = _state.Revision + 1,
                LastChangedTick = tick
            };
        }

        public void BeginFracture(uint tick)
        {
            if (!IsConfigured || IsFractured) return;
            for (int index = 0; index < _pieceStates.Length; index++)
            {
                EarthPieceState piece = _pieceStates[index];
                piece.Phase = EarthPiecePhase.Cracked;
                piece.LastChangedTick = tick;
                _pieceStates[index] = piece;
            }
            _state.Phase = EarthStructurePhase.Fractured;
            _state.Revision++;
            _state.LastChangedTick = tick;
            SolveIslands();
        }

        public void SetBondStrengths(
            int index,
            float tensileStrength,
            float shearStrength,
            float compressionStrength)
        {
            if (index < 0 || index >= _bondDefinitions.Length) return;
            EarthBondDefinition definition = _bondDefinitions[index];
            definition.TensileStrength = math.max(0.0001f, tensileStrength);
            definition.ShearStrength = math.max(0.0001f, shearStrength);
            definition.CompressionStrength = math.max(0.0001f, compressionStrength);
            _bondDefinitions[index] = definition;
        }

        public EarthBondDamageResult ApplyImpact(
            float3 localPoint,
            float3 localImpulse,
            float localRadius,
            float materialResponse,
            uint tick)
        {
            if (!IsConfigured || !IsFractured)
                return default;
            var impact = new EarthBondImpact(
                localPoint, localImpulse, localRadius, materialResponse, tick);
            EarthBondDamageResult result = EarthFractureBatchRunner.ApplyImpact(
                in impact,
                _bondDefinitions,
                _bondStates,
                _bondStates.Length,
                _brokenOutput);
            if (result.NewlyBrokenBondCount > 0)
            {
                _state.Phase = EarthStructurePhase.Fractured;
                _state.Revision++;
                _state.LastChangedTick = tick;
                SolveIslands();
            }
            else if (result.AccumulatedDamage > 0f)
            {
                _state.Phase = EarthStructurePhase.Damaged;
                _state.Revision++;
                _state.LastChangedTick = tick;
            }
            return result;
        }

        public void MarkBondBroken(int index, uint tick)
        {
            if (!IsConfigured || index < 0 || index >= _bondStates.Length ||
                _bondStates[index].Phase == EarthBondPhase.Broken)
            {
                return;
            }
            EarthBondState state = _bondStates[index];
            state.Phase = EarthBondPhase.Broken;
            state.AccumulatedDamage = 1f;
            state.LastChangedTick = tick;
            _bondStates[index] = state;
            _state.Phase = EarthStructurePhase.Fractured;
            _state.Revision++;
            _state.LastChangedTick = tick;
        }

        public void BreakPieceBonds(int pieceIndex, uint tick)
        {
            if (!IsConfigured || pieceIndex < 0 || pieceIndex >= _pieceStates.Length) return;
            for (int bondIndex = 0; bondIndex < _bondDefinitions.Length; bondIndex++)
            {
                EarthBondDefinition bond = _bondDefinitions[bondIndex];
                if (bond.PieceA == pieceIndex || bond.PieceB == pieceIndex)
                    MarkBondBroken(bondIndex, tick);
            }
            EarthPieceState piece = _pieceStates[pieceIndex];
            piece.Phase = EarthPiecePhase.Captured;
            piece.LastChangedTick = tick;
            _pieceStates[pieceIndex] = piece;
            SolveIslands();
        }

        public void SetPieceReleased(int pieceIndex, uint tick)
        {
            if (!IsConfigured || pieceIndex < 0 || pieceIndex >= _pieceStates.Length) return;
            EarthPieceState piece = _pieceStates[pieceIndex];
            piece.Phase = EarthPiecePhase.Dynamic;
            piece.LastChangedTick = tick;
            _pieceStates[pieceIndex] = piece;
        }

        public bool IsBondBroken(int index)
        {
            return index >= 0 && index < _bondStates.Length &&
                   _bondStates[index].Phase == EarthBondPhase.Broken;
        }

        public bool IsPieceSupported(int index)
        {
            if (index < 0 || index >= _islandByPiece.Length) return false;
            int island = _islandByPiece[index];
            return island >= 0 && island < _islandSupported.Length && _islandSupported[island];
        }

        public EarthBondRuntime GetBondRuntime(int index)
        {
            return index >= 0 && index < _bondRuntimes.Length ? _bondRuntimes[index] : null;
        }

        private void SolveIslands()
        {
            EarthIslandSolveResult result = EarthFractureBatchRunner.SolveIslands(
                _pieceDefinitions,
                _pieceStates,
                _pieceStates.Length,
                _bondDefinitions,
                _bondStates,
                _bondStates.Length,
                _islandByPiece,
                _islandSupported,
                _islandPieceCounts,
                _traversalQueue);
            _state.IslandCount = (ushort)result.IslandCount;
            _state.SupportedIslandCount = (ushort)result.SupportedIslandCount;
        }
    }
}
