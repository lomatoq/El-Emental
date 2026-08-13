using System.Collections.Generic;
using Elemental.Simulation.Structures;
using UnityEngine;

namespace Elemental.Authoring.Fracture
{
    public enum EarthFractureValidationError : byte
    {
        None,
        MissingAsset,
        UnsupportedSchema,
        MissingIntactProxy,
        GraphInvalid,
        MissingPieceMesh,
        ExcessiveColliderComplexity,
        NonManifoldPiece,
        MissingFaceMetadata,
        InvalidFaceSubmesh,
        MissingFaceVertexMask,
        MismatchedRestSeam,
        ImpossibleHierarchy,
        UnsupportedFoundation,
        DisconnectedIntactGraph
    }

    public readonly struct EarthFractureValidationResult
    {
        public EarthFractureValidationResult(
            EarthFractureValidationError error,
            int index,
            EarthGraphValidationError graphError = EarthGraphValidationError.None)
        {
            Error = error;
            Index = index;
            GraphError = graphError;
        }

        public EarthFractureValidationError Error { get; }
        public int Index { get; }
        public EarthGraphValidationError GraphError { get; }
        public bool IsValid => Error == EarthFractureValidationError.None;
    }

    public static class EarthFractureValidator
    {
        private const int MaximumConvexColliderVertices = 255;

        public static EarthFractureValidationResult Validate(EarthFractureAsset asset)
        {
            if (asset == null)
                return new EarthFractureValidationResult(EarthFractureValidationError.MissingAsset, -1);
            if (asset.SchemaVersion != EarthFractureAsset.CurrentSchemaVersion)
                return new EarthFractureValidationResult(EarthFractureValidationError.UnsupportedSchema, -1);
            if (asset.IntactRenderMesh == null || asset.IntactColliderMesh == null)
                return new EarthFractureValidationResult(EarthFractureValidationError.MissingIntactProxy, -1);

            var pieceDefinitions = new EarthPieceDefinition[asset.PieceCount];
            var bondDefinitions = new EarthBondDefinition[asset.BondCount];
            if (!asset.CopyDefinitions(pieceDefinitions, bondDefinitions))
                return new EarthFractureValidationResult(EarthFractureValidationError.GraphInvalid, -1);
            EarthGraphValidationResult graph = EarthBondGraph.Validate(
                pieceDefinitions, pieceDefinitions.Length, bondDefinitions, bondDefinitions.Length);
            if (!graph.IsValid)
            {
                return new EarthFractureValidationResult(
                    EarthFractureValidationError.GraphInvalid,
                    graph.Index,
                    graph.Error);
            }

            if (!HasValidHierarchy(pieceDefinitions))
                return new EarthFractureValidationResult(EarthFractureValidationError.ImpossibleHierarchy, -1);

            for (int pieceIndex = 0; pieceIndex < asset.PieceCount; pieceIndex++)
            {
                Mesh renderMesh = asset.GetPieceRenderMesh(pieceIndex);
                Mesh colliderMesh = asset.GetPieceColliderMesh(pieceIndex);
                if (renderMesh == null || colliderMesh == null)
                    return new EarthFractureValidationResult(EarthFractureValidationError.MissingPieceMesh, pieceIndex);
                if (colliderMesh.vertexCount > MaximumConvexColliderVertices)
                    return new EarthFractureValidationResult(
                        EarthFractureValidationError.ExcessiveColliderComplexity, pieceIndex);
                if (!IsClosedManifold(colliderMesh))
                    return new EarthFractureValidationResult(EarthFractureValidationError.NonManifoldPiece, pieceIndex);

                EarthPieceFaceMetadata faces = asset.GetPieceFaceMetadata(pieceIndex);
                EarthPieceFaceFlags required = EarthPieceFaceFlags.HasExterior | EarthPieceFaceFlags.HasInterior;
                if ((faces.Flags & required) != required)
                    return new EarthFractureValidationResult(EarthFractureValidationError.MissingFaceMetadata, pieceIndex);
                if (faces.ExteriorSubmesh >= renderMesh.subMeshCount ||
                    faces.InteriorSubmesh >= renderMesh.subMeshCount)
                {
                    return new EarthFractureValidationResult(EarthFractureValidationError.InvalidFaceSubmesh, pieceIndex);
                }
                if (!HasBakedFaceVertexMasks(renderMesh))
                    return new EarthFractureValidationResult(
                        EarthFractureValidationError.MissingFaceVertexMask, pieceIndex);
            }

            bool hasFoundation = false;
            for (int bondIndex = 0; bondIndex < bondDefinitions.Length; bondIndex++)
            {
                if (!BondCentroidTouchesRestPieces(asset, pieceDefinitions, bondDefinitions[bondIndex]))
                    return new EarthFractureValidationResult(
                        EarthFractureValidationError.MismatchedRestSeam, bondIndex);
                hasFoundation |= bondDefinitions[bondIndex].PieceB == EarthBondGraph.WorldPieceIndex;
            }
            if (!hasFoundation)
                return new EarthFractureValidationResult(EarthFractureValidationError.UnsupportedFoundation, -1);

            EarthPieceState[] pieceStates = new EarthPieceState[asset.PieceCount];
            EarthBondState[] bondStates = new EarthBondState[asset.BondCount];
            for (int index = 0; index < pieceStates.Length; index++) pieceStates[index] = EarthPieceState.Intact;
            for (int index = 0; index < bondStates.Length; index++) bondStates[index] = EarthBondState.Healthy;
            int[] islands = new int[asset.PieceCount];
            bool[] supported = new bool[asset.PieceCount];
            int[] counts = new int[asset.PieceCount];
            int[] queue = new int[asset.PieceCount];
            EarthIslandSolveResult solve = EarthIslandSolver.Solve(
                pieceDefinitions, pieceStates, pieceStates.Length,
                bondDefinitions, bondStates, bondStates.Length,
                islands, supported, counts, queue);
            if (solve.Status != EarthIslandSolveStatus.Success || solve.IslandCount != 1 ||
                solve.SupportedIslandCount != 1)
            {
                return new EarthFractureValidationResult(EarthFractureValidationError.DisconnectedIntactGraph, -1);
            }

            return new EarthFractureValidationResult(EarthFractureValidationError.None, -1);
        }

