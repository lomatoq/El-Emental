using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Elemental.Simulation.Voxel
{
    /// <summary>
    /// Extracts a continuous zero-density surface from the canonical voxel field.
    /// The edit log remains voxel/SDF authority; this is only a replaceable mesh cache.
    /// </summary>
    public sealed class SmoothSdfSurfaceMesher : IChunkMesher, IDisposable
    {
        private static readonly int4[] Tetrahedra =
        {
            new int4(0, 5, 1, 6),
            new int4(0, 1, 2, 6),
            new int4(0, 2, 3, 6),
            new int4(0, 3, 7, 6),
            new int4(0, 7, 4, 6),
            new int4(0, 4, 5, 6)
        };

        private NativeArray<float> _densitySamples;
        private int _paddedSize;
        private int _resolution;

        public void Build(
            IVoxelField field,
            ChunkCoord coord,
            VoxelMeshingSettings settings,
            ChunkMeshBuffers output)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            if (output == null) throw new ArgumentNullException(nameof(output));

            output.Clear();
            float3 chunkOrigin = coord.GetPlanetLocalMin(settings.ChunkWorldSize);
            float cellSize = settings.CellSize;
            EnsureSampleStorage(settings.Resolution);
            for (int z = -1; z <= settings.Resolution + 1; z++)
            for (int y = -1; y <= settings.Resolution + 1; y++)
            for (int x = -1; x <= settings.Resolution + 1; x++)
            {
                float3 position = chunkOrigin + (new float3(x, y, z) * cellSize);
                _densitySamples[SampleIndex(x + 1, y + 1, z + 1)] =
                    field.SampleDensityMaterial(position).Density;
            }

            for (int z = 0; z < settings.Resolution; z++)
            {
                for (int y = 0; y < settings.Resolution; y++)
                {
                    for (int x = 0; x < settings.Resolution; x++)
                    {
                        float3 minimum = chunkOrigin + (new float3(x, y, z) * cellSize);
                        CornerSample s0 = BuildCornerSample(minimum, cellSize, x, y, z, 0);
                        CornerSample s1 = BuildCornerSample(minimum, cellSize, x, y, z, 1);
                        CornerSample s2 = BuildCornerSample(minimum, cellSize, x, y, z, 2);
                        CornerSample s3 = BuildCornerSample(minimum, cellSize, x, y, z, 3);
                        CornerSample s4 = BuildCornerSample(minimum, cellSize, x, y, z, 4);
                        CornerSample s5 = BuildCornerSample(minimum, cellSize, x, y, z, 5);
                        CornerSample s6 = BuildCornerSample(minimum, cellSize, x, y, z, 6);
                        CornerSample s7 = BuildCornerSample(minimum, cellSize, x, y, z, 7);

                        bool anyInside = s0.Density <= 0f || s1.Density <= 0f || s2.Density <= 0f || s3.Density <= 0f ||
                                         s4.Density <= 0f || s5.Density <= 0f || s6.Density <= 0f || s7.Density <= 0f;
                        bool anyOutside = s0.Density > 0f || s1.Density > 0f || s2.Density > 0f || s3.Density > 0f ||
                                          s4.Density > 0f || s5.Density > 0f || s6.Density > 0f || s7.Density > 0f;
                        if (!anyInside || !anyOutside) continue;

                        for (int index = 0; index < Tetrahedra.Length; index++)
                        {
                            int4 tetra = Tetrahedra[index];
                            PolygoniseTetrahedron(
                                output,
                                Select(tetra.x, s0, s1, s2, s3, s4, s5, s6, s7),
                                Select(tetra.y, s0, s1, s2, s3, s4, s5, s6, s7),
                                Select(tetra.z, s0, s1, s2, s3, s4, s5, s6, s7),
                                Select(tetra.w, s0, s1, s2, s3, s4, s5, s6, s7));
                        }
                    }
                }
            }
        }

        private static void PolygoniseTetrahedron(
            ChunkMeshBuffers output,
            CornerSample s0,
            CornerSample s1,
            CornerSample s2,
            CornerSample s3)
        {
            bool i0 = s0.Density <= 0f;
            bool i1 = s1.Density <= 0f;
            bool i2 = s2.Density <= 0f;
            bool i3 = s3.Density <= 0f;
            int insideCount = (i0 ? 1 : 0) + (i1 ? 1 : 0) + (i2 ? 1 : 0) + (i3 ? 1 : 0);
            if (insideCount == 0 || insideCount == 4) return;

            if (insideCount == 1 || insideCount == 3)
            {
                if (i0 == (insideCount == 1)) EmitCap(output, s0, s1, s2, s3);
                else if (i1 == (insideCount == 1)) EmitCap(output, s1, s0, s2, s3);
                else if (i2 == (insideCount == 1)) EmitCap(output, s2, s0, s1, s3);
                else EmitCap(output, s3, s0, s1, s2);
                return;
            }

            if (i0 && i1) EmitQuad(output, s0, s1, s2, s3);
            else if (i0 && i2) EmitQuad(output, s0, s2, s1, s3);
            else if (i0 && i3) EmitQuad(output, s0, s3, s1, s2);
            else if (i1 && i2) EmitQuad(output, s1, s2, s0, s3);
            else if (i1 && i3) EmitQuad(output, s1, s3, s0, s2);
            else EmitQuad(output, s2, s3, s0, s1);
        }

        private static void EmitCap(
            ChunkMeshBuffers output,
            CornerSample lone,
            CornerSample a,
            CornerSample b,
            CornerSample c)
        {
            SurfacePoint pa = Intersect(lone, a);
            SurfacePoint pb = Intersect(lone, b);
            SurfacePoint pc = Intersect(lone, c);
            AddTriangle(output, pa, pb, pc);
        }

        private static void EmitQuad(
            ChunkMeshBuffers output,
            CornerSample insideA,
            CornerSample insideB,
            CornerSample outsideA,
            CornerSample outsideB)
        {
            SurfacePoint ac = Intersect(insideA, outsideA);
            SurfacePoint ad = Intersect(insideA, outsideB);
            SurfacePoint bc = Intersect(insideB, outsideA);
            SurfacePoint bd = Intersect(insideB, outsideB);
            AddTriangle(output, ac, ad, bd);
            AddTriangle(output, ac, bd, bc);
        }

        private static SurfacePoint Intersect(
            CornerSample a,
            CornerSample b)
        {
            float denominator = a.Density - b.Density;
            float t = math.abs(denominator) <= 0.000001f ? 0.5f : math.saturate(a.Density / denominator);
            float3 position = math.lerp(a.Position, b.Position, t);
            float3 gradient = math.lerp(a.Gradient, b.Gradient, t);
            float3 fallback = math.normalizesafe(position, new float3(0f, 1f, 0f));
            return new SurfacePoint(position, math.normalizesafe(gradient, fallback));
        }

        private static void AddTriangle(ChunkMeshBuffers output, SurfacePoint a, SurfacePoint b, SurfacePoint c)
        {
            float3 faceNormal = math.cross(b.Position - a.Position, c.Position - a.Position);
            float3 intendedNormal = a.Normal + b.Normal + c.Normal;
            if (math.dot(faceNormal, intendedNormal) < 0f)
            {
                SurfacePoint swap = b;
                b = c;
                c = swap;
            }

            int baseIndex = output.Vertices.Length;
            output.Vertices.Add(a.Position);
            output.Vertices.Add(b.Position);
            output.Vertices.Add(c.Position);
            output.Normals.Add(a.Normal);
            output.Normals.Add(b.Normal);
            output.Normals.Add(c.Normal);
            output.Indices.Add(baseIndex);
            output.Indices.Add(baseIndex + 1);
            output.Indices.Add(baseIndex + 2);
        }

        private CornerSample BuildCornerSample(
            float3 minimum,
            float cellSize,
            int cellX,
            int cellY,
            int cellZ,
            int index)
        {
            int ox = index == 1 || index == 2 || index == 5 || index == 6 ? 1 : 0;
            int oy = index == 2 || index == 3 || index == 6 || index == 7 ? 1 : 0;
            int oz = index >= 4 ? 1 : 0;
            int gx = cellX + ox;
            int gy = cellY + oy;
            int gz = cellZ + oz;
            float3 position = minimum + (new float3(ox, oy, oz) * cellSize);
            float density = GridDensity(gx, gy, gz);
            float3 gradient = new float3(
                GridDensity(gx + 1, gy, gz) - GridDensity(gx - 1, gy, gz),
                GridDensity(gx, gy + 1, gz) - GridDensity(gx, gy - 1, gz),
                GridDensity(gx, gy, gz + 1) - GridDensity(gx, gy, gz - 1));
            return new CornerSample(position, density, gradient);
        }

        private float GridDensity(int x, int y, int z) =>
            _densitySamples[SampleIndex(x + 1, y + 1, z + 1)];

        private int SampleIndex(int x, int y, int z) => x + (_paddedSize * (y + (_paddedSize * z)));

        private void EnsureSampleStorage(int resolution)
        {
            int padded = resolution + 3;
            if (_densitySamples.IsCreated && _resolution == resolution) return;
            if (_densitySamples.IsCreated) _densitySamples.Dispose();
            _resolution = resolution;
            _paddedSize = padded;
            _densitySamples = new NativeArray<float>(
                padded * padded * padded,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
        }

        private static CornerSample Select(
            int index,
            CornerSample s0, CornerSample s1, CornerSample s2, CornerSample s3,
            CornerSample s4, CornerSample s5, CornerSample s6, CornerSample s7)
        {
            switch (index)
            {
                case 0: return s0;
                case 1: return s1;
                case 2: return s2;
                case 3: return s3;
                case 4: return s4;
                case 5: return s5;
                case 6: return s6;
                default: return s7;
            }
        }

        public void Dispose()
        {
            if (_densitySamples.IsCreated) _densitySamples.Dispose();
        }

        private readonly struct CornerSample
        {
            public CornerSample(float3 position, float density, float3 gradient)
            {
                Position = position;
                Density = density;
                Gradient = gradient;
            }

            public float3 Position { get; }
            public float Density { get; }
            public float3 Gradient { get; }
        }

        private readonly struct SurfacePoint
        {
            public SurfacePoint(float3 position, float3 normal)
            {
                Position = position;
                Normal = normal;
            }

            public float3 Position { get; }
            public float3 Normal { get; }
        }
    }
}
