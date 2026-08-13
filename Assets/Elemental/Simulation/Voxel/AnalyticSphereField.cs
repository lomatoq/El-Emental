using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Voxel
{
    public sealed class AnalyticSphereField : IVoxelField
    {
        private readonly VoxelMaterialId _material;

        public AnalyticSphereField(float radius, uint seed, float noiseAmplitude = 0f, VoxelMaterialId material = default)
        {
            if (!math.isfinite(radius) || radius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            if (!math.isfinite(noiseAmplitude) || noiseAmplitude < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(noiseAmplitude));
            }

            Radius = radius;
            Seed = seed;
            NoiseAmplitude = noiseAmplitude;
            _material = material.IsEmpty ? new VoxelMaterialId(1) : material;
        }

        public float Radius { get; }
        public uint Seed { get; }
        public float NoiseAmplitude { get; }

        public SdfSample SampleDensityMaterial(float3 planetLocalPosition)
        {
            float distance = math.length(planetLocalPosition);
            float noise = NoiseAmplitude <= 0f ? 0f : SampleNoise(planetLocalPosition);
            float density = distance - (Radius + noise);
            return new SdfSample(density, density <= 0f ? _material : default);
        }

        private float SampleNoise(float3 position)
        {
            float3 frequency = new float3(0.173f, 0.137f, 0.191f);
            float seedPhase = (Seed & 0xFFFFu) * 0.00037f;
            float wave = math.sin(math.dot(position, frequency) + seedPhase);
            float secondary = math.sin(math.dot(position, frequency.zxy * 1.71f) - seedPhase * 0.7f);
            return (wave + secondary) * 0.5f * NoiseAmplitude;
        }
    }
}