        private static bool HasBakedFaceVertexMasks(Mesh mesh)
        {
            Color32[] colors = mesh.colors32;
            if (colors == null || colors.Length != mesh.vertexCount) return false;
            bool exterior = false;
            bool interior = false;
            for (int index = 0; index < colors.Length; index++)
            {
                exterior |= colors[index].r >= 192 && colors[index].g <= 64;
                interior |= colors[index].g >= 192 && colors[index].r <= 64;
            }
            return exterior && interior;
        }

        private static bool IsClosedManifold(Mesh mesh)
        {
            int[] triangles = mesh.triangles;
            if (triangles == null || triangles.Length < 12 || triangles.Length % 3 != 0)
                return false;
            var edges = new Dictionary<ulong, int>(triangles.Length);
            for (int triangle = 0; triangle < triangles.Length; triangle += 3)
            {
                CountEdge(edges, triangles[triangle], triangles[triangle + 1]);
                CountEdge(edges, triangles[triangle + 1], triangles[triangle + 2]);
                CountEdge(edges, triangles[triangle + 2], triangles[triangle]);
            }
            foreach (KeyValuePair<ulong, int> edge in edges)
                if (edge.Value != 2) return false;
            return true;
        }

        private static bool HasValidHierarchy(EarthPieceDefinition[] pieces)
        {
            for (int pieceIndex = 0; pieceIndex < pieces.Length; pieceIndex++)
            {
                int current = pieceIndex;
                int depth = 0;
                while (current >= 0)
                {
                    if (++depth > pieces.Length) return false;
                    int parent = pieces[current].ParentPieceIndex;
                    if (parent < 0) break;
                    if (pieces[current].HierarchyLevel != pieces[parent].HierarchyLevel + 1)
                        return false;
                    current = parent;
                }
            }
            return true;
        }

        private static bool BondCentroidTouchesRestPieces(
            EarthFractureAsset asset,
            EarthPieceDefinition[] pieces,
            in EarthBondDefinition bond)
        {
            return RestPointTouchesMesh(asset, pieces, bond.PieceA, bond.LocalCentroid) &&
                   (bond.PieceB == EarthBondGraph.WorldPieceIndex ||
                    RestPointTouchesMesh(asset, pieces, bond.PieceB, bond.LocalCentroid));
        }

        private static bool RestPointTouchesMesh(
            EarthFractureAsset asset,
            EarthPieceDefinition[] pieces,
            int pieceIndex,
            Unity.Mathematics.float3 centroid)
        {
            if (pieceIndex < 0 || pieceIndex >= pieces.Length) return false;
            Mesh mesh = asset.GetPieceRenderMesh(pieceIndex);
            if (mesh == null) return false;
            EarthPieceDefinition piece = pieces[pieceIndex];
            Unity.Mathematics.quaternion rotation = piece.RestLocalRotation;
            Matrix4x4 rest = Matrix4x4.TRS(
                new Vector3(piece.RestLocalPosition.x, piece.RestLocalPosition.y, piece.RestLocalPosition.z),
                new Quaternion(rotation.value.x, rotation.value.y, rotation.value.z, rotation.value.w),
                new Vector3(piece.RestLocalScale.x, piece.RestLocalScale.y, piece.RestLocalScale.z));
            Vector3 localPoint = rest.inverse.MultiplyPoint3x4(
                new Vector3(centroid.x, centroid.y, centroid.z));
            Bounds bounds = mesh.bounds;
            bounds.Expand(0.08f);
            return bounds.Contains(localPoint);
        }

        private static void CountEdge(Dictionary<ulong, int> edges, int a, int b)
        {
            uint low = (uint)Mathf.Min(a, b);
            uint high = (uint)Mathf.Max(a, b);
            ulong key = ((ulong)low << 32) | high;
            edges.TryGetValue(key, out int count);
            edges[key] = count + 1;
        }
    }
}
