using System;
using Elemental.Simulation.Voxel;
using UnityEngine;

namespace Elemental.Runtime.World
{
    /// <summary>Disposable derived cache only. The analytic SDF and ordered edits remain authority.</summary>
    [PreferBinarySerialization]
    public sealed class PlanetBaseMeshCache : ScriptableObject
    {
        public const int CurrentRevision = 2;
        public const int FieldRevision = 1;
        public const int MesherRevision = 1;
        [Serializable] public struct Entry
        {
            public int X, Y, Z;
            public Mesh Mesh;
            public ulong ContentHash;
            public ChunkCoord Coord => new ChunkCoord(X, Y, Z);
        }
        [SerializeField] private int revision, fieldRevision, mesherRevision;
        [SerializeField] private float radius, cellSize, noiseAmplitude;
        [SerializeField] private uint seed;
        [SerializeField] private int resolution;
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        public System.Collections.Generic.IReadOnlyList<Entry> Entries => entries;
        public bool Matches(VoxelPlanetState state) => state != null && revision == CurrentRevision && fieldRevision == FieldRevision && mesherRevision == MesherRevision &&
            radius == state.Radius && seed == state.Seed && resolution == state.ChunkResolution &&
            cellSize == state.CellSize && noiseAmplitude == state.NoiseAmplitude;
        public void Configure(VoxelPlanetState state, Entry[] bakedEntries)
        {
            revision = CurrentRevision; fieldRevision = FieldRevision; mesherRevision = MesherRevision; radius = state.Radius; seed = state.Seed;
            resolution = state.ChunkResolution; cellSize = state.CellSize; noiseAmplitude = state.NoiseAmplitude;
            entries = bakedEntries != null ? (Entry[])bakedEntries.Clone() : throw new ArgumentNullException(nameof(bakedEntries));
        }
    }
}
