using Elemental.Simulation.Capabilities;
using UnityEngine;

namespace Elemental.Runtime.World
{
    [CreateAssetMenu(menuName = "Elemental/World/Planet World Profile", fileName = "PlanetWorldProfile")]
    public sealed class PlanetWorldProfile : ScriptableObject
    {
        [Header("Planet")]
        [SerializeField, Range(12f, 80f)] private float radius = 24f;
        [SerializeField, Min(0.1f)] private float surfaceGravity = 14f;
        [SerializeField] private uint seed = 0xE1E0u;
        [SerializeField, Min(0f)] private float noiseAmplitude = 0.35f;
        [Header("Voxel caches")]
        [SerializeField, Range(4, 32)] private int chunkResolution = 16;
        [SerializeField, Min(0.1f)] private float cellSize = 1f;
        [SerializeField, Min(1)] private int renderChunksPerFrame = 1;
        [SerializeField, Min(1)] private int colliderChunksPerFrame = 1;

        public float Radius => radius;
        public float SurfaceGravity => surfaceGravity;
        public uint Seed => seed;
        public float NoiseAmplitude => noiseAmplitude;
        public int ChunkResolution => chunkResolution;
        public float CellSize => cellSize;
        public int RenderChunksPerFrame => renderChunksPerFrame;
        public int ColliderChunksPerFrame => colliderChunksPerFrame;
        public float ChunkWorldSize => chunkResolution * cellSize;

        public float MaximumRadius(CapabilityProfileKind kind)
        {
            switch (kind)
            {
                case CapabilityProfileKind.WebLab: return 24f;
                case CapabilityProfileKind.NativeLow: return 48f;
                default: return 80f;
            }
        }

        public bool Validate(CapabilityProfileKind kind, out string reason)
        {
            if (radius < 12f || radius > MaximumRadius(kind))
            {
                reason = $"Planet radius {radius:0.#} m exceeds the {kind} range (12-{MaximumRadius(kind):0.#} m).";
                return false;
            }
            if (ChunkWorldSize <= noiseAmplitude * 2f)
            {
                reason = "Chunk world size must remain larger than the complete noise shell.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        public void ConfigureForTests(float configuredRadius, float configuredGravity, uint configuredSeed, float configuredNoise, int configuredResolution, float configuredCellSize, int renderBudget, int colliderBudget)
        {
            radius = configuredRadius;
            surfaceGravity = configuredGravity;
            seed = configuredSeed;
            noiseAmplitude = configuredNoise;
            chunkResolution = configuredResolution;
            cellSize = configuredCellSize;
            renderChunksPerFrame = renderBudget;
            colliderChunksPerFrame = colliderBudget;
        }
    }
}
