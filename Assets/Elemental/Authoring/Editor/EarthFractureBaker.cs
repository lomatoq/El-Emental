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
        // Solve in representative metres, then normalize into the authored wall.
        // Voronoi distance in a unit cube made every chunk inherit the whole wall's
        // aspect ratio after runtime scaling, which is another form of "straw".
        private const float AuthoredWidth = 8f;
        private const float AuthoredHeight = 4f;
        private const float AuthoredDepth = 0.55f;
        private const int VolumetricCellCount = 40;
        private const uint ProductionSeed = 0xE17F1002u;

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

        [MenuItem("Elemental/Fracture/Bake Selected Destructible")]
        public static void BakeSelectedDestructibleFromMenu()
        {
            GameObject selected = Selection.activeGameObject;
            MeshFilter filter = selected != null ? selected.GetComponent<MeshFilter>() : null;
            MeshCollider meshCollider = selected != null ? selected.GetComponent<MeshCollider>() : null;
            Mesh renderMesh = filter != null ? filter.sharedMesh : null;
            Mesh collisionMesh = meshCollider != null && meshCollider.sharedMesh != null
                ? meshCollider.sharedMesh
                : renderMesh;
            if (selected == null || renderMesh == null)
                throw new UnityEditor.Build.BuildFailedException(
                    "Select one GameObject with a MeshFilter before fracture baking.");

            string preset = InferSelectedPreset(renderMesh.bounds.size);
            const string folder = "Assets/Elemental/Content/Fracture/Selected";
            if (!AssetDatabase.IsValidFolder(folder)) CreateFolders(folder);
            string safeName = string.Concat(selected.name.Split(Path.GetInvalidFileNameChars()));
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{folder}/{safeName}_{preset}Fracture.asset");
            var asset = ScriptableObject.CreateInstance<EarthFractureAsset>();
            asset.name = $"{selected.name} {preset} Fracture";
            AssetDatabase.CreateAsset(asset, assetPath);
            BakeInto(asset, renderMesh, collisionMesh);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            EarthFractureValidationResult validation = EarthFractureValidator.Validate(asset);
            if (!validation.IsValid)
                throw new UnityEditor.Build.BuildFailedException(
                    $"Selected fracture is invalid: {validation.Error} at {validation.Index}.");
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log(
                $"[Elemental] Baked selected {preset} destructible: {asset.PieceCount} pieces, " +
                $"{asset.BondCount} bonds at {assetPath}.");
        }

        [MenuItem("Elemental/Fracture/Bake Selected Destructible", true)]
        private static bool ValidateBakeSelectedDestructible() =>
            Selection.activeGameObject != null &&
            Selection.activeGameObject.GetComponent<MeshFilter>()?.sharedMesh != null;

        private static string InferSelectedPreset(Vector3 size)
        {
            Vector3 safe = new Vector3(
                Mathf.Max(0.001f, Mathf.Abs(size.x)),
                Mathf.Max(0.001f, Mathf.Abs(size.y)),
                Mathf.Max(0.001f, Mathf.Abs(size.z)));
            float longest = Mathf.Max(safe.x, Mathf.Max(safe.y, safe.z));
            float shortest = Mathf.Min(safe.x, Mathf.Min(safe.y, safe.z));
            if (safe.y <= longest * 0.34f && safe.x > safe.y * 1.4f && safe.z > safe.y * 1.4f)
                return "Platform";
            if (longest / shortest <= 1.8f) return "Boulder";
            return "Wall";
        }

        public static EarthFractureAsset CreateOrLoadProductionWall(
            Mesh intactRenderMesh,
            Mesh intactColliderMesh)
        {
            EarthFractureAsset asset = AssetDatabase.LoadAssetAtPath<EarthFractureAsset>(
                ProductionWallAssetPath);
            if (asset != null && asset.PieceCount == VolumetricCellCount &&
                HasProductionShapeQuality(asset) &&
                EarthFractureValidator.Validate(asset).IsValid)
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
            float2[] physicalBoundary =
            {
                new float2(-AuthoredWidth * 0.5f, -AuthoredDepth * 0.5f),
                new float2(AuthoredWidth * 0.5f, -AuthoredDepth * 0.5f),
                new float2(AuthoredWidth * 0.5f, AuthoredDepth * 0.5f),
                new float2(-AuthoredWidth * 0.5f, AuthoredDepth * 0.5f)
            };
            EarthVolumetricFracturePlan plan = BuildProductionPlan(physicalBoundary);
            if (!plan.IsValid || plan.Cells.Length != VolumetricCellCount)
                throw new UnityEditor.Build.BuildFailedException(
                    $"Volumetric wall fracture failed conservation: {plan.RelativeVolumeError:P2}.");

            var pieces = new EarthFracturePieceRecord[plan.Cells.Length];
            float volumeScale = 1f / Mathf.Max(0.0001f, plan.SourceVolume);
            var intactNormalSampler = new IntactSurfaceNormalSampler(intactRenderMesh);
            for (int pieceIndex = 0; pieceIndex < plan.Cells.Length; pieceIndex++)
            {
                EarthVolumetricFractureCell cell = plan.Cells[pieceIndex];
                PieceMeshPair meshes = BuildPieceMeshes(cell, pieceIndex, intactNormalSampler);
                AssetDatabase.AddObjectToAsset(meshes.Render, asset);
                AssetDatabase.AddObjectToAsset(meshes.Collider, asset);
                UnityEngine.Physics.BakeMesh(meshes.Collider.GetEntityId(), true);
                float volume = Mathf.Max(0.0001f, cell.Volume * volumeScale);
                pieces[pieceIndex] = new EarthFracturePieceRecord
                {
                    id = (ushort)(pieceIndex + 1),
                    parentPieceIndex = EarthBondGraph.WorldPieceIndex,
                    hierarchyLevel = 0,
                    flags = EarthPieceFlags.Structural | EarthPieceFlags.Repairable |
                            (volume >= 1.35f / VolumetricCellCount
                                ? EarthPieceFlags.HeroPiece
                                : EarthPieceFlags.None),
                    restLocalPosition = MapPoint(cell.Centroid),
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
            }

            var bonds = new List<EarthFractureBondRecord>(192);
            for (int cellIndex = 0; cellIndex < plan.Cells.Length; cellIndex++)
            {
                EarthVolumetricFractureCell cell = plan.Cells[cellIndex];
                for (int faceIndex = 0; faceIndex < cell.Faces.Length; faceIndex++)
                {
                    EarthVolumetricFractureFace face = cell.Faces[faceIndex];
                    int neighbour = face.NeighbourCellIndex;
                    if (neighbour < 0 || neighbour <= cellIndex) continue;
                    Vector3 centroid = MapFaceCentroid(cell, face);
                    Vector3 normal = (pieces[neighbour].restLocalPosition -
                                      pieces[cellIndex].restLocalPosition).normalized;
                    AddBond(
                        bonds,
                        cellIndex,
                        neighbour,
                        centroid,
                        normal,
                        MappedFaceArea(cell, face),
                        false);
                }

                if (!cell.Foundation) continue;
                EarthVolumetricFractureFace foundationFace = FindFoundationFace(cell);
                bool hasFoundationFace = foundationFace.VertexIndices != null &&
                                         foundationFace.VertexIndices.Length >= 3;
                AddBond(
                    bonds,
                    cellIndex,
                    EarthBondGraph.WorldPieceIndex,
                    hasFoundationFace
                        ? MapFaceCentroid(cell, foundationFace)
                        : new Vector3(pieces[cellIndex].restLocalPosition.x, -0.5f,
                            pieces[cellIndex].restLocalPosition.z),
                    Vector3.down,
                    hasFoundationFace
                        ? MappedFaceArea(cell, foundationFace)
                        : Mathf.Max(0.015f, volumeScale * cell.Volume),
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
            EarthVolumetricFractureCell cell,
            int pieceIndex,
            IntactSurfaceNormalSampler intactNormalSampler)
        {
            var vertices = new Vector3[cell.Vertices.Length];
            for (int index = 0; index < vertices.Length; index++)
                vertices[index] = MapPoint(cell.Vertices[index]) - MapPoint(cell.Centroid);
            var collider = new Mesh { name = $"Earth Wall Collider {pieceIndex + 1:000}" };
            collider.vertices = vertices;
            collider.triangles = cell.Triangles;
            collider.RecalculateNormals();
            collider.RecalculateBounds();
            var renderVertices = new List<Vector3>(cell.Triangles.Length);
            var renderNormals = new List<Vector3>(cell.Triangles.Length);
            var colors = new List<Color32>(cell.Triangles.Length);
            var uv = new List<Vector2>(cell.Triangles.Length);
            var renderExterior = new List<int>(cell.Triangles.Length);
            var renderInterior = new List<int>(cell.Triangles.Length);
            var edgeMap = new Dictionary<PieceEdgeKey, PieceEdgeInset>(cell.Triangles.Length);
            var cornerCaps = new Dictionary<int, PieceCornerCap>(cell.Vertices.Length);
            Vector3 restLocalPosition = MapPoint(cell.Centroid);
            const float requestedBevelWidth = 0.012f;
            for (int faceIndex = 0; faceIndex < cell.Faces.Length; faceIndex++)
            {
                EarthVolumetricFractureFace face = cell.Faces[faceIndex];
                if (face.VertexIndices.Length < 3) continue;
                var points = new Vector3[face.VertexIndices.Length];
                Vector3 centroid = Vector3.zero;
                for (int index = 0; index < face.VertexIndices.Length; index++)
                {
                    points[index] = vertices[face.VertexIndices[index]];
                    centroid += points[index];
                }
                centroid /= face.VertexIndices.Length;
                Vector3 faceNormal = PolygonNormal(points);
                if (faceNormal.sqrMagnitude < 0.000001f)
                    faceNormal = Vector3.up;
                var inset = new Vector3[points.Length];
                for (int index = 0; index < points.Length; index++)
                {
                    Vector3 toCenter = centroid - points[index];
                    float distance = toCenter.magnitude;
                    float width = Mathf.Min(requestedBevelWidth, distance * 0.28f);
                    inset[index] = distance > 0.000001f
                        ? points[index] + toCenter / distance * width
                        : points[index];
                }
                byte cavity = (byte)Mathf.RoundToInt(Mathf.Lerp(
                    118f, 186f, Hash01((uint)(pieceIndex + 1), faceIndex)));
                Color32 faceColor = face.IsExterior
                    ? new Color32(255, 0, 0, 28)
                    : new Color32(0, 255, 0, cavity);
                AppendPiecePolygon(
                    renderVertices,
                    renderNormals,
                    colors,
                    uv,
                    face.IsExterior ? renderExterior : renderInterior,
                    inset,
                    faceNormal,
                    faceColor,
                    face.IsExterior,
                    restLocalPosition,
                    intactNormalSampler);

                for (int index = 0; index < inset.Length; index++)
                {
                    int next = (index + 1) % inset.Length;
                    int originalA = face.VertexIndices[index];
                    int originalB = face.VertexIndices[next];
                    var key = new PieceEdgeKey(originalA, originalB);
                    if (!edgeMap.TryGetValue(key, out PieceEdgeInset first))
                    {
                        edgeMap.Add(key, new PieceEdgeInset(
                            originalA,
                            originalB,
                            inset[index],
                            inset[next],
                            faceNormal,
                            face.IsExterior,
                            cavity));
                    }
                    else
                    {
                        Vector3 secondA = originalA == first.OriginalA ? inset[index] : inset[next];
                        Vector3 secondB = originalB == first.OriginalB ? inset[next] : inset[index];
                        Vector3 bevelNormal = (first.FaceNormal + faceNormal).normalized;
                        if (bevelNormal.sqrMagnitude < 0.000001f) bevelNormal = faceNormal;
                        bool exteriorBevel = first.IsExterior && face.IsExterior;
                        byte bevelCavity = (byte)Mathf.Max(first.Cavity, cavity);
                        AppendPiecePolygon(
                            renderVertices,
                            renderNormals,
                            colors,
                            uv,
                            exteriorBevel ? renderExterior : renderInterior,
                            new[] { first.InsetA, first.InsetB, secondB, secondA },
                            bevelNormal,
                            exteriorBevel
                                ? new Color32(242, 0, 0, 190)
                                : new Color32(0, 255, 0, bevelCavity),
                            false,
                            restLocalPosition,
                            intactNormalSampler);
                    }

                    if (!cornerCaps.TryGetValue(originalA, out PieceCornerCap cap))
                    {
                        cap = new PieceCornerCap();
                        cornerCaps.Add(originalA, cap);
                    }
                    cap.Points.Add(inset[index]);
                    cap.NormalSum += faceNormal;
                    cap.IsExterior &= face.IsExterior;
                    cap.Cavity = (byte)Mathf.Max(cap.Cavity, cavity);
                }
            }

            foreach (KeyValuePair<int, PieceCornerCap> pair in cornerCaps)
            {
                PieceCornerCap cap = pair.Value;
                RemoveDuplicatePoints(cap.Points);
                if (cap.Points.Count < 3) continue;
                Vector3 normal = cap.NormalSum.sqrMagnitude > 0.000001f
                    ? cap.NormalSum.normalized
                    : vertices[pair.Key].normalized;
                SortAroundNormal(cap.Points, normal);
                AppendPiecePolygon(
                    renderVertices,
                    renderNormals,
                    colors,
                    uv,
                    cap.IsExterior ? renderExterior : renderInterior,
                    cap.Points,
                    normal,
                    cap.IsExterior
                        ? new Color32(242, 0, 0, 210)
                        : new Color32(0, 255, 0, cap.Cavity),
                    false,
                    restLocalPosition,
                    intactNormalSampler);
            }

            var render = new Mesh { name = $"Earth Wall Baked Piece {pieceIndex + 1:000}" };
            render.SetVertices(renderVertices);
            render.SetNormals(renderNormals);
            render.SetColors(colors);
            render.SetUVs(0, uv);
            render.subMeshCount = 2;
            render.SetTriangles(renderExterior, 0, false);
            render.SetTriangles(renderInterior, 1, false);
            render.RecalculateTangents();
            render.RecalculateBounds();
            return new PieceMeshPair(render, collider);
        }

        private static bool HasProductionShapeQuality(EarthFractureAsset asset)
        {
            EarthFracturePieceRecord[] records = asset.PieceRecords;
            if (records == null || records.Length != VolumetricCellCount) return false;
            float minimumVolume = float.PositiveInfinity;
            float maximumVolume = 0f;
            var aspects = new float[records.Length];
            var vertexFamilies = new HashSet<int>();
            for (int index = 0; index < records.Length; index++)
            {
                Mesh collider = records[index].colliderMesh;
                Mesh render = records[index].renderMesh;
                if (collider == null || collider.vertexCount < 4 ||
                    collider.triangles.Length / 3 > 255 || render == null ||
                    render.subMeshCount < 2 || render.normals.Length != render.vertexCount ||
                    render.vertexCount <= collider.vertexCount) return false;
                vertexFamilies.Add(collider.vertexCount);
                minimumVolume = Mathf.Min(minimumVolume, records[index].volume);
                maximumVolume = Mathf.Max(maximumVolume, records[index].volume);
                Vector3 physicalSize = Vector3.Scale(
                    collider.bounds.size,
                    new Vector3(AuthoredWidth, AuthoredHeight, AuthoredDepth));
                float smallest = Mathf.Max(0.001f,
                    Mathf.Min(physicalSize.x, physicalSize.y, physicalSize.z));
                aspects[index] = Mathf.Max(physicalSize.x, physicalSize.y, physicalSize.z) / smallest;
            }
            Array.Sort(aspects);
            return vertexFamilies.Count >= 4 && minimumVolume > 0.0001f &&
                   maximumVolume / minimumVolume >= 3f &&
                   aspects[aspects.Length / 2] <= 3.5f &&
                   aspects[aspects.Length - 1] <= 6f;
        }

        private static void AppendPiecePolygon(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color32> colors,
            List<Vector2> uv,
            List<int> triangles,
            IReadOnlyList<Vector3> points,
            Vector3 desiredNormal,
            Color32 color,
            bool preserveIntactNormals,
            Vector3 restLocalPosition,
            IntactSurfaceNormalSampler intactNormalSampler)
        {
            if (points == null || points.Count < 3) return;
            bool reverse = Vector3.Dot(PolygonNormal(points), desiredNormal) < 0f;
            int start = vertices.Count;
            for (int outputIndex = 0; outputIndex < points.Count; outputIndex++)
            {
                int sourceIndex = reverse ? points.Count - 1 - outputIndex : outputIndex;
                Vector3 point = points[sourceIndex];
                vertices.Add(point);
                normals.Add(preserveIntactNormals
                    ? intactNormalSampler.Sample(point + restLocalPosition, desiredNormal)
                    : desiredNormal.normalized);
                colors.Add(color);
                uv.Add(new Vector2(point.x, point.y));
            }
            for (int triangle = 1; triangle < points.Count - 1; triangle++)
            {
                triangles.Add(start);
                triangles.Add(start + triangle);
                triangles.Add(start + triangle + 1);
            }
        }

        private static Vector3 PolygonNormal(IReadOnlyList<Vector3> points)
        {
            Vector3 normal = Vector3.zero;
            for (int index = 0; index < points.Count; index++)
            {
                Vector3 current = points[index];
                Vector3 next = points[(index + 1) % points.Count];
                normal.x += (current.y - next.y) * (current.z + next.z);
                normal.y += (current.z - next.z) * (current.x + next.x);
                normal.z += (current.x - next.x) * (current.y + next.y);
            }
            return normal.sqrMagnitude > 0.000001f ? normal.normalized : Vector3.zero;
        }

        private static void RemoveDuplicatePoints(List<Vector3> points)
        {
            const float toleranceSq = 0.00000001f;
            for (int first = points.Count - 1; first >= 0; first--)
                for (int second = first - 1; second >= 0; second--)
                    if ((points[first] - points[second]).sqrMagnitude <= toleranceSq)
                    {
                        points.RemoveAt(first);
                        break;
                    }
        }

        private static void SortAroundNormal(List<Vector3> points, Vector3 normal)
        {
            Vector3 center = Vector3.zero;
            for (int index = 0; index < points.Count; index++) center += points[index];
            center /= points.Count;
            Vector3 axisX = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f
                ? Vector3.up
                : Vector3.right).normalized;
            Vector3 axisY = Vector3.Cross(normal, axisX).normalized;
            points.Sort((a, b) => Mathf.Atan2(
                    Vector3.Dot(a - center, axisY), Vector3.Dot(a - center, axisX))
                .CompareTo(Mathf.Atan2(
                    Vector3.Dot(b - center, axisY), Vector3.Dot(b - center, axisX))));
        }

        private static EarthVolumetricFracturePlan BuildProductionPlan(float2[] physicalBoundary)
        {
            EarthVolumetricFracturePlan best = default;
            float bestPenalty = float.PositiveInfinity;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                uint seed = ProductionSeed + (uint)attempt * 0x9E3779B9u;
                EarthVolumetricFracturePlan candidate = EarthVolumetricFractureSolver.BuildConvexPrism(
                    seed,
                    physicalBoundary,
                    -AuthoredHeight * 0.5f,
                    AuthoredHeight * 0.5f,
                    VolumetricCellCount);
                float penalty = ProductionShapePenalty(candidate);
                if (penalty < bestPenalty)
                {
                    best = candidate;
                    bestPenalty = penalty;
                }
                if (penalty <= 0.0001f) break;
            }
            return best;
        }

        private static float ProductionShapePenalty(EarthVolumetricFracturePlan plan)
        {
            if (!plan.IsValid || plan.Cells.Length != VolumetricCellCount)
                return float.PositiveInfinity;

            var aspects = new float[plan.Cells.Length];
            var volumes = new float[plan.Cells.Length];
            for (int index = 0; index < plan.Cells.Length; index++)
            {
                EarthVolumetricFractureCell cell = plan.Cells[index];
                if (cell.Vertices.Length < 4 || cell.TriangleCount > 255 || cell.Volume <= 0.0001f)
                    return float.PositiveInfinity;
                aspects[index] = cell.AspectRatio;
                volumes[index] = cell.Volume;
            }
            Array.Sort(aspects);
            Array.Sort(volumes);
            float medianAspect = aspects[aspects.Length / 2];
            float maximumAspect = aspects[aspects.Length - 1];
            float p10 = volumes[Mathf.Clamp(Mathf.FloorToInt((volumes.Length - 1) * 0.10f), 0, volumes.Length - 1)];
            float p90 = volumes[Mathf.Clamp(Mathf.FloorToInt((volumes.Length - 1) * 0.90f), 0, volumes.Length - 1)];
            float volumeTail = p90 / Mathf.Max(0.0001f, p10);
            return Mathf.Max(0f, medianAspect - 3.5f) * 4f +
                   Mathf.Max(0f, maximumAspect - 6f) * 2f +
                   Mathf.Max(0f, 3f - volumeTail) * 3f +
                   plan.RelativeVolumeError * 10f;
        }

        private static Vector3 MapPoint(float3 point) => new Vector3(
            point.x / AuthoredWidth,
            point.y / AuthoredHeight,
            point.z / AuthoredDepth);

        private static Vector3 MapFaceCentroid(
            EarthVolumetricFractureCell cell,
            EarthVolumetricFractureFace face)
        {
            Vector3 centroid = Vector3.zero;
            for (int index = 0; index < face.VertexIndices.Length; index++)
                centroid += MapPoint(cell.Vertices[face.VertexIndices[index]]);
            return centroid / Mathf.Max(1, face.VertexIndices.Length);
        }

        private static float MappedFaceArea(
            EarthVolumetricFractureCell cell,
            EarthVolumetricFractureFace face)
        {
            if (face.VertexIndices.Length < 3) return 0f;
            Vector3 origin = MapPoint(cell.Vertices[face.VertexIndices[0]]);
            float area = 0f;
            for (int index = 1; index < face.VertexIndices.Length - 1; index++)
            {
                Vector3 a = MapPoint(cell.Vertices[face.VertexIndices[index]]) - origin;
                Vector3 b = MapPoint(cell.Vertices[face.VertexIndices[index + 1]]) - origin;
                area += Vector3.Cross(a, b).magnitude * 0.5f;
            }
            return Mathf.Max(0.0001f, area);
        }

        private static EarthVolumetricFractureFace FindFoundationFace(
            EarthVolumetricFractureCell cell)
        {
            EarthVolumetricFractureFace best = default;
            float lowest = float.PositiveInfinity;
            for (int faceIndex = 0; faceIndex < cell.Faces.Length; faceIndex++)
            {
                EarthVolumetricFractureFace face = cell.Faces[faceIndex];
                if (!face.IsExterior || face.VertexIndices.Length < 3 || face.Normal.y > -0.8f) continue;
                float height = 0f;
                for (int index = 0; index < face.VertexIndices.Length; index++)
                    height += cell.Vertices[face.VertexIndices[index]].y;
                height /= face.VertexIndices.Length;
                if (height >= lowest) continue;
                lowest = height;
                best = face;
            }
            return best;
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

        private sealed class IntactSurfaceNormalSampler
        {
            private readonly Vector3[] _vertices;
            private readonly Vector3[] _normals;
            private readonly int[] _triangles;
            private readonly Bounds _bounds;

            public IntactSurfaceNormalSampler(Mesh mesh)
            {
                _vertices = mesh != null ? mesh.vertices : Array.Empty<Vector3>();
                _normals = mesh != null ? mesh.normals : Array.Empty<Vector3>();
                _triangles = mesh != null ? mesh.triangles : Array.Empty<int>();
                _bounds = mesh != null ? mesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            }

            public Vector3 Sample(Vector3 normalizedPoint, Vector3 fallback)
            {
                if (_vertices.Length == 0 || _triangles.Length < 3)
                    return fallback.sqrMagnitude > 0.000001f ? fallback.normalized : Vector3.up;
                Vector3 point = _bounds.center + Vector3.Scale(normalizedPoint, _bounds.size);
                float bestDistanceSq = float.PositiveInfinity;
                Vector3 bestNormal = fallback;
                for (int triangle = 0; triangle <= _triangles.Length - 3; triangle += 3)
                {
                    int ia = _triangles[triangle];
                    int ib = _triangles[triangle + 1];
                    int ic = _triangles[triangle + 2];
                    if ((uint)ia >= (uint)_vertices.Length ||
                        (uint)ib >= (uint)_vertices.Length ||
                        (uint)ic >= (uint)_vertices.Length) continue;
                    Vector3 barycentric = ClosestPointBarycentric(
                        point,
                        _vertices[ia],
                        _vertices[ib],
                        _vertices[ic]);
                    Vector3 closest = _vertices[ia] * barycentric.x +
                                      _vertices[ib] * barycentric.y +
                                      _vertices[ic] * barycentric.z;
                    float distanceSq = (point - closest).sqrMagnitude;
                    if (distanceSq >= bestDistanceSq) continue;
                    bestDistanceSq = distanceSq;
                    bestNormal = _normals.Length == _vertices.Length
                        ? _normals[ia] * barycentric.x + _normals[ib] * barycentric.y +
                          _normals[ic] * barycentric.z
                        : Vector3.Cross(
                            _vertices[ib] - _vertices[ia],
                            _vertices[ic] - _vertices[ia]);
                }
                if (bestNormal.sqrMagnitude < 0.000001f) bestNormal = fallback;
                bestNormal.Normalize();
                if (fallback.sqrMagnitude > 0.000001f && Vector3.Dot(bestNormal, fallback) < 0f)
                    bestNormal = -bestNormal;
                return bestNormal;
            }

            private static Vector3 ClosestPointBarycentric(
                Vector3 point,
                Vector3 a,
                Vector3 b,
                Vector3 c)
            {
                Vector3 ab = b - a;
                Vector3 ac = c - a;
                Vector3 ap = point - a;
                float d1 = Vector3.Dot(ab, ap);
                float d2 = Vector3.Dot(ac, ap);
                if (d1 <= 0f && d2 <= 0f) return new Vector3(1f, 0f, 0f);

                Vector3 bp = point - b;
                float d3 = Vector3.Dot(ab, bp);
                float d4 = Vector3.Dot(ac, bp);
                if (d3 >= 0f && d4 <= d3) return new Vector3(0f, 1f, 0f);

                float vc = d1 * d4 - d3 * d2;
                if (vc <= 0f && d1 >= 0f && d3 <= 0f)
                {
                    float v = d1 / Mathf.Max(0.000001f, d1 - d3);
                    return new Vector3(1f - v, v, 0f);
                }

                Vector3 cp = point - c;
                float d5 = Vector3.Dot(ab, cp);
                float d6 = Vector3.Dot(ac, cp);
                if (d6 >= 0f && d5 <= d6) return new Vector3(0f, 0f, 1f);

                float vb = d5 * d2 - d1 * d6;
                if (vb <= 0f && d2 >= 0f && d6 <= 0f)
                {
                    float w = d2 / Mathf.Max(0.000001f, d2 - d6);
                    return new Vector3(1f - w, 0f, w);
                }

                float va = d3 * d6 - d5 * d4;
                if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
                {
                    float w = (d4 - d3) /
                              Mathf.Max(0.000001f, d4 - d3 + d5 - d6);
                    return new Vector3(0f, 1f - w, w);
                }

                float inverse = 1f / Mathf.Max(0.000001f, va + vb + vc);
                float insideV = vb * inverse;
                float insideW = vc * inverse;
                return new Vector3(1f - insideV - insideW, insideV, insideW);
            }
        }

        private readonly struct PieceEdgeKey : IEquatable<PieceEdgeKey>
        {
            public PieceEdgeKey(int a, int b)
            {
                Minimum = Mathf.Min(a, b);
                Maximum = Mathf.Max(a, b);
            }

            private int Minimum { get; }
            private int Maximum { get; }
            public bool Equals(PieceEdgeKey other) =>
                Minimum == other.Minimum && Maximum == other.Maximum;
            public override bool Equals(object obj) => obj is PieceEdgeKey other && Equals(other);
            public override int GetHashCode() => (Minimum * 397) ^ Maximum;
        }

        private readonly struct PieceEdgeInset
        {
            public PieceEdgeInset(
                int originalA,
                int originalB,
                Vector3 insetA,
                Vector3 insetB,
                Vector3 faceNormal,
                bool isExterior,
                byte cavity)
            {
                OriginalA = originalA;
                OriginalB = originalB;
                InsetA = insetA;
                InsetB = insetB;
                FaceNormal = faceNormal;
                IsExterior = isExterior;
                Cavity = cavity;
            }

            public int OriginalA { get; }
            public int OriginalB { get; }
            public Vector3 InsetA { get; }
            public Vector3 InsetB { get; }
            public Vector3 FaceNormal { get; }
            public bool IsExterior { get; }
            public byte Cavity { get; }
        }

        private sealed class PieceCornerCap
        {
            public readonly List<Vector3> Points = new List<Vector3>(8);
            public Vector3 NormalSum;
            public bool IsExterior = true;
            public byte Cavity;
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
