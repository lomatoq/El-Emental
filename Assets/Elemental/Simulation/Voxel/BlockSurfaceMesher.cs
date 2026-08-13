using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Voxel
{
    public sealed class BlockSurfaceMesher : IChunkMesher
    {
        private static readonly int3[] NeighborDirections =
        {
            new int3(1, 0, 0),
            new int3(-1, 0, 0),
            new int3(0, 1, 0),
            new int3(0, -1, 0),
            new int3(0, 0, 1),
            new int3(0, 0, -1)
        };

        public void Build(
            IVoxelField field,
            ChunkCoord coord,
            VoxelMeshingSettings settings,
            ChunkMeshBuffers output)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            output.Clear();
            float3 chunkOrigin = coord.GetPlanetLocalMin(settings.ChunkWorldSize);

            for (int z = 0; z < settings.Resolution; z++)
            {
                for (int y = 0; y < settings.Resolution; y++)
                {
                    for (int x = 0; x < settings.Resolution; x++)
                    {
                        int3 cell = new int3(x, y, z);
                        float3 center = chunkOrigin + ((new float3(cell) + 0.5f) * settings.CellSize);
                        if (!field.SampleDensityMaterial(center).IsSolid)
                        {
                            continue;
                        }

                        for (int face = 0; face < NeighborDirections.Length; face++)
                        {
                            float3 neighborCenter = center + (new float3(NeighborDirections[face]) * settings.CellSize);
                            if (field.SampleDensityMaterial(neighborCenter).IsSolid)
                            {
                                continue;
                            }

                            AddFace(output, chunkOrigin, cell, face, settings.CellSize);
                        }
                    }
                }
            }
        }

        private static void AddFace(
            ChunkMeshBuffers output,
            float3 chunkOrigin,
            int3 cell,
            int face,
            float cellSize)
        {
            float3 min = chunkOrigin + (new float3(cell) * cellSize);
            float3 max = min + cellSize;
            float3 v0;
            float3 v1;
            float3 v2;
            float3 v3;
            float3 normal;

            switch (face)
            {
                case 0:
                    normal = new float3(1f, 0f, 0f);
                    v0 = new float3(max.x, min.y, min.z);
                    v1 = new float3(max.x, max.y, min.z);
                    v2 = new float3(max.x, max.y, max.z);
                    v3 = new float3(max.x, min.y, max.z);
                    break;
                case 1:
                    normal = new float3(-1f, 0f, 0f);
                    v0 = new float3(min.x, min.y, max.z);
                    v1 = new float3(min.x, max.y, max.z);
                    v2 = new float3(min.x, max.y, min.z);
                    v3 = new float3(min.x, min.y, min.z);
                    break;
                case 2:
                    normal = new float3(0f, 1f, 0f);
                    v0 = new float3(min.x, max.y, max.z);
                    v1 = new float3(max.x, max.y, max.z);
                    v2 = new float3(max.x, max.y, min.z);
                    v3 = new float3(min.x, max.y, min.z);
                    break;
                case 3:
                    normal = new float3(0f, -1f, 0f);
                    v0 = new float3(min.x, min.y, min.z);
                    v1 = new float3(max.x, min.y, min.z);
                    v2 = new float3(max.x, min.y, max.z);
                    v3 = new float3(min.x, min.y, max.z);
                    break;
                case 4:
                    normal = new float3(0f, 0f, 1f);
                    v0 = new float3(max.x, min.y, max.z);
                    v1 = new float3(max.x, max.y, max.z);
                    v2 = new float3(min.x, max.y, max.z);
                    v3 = new float3(min.x, min.y, max.z);
                    break;
                default:
                    normal = new float3(0f, 0f, -1f);
                    v0 = new float3(min.x, min.y, min.z);
                    v1 = new float3(min.x, max.y, min.z);
                    v2 = new float3(max.x, max.y, min.z);
                    v3 = new float3(max.x, min.y, min.z);
                    break;
            }

            int baseIndex = output.Vertices.Length;
            output.Vertices.Add(v0);
            output.Vertices.Add(v1);
            output.Vertices.Add(v2);
            output.Vertices.Add(v3);
            output.Normals.Add(normal);
            output.Normals.Add(normal);
            output.Normals.Add(normal);
            output.Normals.Add(normal);
            output.Indices.Add(baseIndex);
            output.Indices.Add(baseIndex + 1);
            output.Indices.Add(baseIndex + 2);
            output.Indices.Add(baseIndex);
            output.Indices.Add(baseIndex + 2);
            output.Indices.Add(baseIndex + 3);
        }
    }
}
