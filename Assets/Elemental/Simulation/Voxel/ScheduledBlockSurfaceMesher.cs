using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Elemental.Simulation.Voxel
{
    public sealed class ScheduledBlockSurfaceMesher
    {
        public ScheduledChunkMeshBuild Schedule(
            IVoxelField field,
            ChunkCoord coord,
            VoxelMeshingSettings settings,
            ChunkMeshBuffers output,
            uint expectedVersion)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            if (output == null) throw new ArgumentNullException(nameof(output));

            int padded = settings.Resolution + 2;
            var solid = new NativeArray<byte>(padded * padded * padded, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            float3 origin = coord.GetPlanetLocalMin(settings.ChunkWorldSize);
            for (int z = -1; z <= settings.Resolution; z++)
            for (int y = -1; y <= settings.Resolution; y++)
            for (int x = -1; x <= settings.Resolution; x++)
            {
                float3 center = origin + ((new float3(x, y, z) + 0.5f) * settings.CellSize);
                solid[Index(x + 1, y + 1, z + 1, padded)] = field.SampleDensityMaterial(center).IsSolid ? (byte)1 : (byte)0;
            }

            output.Clear();
            var job = new BlockSurfaceJob
            {
                Solid = solid,
                Resolution = settings.Resolution,
                PaddedResolution = padded,
                CellSize = settings.CellSize,
                Origin = origin,
                Vertices = output.Vertices,
                Normals = output.Normals,
                Indices = output.Indices
            };
            JobHandle handle = job.Schedule();
            return new ScheduledChunkMeshBuild(handle, solid, output, expectedVersion);
        }

        private static int Index(int x, int y, int z, int size) => x + (size * (y + (size * z)));

        [BurstCompile]
        private struct BlockSurfaceJob : IJob
        {
            [ReadOnly] public NativeArray<byte> Solid;
            public int Resolution;
            public int PaddedResolution;
            public float CellSize;
            public float3 Origin;
            public NativeList<float3> Vertices;
            public NativeList<float3> Normals;
            public NativeList<int> Indices;

            public void Execute()
            {
                for (int z = 0; z < Resolution; z++)
                for (int y = 0; y < Resolution; y++)
                for (int x = 0; x < Resolution; x++)
                {
                    int px = x + 1;
                    int py = y + 1;
                    int pz = z + 1;
                    if (Solid[Index(px, py, pz)] == 0) continue;
                    if (Solid[Index(px + 1, py, pz)] == 0) AddFace(x, y, z, 0);
                    if (Solid[Index(px - 1, py, pz)] == 0) AddFace(x, y, z, 1);
                    if (Solid[Index(px, py + 1, pz)] == 0) AddFace(x, y, z, 2);
                    if (Solid[Index(px, py - 1, pz)] == 0) AddFace(x, y, z, 3);
                    if (Solid[Index(px, py, pz + 1)] == 0) AddFace(x, y, z, 4);
                    if (Solid[Index(px, py, pz - 1)] == 0) AddFace(x, y, z, 5);
                }
            }

            private int Index(int x, int y, int z) => x + (PaddedResolution * (y + (PaddedResolution * z)));

            private void AddFace(int x, int y, int z, int face)
            {
                float3 min = Origin + (new float3(x, y, z) * CellSize);
                float3 max = min + CellSize;
                float3 v0;
                float3 v1;
                float3 v2;
                float3 v3;
                float3 normal;
                switch (face)
                {
                    case 0:
                        normal = new float3(1f, 0f, 0f); v0 = new float3(max.x, min.y, min.z);
                        v1 = new float3(max.x, max.y, min.z); v2 = new float3(max.x, max.y, max.z); v3 = new float3(max.x, min.y, max.z); break;
                    case 1:
                        normal = new float3(-1f, 0f, 0f); v0 = new float3(min.x, min.y, max.z);
                        v1 = new float3(min.x, max.y, max.z); v2 = new float3(min.x, max.y, min.z); v3 = new float3(min.x, min.y, min.z); break;
                    case 2:
                        normal = new float3(0f, 1f, 0f); v0 = new float3(min.x, max.y, max.z);
                        v1 = new float3(max.x, max.y, max.z); v2 = new float3(max.x, max.y, min.z); v3 = new float3(min.x, max.y, min.z); break;
                    case 3:
                        normal = new float3(0f, -1f, 0f); v0 = new float3(min.x, min.y, min.z);
                        v1 = new float3(max.x, min.y, min.z); v2 = new float3(max.x, min.y, max.z); v3 = new float3(min.x, min.y, max.z); break;
                    case 4:
                        normal = new float3(0f, 0f, 1f); v0 = new float3(max.x, min.y, max.z);
                        v1 = new float3(max.x, max.y, max.z); v2 = new float3(min.x, max.y, max.z); v3 = new float3(min.x, min.y, max.z); break;
                    default:
                        normal = new float3(0f, 0f, -1f); v0 = new float3(min.x, min.y, min.z);
                        v1 = new float3(min.x, max.y, min.z); v2 = new float3(max.x, max.y, min.z); v3 = new float3(max.x, min.y, min.z); break;
                }

                int baseIndex = Vertices.Length;
                Vertices.Add(v0); Vertices.Add(v1); Vertices.Add(v2); Vertices.Add(v3);
                Normals.Add(normal); Normals.Add(normal); Normals.Add(normal); Normals.Add(normal);
                Indices.Add(baseIndex); Indices.Add(baseIndex + 1); Indices.Add(baseIndex + 2);
                Indices.Add(baseIndex); Indices.Add(baseIndex + 2); Indices.Add(baseIndex + 3);
            }
        }
    }

    public sealed class ScheduledChunkMeshBuild : IDisposable
    {
        private JobHandle _handle;
        private NativeArray<byte> _samples;
        private readonly ChunkMeshBuffers _output;
        private bool _completed;

        internal ScheduledChunkMeshBuild(JobHandle handle, NativeArray<byte> samples, ChunkMeshBuffers output, uint expectedVersion)
        {
            _handle = handle;
            _samples = samples;
            _output = output;
            ExpectedVersion = expectedVersion;
        }

        public uint ExpectedVersion { get; }
        public bool IsCompleted => _handle.IsCompleted;

        public bool Complete(uint currentVersion)
        {
            if (_completed) throw new InvalidOperationException("Scheduled mesh build was already completed.");
            _handle.Complete();
            _samples.Dispose();
            _completed = true;
            if (currentVersion == ExpectedVersion) return true;
            _output.Clear();
            return false;
        }

        public void Dispose()
        {
            if (_completed) return;
            _handle.Complete();
            if (_samples.IsCreated) _samples.Dispose();
            _output.Clear();
            _completed = true;
        }
    }
}
