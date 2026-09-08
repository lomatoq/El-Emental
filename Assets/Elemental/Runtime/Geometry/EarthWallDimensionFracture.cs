using System;
using System.Collections.Generic;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Structures;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Geometry
{
    /// <summary>Cold wall construction cache. Voronoi distances are measured in metres.</summary>
    public sealed class EarthWallDimensionFracture : IEarthFractureAssetRuntimeData, IDisposable
    {
        private static readonly ProfilerMarker BuildMarker = new("Elemental.Earth.Wall.DimensionTopology");
        private readonly EarthPieceDefinition[] _pieces;
        private readonly EarthBondDefinition[] _bonds;
        private readonly Mesh[] _meshes;
        public Vector3 Dimensions { get; }
        public int SchemaVersion => 1;
        public Mesh IntactRenderMesh { get; }
        public Mesh IntactColliderMesh => IntactRenderMesh;
        public int PieceCount => _pieces.Length;
        public int BondCount => _bonds.Length;
        public Mesh GetPieceRenderMesh(int index) => _meshes[index];
        public Mesh GetPieceColliderMesh(int index) => _meshes[index];
        public EarthPieceFaceMetadata GetPieceFaceMetadata(int index) => new()
        { Flags = EarthPieceFaceFlags.HasExterior | EarthPieceFaceFlags.HasInterior, ExteriorSubmesh = 0, InteriorSubmesh = 0 };
        public bool CopyDefinitions(EarthPieceDefinition[] pieces, EarthBondDefinition[] bonds)
        {
            if (pieces == null || bonds == null || pieces.Length < PieceCount || bonds.Length < BondCount) return false;
            Array.Copy(_pieces, pieces, PieceCount); Array.Copy(_bonds, bonds, BondCount); return true;
        }

        public static EarthVolumetricFracturePlan BuildPlan(float3 dimensions, uint seed)
        {
            dimensions = math.max(dimensions, new float3(.25f, .1f, .05f));
            var boundary = new[] { new float2(-dimensions.x,-dimensions.z)*.5f,
                new float2(dimensions.x,-dimensions.z)*.5f, new float2(dimensions.x,dimensions.z)*.5f,
                new float2(-dimensions.x,dimensions.z)*.5f };
            // Preserve broad crown/chamfer planes, but solve the internal partition
            // anew. Narrower walls receive fewer cells instead of squeezed domains.
            var planes = new[] {
                new float4(.10f/dimensions.x,1f/dimensions.y,0,.5f),
                new float4(-.08f/dimensions.x,1f/dimensions.y,0,.49f),
                new float4(0,1f/dimensions.y,1f/dimensions.z,.95f),
                new float4(0,1f/dimensions.y,-1f/dimensions.z,.95f),
                new float4(1f/dimensions.x,0,1f/dimensions.z,.95f),
                new float4(1f/dimensions.x,0,-1f/dimensions.z,.95f),
                new float4(-1f/dimensions.x,0,1f/dimensions.z,.95f),
                new float4(-1f/dimensions.x,0,-1f/dimensions.z,.95f) };
            int count = math.clamp((int)math.round(dimensions.x * dimensions.y * 1.25f), 8, 64);
            // Depth pairing is meaningful for broad domains; narrow walls retain
            // natural full-depth blocks instead of artificially thin paired slices.
            return EarthVolumetricFractureSolver.BuildConvexPrism(seed, boundary,
                -dimensions.y*.5f, dimensions.y*.5f, count, planes, splitWallDepth: count >= 16);
        }

        public EarthWallDimensionFracture(Vector3 dimensions, uint seed)
        {
            using var sample = BuildMarker.Auto();
            Dimensions = dimensions;
            float3 d = new(dimensions.x, dimensions.y, dimensions.z);
            var plan = BuildPlan(d, seed);
            if (!plan.IsValid || !EarthVolumetricFractureSolver.HasClosedTopology(in plan))
                throw new InvalidOperationException($"Wall {dimensions} dimension topology failed volume/closed-cell validation.");
            _pieces = new EarthPieceDefinition[plan.Cells.Length];
            _meshes = new Mesh[plan.Cells.Length];
            var bonds = new List<EarthBondDefinition>();
            var shellVertices = new List<Vector3>(); var shellTriangles = new List<int>();
            for (int i = 0; i < plan.Cells.Length; i++)
            {
                var cell = plan.Cells[i];
                var points = new Vector3[cell.Vertices.Length];
                for (int v = 0; v < points.Length; v++) points[v] = V((cell.Vertices[v]-cell.Centroid)/d);
                var raw = new Mesh { name = $"Metric wall domain {i+1}" };
                raw.vertices = points; raw.triangles = cell.Triangles; raw.RecalculateNormals(); raw.RecalculateBounds();
                _meshes[i] = raw;
                _pieces[i] = new EarthPieceDefinition {
                    Id = new EarthPieceId((ushort)(i+1)), ParentPieceIndex = -1,
                    Flags = EarthPieceFlags.Structural | EarthPieceFlags.Repairable,
                    RestLocalPosition = cell.Centroid/d, RestLocalRotation = quaternion.identity,
                    RestLocalScale = new float3(1), Volume = cell.Volume/plan.SourceVolume,
                    Mass = cell.Volume/plan.SourceVolume*2600f, MaterialId = 1 };
                foreach (var face in cell.Faces)
                {
                    float3 centroid = float3.zero;
                    foreach (int v in face.VertexIndices) centroid += cell.Vertices[v]/d;
                    centroid /= math.max(1, face.VertexIndices.Length);
                    bool foundation = face.IsExterior && face.Normal.y < -.99f &&
                        math.abs(centroid.y + .5f) < .002f;
                    if (face.NeighbourCellIndex > i || foundation)
                    {
                        float area = 0f;
                        float3 a = cell.Vertices[face.VertexIndices[0]]/d;
                        for (int j=1;j+1<face.VertexIndices.Length;j++)
                            area += math.length(math.cross(cell.Vertices[face.VertexIndices[j]]/d-a,
                                cell.Vertices[face.VertexIndices[j+1]]/d-a))*.5f;
                        float root = math.sqrt(math.max(.04f,area))*(foundation ? 1.45f : 1f);
                        bonds.Add(new EarthBondDefinition { Id = new EarthBondId((ushort)(bonds.Count+1)),
                            PieceA=(short)i, PieceB=(short)(foundation ? -1 : face.NeighbourCellIndex),
                            Flags=EarthBondFlags.Repairable | (foundation ? EarthBondFlags.Foundation : EarthBondFlags.None),
                            LocalCentroid=centroid, LocalNormalA=math.normalizesafe(face.Normal*d),
                            ContactArea=math.max(.0001f,area), TensileStrength=root*10f,
                            ShearStrength=root*12.5f, CompressionStrength=root*35f });
                    }
                    if (!face.IsExterior) continue;
                    // Only external planes belong to the fresh shell. Internal
                    // triangulation has coplanar hard normals, no bevel/seam gaps.
                    int first = shellVertices.Count;
                    foreach (int v in face.VertexIndices) shellVertices.Add(V(cell.Vertices[v]/d));
                    for (int j=1;j+1<face.VertexIndices.Length;j++)
                    { shellTriangles.Add(first); shellTriangles.Add(first+j); shellTriangles.Add(first+j+1); }
                }
            }
            _bonds = bonds.ToArray();
            IntactRenderMesh = new Mesh { name = "Metric wall seamless exterior" };
            IntactRenderMesh.SetVertices(shellVertices); IntactRenderMesh.SetTriangles(shellTriangles,0);
            IntactRenderMesh.RecalculateNormals(); IntactRenderMesh.RecalculateBounds();
        }
        private static Vector3 V(float3 p) => new(p.x,p.y,p.z);
        public void Dispose()
        {
            foreach (var mesh in _meshes) Retire(mesh);
            Retire(IntactRenderMesh);
        }
        private static void Retire(Mesh mesh)
        { if (Application.isPlaying) UnityEngine.Object.Destroy(mesh); else UnityEngine.Object.DestroyImmediate(mesh); }
    }
}
