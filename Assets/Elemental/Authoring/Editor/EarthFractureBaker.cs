using System;
using System.Collections.Generic;
using System.IO;
using Elemental.Authoring.Fracture;
using Elemental.Simulation.Structures;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    public static class EarthFractureBaker
    {
        public const string ProductionWallAssetPath =
            "Assets/Elemental/Content/Fracture/EarthWallFracture.asset";
        private const string DefaultWallMeshPath =
            "Assets/Elemental/Content/Meshes/ChippedEarthWall.asset";
        private const float AuthoredAspect = 1.65f;
        private const int LargeFullDepthCellCount = 5;
        private const uint ProductionSeed = 0xE17F0001u;

        [MenuItem("Elemental/Fracture/Bake Production Earth Wall")]
        public static void BakeProductionWallFromMenu()
        {
            Mesh wallMesh = AssetDatabase.LoadAssetAtPath<Mesh>(DefaultWallMeshPath);
            if (wallMesh == null)
                throw new UnityEditor.Build.BuildFailedException("Create the chipped wall mesh before fracture baking.");
            EarthFractureAsset asset = CreateOrLoadProductionWall(wallMesh, wallMesh);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log($"[Elemental] Validated baked wall fracture: {asset.PieceCount} pieces, {asset.BondCount} bonds.");
        }

        public static EarthFractureAsset CreateOrLoadProductionWall(
            Mesh intactRenderMesh,
            Mesh intactColliderMesh)
        {
            EarthFractureAsset asset = AssetDatabase.LoadAssetAtPath<EarthFractureAsset>(
                ProductionWallAssetPath);
            if (asset != null && EarthFractureValidator.Validate(asset).IsValid)
                return asset;

            if (intactRenderMesh == null || intactColliderMesh == null)
                throw new ArgumentNullException(nameof(intactRenderMesh));
            string folder = Path.GetDirectoryName(ProductionWallAssetPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
                CreateFolders(folder);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<EarthFractureAsset>();
                asset.name = "Earth Wall Fracture";
                AssetDatabase.CreateAsset(asset, ProductionWallAssetPath);
            }

            BakeInto(asset, intactRenderMesh, intactColliderMesh);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            EarthFractureValidationResult validation = EarthFractureValidator.Validate(asset);
            if (!validation.IsValid)
            {
                throw new UnityEditor.Build.BuildFailedException(
                    $"Baked wall fracture is invalid: {validation.Error} at {validation.Index} " +
                    $"(graph {validation.GraphError}).");
            }
            return asset;
        }

        private static void BakeInto(
            EarthFractureAsset asset,
            Mesh intactRenderMesh,
            Mesh intactColliderMesh)
        {
            RemoveOldPieceMeshes(asset);
            VoronoiFractureCell[] cells = VoronoiFractureSolver.BuildHierarchicalNormalizedForAspect(
                ProductionSeed, AuthoredAspect);
            int[] areaOrder = new int[cells.Length];
            bool[] fullDepth = new bool[cells.Length];
            for (int index = 0; index < areaOrder.Length; index++) areaOrder[index] = index;
            Array.Sort(areaOrder, (a, b) => cells[b].Area.CompareTo(cells[a].Area));
            for (int index = 0; index < Mathf.Min(LargeFullDepthCellCount, areaOrder.Length); index++)
                fullDepth[areaOrder[index]] = true;

            int pieceCount = LargeFullDepthCellCount + ((cells.Length - LargeFullDepthCellCount) * 2);
            var pieces = new EarthFracturePieceRecord[pieceCount];
            var slices = new List<BakedSlice>(pieceCount);
            int nextPiece = 0;
            for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
            {
                int depthLayers = fullDepth[cellIndex] ? 1 : 2;
                for (int depthIndex = 0; depthIndex < depthLayers; depthIndex++)
                {
                    float depthMin = Mathf.Lerp(-0.5f, 0.5f, depthIndex / (float)depthLayers);
                    float depthMax = Mathf.Lerp(-0.5f, 0.5f, (depthIndex + 1f) / depthLayers);
                    float depthCenter = (depthMin + depthMax) * 0.5f;
                    float depth = depthMax - depthMin;
                    int pieceIndex = nextPiece++;
                    PieceMeshPair meshes = BuildPieceMeshes(
                        cells[cellIndex], pieceIndex, depthMin - depthCenter, depthMax - depthCenter);
                    AssetDatabase.AddObjectToAsset(meshes.Render, asset);
                    AssetDatabase.AddObjectToAsset(meshes.Collider, asset);
                    float volume = Mathf.Max(0.0001f, cells[cellIndex].Area * depth);
                    pieces[pieceIndex] = new EarthFracturePieceRecord
                    {
                        id = (ushort)(pieceIndex + 1),
                        parentPieceIndex = EarthBondGraph.WorldPieceIndex,
                        hierarchyLevel = 0,
                        flags = EarthPieceFlags.Structural | EarthPieceFlags.Repairable |
                                (cells[cellIndex].Area >= 0.055f
                                    ? EarthPieceFlags.HeroPiece
                                    : EarthPieceFlags.None),
                        restLocalPosition = new Vector3(
                            cells[cellIndex].Centroid.x,
                            cells[cellIndex].Centroid.y,
                            depthCenter),
                        restLocalRotation = Quaternion.identity,
                        restLocalScale = Vector3.one,
                        mass = volume * 2600f,
                        volume = volume,
                        localCenterOfMass = Vector3.zero,
                        materialId = 1,
                        renderMesh = meshes.Render,
                        colliderMesh = meshes.Collider,
                        faceFlags = EarthPieceFaceFlags.HasExterior |
                                    EarthPieceFaceFlags.HasInterior |
                                    EarthPieceFaceFlags.HasMagicMask,
                        exteriorSubmesh = 0,
                        interiorSubmesh = 1,
                        magicMaskChannel = 2
                    };
                    slices.Add(new BakedSlice(
                        cellIndex, pieceIndex, depthMin, depthMax,
                        pieces[pieceIndex].restLocalPosition));
                }
            }

            var bonds = new List<EarthFractureBondRecord>(96);
            for (int first = 0; first < slices.Count; first++)
            {
                BakedSlice a = slices[first];
                for (int second = first + 1; second < slices.Count; second++)
                {
                    BakedSlice b = slices[second];
                    float area;
                    Vector3 centroid;
                    if (a.CellIndex == b.CellIndex)
                    {
                        bool adjacent = Mathf.Abs(a.DepthMax - b.DepthMin) < 0.001f ||
                                        Mathf.Abs(b.DepthMax - a.DepthMin) < 0.001f;
                        if (!adjacent) continue;
                        area = cells[a.CellIndex].Area;
                        centroid = new Vector3(
                            cells[a.CellIndex].Centroid.x,
                            cells[a.CellIndex].Centroid.y,
                            Mathf.Abs(a.DepthMax - b.DepthMin) < 0.001f ? a.DepthMax : b.DepthMax);
                    }
                    else
                    {
                        float depthOverlap = Mathf.Min(a.DepthMax, b.DepthMax) -
                                             Mathf.Max(a.DepthMin, b.DepthMin);
                        if (depthOverlap <= 0.0001f ||
                            !TryGetSharedEdge(cells[a.CellIndex], cells[b.CellIndex], out float2 e0, out float2 e1))
                        {
                            continue;
                        }
                        area = math.distance(e0, e1) * depthOverlap;
                        float2 edgeMid = (e0 + e1) * 0.5f;
                        centroid = new Vector3(
                            edgeMid.x,
                            edgeMid.y,
                            (Mathf.Max(a.DepthMin, b.DepthMin) + Mathf.Min(a.DepthMax, b.DepthMax)) * 0.5f);
                    }

                    Vector3 normal = (b.RestPosition - a.RestPosition).normalized;
                    AddBond(bonds, a.PieceIndex, b.PieceIndex, centroid, normal, area, false);
                }
            }

            for (int index = 0; index < slices.Count; index++)
            {
                BakedSlice slice = slices[index];
                if (!TouchesBottom(cells[slice.CellIndex])) continue;
                float depth = slice.DepthMax - slice.DepthMin;
                AddBond(
                    bonds,
                    slice.PieceIndex,
                    EarthBondGraph.WorldPieceIndex,
                    new Vector3(
                        cells[slice.CellIndex].Centroid.x,
                        -0.5f,
                        (slice.DepthMin + slice.DepthMax) * 0.5f),
                    Vector3.down,
                    Mathf.Max(0.02f, cells[slice.CellIndex].Area * depth),
                    true);
            }

            asset.SetBakedData(intactRenderMesh, intactColliderMesh, pieces, bonds.ToArray());
        }

        private static void AddBond(
            List<EarthFractureBondRecord> bonds,
            int pieceA,
            int pieceB,
            Vector3 centroid,
            Vector3 normal,
            float area,
            bool foundation)
        {
            float areaRoot = Mathf.Sqrt(Mathf.Max(0.04f, area));
            float foundationMultiplier = foundation ? 1.45f : 1f;
            bonds.Add(new EarthFractureBondRecord
            {
                id = (ushort)(bonds.Count + 1),
                pieceA = (short)pieceA,
                pieceB = (short)pieceB,
                flags = EarthBondFlags.Repairable |
                        (foundation ? EarthBondFlags.Foundation : EarthBondFlags.None),
                localCentroid = centroid,
                localNormalA = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.right,
                contactArea = Mathf.Max(0.0001f, area),
                tensileStrength = areaRoot * 10f * foundationMultiplier,
                shearStrength = areaRoot * 12.5f * foundationMultiplier,
                compressionStrength = areaRoot * 35f * foundationMultiplier
            });
        }

        private static PieceMeshPair BuildPieceMeshes(
            VoronoiFractureCell cell,
            int pieceIndex,
            float localDepthMin,
            float localDepthMax)
        {
            int count = cell.Vertices.Length;
            var vertices = new Vector3[count * 2];
            for (int index = 0; index < count; index++)
            {
                float x = cell.Vertices[index].x - cell.Centroid.x;
                float y = cell.Vertices[index].y - cell.Centroid.y;
                vertices[index] = new Vector3(x, y, localDepthMin);
                vertices[count + index] = new Vector3(x, y, localDepthMax);
            }

            var exterior = new int[(count - 2) * 6];
            int triangle = 0;
            for (int index = 1; index < count - 1; index++)
            {
                exterior[triangle++] = 0;
                exterior[triangle++] = index + 1;
                exterior[triangle++] = index;
                exterior[triangle++] = count;
                exterior[triangle++] = count + index;
                exterior[triangle++] = count + index + 1;
            }

            var interior = new int[count * 6];
            triangle = 0;
            for (int index = 0; index < count; index++)
            {
                int next = (index + 1) % count;
                interior[triangle++] = index;
                interior[triangle++] = next;
                interior[triangle++] = count + next;
                interior[triangle++] = index;
                interior[triangle++] = count + next;
                interior[triangle++] = count + index;
            }

            var collider = new Mesh { name = $"Earth Wall Collider {pieceIndex + 1:000}" };
            collider.vertices = vertices;
            collider.subMeshCount = 2;
            collider.SetTriangles(exterior, 0, false);
            collider.SetTriangles(interior, 1, false);
            collider.RecalculateNormals();
            collider.RecalculateBounds();

            int sideStart = count * 2;
            var renderVertices = new Vector3[count * 6];
            var colors = new Color32[renderVertices.Length];
            var uv = new Vector2[renderVertices.Length];
            var renderExterior = new int[(count - 2) * 6];
            var renderInterior = new int[count * 6];
            for (int index = 0; index < count; index++)
            {
                renderVertices[index] = vertices[index];
                renderVertices[count + index] = vertices[count + index];
                colors[index] = new Color32(255, 0, 0, 28);
                colors[count + index] = new Color32(255, 0, 0, 28);
                uv[index] = new Vector2(vertices[index].x, vertices[index].y);
                uv[count + index] = uv[index];
            }
            triangle = 0;
            for (int index = 1; index < count - 1; index++)
            {
                renderExterior[triangle++] = 0;
                renderExterior[triangle++] = index + 1;
                renderExterior[triangle++] = index;
                renderExterior[triangle++] = count;
                renderExterior[triangle++] = count + index;
                renderExterior[triangle++] = count + index + 1;
            }
            triangle = 0;
            for (int index = 0; index < count; index++)
            {
                int next = (index + 1) % count;
                int vertex = sideStart + index * 4;
                renderVertices[vertex] = vertices[index];
                renderVertices[vertex + 1] = vertices[next];
                renderVertices[vertex + 2] = vertices[count + next];
                renderVertices[vertex + 3] = vertices[count + index];
                byte cavity = (byte)Mathf.RoundToInt(Mathf.Lerp(118f, 186f, Hash01((uint)(pieceIndex + 1), index)));
                colors[vertex] = colors[vertex + 1] = colors[vertex + 2] = colors[vertex + 3] =
                    new Color32(0, 255, 0, cavity);
                float edgeLength = Vector3.Distance(vertices[index], vertices[next]);
                uv[vertex] = new Vector2(0f, 0f);
                uv[vertex + 1] = new Vector2(edgeLength, 0f);
                uv[vertex + 2] = new Vector2(edgeLength, localDepthMax - localDepthMin);
                uv[vertex + 3] = new Vector2(0f, localDepthMax - localDepthMin);
                renderInterior[triangle++] = vertex;
                renderInterior[triangle++] = vertex + 1;
                renderInterior[triangle++] = vertex + 2;
                renderInterior[triangle++] = vertex;
                renderInterior[triangle++] = vertex + 2;
                renderInterior[triangle++] = vertex + 3;
            }

            var render = new Mesh { name = $"Earth Wall Baked Piece {pieceIndex + 1:000}" };
            render.vertices = renderVertices;
            render.colors32 = colors;
            render.uv = uv;
            render.subMeshCount = 2;
            render.SetTriangles(renderExterior, 0, false);
            render.SetTriangles(renderInterior, 1, false);
            render.RecalculateNormals();
            render.RecalculateTangents();
            render.RecalculateBounds();
            return new PieceMeshPair(render, collider);
        }

        private static void RemoveOldPieceMeshes(EarthFractureAsset asset)
        {
            EarthFracturePieceRecord[] records = asset.PieceRecords;
            if (records == null || records.Length == 0) return;
            var removed = new HashSet<Mesh>();
            for (int index = 0; index < records.Length; index++)
            {
                RemoveSubAsset(records[index].renderMesh, asset, removed);
                RemoveSubAsset(records[index].colliderMesh, asset, removed);
            }
        }

        private static void RemoveSubAsset(Mesh mesh, EarthFractureAsset owner, HashSet<Mesh> removed)
        {
            if (mesh == null || mesh == owner.IntactRenderMesh || mesh == owner.IntactColliderMesh ||
                !AssetDatabase.IsSubAsset(mesh) || !removed.Add(mesh)) return;
            UnityEngine.Object.DestroyImmediate(mesh, true);
        }

        private static float Hash01(uint seed, int index)
        {
            uint value = seed ^ ((uint)(index + 1) * 0x9E3779B9u);
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }

        private static bool TryGetSharedEdge(
            VoronoiFractureCell a,
            VoronoiFractureCell b,
            out float2 edgeA,
            out float2 edgeB)
        {
            const float toleranceSq = 0.00008f * 0.00008f;
            for (int first = 0; first < a.Vertices.Length; first++)
            {
                float2 a0 = a.Vertices[first];
                float2 a1 = a.Vertices[(first + 1) % a.Vertices.Length];
                for (int second = 0; second < b.Vertices.Length; second++)
                {
                    float2 b0 = b.Vertices[second];
                    float2 b1 = b.Vertices[(second + 1) % b.Vertices.Length];
                    bool same = (math.distancesq(a0, b1) <= toleranceSq &&
                                 math.distancesq(a1, b0) <= toleranceSq) ||
                                (math.distancesq(a0, b0) <= toleranceSq &&
                                 math.distancesq(a1, b1) <= toleranceSq);
                    if (!same) continue;
                    edgeA = a0;
                    edgeB = a1;
                    return true;
                }
            }
            edgeA = default;
            edgeB = default;
            return false;
        }

        private static bool TouchesBottom(VoronoiFractureCell cell)
        {
            for (int index = 0; index < cell.Vertices.Length; index++)
                if (cell.Vertices[index].y <= -0.499f) return true;
            return false;
        }

        private static void CreateFolders(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        private readonly struct BakedSlice
        {
            public BakedSlice(
                int cellIndex,
                int pieceIndex,
                float depthMin,
                float depthMax,
                Vector3 restPosition)
            {
                CellIndex = cellIndex;
                PieceIndex = pieceIndex;
                DepthMin = depthMin;
                DepthMax = depthMax;
                RestPosition = restPosition;
            }

            public int CellIndex { get; }
            public int PieceIndex { get; }
            public float DepthMin { get; }
            public float DepthMax { get; }
            public Vector3 RestPosition { get; }
        }

        private readonly struct PieceMeshPair
        {
            public PieceMeshPair(Mesh render, Mesh collider)
            {
                Render = render;
                Collider = collider;
            }

            public Mesh Render { get; }
            public Mesh Collider { get; }
        }
    }
}
