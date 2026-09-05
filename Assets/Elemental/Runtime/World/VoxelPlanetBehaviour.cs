using System;
using System.Collections.Generic;
using Elemental.Simulation.Voxel;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Runtime.World
{
    [DisallowMultipleComponent]
    public sealed class VoxelPlanetBehaviour : MonoBehaviour
    {
        private static readonly ProfilerMarker RenderQueueMarker = new ProfilerMarker("Elemental.Voxel.RenderQueue");
        private static readonly ProfilerMarker ColliderQueueMarker = new ProfilerMarker("Elemental.Voxel.ColliderQueue");
        private static readonly int UsePlanetFrameId = Shader.PropertyToID("_UsePlanetFrame");
        private static readonly int WorldToPlanetId = Shader.PropertyToID("_ProjectionWorldToPlanet");
        private static readonly int PlanetToWorldId = Shader.PropertyToID("_ProjectionPlanetToWorld");

        private sealed class RuntimeChunk
        {
            public GameObject GameObject;
            public MeshFilter Filter;
            public Mesh ActiveMesh;
            public Mesh StagingMesh;
            public bool ActiveMeshShared, StagingMeshShared;
            public MeshCollider ActiveCollider;
            public MeshCollider StagingCollider;
            public uint VisualVersion;
            public uint ColliderVersion;
            public uint StagingVisualVersion;
            public uint StagingColliderVersion;
            public float ColliderDebtAge;
        }

        private sealed class PendingEditTransaction
        {
            public VoxelEditReceipt Receipt;
            public ChunkCoord[] Coords;
            public uint[] RequiredVersions;
        }

        [Header("Canonical state")]
        [SerializeField] private PlanetWorldProfile worldProfile;
        [SerializeField, Min(1f)] private float radius = 1f;
        [SerializeField] private uint seed = 0xE1E0u;
        [SerializeField, Range(4, 32)] private int chunkResolution = 16;
        [SerializeField, Min(0.1f)] private float cellSize = 1f;
        [SerializeField, Min(0f)] private float noiseAmplitude = 0.35f;

        [Header("Budgeted caches")]
        [SerializeField, Min(1)] private int renderChunksPerFrame = 2;
        [SerializeField, Min(1)] private int colliderChunksPerFrame = 1;
        [SerializeField] private Material surfaceMaterial;
        [SerializeField] private PlanetBaseMeshCache baseMeshCache;
        private static readonly ProfilerMarker CacheHydrateMarker = new("Elemental.Voxel.BaseCache.Hydrate");
        public bool BaseCacheUsed { get; private set; }
        public string BaseCacheStatus { get; private set; } = "not-initialized";
        public double BaseCacheHydrateMilliseconds { get; private set; }
        public void ConfigureBaseMeshCache(PlanetBaseMeshCache cache) => baseMeshCache = cache;

        private readonly Queue<ChunkCoord> _renderQueue = new Queue<ChunkCoord>();
        private readonly Queue<ChunkCoord> _colliderQueue = new Queue<ChunkCoord>();
        private readonly HashSet<ChunkCoord> _renderQueued = new HashSet<ChunkCoord>();
        private readonly HashSet<ChunkCoord> _colliderQueued = new HashSet<ChunkCoord>();
        private readonly Dictionary<ChunkCoord, RuntimeChunk> _runtimeChunks =
            new Dictionary<ChunkCoord, RuntimeChunk>();
        private readonly List<ChunkCoord> _dirtyScratch = new List<ChunkCoord>(64);
        private readonly List<Vector3> _uploadVertices = new List<Vector3>(8192);
        private readonly List<Vector3> _uploadNormals = new List<Vector3>(8192);
        private readonly List<int> _uploadIndices = new List<int>(12288);
        private readonly List<PendingEditTransaction> _pendingTransactions =
            new List<PendingEditTransaction>(16);
        private readonly List<ChunkCoord> _transactionCoordScratch = new List<ChunkCoord>(32);
        private readonly List<ChunkCoord> _queueOrderScratch = new List<ChunkCoord>(256);
        private readonly HashSet<ChunkCoord> _queuePriorityScratch = new HashSet<ChunkCoord>();
        private readonly ChunkCoord[] _singlePriorityScratch = new ChunkCoord[1];

        private VoxelPlanetState _state;
        private IChunkMesher _mesher;
        private ChunkMeshBuffers _meshBuffers;
        private VoxelMeshingSettings _meshingSettings;
        private uint _nextEditSequence = 1u;
        private uint _nextTransactionId = 1u;
        private Material _runtimeSurfaceMaterial;

        public VoxelPlanetState State => _state;
        public bool GeometryReady => _state != null && PendingRenderCount == 0 && PendingColliderCount == 0 && PendingEditTransactionCount == 0 && RuntimeChunkCount > 0;
        public float Radius => radius;
        public PlanetWorldProfile WorldProfile => worldProfile;
        public int PendingRenderCount => _renderQueue.Count;
        public int PendingColliderCount => _colliderQueue.Count;
        public int RuntimeChunkCount => _runtimeChunks.Count;
        public int ProcessedChunkCount { get; private set; }
        public int DiscardedStaleBuildCount { get; private set; }
        public int OutstandingColliderDebtCount { get; private set; }
        public double LastRenderQueueMilliseconds { get; private set; }
        public double PeakRenderQueueMilliseconds { get; private set; }
        public double LastColliderQueueMilliseconds { get; private set; }
        public double PeakColliderQueueMilliseconds { get; private set; }
        public int PendingEditTransactionCount => _pendingTransactions.Count;
        public event Action<VoxelEditReceipt> EditCommitted;

        public void ResetQueueTimingTelemetry()
        {
            LastRenderQueueMilliseconds = 0.0;
            PeakRenderQueueMilliseconds = 0.0;
            LastColliderQueueMilliseconds = 0.0;
            PeakColliderQueueMilliseconds = 0.0;
        }

        public void Configure(
            float configuredRadius,
            uint configuredSeed,
            int configuredResolution,
            float configuredCellSize,
            int configuredRenderBudget,
            int configuredColliderBudget,
            Material configuredMaterial)
        {
            radius = configuredRadius;
            seed = configuredSeed;
            chunkResolution = configuredResolution;
            cellSize = configuredCellSize;
            renderChunksPerFrame = configuredRenderBudget;
            colliderChunksPerFrame = configuredColliderBudget;
            surfaceMaterial = configuredMaterial;
        }

        public void Configure(PlanetWorldProfile profile, Material configuredMaterial)
        {
            if (Application.isPlaying && _state != null)
                throw new InvalidOperationException("Planet size is immutable while the world is running. Rebuild the world outside Play Mode.");
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            worldProfile = profile;
            radius = profile.Radius;
            seed = profile.Seed;
            noiseAmplitude = profile.NoiseAmplitude;
            chunkResolution = profile.ChunkResolution;
            cellSize = profile.CellSize;
            renderChunksPerFrame = profile.RenderChunksPerFrame;
            colliderChunksPerFrame = profile.ColliderChunksPerFrame;
            surfaceMaterial = configuredMaterial;
        }

        private void Awake()
        {
            if (surfaceMaterial != null)
            {
                _runtimeSurfaceMaterial = new Material(surfaceMaterial)
                {
                    name = surfaceMaterial.name + " (Planet Runtime)"
                };
                UpdatePlanetProjectionFrame();
            }
            _state = new VoxelPlanetState(radius, seed, chunkResolution, cellSize, noiseAmplitude);
            _meshingSettings = new VoxelMeshingSettings(chunkResolution, cellSize);
            _mesher = new SmoothSdfSurfaceMesher();
            _meshBuffers = new ChunkMeshBuffers();
            if (!TryHydrateBaseCache()) QueueInitialChunks();
        }

        private void Update()
        {
            UpdatePlanetProjectionFrame();
            UpdateColliderDebt(Time.deltaTime);
            ProcessRenderQueue(renderChunksPerFrame);
            ProcessColliderQueue(colliderChunksPerFrame);
            ConfirmCompletedEditTransactions();
        }

        public bool TryGetColliderDebt(ChunkCoord coord, out ColliderDebt debt)
        {
            if (!_runtimeChunks.TryGetValue(coord, out RuntimeChunk runtimeChunk))
            {
                debt = default;
                return false;
            }

            debt = new ColliderDebt(
                coord,
                runtimeChunk.VisualVersion,
                runtimeChunk.ColliderVersion,
                runtimeChunk.ColliderDebtAge,
                float.MaxValue);
            return true;
        }

        public void ApplyEditBatch(EditBatch batch)
        {
            _state.Apply(batch);
            QueueDirtyChunks();
        }

        public VoxelEditReceipt ApplyEditBatchTransactional(EditBatch batch)
        {
            if (batch == null || batch.Count <= 0) return default;
            uint firstSequence = batch[0].Sequence;
            uint lastSequence = batch[batch.Count - 1].Sequence;
            _state.Apply(batch);

            _transactionCoordScratch.Clear();
            for (int editIndex = 0; editIndex < batch.Count; editIndex++)
            {
                VoxelBounds rawBounds = batch[editIndex].GetBounds();
                float3 halo = new float3(_state.CellSize);
                var bounds = new VoxelBounds(rawBounds.Min - halo, rawBounds.Max + halo);
                // Marching-cubes faces sample across chunk boundaries. Marking the
                // one-voxel halo is what keeps both sides of every shared face in
                // the same transaction instead of leaving a temporary seam.
                _state.Chunks.MarkDirty(bounds, _state.ChunkWorldSize);
                ChunkCoord minimum = ChunkCoord.FromPlanetLocal(bounds.Min, _state.ChunkWorldSize);
                ChunkCoord maximum = ChunkCoord.FromPlanetLocal(bounds.Max, _state.ChunkWorldSize);
                for (int z = minimum.Z; z <= maximum.Z; z++)
                for (int y = minimum.Y; y <= maximum.Y; y++)
                for (int x = minimum.X; x <= maximum.X; x++)
                {
                    ChunkCoord coord = new ChunkCoord(x, y, z);
                    if (!_transactionCoordScratch.Contains(coord)) _transactionCoordScratch.Add(coord);
                }
            }
            QueueDirtyChunks();

            var receipt = new VoxelEditReceipt(AllocateTransactionId(), firstSequence, lastSequence);
            var pending = new PendingEditTransaction
            {
                Receipt = receipt,
                Coords = _transactionCoordScratch.ToArray(),
                RequiredVersions = new uint[_transactionCoordScratch.Count]
            };
            for (int index = 0; index < pending.Coords.Length; index++)
                pending.RequiredVersions[index] = _state.Chunks.GetOrCreate(pending.Coords[index]).Version;
            _pendingTransactions.Add(pending);
            // A freshly authored extraction must not wait behind the planet's entire
            // cold-start shell queue. Reordering only the already-budgeted work keeps
            // frame cost bounded while making the visible matter transaction responsive.
            PrioritizeQueuedCoordinates(_renderQueue, _renderQueued, pending.Coords);
            return receipt;
        }

        public void ApplySphereEdit(Vector3 planetLocalCenter, float editRadius, bool additive)
        {
            SdfEdit edit = new SdfEdit(
                _nextEditSequence++,
                additive ? SdfEditKind.AddSphere : SdfEditKind.SubtractSphere,
                ToFloat3(planetLocalCenter),
                ToFloat3(planetLocalCenter),
                editRadius,
                new VoxelMaterialId(1));
            ApplyEditBatch(new EditBatch(edit));
        }

        public VoxelEditReceipt ApplySphereEditTransactional(
            Vector3 planetLocalCenter,
            float editRadius,
            bool additive)
        {
            SdfEdit edit = new SdfEdit(
                _nextEditSequence++,
                additive ? SdfEditKind.AddSphere : SdfEditKind.SubtractSphere,
                ToFloat3(planetLocalCenter),
                ToFloat3(planetLocalCenter),
                editRadius,
                new VoxelMaterialId(1));
            return ApplyEditBatchTransactional(new EditBatch(edit));
        }

        public bool IsEditCommitted(VoxelEditReceipt receipt)
        {
            if (!receipt.IsValid) return false;
            for (int index = 0; index < _pendingTransactions.Count; index++)
                if (_pendingTransactions[index].Receipt.Equals(receipt)) return false;
            return receipt.TransactionId < _nextTransactionId;
        }

        public void ApplyCapsuleEdit(Vector3 pointA, Vector3 pointB, float editRadius, bool additive)
        {
            SdfEdit edit = new SdfEdit(
                _nextEditSequence++,
                additive ? SdfEditKind.AddCapsule : SdfEditKind.SubtractCapsule,
                ToFloat3(pointA),
                ToFloat3(pointB),
                editRadius,
                new VoxelMaterialId(1));
            ApplyEditBatch(new EditBatch(edit));
        }

        public void ApplySplineEdit(Vector3[] planetLocalPath, float editRadius, bool additive)
        {
            if (planetLocalPath == null || planetLocalPath.Length < 2)
            {
                throw new ArgumentException("A spline edit needs at least two points.", nameof(planetLocalPath));
            }

            SdfEdit[] edits = new SdfEdit[planetLocalPath.Length - 1];
            for (int index = 0; index < edits.Length; index++)
            {
                edits[index] = new SdfEdit(
                    _nextEditSequence++,
                    additive ? SdfEditKind.AddCapsule : SdfEditKind.SubtractCapsule,
                    ToFloat3(planetLocalPath[index]),
                    ToFloat3(planetLocalPath[index + 1]),
                    editRadius,
                    new VoxelMaterialId(1));
            }

            ApplyEditBatch(new EditBatch(edits));
        }

        private bool TryHydrateBaseCache()
        {
            if (baseMeshCache == null) { BaseCacheStatus = "not-configured: budgeted meshing"; return false; }
            if (!baseMeshCache.Matches(_state))
            {
                BaseCacheStatus = "stale signature: budgeted meshing";
                Debug.LogWarning("Planet base mesh cache is stale. Run Elemental/World/Bake Startup Caches In Current Scene. Using canonical budgeted meshing.", this);
                return false;
            }
            var seen = new HashSet<ChunkCoord>();
            foreach (var entry in baseMeshCache.Entries)
                if (entry.Mesh == null || !seen.Add(entry.Coord))
                {
                    BaseCacheStatus = "invalid entries: budgeted meshing";
                    Debug.LogWarning("Planet base mesh cache has missing/duplicate chunks. Rebake startup caches.", this);
                    return false;
                }
            // Reject incomplete caches before creating any visible runtime chunk.
            float size = _meshingSettings.ChunkWorldSize;
            int minimum = Mathf.FloorToInt(-radius / size), maximum = Mathf.FloorToInt(radius / size);
            int expected = 0;
            for (int z = minimum; z <= maximum; z++)
            for (int y = minimum; y <= maximum; y++)
            for (int x = minimum; x <= maximum; x++)
                if (PlanetChunkShellSolver.IntersectsSurfaceShell(new int3(x,y,z), size, radius, noiseAmplitude + cellSize * 1.5f))
                {
                    expected++;
                    if (!seen.Contains(new ChunkCoord(x,y,z))) { BaseCacheStatus = "incomplete: budgeted meshing"; Debug.LogWarning("Planet base mesh cache is incomplete. Rebake startup caches.", this); return false; }
                }
            if (expected != seen.Count) { BaseCacheStatus = "extra chunks: budgeted meshing"; Debug.LogWarning("Planet base mesh cache has unexpected chunks. Rebake startup caches.", this); return false; }
            double started = Time.realtimeSinceStartupAsDouble;
            using (CacheHydrateMarker.Auto())
                foreach (var entry in baseMeshCache.Entries)
                {
                    RuntimeChunk runtime = GetOrCreateRuntimeChunk(entry.Coord, entry.Mesh);
                    VoxelChunkState chunk = _state.Chunks.GetOrCreate(entry.Coord);
                    runtime.VisualVersion = runtime.ColliderVersion = chunk.Version;
                    chunk.MarkBuilt(entry.ContentHash);
                    ProcessedChunkCount++;
                }
            BaseCacheHydrateMilliseconds = (Time.realtimeSinceStartupAsDouble - started) * 1000;
            BaseCacheUsed = true; BaseCacheStatus = "exact baked base";
            return true;
        }

        private void QueueInitialChunks()
        {
            float chunkSize = _meshingSettings.ChunkWorldSize;
            int minimum = Mathf.FloorToInt(-radius / chunkSize);
            int maximum = Mathf.FloorToInt(radius / chunkSize);

            for (int z = minimum; z <= maximum; z++)
            {
                for (int y = minimum; y <= maximum; y++)
                {
                    for (int x = minimum; x <= maximum; x++)
                    {
                        ChunkCoord coord = new ChunkCoord(x, y, z);
                        if (!PlanetChunkShellSolver.IntersectsSurfaceShell(
                                new int3(x, y, z),
                                chunkSize,
                                radius,
                                noiseAmplitude + cellSize * 1.5f))
                            continue;
                        _state.Chunks.GetOrCreate(coord);
                        EnqueueRender(coord);
                    }
                }
            }
        }

        private void QueueDirtyChunks()
        {
            _dirtyScratch.Clear();
            _state.Chunks.CollectDirty(_dirtyScratch);
            for (int index = 0; index < _dirtyScratch.Count; index++)
            {
                EnqueueRender(_dirtyScratch[index]);
            }
        }

        private void EnqueueRender(ChunkCoord coord)
        {
            if (_renderQueued.Add(coord))
            {
                _renderQueue.Enqueue(coord);
            }
        }

        private void EnqueueCollider(ChunkCoord coord)
        {
            if (_colliderQueued.Add(coord))
            {
                _colliderQueue.Enqueue(coord);
            }
        }

        private void ProcessRenderQueue(int budget)
        {
            double startedAt = Time.realtimeSinceStartupAsDouble;
            using (RenderQueueMarker.Auto())
            {
                for (int processed = 0; processed < budget && _renderQueue.Count > 0; processed++)
                {
                    ChunkCoord coord = _renderQueue.Dequeue();
                    _renderQueued.Remove(coord);
                    VoxelChunkState chunkState = _state.Chunks.GetOrCreate(coord);
                    var request = new MeshBuildRequest(coord, chunkState.Version);
                    _mesher.Build(_state, coord, _meshingSettings, _meshBuffers);
                    if (chunkState.Version != request.ExpectedVersion)
                    {
                        DiscardedStaleBuildCount++;
                        EnqueueRender(coord);
                        continue;
                    }

                    bool stageForTransaction = RequiresTransactionalStaging(
                        coord, request.ExpectedVersion);
                    UploadMesh(coord, request.ExpectedVersion, stageForTransaction);
                    ulong hash = _state.ComputeChunkHash(coord);
                    if (!chunkState.TryMarkBuilt(request.ExpectedVersion, hash))
                    {
                        DiscardedStaleBuildCount++;
                        EnqueueRender(coord);
                        continue;
                    }

                    ProcessedChunkCount++;
                }
            }
            LastRenderQueueMilliseconds = (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;
            PeakRenderQueueMilliseconds = Math.Max(PeakRenderQueueMilliseconds, LastRenderQueueMilliseconds);
        }

        private void UploadMesh(ChunkCoord coord, uint visualVersion, bool stageForTransaction)
        {
            RuntimeChunk runtimeChunk = GetOrCreateRuntimeChunk(coord);
            if (stageForTransaction)
            {
                if (runtimeChunk.StagingMesh == null || runtimeChunk.StagingMeshShared)
                    runtimeChunk.StagingMesh = new Mesh { name = $"Voxel Chunk {coord} (Staging)" };
                runtimeChunk.StagingMeshShared = false;
            }
            else
            {
                if (runtimeChunk.ActiveMeshShared)
                {
                    runtimeChunk.ActiveMesh = new Mesh { name = $"Voxel Chunk {coord} (Edited)" };
                    runtimeChunk.Filter.sharedMesh = runtimeChunk.ActiveMesh;
                    runtimeChunk.ActiveMeshShared = false;
                }
            }
            Mesh mesh = stageForTransaction ? runtimeChunk.StagingMesh : runtimeChunk.ActiveMesh;
            if (_meshBuffers.Vertices.Length == 0)
            {
                mesh.Clear();
                MarkVisualVersion(runtimeChunk, visualVersion, stageForTransaction);
                EnqueueCollider(coord);
                if (stageForTransaction) PrioritizeCollider(coord);

                return;
            }

            MarkVisualVersion(runtimeChunk, visualVersion, stageForTransaction);
            _uploadVertices.Clear();
            _uploadNormals.Clear();
            _uploadIndices.Clear();

            for (int index = 0; index < _meshBuffers.Vertices.Length; index++)
            {
                float3 vertex = _meshBuffers.Vertices[index];
                float3 normal = _meshBuffers.Normals[index];
                _uploadVertices.Add(new Vector3(vertex.x, vertex.y, vertex.z));
                _uploadNormals.Add(new Vector3(normal.x, normal.y, normal.z));
            }

            for (int index = 0; index < _meshBuffers.Indices.Length; index++)
            {
                _uploadIndices.Add(_meshBuffers.Indices[index]);
            }

            mesh.Clear();
            mesh.indexFormat = _uploadVertices.Count > ushort.MaxValue
                ? IndexFormat.UInt32
                : IndexFormat.UInt16;
            mesh.SetVertices(_uploadVertices);
            mesh.SetNormals(_uploadNormals);
            mesh.SetTriangles(_uploadIndices, 0, true);
            mesh.RecalculateBounds();
            EnqueueCollider(coord);
            if (stageForTransaction) PrioritizeCollider(coord);
        }

        private void PrioritizeCollider(ChunkCoord coord)
        {
            _singlePriorityScratch[0] = coord;
            PrioritizeQueuedCoordinates(_colliderQueue, _colliderQueued, _singlePriorityScratch);
        }

        private void PrioritizeQueuedCoordinates(
            Queue<ChunkCoord> queue,
            HashSet<ChunkCoord> queued,
            IReadOnlyList<ChunkCoord> priority)
        {
            if (queue.Count <= 1 || priority == null || priority.Count == 0) return;
            _queuePriorityScratch.Clear();
            for (int index = 0; index < priority.Count; index++)
                if (queued.Contains(priority[index])) _queuePriorityScratch.Add(priority[index]);
            if (_queuePriorityScratch.Count == 0) return;

            _queueOrderScratch.Clear();
            while (queue.Count > 0) _queueOrderScratch.Add(queue.Dequeue());
            for (int index = 0; index < priority.Count; index++)
                if (_queuePriorityScratch.Contains(priority[index])) queue.Enqueue(priority[index]);
            for (int index = 0; index < _queueOrderScratch.Count; index++)
                if (!_queuePriorityScratch.Contains(_queueOrderScratch[index]))
                    queue.Enqueue(_queueOrderScratch[index]);
        }

        private RuntimeChunk GetOrCreateRuntimeChunk(ChunkCoord coord, Mesh sharedBase = null)
        {
            if (_runtimeChunks.TryGetValue(coord, out RuntimeChunk runtimeChunk))
            {
                return runtimeChunk;
            }

            GameObject chunkObject = new GameObject($"Voxel Chunk {coord}");
            chunkObject.transform.SetParent(transform, false);
            Mesh mesh = sharedBase != null ? sharedBase : new Mesh { name = $"Voxel Chunk {coord}" };
            Mesh stagingMesh = null;
            MeshFilter filter = chunkObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = chunkObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _runtimeSurfaceMaterial != null ? _runtimeSurfaceMaterial : surfaceMaterial;
            MeshCollider collider = chunkObject.AddComponent<MeshCollider>();
            MeshCollider stagingCollider = chunkObject.AddComponent<MeshCollider>();
            stagingCollider.enabled = false;
            filter.sharedMesh = mesh;
            if (sharedBase != null && sharedBase.vertexCount > 0) collider.sharedMesh = sharedBase;

            runtimeChunk = new RuntimeChunk
            {
                GameObject = chunkObject,
                Filter = filter,
                ActiveMesh = mesh,
                ActiveMeshShared = sharedBase != null,
                StagingMesh = stagingMesh,
                ActiveCollider = collider,
                StagingCollider = stagingCollider
            };
            _runtimeChunks.Add(coord, runtimeChunk);
            return runtimeChunk;
        }

        private void ProcessColliderQueue(int budget)
        {
            double startedAt = Time.realtimeSinceStartupAsDouble;
            using (ColliderQueueMarker.Auto())
            {
                for (int processed = 0; processed < budget && _colliderQueue.Count > 0; processed++)
                {
                    ChunkCoord coord = _colliderQueue.Dequeue();
                    _colliderQueued.Remove(coord);
                    if (!_runtimeChunks.TryGetValue(coord, out RuntimeChunk runtimeChunk))
                    {
                        continue;
                    }

                    if (runtimeChunk.StagingVisualVersion > runtimeChunk.VisualVersion)
                    {
                        runtimeChunk.StagingCollider.sharedMesh = null;
                        if (runtimeChunk.StagingMesh.vertexCount > 0)
                            runtimeChunk.StagingCollider.sharedMesh = runtimeChunk.StagingMesh;
                        runtimeChunk.StagingColliderVersion = runtimeChunk.StagingVisualVersion;
                    }
                    else
                    {
                        runtimeChunk.ActiveCollider.sharedMesh = null;
                        if (runtimeChunk.ActiveMesh.vertexCount > 0)
                            runtimeChunk.ActiveCollider.sharedMesh = runtimeChunk.ActiveMesh;
                        runtimeChunk.ColliderVersion = runtimeChunk.VisualVersion;
                    }
                    runtimeChunk.ColliderDebtAge = 0f;
                }
            }
            LastColliderQueueMilliseconds = (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;
            PeakColliderQueueMilliseconds = Math.Max(
                PeakColliderQueueMilliseconds,
                LastColliderQueueMilliseconds);
        }

        private static void MarkVisualVersion(
            RuntimeChunk runtimeChunk,
            uint visualVersion,
            bool staged)
        {
            if (staged)
            {
                runtimeChunk.StagingVisualVersion = visualVersion;
                runtimeChunk.StagingColliderVersion = 0u;
                return;
            }
            if (runtimeChunk.VisualVersion != visualVersion)
            {
                runtimeChunk.VisualVersion = visualVersion;
                runtimeChunk.ColliderDebtAge = 0f;
            }
        }

        private void UpdateColliderDebt(float deltaTime)
        {
            OutstandingColliderDebtCount = 0;
            foreach (KeyValuePair<ChunkCoord, RuntimeChunk> pair in _runtimeChunks)
            {
                RuntimeChunk runtimeChunk = pair.Value;
                if (runtimeChunk.VisualVersion <= runtimeChunk.ColliderVersion)
                {
                    runtimeChunk.ColliderDebtAge = 0f;
                    continue;
                }

                runtimeChunk.ColliderDebtAge += Mathf.Max(0f, deltaTime);
                OutstandingColliderDebtCount++;
            }
        }

        private void ConfirmCompletedEditTransactions()
        {
            for (int transactionIndex = _pendingTransactions.Count - 1; transactionIndex >= 0; transactionIndex--)
            {
                PendingEditTransaction pending = _pendingTransactions[transactionIndex];
                bool ready = true;
                for (int index = 0; index < pending.Coords.Length; index++)
                {
                    ChunkCoord coord = pending.Coords[index];
                    uint required = pending.RequiredVersions[index];
                    if (!_state.Chunks.TryGet(coord, out VoxelChunkState state) || state.IsDirty ||
                        !_runtimeChunks.TryGetValue(coord, out RuntimeChunk runtime) ||
                        !HasCommittedOrStagedVersion(runtime, required))
                    {
                        ready = false;
                        break;
                    }
                }
                if (!ready) continue;
                CommitStagedTransaction(pending);
                VoxelEditReceipt receipt = pending.Receipt;
                _pendingTransactions.RemoveAt(transactionIndex);
                EditCommitted?.Invoke(receipt);
            }
        }

        private bool RequiresTransactionalStaging(ChunkCoord coord, uint version)
        {
            for (int transactionIndex = 0; transactionIndex < _pendingTransactions.Count; transactionIndex++)
            {
                PendingEditTransaction pending = _pendingTransactions[transactionIndex];
                for (int index = 0; index < pending.Coords.Length; index++)
                {
                    if (!pending.Coords[index].Equals(coord) || pending.RequiredVersions[index] > version)
                        continue;
                    if (_runtimeChunks.TryGetValue(coord, out RuntimeChunk runtime) &&
                        runtime.VisualVersion >= pending.RequiredVersions[index] &&
                        runtime.ColliderVersion >= pending.RequiredVersions[index]) continue;
                    return true;
                }
            }
            return false;
        }

        private static bool HasCommittedOrStagedVersion(RuntimeChunk runtime, uint required)
        {
            bool committed = runtime.VisualVersion >= required && runtime.ColliderVersion >= required;
            bool staged = runtime.StagingVisualVersion >= required &&
                          runtime.StagingColliderVersion >= required;
            return committed || staged;
        }

        private void CommitStagedTransaction(PendingEditTransaction pending)
        {
            // Every staged Mesh and disabled collider is already prepared. The loop only
            // swaps references/enabled flags, so adjacent chunks become visible and
            // physical in the same frame without rebuilding or cooking here.
            for (int index = 0; index < pending.Coords.Length; index++)
            {
                uint required = pending.RequiredVersions[index];
                if (!_runtimeChunks.TryGetValue(pending.Coords[index], out RuntimeChunk runtime) ||
                    runtime.VisualVersion >= required && runtime.ColliderVersion >= required)
                    continue;
                if (runtime.StagingVisualVersion < required || runtime.StagingColliderVersion < required)
                    continue;

                runtime.ActiveCollider.enabled = false;
                runtime.StagingCollider.enabled = true;
                runtime.Filter.sharedMesh = runtime.StagingMesh;

                (runtime.ActiveMesh, runtime.StagingMesh) =
                    (runtime.StagingMesh, runtime.ActiveMesh);
                (runtime.ActiveMeshShared, runtime.StagingMeshShared) =
                    (runtime.StagingMeshShared, runtime.ActiveMeshShared);
                (runtime.ActiveCollider, runtime.StagingCollider) =
                    (runtime.StagingCollider, runtime.ActiveCollider);
                runtime.VisualVersion = runtime.StagingVisualVersion;
                runtime.ColliderVersion = runtime.StagingColliderVersion;
                runtime.StagingVisualVersion = 0u;
                runtime.StagingColliderVersion = 0u;
                runtime.StagingCollider.enabled = false;
                runtime.ColliderDebtAge = 0f;
            }
        }

        private uint AllocateTransactionId()
        {
            uint value = _nextTransactionId++;
            if (_nextTransactionId == 0u) _nextTransactionId = 1u;
            return value == 0u ? _nextTransactionId++ : value;
        }

        private void OnDestroy()
        {
            if (_mesher is IDisposable disposableMesher) disposableMesher.Dispose();
            _meshBuffers?.Dispose();
            foreach (KeyValuePair<ChunkCoord, RuntimeChunk> pair in _runtimeChunks)
            {
                if (Application.isPlaying)
                {
                    if (pair.Value.ActiveMesh != null && !pair.Value.ActiveMeshShared) Destroy(pair.Value.ActiveMesh);
                    if (pair.Value.StagingMesh != null && !pair.Value.StagingMeshShared) Destroy(pair.Value.StagingMesh);
                }
                else
                {
                    if (pair.Value.ActiveMesh != null && !pair.Value.ActiveMeshShared) DestroyImmediate(pair.Value.ActiveMesh);
                    if (pair.Value.StagingMesh != null && !pair.Value.StagingMeshShared) DestroyImmediate(pair.Value.StagingMesh);
                }
            }
            if (_runtimeSurfaceMaterial != null)
            {
                if (Application.isPlaying) Destroy(_runtimeSurfaceMaterial);
                else DestroyImmediate(_runtimeSurfaceMaterial);
            }
        }

        private void UpdatePlanetProjectionFrame()
        {
            if (_runtimeSurfaceMaterial == null) return;
            _runtimeSurfaceMaterial.SetFloat(UsePlanetFrameId, 1f);
            _runtimeSurfaceMaterial.SetMatrix(WorldToPlanetId, transform.worldToLocalMatrix);
            _runtimeSurfaceMaterial.SetMatrix(PlanetToWorldId, transform.localToWorldMatrix);
        }

        private void OnDrawGizmosSelected()
        {
            if (_state == null)
            {
                return;
            }

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            float chunkSize = _state.ChunkWorldSize;

            foreach (KeyValuePair<ChunkCoord, RuntimeChunk> pair in _runtimeChunks)
            {
                bool dirty = _state.Chunks.TryGet(pair.Key, out VoxelChunkState chunkState) && chunkState.IsDirty;
                Gizmos.color = dirty
                    ? new Color(1f, 0.45f, 0.15f, 0.8f)
                    : new Color(0.15f, 0.8f, 0.75f, 0.35f);
                float3 minimum = pair.Key.GetPlanetLocalMin(chunkSize);
                Vector3 center = new Vector3(minimum.x, minimum.y, minimum.z) + (Vector3.one * chunkSize * 0.5f);
                Gizmos.DrawWireCube(center, Vector3.one * chunkSize);
            }

            Gizmos.matrix = previousMatrix;
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }
    }
}
