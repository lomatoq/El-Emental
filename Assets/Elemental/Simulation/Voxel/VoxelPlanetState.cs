using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Elemental.Simulation.Voxel
{
    public sealed class VoxelPlanetState : IVoxelField
    {
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        private readonly AnalyticSphereField _baseField;
        private readonly List<SdfEdit> _edits;
        private uint _lastSequence;

        public VoxelPlanetState(
            float radius,
            uint seed,
            int chunkResolution = 16,
            float cellSize = 1f,
            float noiseAmplitude = 0f)
        {
            if (chunkResolution <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkResolution));
            }

            if (!math.isfinite(cellSize) || cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            }

            Radius = radius;
            Seed = seed;
            ChunkResolution = chunkResolution;
            CellSize = cellSize;
            NoiseAmplitude = noiseAmplitude;
            _baseField = new AnalyticSphereField(radius, seed, noiseAmplitude);
            _edits = new List<SdfEdit>(64);
            Chunks = new ChunkStore();
        }

        public float Radius { get; }
        public uint Seed { get; }
        public int ChunkResolution { get; }
        public float CellSize { get; }
        public float NoiseAmplitude { get; }
        public float ChunkWorldSize => ChunkResolution * CellSize;
        public int EditCount => _edits.Count;
        public ChunkStore Chunks { get; }

        public SdfEdit GetEdit(int index)
        {
            return _edits[index];
        }

        public void Apply(EditBatch batch)
        {
            if (batch == null)
            {
                throw new ArgumentNullException(nameof(batch));
            }

            for (int index = 0; index < batch.Count; index++)
            {
                SdfEdit edit = batch[index];
                if (_edits.Count > 0 && edit.Sequence <= _lastSequence)
                {
                    throw new InvalidOperationException(
                        $"Edit sequence {edit.Sequence} must be greater than {_lastSequence}.");
                }

                _edits.Add(edit);
                _lastSequence = edit.Sequence;
                Chunks.MarkDirty(edit.GetBounds(), ChunkWorldSize);
            }
        }

        public SdfSample SampleDensityMaterial(float3 planetLocalPosition)
        {
            SdfSample sample = _baseField.SampleDensityMaterial(planetLocalPosition);
            float density = sample.Density;
            VoxelMaterialId material = sample.Material;

            for (int index = 0; index < _edits.Count; index++)
            {
                SdfEdit edit = _edits[index];
                float shapeDistance = edit.SampleShapeDistance(planetLocalPosition);
                if (edit.IsAdditive)
                {
                    if (shapeDistance < density)
                    {
                        density = shapeDistance;
                        material = edit.Material;
                    }
                }
                else
                {
                    float carvedDensity = -shapeDistance;
                    if (carvedDensity > density)
                    {
                        density = carvedDensity;
                        if (density > 0f)
                        {
                            material = default;
                        }
                    }
                }
            }

            return new SdfSample(density, density <= 0f ? material : default);
        }

        public ulong ComputeChunkHash(ChunkCoord coord)
        {
            ulong hash = FnvOffset;
            float3 origin = coord.GetPlanetLocalMin(ChunkWorldSize);

            unchecked
            {
                for (int z = 0; z < ChunkResolution; z++)
                {
                    for (int y = 0; y < ChunkResolution; y++)
                    {
                        for (int x = 0; x < ChunkResolution; x++)
                        {
                            float3 position = origin + (new float3(x + 0.5f, y + 0.5f, z + 0.5f) * CellSize);
                            SdfSample sample = SampleDensityMaterial(position);
                            int quantizedDensity = (int)math.round(sample.Density * 1024f);
                            hash ^= (uint)quantizedDensity;
                            hash *= FnvPrime;
                            hash ^= sample.Material.Value;
                            hash *= FnvPrime;
                        }
                    }
                }
            }

            return hash;
        }
    }
}
