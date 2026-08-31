using System;
using System.Collections.Generic;
using Elemental.Runtime.Geometry;
using Elemental.Runtime.Matter;
using Elemental.Simulation.Matter;
using Elemental.Simulation.Structures;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    public sealed class EarthWallPool : MonoBehaviour
    {
        private const float FallbackWidth = 8f;
        private const float FallbackHeight = 4f;
        private const float FallbackDepth = 0.55f;
        private const int FallbackVolumetricCellCount = 40;

        [SerializeField, Range(1, 24)] private int capacity = 8;
        [SerializeField] private Mesh wallMesh;
        [SerializeField] private Material wallMaterial;
        [SerializeField] private Material fractureInteriorMaterial;
        [SerializeField] private EarthWallProfile wallProfile;
        [SerializeField] private EarthPhysicsFeelProfile physicsFeelProfile;
        [SerializeField] private EarthRepairProfile repairProfile;
        [SerializeField] private ScriptableObject fractureAsset;
        [SerializeField] private bool allowRuntimeProceduralDebugFallback = true;
        [SerializeField] private EarthSurfaceQueryService surfaceQueries;
        [SerializeField] private EarthStructureFractureProfile structureFractureProfile;
        [SerializeField] private EarthMatterKernelBehaviour matterKernel;
        [SerializeField] private EarthShapeGrammarProfile shapeGrammarProfile;
        [SerializeField] private GravityWorldBehaviour gravityWorld;

        private readonly List<EarthWall> _walls = new List<EarthWall>(8);
        private readonly Dictionary<EarthWall, MeshFilter> _wallFilters = new Dictionary<EarthWall, MeshFilter>(8);
        private readonly Dictionary<EarthWall, Mesh> _runtimeWallMeshes = new Dictionary<EarthWall, Mesh>(8);
        private EarthWallShapeDiversityTracker _wallShapeDiversity;
        private uint _nextId = 1u;
        private readonly Collider[] _constructionHits = new Collider[48];
        private readonly IEarthDamageableStructure[] _constructionTargets = new IEarthDamageableStructure[24];

        public int ActiveCount
        {
            get
            {
                int active = 0;
                for (int index = 0; index < _walls.Count; index++)
                    if (_walls[index].gameObject.activeSelf) active++;
                return active;
            }
        }
        public EarthWall LastAcquired { get; private set; }
        public bool UsingBakedFractureAsset => fractureAsset is IEarthFractureAssetRuntimeData;
        public bool RuntimeFallbackUsed { get; private set; }
        public int RuntimeIntegrityFallbackCount { get; private set; }
        public event Action<EarthWall> WallCollapsed;

        public EarthWall FindActive(uint structureId)
        {
            for (int index = 0; index < _walls.Count; index++)
            {
                EarthWall wall = _walls[index];
                if (wall.gameObject.activeSelf && wall.WallId == structureId) return wall;
            }
            return null;
        }

        /// <summary>
        /// Explicitly retires a transient/replay wall. Live gameplay walls are never
        /// selected implicitly for reuse when the representation budget is full.
        /// </summary>
        public bool ReleaseTransient(EarthWall wall)
        {
            if (wall == null || !_walls.Contains(wall)) return false;
            wall.ReturnToPoolAsTransientProxy();
            return true;
        }

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
        public void ConfigureGravity(GravityWorldBehaviour configuredWorld)
        {
            gravityWorld = configuredWorld;
            for (int index = 0; index < _walls.Count; index++)
                ConfigureGravityBodies(_walls[index] != null
                    ? _walls[index].gameObject
                    : null);
        }

        public void ConfigureSurfaceQueries(EarthSurfaceQueryService configuredService)
        {
            surfaceQueries = configuredService;
            for (int index = 0; index < _walls.Count; index++)
            {
                EarthWallSurfaceProvider provider = _walls[index].GetComponent<EarthWallSurfaceProvider>();
                provider?.Configure(_walls[index], surfaceQueries);
            }
        }
        public void ConfigureRepair(EarthRepairProfile profile) => repairProfile = profile;
        public void ConfigureStructureFracture(EarthStructureFractureProfile profile) =>
            structureFractureProfile = profile;

        public void ConfigureShapeGrammar(EarthShapeGrammarProfile profile)
        {
            shapeGrammarProfile = profile;
            _wallShapeDiversity = new EarthWallShapeDiversityTracker(
                profile != null ? profile.LocalHistoryLength : 16);
        }

        public void ConfigureFractureMaterials(Material exterior, Material freshInterior)
        {
            if (exterior != null) wallMaterial = exterior;
            fractureInteriorMaterial = freshInterior != null ? freshInterior : wallMaterial;
        }

        public void ConfigureFractureAsset(
            ScriptableObject configuredAsset,
            bool allowDebugFallback = true)
        {
            fractureAsset = configuredAsset;
            allowRuntimeProceduralDebugFallback = allowDebugFallback;
        }

        private void Awake()
        {
            if (matterKernel == null) matterKernel = EarthMatterKernelBehaviour.FindOrCreate(this);
            _wallShapeDiversity ??= new EarthWallShapeDiversityTracker(
                shapeGrammarProfile != null ? shapeGrammarProfile.LocalHistoryLength : 16);
            // A baked wall owns forty convex fracture pieces. Prewarming the
            // entire pool cooked hundreds of colliders before the first frame.
            if (capacity > 0) CreateWall();
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<EarthWall, Mesh> pair in _runtimeWallMeshes)
            {
                if (pair.Value == null) continue;
                if (Application.isPlaying) Destroy(pair.Value);
                else DestroyImmediate(pair.Value);
            }
            _runtimeWallMeshes.Clear();
            _wallFilters.Clear();
        }

        public EarthWall Acquire(
            Vector3 start,
            Vector3 end,
            Vector3 planetCenter,
            float height,
            float thickness,
            uint sourceTick = 0u,
            Vector3 supportNormal = default,
            uint excludedSupportId = 0u)
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
                if (_walls.Count >= 24)
                {
                    Debug.LogWarning("[EarthMatter] Wall representation budget exhausted; construction rejected without overwriting a live wall.", this);
                    return null;
                }
                wall = CreateWall();
                capacity = Mathf.Max(capacity, _walls.Count);
            }

            ApplyVisualShapeVariant(wall, sourceTick);
            ApplyConstructionIntersection(
                wall,
                start,
                end,
                planetCenter,
                height,
                thickness,
                supportNormal,
                sourceTick,
                excludedSupportId);
            wall.Initialize(_nextId++, start, end, planetCenter, height, thickness, sourceTick, supportNormal);
            float volume = Mathf.Max(0.000001f, Vector3.Distance(start, end) * height * thickness);
            var source = new EarthSourceProvenance(
                EarthSourceKind.TerrainEdit,
                wall.WallId,
                wall.Generation >= ushort.MaxValue ? ushort.MaxValue : (ushort)Mathf.Max(1, (int)wall.Generation),
                -1,
                sourceTick,
                new float3((start.x + end.x) * 0.5f, (start.y + end.y) * 0.5f, (start.z + end.z) * 0.5f),
                volume,
                EarthProvenanceFlags.ExactReturnSupported |
                EarthProvenanceFlags.SourceCavityValid |
                EarthProvenanceFlags.VolumeReserved);
            EarthMatterRuntimeBridge.EnsureIdentity(
                wall,
                matterKernel,
                wall.Body,
                EarthMatterPhase.Forming,
                EarthRepresentationTier.HeroPhysical,
                EarthMaterialKind.Stone,
                EarthShapeSemantic.Slab,
                volume,
                wall.EstimatedMass,
                source);
            LastAcquired = wall;
            return wall;
        }

        private void ApplyConstructionIntersection(
            EarthWall newWall,
            Vector3 start,
            Vector3 end,
            Vector3 planetCenter,
            float height,
            float thickness,
            Vector3 supportNormal,
            uint sourceTick,
            uint excludedSupportId)
        {
            Vector3 chord = end - start;
            if (chord.sqrMagnitude < 0.04f) return;
            Vector3 tangent = chord.normalized;
            Vector3 midpoint = (start + end) * 0.5f;
            Vector3 up = supportNormal.sqrMagnitude > 0.5f
                ? Vector3.ProjectOnPlane(supportNormal, tangent).normalized
                : Vector3.ProjectOnPlane(midpoint - planetCenter, tangent).normalized;
            if (up.sqrMagnitude < 0.5f) up = Vector3.up;
            Vector3 forward = Vector3.Cross(tangent, up).normalized;
            Quaternion rotation = Quaternion.LookRotation(forward, up);
            Vector3 center = midpoint + up * (height * 0.5f - 0.12f);
            Vector3 halfExtents = new Vector3(chord.magnitude * 0.5f, height * 0.5f, thickness * 0.65f);
            int hitCount = UnityEngine.Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                _constructionHits,
                rotation,
                ~0,
                QueryTriggerInteraction.Ignore);
            int targetCount = 0;
            for (int index = 0; index < hitCount && targetCount < _constructionTargets.Length; index++)
            {
                Collider hit = _constructionHits[index];
                if (hit == null || hit.transform.IsChildOf(newWall.transform)) continue;
                EarthWall wall = hit.GetComponentInParent<EarthWall>();
                EarthPlatform platform = wall == null ? hit.GetComponentInParent<EarthPlatform>() : null;
                EarthArenaStructure arena = wall == null && platform == null
                    ? hit.GetComponentInParent<EarthArenaStructure>()
                    : null;
                IEarthDamageableStructure target = wall != null
                    ? wall
                    : platform != null
                        ? platform
                        : arena;
                if (target == null || !((MonoBehaviour)target).gameObject.activeInHierarchy ||
                    target.StructureId == excludedSupportId) continue;
                bool duplicate = false;
                for (int existing = 0; existing < targetCount; existing++)
                    if (ReferenceEquals(_constructionTargets[existing], target)) duplicate = true;
                if (duplicate) continue;
                _constructionTargets[targetCount++] = target;
                Vector3 impactPoint = hit is MeshCollider meshCollider && !meshCollider.convex
                    ? hit.bounds.ClosestPoint(midpoint)
                    : hit.ClosestPoint(midpoint);
                var impact = new EarthStructureImpact(
                    impactPoint,
                    up + forward * 0.18f,
                    structureFractureProfile != null
                        ? structureFractureProfile.ConstructionImpactImpulse
                        : 2850f,
                    EarthStructureImpactKind.Construction,
                    sourceTick);
                target.ApplyEarthImpact(in impact);
            }
            for (int index = 0; index < targetCount; index++) _constructionTargets[index] = null;
        }

        private EarthWall CreateWall()
        {
            GameObject wallObject = new GameObject($"Earth Wall {_walls.Count + 1:00}");
            wallObject.transform.SetParent(transform, false);
            GameObject visualObject = new GameObject("VisualEmergenceRoot");
            visualObject.transform.SetParent(wallObject.transform, false);
            MeshFilter filter = visualObject.AddComponent<MeshFilter>();
            filter.sharedMesh = wallMesh;
            MeshRenderer renderer = visualObject.AddComponent<MeshRenderer>();
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
            _wallFilters[wall] = filter;
            EarthWallSurfaceProvider surfaceProvider = wallObject.AddComponent<EarthWallSurfaceProvider>();
            surfaceProvider.Configure(wall, surfaceQueries);
            wall.ConfigureProfile(wallProfile);
            wall.Collapsed += value => WallCollapsed?.Invoke(value);
            if (TryConfigureBakedWall(
                    wallObject, filter, wallBody, wall, out Transform[] bakedPieces))
            {
                ConfigureGravityBodies(wallObject);
                wallObject.SetActive(false);
                _walls.Add(wall);
                return wall;
            }
            if (!allowRuntimeProceduralDebugFallback)
                throw new InvalidOperationException("A valid baked Earth fracture asset is required.");
            RuntimeFallbackUsed = true;
            int patternIndex = _walls.Count;
            uint fractureSeed = 0xE17F1002u + ((uint)patternIndex * 0x9E3779B9u);
            float2[] boundary =
            {
                new float2(-FallbackWidth * 0.5f, -FallbackDepth * 0.5f),
                new float2(FallbackWidth * 0.5f, -FallbackDepth * 0.5f),
                new float2(FallbackWidth * 0.5f, FallbackDepth * 0.5f),
                new float2(-FallbackWidth * 0.5f, FallbackDepth * 0.5f)
            };
            EarthVolumetricFracturePlan plan = EarthVolumetricFractureSolver.BuildConvexPrism(
                fractureSeed,
                boundary,
                -FallbackHeight * 0.5f,
                FallbackHeight * 0.5f,
                FallbackVolumetricCellCount);
            if (!plan.IsValid)
                throw new InvalidOperationException("Runtime volumetric wall fallback failed conservation.");
            Transform[] collapsePieces = new Transform[plan.Cells.Length];
            float[] volumeFractions = new float[plan.Cells.Length];
            for (int pieceIndex = 0; pieceIndex < plan.Cells.Length; pieceIndex++)
            {
                EarthVolumetricFractureCell cell = plan.Cells[pieceIndex];
                GameObject piece = new GameObject($"Volume Piece {pieceIndex + 1:00}");
                piece.transform.SetParent(wallObject.transform, false);
                piece.transform.localPosition = MapFallbackPoint(cell.Centroid);
                Mesh mesh = BuildVolumetricFallbackMesh(cell, patternIndex, pieceIndex);
                piece.AddComponent<MeshFilter>().sharedMesh = mesh;
                piece.AddComponent<MeshRenderer>().sharedMaterial = wallMaterial;
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
                volumeFractions[pieceIndex] = cell.Volume / Mathf.Max(0.0001f, plan.SourceVolume);
            }
            EarthWallBond[] bonds = BuildVolumetricFallbackBonds(wallBody, collapsePieces, plan);
            wall.ConfigureCollapsePieces(collapsePieces, volumeFractions, bonds);
            ConfigureGravityBodies(wallObject);
            wallObject.SetActive(false);
            _walls.Add(wall);
            return wall;
        }

        private void ConfigureGravityBodies(GameObject wallObject)
        {
            if (wallObject == null) return;
            Rigidbody[] bodies = wallObject.GetComponentsInChildren<Rigidbody>(true);
            for (int index = 0; index < bodies.Length; index++)
            {
                Rigidbody body = bodies[index];
                if (body == null) continue;
                GravityBody gravity = body.GetComponent<GravityBody>();
                if (gravity == null) gravity = body.gameObject.AddComponent<GravityBody>();
                gravity.Configure(gravityWorld, body);
            }
        }

        private void ApplyVisualShapeVariant(EarthWall wall, uint sourceTick)
        {
            if (wall == null || !_wallFilters.TryGetValue(wall, out MeshFilter filter) || filter == null)
                return;
            _wallShapeDiversity ??= new EarthWallShapeDiversityTracker(
                shapeGrammarProfile != null ? shapeGrammarProfile.LocalHistoryLength : 16);
            uint librarySeed = shapeGrammarProfile != null ? shapeGrammarProfile.LibrarySeed : 0xE17F0411u;
            uint seed = EarthShapeSeed.Compose(
                librarySeed,
                _nextId,
                0x57414C4Cu,
                wall.Generation + 1u,
                sourceTick).Value;
            EarthWallArchetype archetype = _wallShapeDiversity.Select(
                seed,
                shapeGrammarProfile != null ? shapeGrammarProfile.CandidateAttempts : 12);
            Mesh replacement = EarthWallMeshFactory.Create(archetype, seed);
            if (_runtimeWallMeshes.TryGetValue(wall, out Mesh previous) && previous != null)
            {
                if (Application.isPlaying) Destroy(previous);
                else DestroyImmediate(previous);
            }
            _runtimeWallMeshes[wall] = replacement;
            filter.sharedMesh = replacement;
        }

        private Mesh BuildVolumetricFallbackMesh(
            EarthVolumetricFractureCell cell,
            int patternIndex,
            int pieceIndex)
        {
            Vector3 center = MapFallbackPoint(cell.Centroid);
            var vertices = new Vector3[cell.Vertices.Length];
            for (int index = 0; index < vertices.Length; index++)
                vertices[index] = MapFallbackPoint(cell.Vertices[index]) - center;
            var mesh = new Mesh { name = $"Earth Wall Volume {patternIndex:00}-{pieceIndex:00}" };
            mesh.vertices = vertices;
            mesh.triangles = cell.Triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EarthMeshIntegrityReport report = EarthMeshIntegrityValidator.Validate(
                mesh, EarthMeshIntegrityPolicy.ConvexCollider);
            if (!report.IsValid)
            {
                Bounds bounds = mesh.bounds;
                string name = mesh.name;
                if (Application.isPlaying) Destroy(mesh);
                else DestroyImmediate(mesh);
                mesh = EarthSafeMeshFactory.CreateSkewedBlock(
                    $"{name}_IntegrityFallback",
                    bounds,
                    unchecked((uint)(patternIndex * 397) ^ (uint)pieceIndex ^ cell.Id));
                RuntimeIntegrityFallbackCount++;
            }
            return mesh;
        }

        private static EarthWallBond[] BuildVolumetricFallbackBonds(
            Rigidbody wallBody,
            Transform[] pieces,
            in EarthVolumetricFracturePlan plan)
        {
            var bonds = new List<EarthWallBond>(160);
            for (int cellIndex = 0; cellIndex < plan.Cells.Length; cellIndex++)
            {
                EarthVolumetricFractureCell cell = plan.Cells[cellIndex];
                for (int faceIndex = 0; faceIndex < cell.Faces.Length; faceIndex++)
                {
                    EarthVolumetricFractureFace face = cell.Faces[faceIndex];
                    if (face.NeighbourCellIndex < 0 || face.NeighbourCellIndex <= cellIndex) continue;
                    bonds.Add(CreateBond(
                        pieces,
                        cellIndex,
                        face.NeighbourCellIndex,
                        MappedFallbackFaceArea(cell, face),
                        false));
                }
                if (cell.Foundation)
                    bonds.Add(CreateBond(
                        pieces,
                        cellIndex,
                        -1,
                        Mathf.Max(0.01f, cell.Volume / Mathf.Max(0.0001f, plan.SourceVolume)),
                        true,
                        wallBody));
            }
            return bonds.ToArray();
        }

        private static float MappedFallbackFaceArea(
            EarthVolumetricFractureCell cell,
            EarthVolumetricFractureFace face)
        {
            if (face.VertexIndices.Length < 3) return 0.0001f;
            Vector3 origin = MapFallbackPoint(cell.Vertices[face.VertexIndices[0]]);
            float area = 0f;
            for (int index = 1; index < face.VertexIndices.Length - 1; index++)
            {
                Vector3 a = MapFallbackPoint(cell.Vertices[face.VertexIndices[index]]) - origin;
                Vector3 b = MapFallbackPoint(cell.Vertices[face.VertexIndices[index + 1]]) - origin;
                area += Vector3.Cross(a, b).magnitude * 0.5f;
            }
            return Mathf.Max(0.0001f, area);
        }

        private static Vector3 MapFallbackPoint(float3 point) => new Vector3(
            point.x / FallbackWidth,
            point.y / FallbackHeight,
            point.z / FallbackDepth);

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
                    ? new[] { wallMaterial, fractureInteriorMaterial != null ? fractureInteriorMaterial : wallMaterial }
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
            if (!wall.ConfigureBakedRuntime(data, repairProfile))
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
            uint fractureSeed,
            int patternIndex,
            int pieceIndex,
            float depthMin,
            float depthMax)
        {
            float2[] outline = VoronoiFractureSolver.BuildChippedOutline(cell, fractureSeed);
            int count = outline.Length;
            var vertices = new Vector3[count * 2];
            for (int index = 0; index < count; index++)
            {
                float x = outline[index].x - cell.Centroid.x;
                float y = outline[index].y - cell.Centroid.y;
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
            EarthMeshIntegrityGate.ValidateInPlaceOrUseFallback(
                mesh,
                EarthMeshIntegrityPolicy.ConvexCollider,
                mesh.name,
                mesh.bounds);
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
