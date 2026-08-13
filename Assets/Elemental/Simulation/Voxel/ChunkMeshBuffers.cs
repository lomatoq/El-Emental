using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Elemental.Simulation.Voxel
{
    public sealed class ChunkMeshBuffers : IDisposable
    {
        public ChunkMeshBuffers(int initialVertexCapacity = 4096, int initialIndexCapacity = 6144)
        {
            Vertices = new NativeList<float3>(initialVertexCapacity, Allocator.Persistent);
            Normals = new NativeList<float3>(initialVertexCapacity, Allocator.Persistent);
            Indices = new NativeList<int>(initialIndexCapacity, Allocator.Persistent);
        }

        public NativeList<float3> Vertices { get; }
        public NativeList<float3> Normals { get; }
        public NativeList<int> Indices { get; }

        public void Clear()
        {
            Vertices.Clear();
            Normals.Clear();
            Indices.Clear();
        }

        public void Dispose()
        {
            if (Vertices.IsCreated)
            {
                Vertices.Dispose();
            }

            if (Normals.IsCreated)
            {
                Normals.Dispose();
            }

            if (Indices.IsCreated)
            {
                Indices.Dispose();
            }
        }
    }
}
