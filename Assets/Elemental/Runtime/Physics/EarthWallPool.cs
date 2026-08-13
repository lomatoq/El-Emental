using System;
using System.Collections.Generic;
using Elemental.Simulation.Structures;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    public sealed class EarthWallPool : MonoBehaviour
    {
        private const float AuthoredFractureAspect = 1.65f;
        private const int LargeFullDepthCellCount = 5;

        [SerializeField, Range(1, 24)] private int capacity = 8;
        [SerializeField] private Mesh wallMesh;
        [SerializeField] private Material wallMaterial;
        [SerializeField] private EarthWallProfile wallProfile;
        [SerializeField] private EarthPhysicsFeelProfile physicsFeelProfile;
        [SerializeField] private ScriptableObject fractureAsset;
        [SerializeField] private bool allowRuntimeProceduralDebugFallback = true;

        private readonly List<EarthWall> _walls = new List<EarthWall>(8);
        private int _reuseCursor;
        private uint _nextId = 1u;

        public int ActiveCount { get; private set; }
        public EarthWall LastAcquired { get; private set; }
        public bool UsingBakedFractureAsset => fractureAsset is IEarthFractureAssetRuntimeData;
        public bool RuntimeFallbackUsed { get; private set; }
        public event Action<EarthWall> WallCollapsed;

        public void Configure(
            int configuredCapacity,
            Mesh configuredMesh,
            Material configuredMaterial,
            EarthWallProfile configuredProfile = null)
        {
            capacity = Mathf.Clamp(configuredCapacity, 1, 24);
            wallMesh = configuredMesh;
            wallMaterial = configuredMaterial;
            wallProfile = configuredProfile;
        }

        public void ConfigurePhysicsFeel(EarthPhysicsFeelProfile profile) => physicsFeelProfile = profile;

        public void ConfigureFractureAsset(
            ScriptableObject configuredAsset,
            bool allowDebugFallback = true)
        {
            fractureAsset = configuredAsset;
            allowRuntimeProceduralDebugFallback = allowDebugFallback;
        }

        private void Awake()
        {
            for (int index = 0; index < capacity; index++) CreateWall();
        }

        public EarthWall Acquire(
            Vector3 start,
            Vector3 end,
            Vector3 planetCenter,
            float height,
            float thickness,
            uint sourceTick = 0u)
        {
            EarthWall wall = null;
            for (int index = 0; index < _walls.Count; index++)
            {
                if (_walls[index].gameObject.activeSelf) continue;
                wall = _walls[index];
                break;
            }

            if (wall == null)
            {
                wall = _walls[_reuseCursor];
                _reuseCursor = (_reuseCursor + 1) % _walls.Count;
            }
            else
            {
                ActiveCount = Mathf.Min(capacity, ActiveCount + 1);
            }

            wall.Initialize(_nextId++, start, end, planetCenter, height, thickness, sourceTick);
            LastAcquired = wall;
            return wall;
        }

        private EarthWall CreateWall()
        {
            GameObject wallObject = new GameObject($"Earth Wall {_walls.Count + 1:00}");
            wallObject.transform.SetParent(transform, false);
            MeshFilter filter = wallObject.AddComponent<MeshFilter>();
            filter.sharedMesh = wallMesh;
            MeshRenderer renderer = wallObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = wallMaterial;
            BoxCollider collider = wallObject.AddComponent<BoxCollider>();
            collider.size = Vector3.one;
            Rigidbody wallBody = wallObject.AddComponent<Rigidbody>();
            wallBody.useGravity = false;
            wallBody.isKinematic = true;
            wallBody.interpolation = RigidbodyInterpolation.Interpolate;
            wallBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            wallBody.constraints = RigidbodyConstraints.FreezeRotation;
            physicsFeelProfile?.Apply(wallBody, collider, EarthPhysicsBodyClass.Structure);
            EarthWall wall = wallObject.AddComponent<EarthWall>();
            wall.ConfigureProfile(wallProfile);
            wall.Collapsed += value => WallCollapsed?.Invoke(value);
            if (TryConfigureBakedWall(
                    wallObject, filter, wallBody, wall, out Transform[] bakedPieces))
            {
                wallObject.SetActive(false);
                _walls.Add(wall);
                return wall;
            }
            if (!allowRuntimeProceduralDebugFallback)
                throw new InvalidOperationException("A valid baked Earth fracture asset is required.");
            RuntimeFallbackUsed = true;
            int patternIndex = _walls.Count;
            VoronoiFractureCell[] cells = VoronoiFractureSolver.BuildHierarchicalNormalizedForAspect(
                0xE17F0001u + ((uint)patternIndex * 0x9E3779B9u),
                AuthoredFractureAspect);
            int[] areaOrder = new int[cells.Length];
            bool[] fullDepthCells = new bool[cells.Length];
            for (int index = 0; index < areaOrder.Length; index++) areaOrder[index] = index;
            Array.Sort(areaOrder, (a, b) => cells[b].Area.CompareTo(cells[a].Area));
            for (int index = 0; index < Mathf.Min(LargeFullDepthCellCount, areaOrder.Length); index++)
                fullDepthCells[areaOrder[index]] = true;

            int pieceCount = (LargeFullDepthCellCount * 1) +
                             ((cells.Length - LargeFullDepthCellCount) * 2);
            Transform[] collapsePieces = new Transform[pieceCount];
            float[] volumeFractions = new float[pieceCount];
            var slices = new List<PieceSlice>(pieceCount);
            int nextPiece = 0;
            for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
            {
                VoronoiFractureCell cell = cells[cellIndex];
                int depthLayers = fullDepthCells[cellIndex] ? 1 : 2;
                for (int depthIndex = 0; depthIndex < depthLayers; depthIndex++)
                {
                    int pieceIndex = nextPiece++;
                    float depthMin = Mathf.Lerp(-0.5f, 0.5f, depthIndex / (float)depthLayers);
                    float depthMax = Mathf.Lerp(-0.5f, 0.5f, (depthIndex + 1f) / depthLayers);
                    GameObject piece = new GameObject(
                        $"Voronoi Piece {cellIndex + 1:00}-{depthIndex + 1:00}");
                    piece.transform.SetParent(wallObject.transform, false);
                    piece.transform.localPosition = new Vector3(cell.Centroid.x, cell.Centroid.y, 0f);
                    Mesh mesh = BuildVoronoiPrismMesh(
                        cell, patternIndex, pieceIndex, depthMin, depthMax);
                    MeshFilter pieceFilter = piece.AddComponent<MeshFilter>();
                    pieceFilter.sharedMesh = mesh;
                    MeshRenderer pieceRenderer = piece.AddComponent<MeshRenderer>();
                    pieceRenderer.sharedMaterial = wallMaterial;
                    MeshCollider pieceCollider = piece.AddComponent<MeshCollider>();
                    pieceCollider.sharedMesh = mesh;
                    pieceCollider.convex = true;
                    Rigidbody pieceBody = piece.AddComponent<Rigidbody>();
                    pieceBody.useGravity = false;
                    pieceBody.isKinematic = true;
                    pieceBody.detectCollisions = false;
                    pieceBody.interpolation = RigidbodyInterpolation.Interpolate;
                    pieceBody.maxAngularVelocity = 22f;
                    physicsFeelProfile?.Apply(pieceBody, pieceCollider, EarthPhysicsBodyClass.HeavyBlock);
                    piece.SetActive(false);
                    collapsePieces[pieceIndex] = piece.transform;
                    volumeFractions[pieceIndex] = cell.Area / depthLayers;
                    slices.Add(new PieceSlice(cellIndex, pieceIndex, depthMin, depthMax));
                }
            }
            EarthWallBond[] bonds = BuildBonds(
                wallBody, collapsePieces, cells, slices);
            wall.ConfigureCollapsePieces(collapsePieces, volumeFractions, bonds);
            wallObject.SetActive(false);
            _walls.Add(wall);
            return wall;
        }

        private bool TryConfigureBakedWall(
            GameObject wallObject,
            MeshFilter intactFilter,
            Rigidbody wallBody,
            EarthWall wall,
            out Transform[] pieces)
        {
            pieces = null;
            IEarthFractureAssetRuntimeData data = fractureAsset as IEarthFractureAssetRuntimeData;
            if (data == null || data.SchemaVersion <= 0 || data.PieceCount <= 0 || data.BondCount <= 0)
                return false;

            var definitions = new EarthPieceDefinition[data.PieceCount];
            var bondDefinitions = new EarthBondDefinition[data.BondCount];
            if (!data.CopyDefinitions(definitions, bondDefinitions))
                throw new InvalidOperationException("The baked Earth fracture asset could not copy its data.");
            EarthGraphValidationResult validation = EarthBondGraph.Validate(
                definitions, definitions.Length, bondDefinitions, bondDefinitions.Length);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    $"The baked Earth fracture graph is invalid: {validation.Error} at {validation.Index}.");
            }

            if (data.IntactRenderMesh != null) intactFilter.sharedMesh = data.IntactRenderMesh;
            pieces = new Transform[data.PieceCount];
            var volumeFractions = new float[data.PieceCount];
            float totalVolume = 0f;
            for (int index = 0; index < definitions.Length; index++)
                totalVolume += Mathf.Max(0.0001f, definitions[index].Volume);

            for (int pieceIndex = 0; pieceIndex < definitions.Length; pieceIndex++)
            {
                EarthPieceDefinition definition = definitions[pieceIndex];
                Mesh renderMesh = data.GetPieceRenderMesh(pieceIndex);
                Mesh colliderMesh = data.GetPieceColliderMesh(pieceIndex);
                if (renderMesh == null || colliderMesh == null)
                    throw new InvalidOperationException($"Baked Earth piece {pieceIndex} has no render/collider mesh.");

                GameObject piece = new GameObject($"Baked Piece {definition.Id.Value:000}");
                piece.transform.SetParent(wallObject.transform, false);
                piece.transform.localPosition = ToVector3(definition.RestLocalPosition);
                quaternion rotation = definition.RestLocalRotation;
                piece.transform.localRotation = new Quaternion(
                    rotation.value.x, rotation.value.y, rotation.value.z, rotation.value.w);
                piece.transform.localScale = ToVector3(definition.RestLocalScale);
                MeshFilter pieceFilter = piece.AddComponent<MeshFilter>();
                pieceFilter.sharedMesh = renderMesh;
                MeshRenderer pieceRenderer = piece.AddComponent<MeshRenderer>();
                pieceRenderer.sharedMaterials = renderMesh.subMeshCount > 1
                    ? new[] { wallMaterial, wallMaterial }
                    : new[] { wallMaterial };
                MeshCollider pieceCollider = piece.AddComponent<MeshCollider>();
                pieceCollider.sharedMesh = colliderMesh;
                pieceCollider.convex = true;
                Rigidbody pieceBody = piece.AddComponent<Rigidbody>();
                pieceBody.useGravity = false;
                pieceBody.isKinematic = true;
                pieceBody.detectCollisions = false;
                pieceBody.interpolation = RigidbodyInterpolation.Interpolate;
                pieceBody.maxAngularVelocity = 22f;
                physicsFeelProfile?.Apply(pieceBody, pieceCollider, EarthPhysicsBodyClass.HeavyBlock);
                piece.SetActive(false);
                pieces[pieceIndex] = piece.transform;
                volumeFractions[pieceIndex] = Mathf.Max(0.0001f, definition.Volume) / totalVolume;
            }

            var bonds = new EarthWallBond[bondDefinitions.Length];
            for (int bondIndex = 0; bondIndex < bondDefinitions.Length; bondIndex++)
            {
                EarthBondDefinition definition = bondDefinitions[bondIndex];
                bool foundation = definition.PieceB == EarthBondGraph.WorldPieceIndex;
                bonds[bondIndex] = CreateBond(
                    pieces,
                    definition.PieceA,
                    definition.PieceB,
                    definition.ContactArea,
                    foundation,
                    foundation ? wallBody : null);
            }

            wall.ConfigureCollapsePieces(pieces, volumeFractions, bonds);
            if (!wall.ConfigureBakedRuntime(data))
                throw new InvalidOperationException("The baked Earth runtime adapter rejected validated data.");
            return true;
        }

        private static EarthWallBond[] BuildBonds(
            Rigidbody wallBody,
            Transform[] pieces,
            VoronoiFractureCell[] cells,
            List<PieceSlice> slices)
        {
            var bonds = new List<EarthWallBond>(96);
            for (int first = 0; first < slices.Count; first++)
            {
                PieceSlice a = slices[first];
                for (int second = first + 1; second < slices.Count; second++)
                {
                    PieceSlice b = slices[second];
                    float contactArea;
                    if (a.CellIndex == b.CellIndex)
                    {
                        bool adjacentDepth = Mathf.Abs(a.DepthMax - b.DepthMin) < 0.001f ||
                                             Mathf.Abs(b.DepthMax - a.DepthMin) < 0.001f;
                        if (!adjacentDepth) continue;
                        contactArea = cells[a.CellIndex].Area;
                    }
                    else
                    {
                        float depthOverlap = Mathf.Min(a.DepthMax, b.DepthMax) -
                                             Mathf.Max(a.DepthMin, b.DepthMin);
                        if (depthOverlap <= 0.0001f) continue;
                        float sharedEdge = SharedEdgeLength(cells[a.CellIndex], cells[b.CellIndex]);
                        if (sharedEdge <= 0.0001f) continue;
                        contactArea = sharedEdge * depthOverlap;
                    }

                    bonds.Add(CreateBond(pieces, a.PieceIndex, b.PieceIndex, contactArea, false));
                }
            }

            for (int index = 0; index < slices.Count; index++)
            {
                PieceSlice slice = slices[index];
                if (!TouchesBottom(cells[slice.CellIndex])) continue;
                bonds.Add(CreateBond(
                    pieces, slice.PieceIndex, -1,
                    Mathf.Max(0.02f, cells[slice.CellIndex].Area * (slice.DepthMax - slice.DepthMin)),
                    true,
                    wallBody));
            }

            return bonds.ToArray();
        }

        private static EarthWallBond CreateBond(
            Transform[] pieces,
            int pieceA,
            int pieceB,
            float contactArea,
            bool foundation,
            Rigidbody foundationBody = null)
        {
            ConfigurableJoint joint = pieces[pieceA].gameObject.AddComponent<ConfigurableJoint>();
            joint.autoConfigureConnectedAnchor = true;
            joint.connectedBody = null;
            joint.xMotion = ConfigurableJointMotion.Free;
            joint.yMotion = ConfigurableJointMotion.Free;
            joint.zMotion = ConfigurableJointMotion.Free;
            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;
            joint.enableCollision = true;
            joint.enablePreprocessing = false;
            joint.breakForce = Mathf.Infinity;
            joint.breakTorque = Mathf.Infinity;
            if (foundation && foundationBody == null)
                throw new ArgumentNullException(nameof(foundationBody));
            return new EarthWallBond(joint, pieceA, pieceB, contactArea, foundation);
        }

        private static float SharedEdgeLength(VoronoiFractureCell a, VoronoiFractureCell b)
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
                    bool sameEdge = (math.distancesq(a0, b1) <= toleranceSq &&
                                     math.distancesq(a1, b0) <= toleranceSq) ||
                                    (math.distancesq(a0, b0) <= toleranceSq &&
                                     math.distancesq(a1, b1) <= toleranceSq);
                    if (sameEdge) return math.distance(a0, a1);
                }
            }
            return 0f;
        }

        private static bool TouchesBottom(VoronoiFractureCell cell)
        {
            for (int index = 0; index < cell.Vertices.Length; index++)
                if (cell.Vertices[index].y <= -0.499f) return true;
            return false;
        }

        private static Mesh BuildVoronoiPrismMesh(
            VoronoiFractureCell cell,
            int patternIndex,
            int pieceIndex,
            float depthMin,
            float depthMax)
        {
            int count = cell.Vertices.Length;
            var vertices = new Vector3[count * 2];
            for (int index = 0; index < count; index++)
            {
                float x = cell.Vertices[index].x - cell.Centroid.x;
                float y = cell.Vertices[index].y - cell.Centroid.y;
                vertices[index] = new Vector3(x, y, depthMin);
                vertices[count + index] = new Vector3(x, y, depthMax);
            }

            var triangles = new int[(count - 2) * 6 + count * 6];
            int triangle = 0;
            for (int index = 1; index < count - 1; index++)
            {
                triangles[triangle++] = 0;
                triangles[triangle++] = index + 1;
                triangles[triangle++] = index;
                triangles[triangle++] = count;
                triangles[triangle++] = count + index;
                triangles[triangle++] = count + index + 1;
            }
            for (int index = 0; index < count; index++)
            {
                int next = (index + 1) % count;
                triangles[triangle++] = index;
                triangles[triangle++] = next;
                triangles[triangle++] = count + next;
                triangles[triangle++] = index;
                triangles[triangle++] = count + next;
                triangles[triangle++] = count + index;
            }

            var mesh = new Mesh { name = $"Earth Wall Voronoi {patternIndex:00}-{pieceIndex:00}" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private readonly struct PieceSlice
        {
            public PieceSlice(int cellIndex, int pieceIndex, float depthMin, float depthMax)
            {
                CellIndex = cellIndex;
                PieceIndex = pieceIndex;
                DepthMin = depthMin;
                DepthMax = depthMax;
            }

            public int CellIndex { get; }
            public int PieceIndex { get; }
            public float DepthMin { get; }
            public float DepthMax { get; }
        }

        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
