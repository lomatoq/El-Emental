using System.Collections.Generic;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Geometry;
using Elemental.Runtime.Matter;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Combat;
using Elemental.Simulation.Matter;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;

namespace Elemental.Runtime.Physics
{
    public readonly struct EarthPillarWavePulse
    {
        public EarthPillarWavePulse(
            Vector3 position, Vector3 up, Vector3 outward,
            float width, float height, float crest01, uint stableId)
        {
            Position = position;
            Up = up;
            Outward = outward;
            Width = width;
            Height = height;
            Crest01 = crest01;
            StableId = stableId;
        }

        public Vector3 Position { get; }
        public Vector3 Up { get; }
        public Vector3 Outward { get; }
        public float Width { get; }
        public float Height { get; }
        public float Crest01 { get; }
        public uint StableId { get; }
    }

    [DisallowMultipleComponent]
    public sealed class EarthPillarWavePool : MonoBehaviour
    {
        private static readonly Collider[] ImpactHits = new Collider[24];
        private static readonly ProfilerMarker ScheduleMarker = new("Elemental.Wave.Schedule");

        [SerializeField, Range(64, 96)] private int capacity = 96;
        [SerializeField] private Mesh columnMesh;
        [SerializeField] private Mesh[] columnMeshVariants;
        [SerializeField, Tooltip("Compatibility only. Production waves use deterministic polygonal web-cell meshes.")]
        private bool useLegacyColumnMeshes;
        [SerializeField] private Material columnMaterial;
        [SerializeField] private Transform planetCenter;
        [SerializeField] private EarthPillarWaveProfile profile;
        [SerializeField] private EarthSurfaceQueryService surfaceQueries;
        [SerializeField] private EarthStoneBevelProfile stoneBevelProfile;
        [SerializeField] private EarthMaterialFeedbackHub materialFeedback;
        private readonly EarthStoneRenderBevelCache _renderBevels = new();
        public void ConfigureMaterialFeedback(EarthMaterialFeedbackHub hub)
        {
            materialFeedback = hub;
            for (int index = 0; index < _columns.Count; index++) _columns[index].ConfigureMaterialFeedback(hub);
        }

        private readonly List<EarthPillarWaveColumn> _columns = new List<EarthPillarWaveColumn>(96);
        public int RejectedBusyCasts { get; private set; }
        private float _protectedWaveUntil;
        public float LastWaveDuration { get; private set; }
        public float LastWaveTravelSeconds { get; private set; }
        public float LastWaveEffectiveSpeed { get; private set; }
        private bool HasAnchoredColumns
        {
            get { foreach (var column in _columns) if (column.IsAnchoredAnimation) return true; return false; }
        }
        public int AvailableColumns
        {
            get { int free = capacity - _columns.Count; foreach (var column in _columns) if (!column.gameObject.activeSelf) free++; return free; }
        }
        private uint _nextPulseId = 1u;
        private uint _nextImpactCastId = 0x57000001u;
        private readonly uint[] _claimedImpactCasts = new uint[96];
        private int _claimCursor;
        private uint _claimedArenaStructureId;
        private int _nextTopologySeed;
        private EarthWaveSemanticFamily _lastSemanticFamily = EarthWaveSemanticFamily.RollingTerraces;
        private EarthMatterKernelBehaviour _matterKernel;
        private readonly Mesh[] _webCellMeshes = new Mesh[6];
        private readonly Mesh[] _webCellRenderMeshes = new Mesh[6];
        private readonly Matrix4x4[][] _webMatrices = new Matrix4x4[6][];
        private readonly int[] _webMatrixCounts = new int[6];

        public event System.Action<EarthPillarWavePulse> ColumnBurst;
        public uint LastFaultLineTargetStructureId => _claimedArenaStructureId;

        public bool TryClaimFaultLineTarget(uint impactCastId, uint structureId)
        {
            if (impactCastId == 0u || structureId == 0u) return false;
            for (int i = 0; i < _claimedImpactCasts.Length; i++)
                if (_claimedImpactCasts[i] == impactCastId) return false;
            _claimedImpactCasts[_claimCursor] = impactCastId;
            _claimCursor = (_claimCursor + 1) % _claimedImpactCasts.Length;
            _claimedArenaStructureId = structureId;
            return true;
        }
        public WaveMotionMode MotionMode => profile != null
            ? profile.MotionMode
            : WaveMotionMode.Legacy;
        public EarthPillarWaveProfile Profile => profile;
        public EarthMatterId PrimaryMatterId
        {
            get
            {
                for (int index = 0; index < _columns.Count; index++)
                {
                    EarthPillarWaveColumn column = _columns[index];
                    EarthMatterIdentity identity = column != null && column.gameObject.activeSelf
                        ? column.MatterIdentity
                        : null;
                    if (identity != null && identity.MatterId.IsValid) return identity.MatterId;
                }
                return default;
            }
        }

        public void Configure(
            int configuredCapacity,
            Mesh mesh,
            Material material,
            Transform configuredPlanetCenter,
            EarthPillarWaveProfile configuredProfile)
        {
            capacity = Mathf.Clamp(configuredCapacity, 64, 96);
            columnMesh = mesh;
            columnMaterial = material;
            planetCenter = configuredPlanetCenter;
            profile = configuredProfile;
        }

        public void ConfigureSurfaceQueries(EarthSurfaceQueryService configuredService) =>
            surfaceQueries = configuredService;

        public void ConfigureMeshVariants(params Mesh[] meshes)
        {
            columnMeshVariants = meshes;
            if (!useLegacyColumnMeshes) return;
            for (int index = 0; index < _columns.Count; index++)
            {
                MeshFilter filter = _columns[index].GetComponent<MeshFilter>();
                if (filter != null && meshes != null && meshes.Length > 0)
                {
                    Mesh mesh = meshes[index % meshes.Length];
                    filter.sharedMesh = _renderBevels.Get(mesh, stoneBevelProfile);
                    MeshCollider meshCollider = _columns[index].GetComponent<MeshCollider>();
                    if (meshCollider != null) meshCollider.sharedMesh = mesh;
                }
            }
        }

        private void Awake()
        {
            if (surfaceQueries == null)
                surfaceQueries = FindAnyObjectByType<EarthSurfaceQueryService>(FindObjectsInactive.Include);
            _matterKernel = EarthMatterKernelBehaviour.FindOrCreate(this);
            if (!useLegacyColumnMeshes)
            {
                for (int family = 0; family < _webCellMeshes.Length; family++)
                {
                    _webCellMeshes[family] = EarthWebWaveCellMeshFactory.Create(family);
                    _webCellRenderMeshes[family] = _renderBevels.Get(_webCellMeshes[family], stoneBevelProfile);
                    _webMatrices[family] = new Matrix4x4[capacity];
                }
            }
            for (int index = 0; index < capacity; index++) CreateColumn();
        }

        private void LateUpdate()
        {
            if (useLegacyColumnMeshes || columnMaterial == null) return;
            System.Array.Clear(_webMatrixCounts, 0, _webMatrixCounts.Length);
            for (int index = 0; index < _columns.Count; index++)
            {
                EarthPillarWaveColumn column = _columns[index];
                if (column != null) column.UpdateRenderPose();
                if (column == null || !column.TryGetInstancedRenderMatrix(out Matrix4x4 matrix)) continue;
                int family = index % _webCellMeshes.Length;
                _webMatrices[family][_webMatrixCounts[family]++] = matrix;
            }
            Vector3 center = planetCenter != null ? planetCenter.position : transform.position;
            for (int family = 0; family < _webCellMeshes.Length; family++)
            {
                int count = _webMatrixCounts[family];
                if (count <= 0 || _webCellMeshes[family] == null) continue;
                var renderParams = new RenderParams(columnMaterial)
                {
                    worldBounds = new Bounds(center, Vector3.one * 256f),
                    layer = gameObject.layer,
                    shadowCastingMode = ShadowCastingMode.On,
                    receiveShadows = true
                };
                Graphics.RenderMeshInstanced(
                    renderParams,
                    _webCellRenderMeshes[family],
                    0,
                    _webMatrices[family],
                    count);
            }
        }

        private void OnDestroy()
        {
            _renderBevels.Clear();
            if (useLegacyColumnMeshes) return;
            for (int index = 0; index < _webCellMeshes.Length; index++)
            {
                if (_webCellMeshes[index] == null) continue;
                Destroy(_webCellMeshes[index]);
                _webCellMeshes[index] = null;
            }
        }

        public int Launch(
            Vector3 surfaceOrigin,
            Vector3 localUp,
            Vector3 forward,
            float sectorCharge01,
            float powerCharge01,
            Rigidbody caster)
        {
            using var schedule = ScheduleMarker.Auto();
            if (Time.time < _protectedWaveUntil || HasAnchoredColumns) { RejectedBusyCasts++; return 0; }
            uint impactCastId = _nextImpactCastId++;
            if (_nextImpactCastId == 0u) _nextImpactCastId = 0x57000001u;
            EarthWaveSemanticFamily family = EarthWaveFamilySelector.Select(
                sectorCharge01,
                powerCharge01,
                _lastSemanticFamily,
                _nextTopologySeed);
            _lastSemanticFamily = family;
            EarthWebWaveTopology topology;
            if (profile != null)
            {
                EarthPillarWaveTuning tuning = profile.Tuning;
                topology = EarthPillarWaveSolver.BuildTopology(
                    sectorCharge01,
                    powerCharge01,
                    in tuning,
                    _nextTopologySeed,
                    family);
            }
            else
            {
                EarthPillarWaveTuning tuning = EarthPillarWaveTuning.Default;
                topology = EarthPillarWaveSolver.BuildTopology(
                    sectorCharge01,
                    powerCharge01,
                    in tuning,
                    _nextTopologySeed,
                    family);
            }
            // Reserve the complete cast atomically. Never rewrite a still-visible
            // fracture because a player/bot launches another move during a long phase.
            if (AvailableColumns < topology.Cells.Length) { RejectedBusyCasts++; return 0; }
            _nextTopologySeed = (_nextTopologySeed + 1) % 6;
            Vector3 center = planetCenter != null ? planetCenter.position : Vector3.zero;
            Vector3 up = localUp.sqrMagnitude > 0.5f ? localUp.normalized : (surfaceOrigin - center).normalized;
            ResolveConstructedSurface(ref surfaceOrigin, ref up);
            float planetRadius = Mathf.Max(1f, Vector3.Distance(surfaceOrigin, center));
            Vector3 tangentForward = Vector3.ProjectOnPlane(forward, up).normalized;
            if (tangentForward.sqrMagnitude < 0.5f) tangentForward = Vector3.Cross(up, Vector3.right).normalized;
            float impulse = Mathf.Lerp(
                profile != null ? profile.MinimumImpulse : 85f,
                profile != null ? profile.MaximumImpulse : 420f,
                Mathf.Clamp01(powerCharge01));
            float firstDistance = float.MaxValue, lastDistance = 0f;
            foreach (var cell in topology.Cells)
            {
                firstDistance = Mathf.Min(firstDistance, cell.Sample.ArcDistance);
                lastDistance = Mathf.Max(lastDistance, cell.Sample.ArcDistance);
            }
            var timing = profile != null ? profile.AnimationTiming : new EarthWaveAnimationTiming(.055f,.36f,.14f,.1f,.46f);
            var travel = new EarthWaveTravelSchedule(firstDistance, lastDistance,
                profile != null ? profile.Tuning.WaveSpeed : 6f, in timing);
            LastWaveDuration = travel.Duration;
            LastWaveTravelSeconds = travel.TravelSeconds;
            LastWaveEffectiveSpeed = travel.EffectiveSpeed;
            for (int index = 0; index < topology.Cells.Length; index++)
            {
                EarthWebWaveCell cell = topology.Cells[index];
                EarthPillarWaveSample sample = cell.Sample;
                Vector3 tangentDirection = Quaternion.AngleAxis(sample.AngleDegrees, up) * tangentForward;
                // The Voronoi partition is authored in one tangent plane. Independent
                // radial/sampled normals rotate those prisms into one another, especially
                // over arena stone faces. Keep its XZ frame; only ground height varies.
                Vector3 columnUp = up;
                float radialDrop = Mathf.Sqrt(Mathf.Max(0f, planetRadius * planetRadius -
                    sample.ArcDistance * sample.ArcDistance)) - planetRadius;
                Vector3 surface = surfaceOrigin + tangentDirection * sample.ArcDistance + up * radialDrop;
                Vector3 sampledSurface = surface, sampledNormal = up;
                ResolveConstructedSurface(ref sampledSurface, ref sampledNormal);
                surface += up * Vector3.Dot(sampledSurface - surface, up);
                Vector3 columnForward = tangentDirection;
                EarthPillarWaveColumn column = Acquire();
                column.Schedule(
                    this,
                    surface,
                    columnUp,
                    columnForward,
                    (profile != null ? profile.Tuning.CrestHeight : 1.55f) * Mathf.Lerp(.72f, 1f, Mathf.Clamp01(powerCharge01)),
                    sample.Width,
                    sample.Depth,
                    travel.Delay(sample.ArcDistance),
                    sample.HoldDuration,
                    1f,
                    _nextPulseId++,
                    impulse,
                    caster,
                    profile,
                    ImpactHits,
                    sample.ShapeSides,
                    sample.ShapeAreaScale,
                    sample.SpiralPhase01,
                    cell.Footprint,
                    cell.VisualArea,
                    impactCastId);
            }
            // Keep the complete partition reserved until its last row has passed.
            // A second cast cannot fill already-retreated slots with a new topology
            // while the rest of this wave is still on screen.
            _protectedWaveUntil = Time.time + LastWaveDuration;
            return topology.Cells.Length;
        }

        private void ResolveConstructedSurface(ref Vector3 surface, ref Vector3 up)
        {
            if (surfaceQueries == null) return;
            Vector3 safeUp = up.sqrMagnitude > 0.5f ? up.normalized : Vector3.up;
            EarthSurfaceQuery query = EarthWaveSurfaceFollow.CreateQuery(
                new float3(surface.x, surface.y, surface.z),
                new float3(safeUp.x, safeUp.y, safeUp.z));
            if (!surfaceQueries.TrySample(in query, out EarthSurfaceSample sample)) return;
            surface = new Vector3(sample.Point.x, sample.Point.y, sample.Point.z);
            up = new Vector3(sample.Normal.x, sample.Normal.y, sample.Normal.z).normalized;
        }

        public int LaunchCrest(
            Vector3 surfaceOrigin,
            Vector3 localUp,
            Vector3 forward,
            int requestedCount,
            Rigidbody caster)
        {
            int count = requestedCount <= 1 ? 1 : requestedCount <= 3 ? 3 : requestedCount <= 5 ? 5 : 7;
            if (Time.time < _protectedWaveUntil || HasAnchoredColumns || AvailableColumns < count) { RejectedBusyCasts++; return 0; }
            uint impactCastId = _nextImpactCastId++;
            if (_nextImpactCastId == 0u) _nextImpactCastId = 0x57000001u;
            Vector3 center = planetCenter != null ? planetCenter.position : Vector3.zero;
            Vector3 up = localUp.sqrMagnitude > 0.5f ? localUp.normalized : (surfaceOrigin - center).normalized;
            ResolveConstructedSurface(ref surfaceOrigin, ref up);
            float radius = Mathf.Max(1f, Vector3.Distance(surfaceOrigin, center));
            Vector3 tangentForward = Vector3.ProjectOnPlane(forward, up).normalized;
            if (tangentForward.sqrMagnitude < 0.5f) tangentForward = Vector3.Cross(up, Vector3.right).normalized;
            for (int index = 0; index < count; index++)
            {
                // A crest is read from the caster outwards: the nearest tooth rises
                // first, then each overlapping tooth continues the line away from
                // the player. The small overlap removes the old fence-like gaps.
                EarthPillarCrestLayoutSample layout =
                    EarthPillarCrestLayoutSolver.Sample(index, count);
                Vector3 candidate = surfaceOrigin + tangentForward * layout.ForwardOffset;
                Vector3 columnUp = (candidate - center).normalized;
                Vector3 surface = center + columnUp * radius;
                ResolveConstructedSurface(ref surface, ref columnUp);
                Vector3 columnForward = Vector3.ProjectOnPlane(tangentForward, columnUp).normalized;
                float height = 3.15f * layout.HeightScale;
                EarthPillarWaveColumn column = Acquire();
                column.Schedule(
                    this,
                    surface,
                    columnUp,
                    columnForward,
                    height,
                    layout.Width,
                    layout.Depth,
                    layout.StartDelay,
                    0.42f,
                    layout.HeightScale,
                    _nextPulseId++,
                    360f,
                    caster,
                    profile,
                    ImpactHits,
                    6,
                    1f,
                    0f,
                    null,
                    0f,
                    impactCastId,
                    45f,
                    EarthCharacterImpactSourceKind.PillarCrest);
            }
            return count;
        }

        public int LaunchCrest(
            Vector3 surfaceStart,
            Vector3 surfaceEnd,
            int requestedCount,
            Rigidbody caster)
        {
            Vector3 center = planetCenter != null ? planetCenter.position : Vector3.zero;
            Vector3 up = (surfaceStart - center).normalized;
            if (up.sqrMagnitude < 0.5f) up = transform.up;
            Vector3 direction = Vector3.ProjectOnPlane(surfaceEnd - surfaceStart, up).normalized;
            if (direction.sqrMagnitude < 0.5f && caster != null)
                direction = Vector3.ProjectOnPlane(caster.transform.forward, up).normalized;
            if (direction.sqrMagnitude < 0.5f) direction = Vector3.Cross(up, Vector3.right).normalized;
            if (caster != null &&
                (surfaceEnd - caster.worldCenterOfMass).sqrMagnitude <
                (surfaceStart - caster.worldCenterOfMass).sqrMagnitude)
            {
                Vector3 swap = surfaceStart;
                surfaceStart = surfaceEnd;
                surfaceEnd = swap;
                up = (surfaceStart - center).normalized;
                direction = Vector3.ProjectOnPlane(surfaceEnd - surfaceStart, up).normalized;
            }
            return LaunchCrest(surfaceStart, up, direction, requestedCount, caster);
        }

        public int LaunchCrest(in EarthCrestPath path, int requestedCount, Rigidbody caster) =>
            LaunchCrest(
                new Vector3(path.Start.x, path.Start.y, path.Start.z),
                new Vector3(path.End.x, path.End.y, path.End.z),
                requestedCount,
                caster);

        private EarthPillarWaveColumn Acquire()
        {
            for (int index = 0; index < _columns.Count; index++)
                if (!_columns[index].gameObject.activeSelf) return _columns[index];
            if (_columns.Count < capacity) return CreateColumn();
            throw new System.InvalidOperationException("Wave capacity must be reserved before scheduling; live geometry cannot be reused.");
        }

        private EarthPillarWaveColumn CreateColumn()
        {
            GameObject go = new GameObject($"Earth Wave Column {_columns.Count + 1:00}");
            go.transform.SetParent(transform, false);
            MeshFilter filter = go.AddComponent<MeshFilter>();
            Mesh configuredMesh = useLegacyColumnMeshes
                ? columnMeshVariants != null && columnMeshVariants.Length > 0
                    ? columnMeshVariants[_columns.Count % columnMeshVariants.Length]
                    : columnMesh
                : null;
            Mesh cellMesh = configuredMesh != null
                ? configuredMesh
                : _webCellMeshes[_columns.Count % _webCellMeshes.Length];
            filter.sharedMesh = _renderBevels.Get(cellMesh, stoneBevelProfile);
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = columnMaterial;
            MeshCollider collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = cellMesh;
            collider.convex = true;
            Rigidbody body = go.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            EarthPillarWaveColumn column = go.AddComponent<EarthPillarWaveColumn>();
            column.ConfigureStoneBevel(stoneBevelProfile);
            column.ConfigureMaterialFeedback(materialFeedback);
            column.SetInstancedRendering(!useLegacyColumnMeshes);
            column.ConfigureBaseGeometry(filter.sharedMesh, cellMesh);
            go.SetActive(false);
            _columns.Add(column);
            return column;
        }

        internal void ReportBurst(in EarthPillarWavePulse pulse) => ColumnBurst?.Invoke(pulse);
        internal EarthMatterKernelBehaviour MatterKernel =>
            _matterKernel != null ? _matterKernel : (_matterKernel = EarthMatterKernelBehaviour.FindOrCreate(this));
        internal Vector3 ToPlanetLocal(Vector3 worldPoint) =>
            planetCenter != null ? planetCenter.InverseTransformPoint(worldPoint) : worldPoint;
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(MeshRenderer))]
    public sealed class EarthPillarWaveColumn : MonoBehaviour, IEarthPhysicalTarget
    {
        private EarthStoneBevelProfile _stoneBevelProfile;
        private readonly EarthStoneRenderBevelCache _renderBevels = new();
        public void ConfigureStoneBevel(EarthStoneBevelProfile value) => _stoneBevelProfile = value;
        private EarthMaterialFeedbackHub _materialFeedback;
        private float _nextContactFeedbackAt;
        private readonly EarthContactFrictionFeedback _frictionFeedback = new();
        public void ConfigureMaterialFeedback(EarthMaterialFeedbackHub hub) => _materialFeedback = hub;

        private void OnCollisionEnter(Collision collision)
        {
            if (!_magicDetached || _magicGripCount > 0 || _body == null || _body.isKinematic ||
                collision == null || collision.contactCount == 0 || Time.fixedTime < _nextContactFeedbackAt) return;
            ContactPoint contact = collision.GetContact(0);
            float approach = Mathf.Max(0f, -Vector3.Dot(collision.relativeVelocity, contact.normal));
            if (approach < 0.75f) return;
            _nextContactFeedbackAt = Time.fixedTime + 0.12f;
            _materialFeedback?.Emit(EarthMaterialFeedbackKind.Impact, contact.point, contact.normal,
                Mathf.Clamp(approach / 8f, 0.4f, 1.5f),
                _collider != null ? Mathf.Clamp(_collider.bounds.extents.magnitude, 0.2f, 2f) : 0.5f,
                _stableId, _generation);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (_magicDetached) _frictionFeedback.Emit(_materialFeedback, collision, _stableId, _generation);
        }
        private Rigidbody _body;
        private Collider _collider;
        private MeshRenderer _renderer;
        private Transform _visualProxy;
        private MeshFilter _visualProxyFilter;
        private MeshRenderer _visualProxyRenderer;
        private Matrix4x4 _instancedVisualMatrix;
        private bool _hasInstancedVisualMatrix;
        private EarthPillarWaveProfile _profile;
        private Collider[] _impactHits;
        private Rigidbody _caster;
        private ActiveRagdollPuppet _casterPuppet;
        private Vector3 _surface;
        private Vector3 _up;
        private Vector3 _outward;
        private Vector3 _fullScale;
        private float _delay;
        private bool _emergencePresented;
        private float _holdDuration;
        private float _impulse;
        private float _elapsed;
        private bool _impacted;
        private EarthPillarWavePool _owner;
        private Quaternion _baseRotation;
        private float _crest01;
        private uint _stableId;
        private uint _impactSourceId;
        private EarthCharacterImpactSourceKind _impactKind;
        private uint _generation;
        private bool _magicDetached;
        private bool _instancedRendering;
        private bool _visualVisible;
        private int _magicGripCount;
        private float _detachedElapsed;
        private bool _polygonCell;
        private int _burialFramesRemaining;
        private float _sampleHeight;
        private float _slabThickness;
        private float _footprintArea;
        private float _currentVisualHeight01;
        private Mesh _ownedCellMesh;
        private EarthMatterIdentity _matterIdentity;
        private readonly List<Collider> _ignoredCasterColliders = new List<Collider>(16);
        private readonly Collider[] _casterColliderBuffer = new Collider[32];

        public Rigidbody Body
        {
            get
            {
                Resolve();
                return _body;
            }
        }
        public uint StableEarthId => _stableId;
        public uint ImpactSourceId => _impactSourceId != 0u ? _impactSourceId : _stableId;
        public EarthCharacterImpactSourceKind ImpactKind => _impactKind;
        public EarthPhysicalTargetHandle TargetHandle =>
            new EarthPhysicalTargetHandle(_stableId, _generation);
        public float EarthMass => _body != null
            ? Mathf.Max(0.5f, _body.mass)
            : Mathf.Max(0.5f, _fullScale.x * _fullScale.y * _fullScale.z * 150f);
        public EarthPhysicalTargetKind TargetKind => EarthPhysicalTargetKind.WaveCell;
        public bool IsEarthTargetValid => gameObject.activeInHierarchy && _stableId != 0u &&
                                                  _collider != null && _collider.enabled;
        public EarthMatterIdentity MatterIdentity =>
            _matterIdentity != null ? _matterIdentity : (_matterIdentity = GetComponent<EarthMatterIdentity>());

        public static EarthSurfacePlacementResult ResolveFullRisePlacement(
            Mesh mesh,
            Vector3 surface,
            Vector3 up,
            Quaternion rotation,
            Vector3 scale,
            float foundationBurialRatio = 0f)
        {
            EarthSurfacePlacementResult result = EarthSurfacePlacementSolver.Solve(
                mesh,
                surface,
                up,
                rotation,
                scale,
                0.01f);
            if (!result.IsValid) return result;
            float burial = mesh.bounds.size.y * Mathf.Abs(scale.y) * Mathf.Clamp01(foundationBurialRatio);
            Vector3 offset = up.normalized * burial;
            // The generic placement solver pins a lowest *vertex* to the supplied
            // point. Here the point is a Voronoi cell centre: only use its height
            // correction. Otherwise switching support vertices teleports the cell
            // sideways and even fixed cells start inside their neighbours.
            Vector3 restoreFootprint = Vector3.ProjectOnPlane(surface - result.RootPosition, up.normalized);
            return new EarthSurfacePlacementResult(result.RootPosition + restoreFootprint - offset,
                result.SupportPoint + restoreFootprint - offset,
                result.SupportError - burial, result.Embed + burial, result.SurfaceHandle, true);
        }

        private Mesh _baseRenderMesh, _baseColliderMesh;
        private float3[] _contactVertices;
        private int[] _contactTriangles;
        private readonly float3[] _contactPoints = new float3[64];
        private readonly Vector3[] _lastSurfacePoints = new Vector3[8];
        private int _lastSurfacePointCount;
        private float _nextSurfaceFeedback, _previousFeedbackHeight;
        private bool _surfaceRiseBurst, _surfaceRetreatBurst;
        private int _contactSequence;
        public uint CastGeneration => _generation;
        public float ScheduledDelay => _delay;
        public bool IsAnchoredAnimation => gameObject.activeSelf && !_magicDetached;
        public int SurfaceContactEmissions { get; private set; }
        private static readonly ProfilerMarker SurfaceFeedbackMarker = new("Elemental.Wave.SurfaceContact");
        public void ConfigureBaseGeometry(Mesh render, Mesh collider)
        { _baseRenderMesh = render; _baseColliderMesh = collider; }
        private float FoundationBurialRatio => _profile != null ? _profile.FoundationBurialRatio : 0.20f;

        public bool TryGetVisiblePlacementDiagnostic(
            out Mesh mesh,
            out Matrix4x4 matrix,
            out Vector3 surface,
            out Vector3 up,
            out float visualHeight01,
            out bool polygonCell)
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            bool proxyVisible = _visualProxyRenderer != null && _visualProxyRenderer.enabled;
            mesh = proxyVisible ? _visualProxyFilter.sharedMesh : filter != null ? filter.sharedMesh : null;
            matrix = proxyVisible ? _visualProxy.localToWorldMatrix : _hasInstancedVisualMatrix
                ? _instancedVisualMatrix
                : transform.localToWorldMatrix;
            surface = _surface;
            up = _up;
            visualHeight01 = _currentVisualHeight01;
            polygonCell = _polygonCell;
            return gameObject.activeInHierarchy && _visualVisible && mesh != null;
        }

        public void SetInstancedRendering(bool value)
        {
            _instancedRendering = value;
            Resolve();
            SetVisualVisible(_visualVisible);
        }

        public bool TryGetInstancedRenderMatrix(out Matrix4x4 matrix)
        {
            matrix = default;
            if (!_instancedRendering || !_visualVisible || _magicDetached || !gameObject.activeInHierarchy)
                return false;
            matrix = _hasInstancedVisualMatrix
                ? _instancedVisualMatrix
                : transform.localToWorldMatrix;
            return true;
        }

        public void Schedule(
            EarthPillarWavePool owner,
            Vector3 surface,
            Vector3 up,
            Vector3 forward,
            float height,
            float width,
            float depth,
            float delay,
            float holdDuration,
            float crest01,
            uint stableId,
            float impulse,
            Rigidbody caster,
            EarthPillarWaveProfile profile,
            Collider[] impactHits,
            int shapeSides = 6,
            float shapeAreaScale = 1f,
            float spiralPhase01 = 0f,
            float2[] sharedFootprint = null,
            float footprintArea = 0f,
            uint impactSourceId = 0u,
            float tiltDegrees = 0f,
            EarthCharacterImpactSourceKind impactKind = EarthCharacterImpactSourceKind.PillarWave)
        {
            Resolve();
            RestoreCasterCollisions();
            _owner = owner;
            _profile = profile;
            _impactHits = impactHits;
            _caster = caster;
            _casterPuppet = caster != null ? caster.GetComponent<ActiveRagdollPuppet>() : null;
            _up = up.normalized;
            _outward = Vector3.ProjectOnPlane(surface - (caster != null ? caster.worldCenterOfMass : surface - forward), _up).normalized;
            if (_outward.sqrMagnitude < 0.5f) _outward = forward;
            _polygonCell = sharedFootprint != null && sharedFootprint.Length >= 3;
            _sampleHeight = Mathf.Max(0.1f, height);
            // Full rise means the real lowest mesh vertex is seated at the
            // support plane. The former +0.20 m slab tail was still underground
            // when the root had finished rising and made every polygon cell look
            // half-buried.
            _slabThickness = Mathf.Max(0.18f, _sampleHeight + 0.01f);
            _footprintArea = Mathf.Max(0.05f, footprintArea);
            if (_polygonCell)
            {
                if (_ownedCellMesh == null)
                {
                    _ownedCellMesh = new Mesh
                    {
                        name = $"Earth Web Runtime Cell {stableId}",
                        hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor
                    };
                }
                EarthWebWaveCellMeshFactory.ConfigureSharedBoundaryCell(
                    _ownedCellMesh,
                    sharedFootprint,
                    stableId,
                    _slabThickness,
                    true);
                MeshFilter filter = GetComponent<MeshFilter>();
                if (filter != null)
                {
                    filter.sharedMesh = _renderBevels.Rebuild(_ownedCellMesh, _stoneBevelProfile);
                    EarthWebWaveCellMeshFactory.ContainRenderFootprint(filter.sharedMesh, sharedFootprint);
                }
                MeshCollider meshCollider = _collider as MeshCollider;
                if (meshCollider != null)
                {
                    meshCollider.sharedMesh = null;
                    meshCollider.sharedMesh = _ownedCellMesh;
                    meshCollider.convex = true;
                }
                _fullScale = Vector3.one;
                SetInstancedRendering(false);
            }
            else
            {
                _fullScale = new Vector3(width, height, depth);
                // A crest can reuse a previously metre-sized polygon cell.
                // Restore unit geometry before applying its authored dimensions.
                if (_baseRenderMesh != null) GetComponent<MeshFilter>().sharedMesh = _baseRenderMesh;
                if (_baseColliderMesh != null && _collider is MeshCollider restored)
                    restored.sharedMesh = _baseColliderMesh;
            }
            if (_profile != null && _profile.MotionMode == WaveMotionMode.PremiumVisual)
                EnsureVisualProxy();
            SyncVisualProxyMeshAndMaterial();
            Mesh contactMesh = (_collider as MeshCollider)?.sharedMesh;
            if (contactMesh != null)
            {
                Vector3[] source = contactMesh.vertices;
                _contactVertices = new float3[source.Length];
                for (int i = 0; i < source.Length; i++) _contactVertices[i] = source[i];
                _contactTriangles = contactMesh.triangles;
            }
            _nextSurfaceFeedback = 0f; _previousFeedbackHeight = 0f;
            _surfaceRiseBurst = _surfaceRetreatBurst = false;
            _contactSequence = 0; SurfaceContactEmissions = 0;
            _lastSurfacePointCount = 0;
            _surface = surface;
            _delay = Mathf.Max(0f, delay);
            _holdDuration = Mathf.Max(0.05f, holdDuration);
            _impulse = impulse;
            _crest01 = Mathf.Clamp01(crest01);
            _stableId = stableId;
            _impactSourceId = impactSourceId != 0u ? impactSourceId : stableId;
            _impactKind = impactKind;
            _generation++;
            if (_generation == 0u) _generation = 1u;
            _elapsed = 0f;
            _emergencePresented = false;
            _impacted = false;
            _magicDetached = false;
            _magicGripCount = 0;
            _detachedElapsed = 0f;
            _burialFramesRemaining = 0;
            _hasInstancedVisualMatrix = false;
            _currentVisualHeight01 = 0f;
            if (_polygonCell)
            {
                // Every cell keeps the common topology frame. Independent yaw was
                // the hidden source of the visible rectangular gaps in the old wave.
                _baseRotation = Quaternion.LookRotation(forward, _up);
                transform.SetPositionAndRotation(_surface, _baseRotation);
                transform.localScale = Vector3.one;
            }
            else
            {
                float geologicalYaw = (Mathf.Repeat(delay * 173f, 14f) - 7f) +
                                      Mathf.Lerp(-7f, 7f, spiralPhase01);
                Vector3 tiltAxis = Vector3.Cross(_up, forward).normalized;
                if (tiltAxis.sqrMagnitude < 0.5f) tiltAxis = transform.right;
                Vector3 visualUp = Quaternion.AngleAxis(
                    Mathf.Clamp(tiltDegrees, -55f, 55f),
                    tiltAxis) * _up;
                Vector3 visualForward = Vector3.ProjectOnPlane(forward, visualUp).normalized;
                _baseRotation = Quaternion.AngleAxis(geologicalYaw, _up) *
                                Quaternion.LookRotation(visualForward, visualUp);
                float areaScale = Mathf.Clamp(shapeAreaScale, 0.45f, 1.70f);
                float anisotropy = Mathf.Lerp(0.88f, 1.12f, (shapeSides - 3f) / 5f);
                _fullScale.x *= Mathf.Sqrt(areaScale) * anisotropy;
                _fullScale.z *= Mathf.Sqrt(areaScale) / anisotropy;
                transform.SetPositionAndRotation(
                    _surface + (_up * height * 0.0125f),
                    _baseRotation);
                transform.localScale = new Vector3(width * 0.70f, height * 0.025f, depth * 0.70f);
            }
            SetVisualVisible(false);
            _collider.enabled = false;
            _body.isKinematic = true;
            _body.mass = _polygonCell
                ? Mathf.Max(1f, _footprintArea * _slabThickness * 150f)
                : Mathf.Max(1f, width * height * depth * 150f);
            _body.maxAngularVelocity = 3.2f;
            gameObject.SetActive(true);
            RegisterMatter();
            IgnoreCasterCollisions();
        }

        public void ResetColumn()
        {
            Resolve();
            MatterIdentity?.RetireTransientRepresentation();
            SetVisualVisible(false);
            _collider.enabled = false;
            RestoreCasterCollisions();
            _magicDetached = false;
            _magicGripCount = 0;
            _detachedElapsed = 0f;
            _nextContactFeedbackAt = 0f;
            _burialFramesRemaining = 0;
            if (_body != null && !_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
                _body.isKinematic = true;
            }
            gameObject.SetActive(false);
        }

        public void OnEarthMagicGrabbed(EarthMagicGripKind grip)
        {
            Resolve();
            if (!_magicDetached) DetachForMagic();
            _magicGripCount++;
            GravityBody gravity = GetComponent<GravityBody>();
            if (gravity != null) gravity.enabled = false;
            _body.WakeUp();
            MatterIdentity?.TryTransition(EarthMatterPhase.Controlled);
        }

        public void OnEarthMagicReleased(EarthMagicGripKind grip)
        {
            _magicGripCount = Mathf.Max(0, _magicGripCount - 1);
            GravityBody gravity = GetComponent<GravityBody>();
            if (gravity != null && _magicGripCount == 0) gravity.enabled = true;
            if (_body != null) _body.WakeUp();
            if (_magicGripCount == 0 && MatterIdentity != null &&
                MatterIdentity.TryRead(out EarthMatterRecord record) &&
                record.Phase == EarthMatterPhase.Controlled)
                MatterIdentity.TryTransition(EarthMatterPhase.FreeDynamic);
        }

        private void RegisterMatter()
        {
            if (_body == null) return;
            float volume = Mathf.Max(0.000001f, _body.mass / 150f);
            EarthMatterKernelBehaviour kernel = _owner != null
                ? _owner.MatterKernel
                : EarthMatterKernelBehaviour.FindOrCreate(this);
            Vector3 sourceLocal = _owner != null ? _owner.ToPlanetLocal(_surface) : _surface;
            var source = new EarthSourceProvenance(
                EarthSourceKind.TerrainEdit,
                1u,
                1,
                unchecked((int)_stableId),
                _stableId,
                new float3(sourceLocal.x, sourceLocal.y, sourceLocal.z),
                volume,
                EarthProvenanceFlags.VolumeReserved);
            _matterIdentity = EarthMatterRuntimeBridge.EnsureIdentity(
                this,
                kernel,
                _body,
                EarthMatterPhase.Forming,
                EarthRepresentationTier.SecondaryPhysical,
                EarthMaterialKind.Stone,
                EarthShapeSemantic.Pillar,
                volume,
                _body.mass,
                source,
                new EarthOwnerId(1u, 1));
        }

        private void DetachForMagic()
        {
            _magicDetached = true;
            _detachedElapsed = 0f;
            RestoreCasterCollisions();
            SetVisualVisible(true);
            _collider.enabled = true;
            _body.isKinematic = false;
            // Kinematic rise uses MovePosition/MoveRotation. PhysX exposes that
            // authored motion as body velocity; carrying it into hand-control makes
            // a grabbed pillar shoot away before the vector solver can clamp it.
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.detectCollisions = true;
            _body.constraints = RigidbodyConstraints.None;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _body.angularDamping = 7.5f;
            _body.maxAngularVelocity = 3.2f;
            GravityBody gravity = GetComponent<GravityBody>();
            if (gravity == null) gravity = gameObject.AddComponent<GravityBody>();
            gravity.Configure(FindAnyObjectByType<GravityWorldBehaviour>(), _body);
        }

        private void IgnoreCasterCollisions()
        {
            _ignoredCasterColliders.Clear();
            if (_caster == null || _collider == null) return;
            if (_casterPuppet != null)
            {
                int count = _casterPuppet.CopySelfCollidersNonAlloc(_casterColliderBuffer);
                for (int index = 0; index < count; index++)
                {
                    Collider casterCollider = _casterColliderBuffer[index];
                    if (casterCollider != null && casterCollider != _collider)
                        _ignoredCasterColliders.Add(casterCollider);
                    _casterColliderBuffer[index] = null;
                }
            }
            else
            {
                _caster.GetComponentsInChildren(false, _ignoredCasterColliders);
            }
            for (int index = _ignoredCasterColliders.Count - 1; index >= 0; index--)
            {
                Collider casterCollider = _ignoredCasterColliders[index];
                if (casterCollider == null || casterCollider == _collider)
                {
                    _ignoredCasterColliders.RemoveAt(index);
                    continue;
                }
                UnityEngine.Physics.IgnoreCollision(_collider, casterCollider, true);
            }
        }

        private void RestoreCasterCollisions()
        {
            if (_collider != null)
            {
                for (int index = 0; index < _ignoredCasterColliders.Count; index++)
                {
                    Collider casterCollider = _ignoredCasterColliders[index];
                    if (casterCollider != null)
                        UnityEngine.Physics.IgnoreCollision(_collider, casterCollider, false);
                }
            }
            _ignoredCasterColliders.Clear();
        }

        private void FixedUpdate()
        {
            if (_magicDetached)
            {
                _body.angularVelocity = Vector3.MoveTowards(
                    _body.angularVelocity, Vector3.zero, 5.5f * Time.fixedDeltaTime);
                if (_magicGripCount > 0) return;
                _detachedElapsed += Time.fixedDeltaTime;
                if (_detachedElapsed > 7f)
                {
                    float shrink01 = Mathf.Clamp01((_detachedElapsed - 7f) / 1.2f);
                    transform.localScale = Vector3.Lerp(_fullScale, Vector3.zero, shrink01);
                    if (shrink01 >= 1f) ResetColumn();
                }
                return;
            }
            _elapsed += Time.fixedDeltaTime;
            float tremorLeadSeconds = _profile != null && _profile.MotionMode == WaveMotionMode.PremiumVisual
                ? Mathf.Max(.09f, _profile.AnimationTiming.Anticipation) : .09f;
            if (_elapsed < _delay - tremorLeadSeconds) return;
            float localTime = _elapsed - _delay;
            float rise = _profile != null ? _profile.ColumnRiseSeconds : 0.36f;
            float hold = _profile != null && _profile.MotionMode == WaveMotionMode.PremiumVisual
                ? _profile.ColumnHoldSeconds : _holdDuration;
            float retreat = _profile != null ? _profile.ColumnRetreatSeconds : 0.46f;
            EarthPillarWaveMotionSample motion = EarthPillarWaveSolver.EvaluateMotion(
                localTime, rise, hold, retreat);
            WaveMotionMode motionMode = _profile != null
                ? _profile.MotionMode
                : WaveMotionMode.Legacy;
            EarthPillarWaveVisualTuning visualTuning = _profile != null
                ? _profile.VisualTuning
                : EarthPillarWaveVisualTuning.PremiumDefault;
            EarthPillarWaveVisualSample visualMotion = _profile != null ? _profile.EvaluateVisualMotion(localTime, _stableId) : EarthPillarWaveSolver.EvaluateVisualMotion(
                localTime,
                rise,
                hold,
                retreat,
                motionMode,
                in visualTuning,
                _stableId);
            bool premiumVisualTail = motionMode == WaveMotionMode.PremiumVisual &&
                                     localTime < (_profile != null ? _profile.AnimationTiming.Duration : visualTuning.Duration);
            if (motion.Complete)
            {
                EmitLastSurfaceRetreatBurst();
                if (premiumVisualTail)
                {
                    float burialDepth = Mathf.Max(
                        _polygonCell ? _slabThickness * 1.12f : _fullScale.y * 0.72f,
                        0.42f);
                    _body.MovePosition(_surface - _up * burialDepth);
                    _body.MoveRotation(_baseRotation);
                    _collider.enabled = false;
                    SetVisualVisible(true);
                    ApplyPremiumVisualPose(localTime, in visualMotion, in visualTuning);
                    return;
                }
                if (_burialFramesRemaining <= 0)
                {
                    float burialDepth = Mathf.Max(
                        _polygonCell ? _slabThickness * 1.12f : _fullScale.y * 0.72f,
                        0.42f);
                    _body.MovePosition(_surface - _up * burialDepth);
                    _body.MoveRotation(_baseRotation);
                    _collider.enabled = false;
                    _burialFramesRemaining = 2;
                    return;
                }
                _burialFramesRemaining--;
                if (_burialFramesRemaining > 0) return;
                ResetColumn();
                return;
            }
            if (!_visualVisible)
            {
                SetVisualVisible(true);
            }
            if (!_emergencePresented && localTime >= 0f && motion.Height01 > .08f)
            {
                _emergencePresented = true;
                _materialFeedback?.Emit(EarthMaterialFeedbackKind.Emerge, _surface, _up,
                    1f, Mathf.Clamp(_fullScale.x * .5f, .2f, 1.2f), _stableId, _generation, 24, 8);
            }
            float rise01 = Mathf.Clamp01(localTime / Mathf.Max(0.05f, rise));
            float preEnvelope = Mathf.Clamp01((localTime + tremorLeadSeconds) / tremorLeadSeconds);
            float riseEnvelope = Mathf.SmoothStep(0.72f, 1f, Mathf.Clamp01(rise01 / 0.16f)) *
                                 (1f - Mathf.SmoothStep(0.42f, 0.70f, motion.Height01));
            float tremorEnvelope = (localTime < 0f ? preEnvelope * 0.72f : riseEnvelope) *
                                   (1f - motion.Sink01);
            Vector3 coherentAxis = Vector3.ProjectOnPlane(
                new Vector3(0.73f, 0.19f, 0.65f),
                _up).normalized;
            if (coherentAxis.sqrMagnitude < 0.5f) coherentAxis = transform.right;
            float spatialPhase = Vector3.Dot(_surface, coherentAxis) * (Mathf.PI * 2f / 12f);
            float phase = localTime * (Mathf.PI * 2f * (_profile != null ? _profile.TremorFrequency : 8f)) + spatialPhase;
            Vector3 lateral = coherentAxis * (Mathf.Sin(phase) * (_profile != null ? _profile.TremorDistance : .006f) * tremorEnvelope);
            Vector3 tremorAxis = Vector3.Cross(_up, coherentAxis).normalized;
            Quaternion tremorRotation = Quaternion.AngleAxis(
                Mathf.Cos(phase) * (_profile != null ? _profile.TremorAngle : .2f) * tremorEnvelope,
                tremorAxis) * _baseRotation;
            if (_polygonCell)
            {
                // A shared fracture is a fixed ground partition. Rotating each cell
                // on its own phase sweeps its boundary through its neighbours.
                // Only emergence along the original normal may animate this volume.
                tremorRotation = _baseRotation;
                // The complete underground rock volume translates upward. No axis
                // scaling means the silhouette remains a real Voronoi plate instead
                // of turning back into a stretched rectangular pillar.
                MeshFilter filter = GetComponent<MeshFilter>();
                EarthSurfacePlacementResult placement = ResolveFullRisePlacement(
                    filter != null ? filter.sharedMesh : null,
                    _surface,
                    _up,
                    tremorRotation,
                    Vector3.one,
                    FoundationBurialRatio);
                float burial = _sampleHeight * (1f - Mathf.Clamp01(motion.Height01)) +
                               Mathf.Max(_slabThickness * 1.12f, 0.42f) * motion.Sink01;
                Vector3 fullRiseRoot = placement.IsValid
                    ? placement.RootPosition
                    : _surface + _up * _slabThickness;
                _body.MovePosition(fullRiseRoot - _up * burial + lateral);
                _body.MoveRotation(tremorRotation);
                transform.localScale = Vector3.one;
            }
            else
            {
                float visibleHeight = Mathf.Max(0.012f, _fullScale.y * motion.Height01);
                float sink = Mathf.Max(_fullScale.y * 0.72f, 0.42f) * motion.Sink01;
                Vector3 visibleScale = new Vector3(
                    _fullScale.x * motion.Width01,
                    visibleHeight,
                    _fullScale.z * motion.Width01);
                MeshFilter filter = GetComponent<MeshFilter>();
                EarthSurfacePlacementResult placement = ResolveFullRisePlacement(
                    filter != null ? filter.sharedMesh : null,
                    _surface - (_up * sink),
                    _up,
                    tremorRotation,
                    visibleScale,
                    FoundationBurialRatio);
                _body.MovePosition((placement.IsValid ? placement.RootPosition : _surface) + lateral);
                _body.MoveRotation(tremorRotation);
                transform.localScale = visibleScale;
            }
            if (motionMode == WaveMotionMode.PremiumVisual)
                ApplyPremiumVisualPose(localTime, in visualMotion, in visualTuning);
            _collider.enabled = motion.Height01 >= 0.16f && motion.Sink01 < 0.72f;
            if (!_impacted && localTime <= rise && motion.Height01 >= 0.56f)
            {
                _impacted = true;
                EarthPillarWavePulse pulse = new EarthPillarWavePulse(
                    _surface,
                    _up,
                    _outward,
                    _polygonCell ? Mathf.Sqrt(_footprintArea) : _fullScale.x,
                    _polygonCell ? _sampleHeight : _fullScale.y,
                    _crest01,
                    _stableId);
                _owner?.ReportBurst(in pulse);
                ApplyImpact();
            }
        }

        private void ApplyImpact()
        {
            float radius = _profile != null ? _profile.ImpactRadius : 1.05f;
            int count = UnityEngine.Physics.OverlapSphereNonAlloc(
                transform.position + (_up * (_polygonCell ? _sampleHeight : _fullScale.y) * 0.28f),
                radius,
                _impactHits,
                ~0,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < count; index++)
            {
                Collider hit = _impactHits[index];
                if (hit == null || hit.attachedRigidbody == _body || hit.attachedRigidbody == _caster ||
                    (_casterPuppet != null && _casterPuppet.OwnsCollider(hit))) continue;
                EarthWall wall = hit.GetComponentInParent<EarthWall>();
                if (wall == null) wall = hit.GetComponent<EarthWallPiece>()?.Owner;
                if (wall != null && _owner != null &&
                    _owner.TryClaimFaultLineTarget(ImpactSourceId, wall.WallId))
                    wall.ApplyStructureImpact(transform.position, _outward + _up, _impulse);
                EarthArenaStructure arena = hit.GetComponentInParent<EarthArenaStructure>();
                if (arena == null)
                    arena = hit.GetComponentInParent<EarthArenaPiece>()?.Owner;
                if (wall == null && arena != null && arena.OrdinaryDamageEnabled && _owner != null &&
                    _owner.TryClaimFaultLineTarget(ImpactSourceId, arena.StructureId))
                {
                    var arenaImpact = new EarthStructureImpact(
                        transform.position,
                        _outward + _up,
                        _impulse,
                        EarthStructureImpactKind.Construction,
                        ImpactSourceId);
                    arena.ApplyEarthImpact(in arenaImpact);
                }
                EarthCharacterImpactTarget characterTarget =
                    hit.GetComponentInParent<EarthCharacterImpactTarget>();
                if (characterTarget != null)
                {
                    characterTarget.ApplyImpact(
                        transform.position,
                        _outward + _up,
                        _impulse,
                        _impactKind,
                        ImpactSourceId,
                        0f,
                        _crest01);
                    continue;
                }
                EarthDestructibleDecorRock decorRock =
                    hit.GetComponentInParent<EarthDestructibleDecorRock>();
                if (decorRock != null)
                {
                    decorRock.ApplyImpact(
                        transform.position,
                        (_outward * 0.55f + _up).normalized,
                        _impulse);
                    continue;
                }
                Rigidbody target = hit.attachedRigidbody;
                if (target == null || target.isKinematic) continue;
                target.AddForce((_outward * 0.55f + _up).normalized * _impulse, ForceMode.Impulse);
            }
        }

        private void Resolve()
        {
            if (_body == null) _body = GetComponent<Rigidbody>();
            if (_collider == null) _collider = GetComponent<MeshCollider>();
            if (_collider == null) _collider = GetComponent<Collider>();
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
        }

        private void EnsureVisualProxy()
        {
            if (_visualProxy != null) return;
            Resolve();
            GameObject proxy = new GameObject("Premium Visual Only");
            proxy.layer = gameObject.layer;
            _visualProxy = proxy.transform;
            _visualProxy.SetParent(_owner != null ? _owner.transform : transform.parent, false);
            _visualProxyFilter = proxy.AddComponent<MeshFilter>();
            _visualProxyRenderer = proxy.AddComponent<MeshRenderer>();
            _visualProxyRenderer.shadowCastingMode = _renderer.shadowCastingMode;
            _visualProxyRenderer.receiveShadows = _renderer.receiveShadows;
            SyncVisualProxyMeshAndMaterial();
            _renderer.enabled = false;
        }

        private void SyncVisualProxyMeshAndMaterial()
        {
            if (_visualProxy == null) return;
            MeshFilter sourceFilter = GetComponent<MeshFilter>();
            _visualProxyFilter.sharedMesh = sourceFilter != null ? sourceFilter.sharedMesh : null;
            _visualProxyRenderer.sharedMaterials = _renderer.sharedMaterials;
        }

        private void LateUpdate()
        {
            if (!_instancedRendering) UpdateRenderPose();
        }

        public void UpdateRenderPose()
        {
            if (!gameObject.activeInHierarchy || !_visualVisible || _magicDetached || _profile == null ||
                _profile.MotionMode != WaveMotionMode.PremiumVisual) return;
            float time = _elapsed + Mathf.Clamp(Time.time - Time.fixedTime, 0f, Time.fixedDeltaTime) - _delay;
            var tuning = _profile.VisualTuning;
            var visual = _profile.EvaluateVisualMotion(time, _stableId);
            ApplyPremiumVisualPose(time, in visual, in tuning);
        }

        private void ApplyPremiumVisualPose(
            float localTime,
            in EarthPillarWaveVisualSample visual,
            in EarthPillarWaveVisualTuning tuning)
        {
            _currentVisualHeight01 = Mathf.Max(0f, visual.Height01);
            float retreatStart = _profile != null ? _profile.AnimationTiming.Rise +
                _profile.AnimationTiming.Settle + _profile.AnimationTiming.Hold :
                tuning.RiseSeconds + tuning.SettleSeconds + tuning.HoldSeconds;
            float visualSink01 = localTime > retreatStart
                ? 1f - Mathf.Clamp01(visual.Height01)
                : 0f;
            Vector3 coherentAxis = Vector3.ProjectOnPlane(
                new Vector3(0.73f, 0.19f, 0.65f),
                _up).normalized;
            if (coherentAxis.sqrMagnitude < 0.5f) coherentAxis = transform.right;
            Vector3 tiltAxis = Vector3.Cross(_up, _outward).normalized;
            if (tiltAxis.sqrMagnitude < 0.5f) tiltAxis = coherentAxis;
            float spatialPhase = Vector3.Dot(_surface, coherentAxis) * (Mathf.PI * 2f / 12f);
            float phase = localTime * (Mathf.PI * 2f * (_profile != null ? _profile.TremorFrequency : 8f)) + spatialPhase;
            Vector3 lateral = coherentAxis *
                              (Mathf.Sin(phase) * (_profile != null ? _profile.TremorDistance : .006f) * visual.Tremor01);
            Quaternion rotation = Quaternion.AngleAxis(visual.TiltDegrees, tiltAxis) *
                                  Quaternion.AngleAxis(
                                      Mathf.Cos(phase) * (_profile != null ? _profile.TremorAngle : .2f) * visual.Tremor01,
                                      Vector3.Cross(_up, coherentAxis).normalized) *
                                  _baseRotation;

            Vector3 worldPosition;
            Vector3 worldScale;
            if (_polygonCell)
            {
                float burialDepth = Mathf.Max(_slabThickness * 1.12f, 0.42f);
                rotation = _baseRotation;
                worldScale = Vector3.one;
                MeshFilter filter = GetComponent<MeshFilter>();
                EarthSurfacePlacementResult placement = ResolveFullRisePlacement(
                    filter != null ? filter.sharedMesh : null,
                    _surface,
                    _up,
                    rotation,
                    worldScale,
                    FoundationBurialRatio);
                float emergenceBurial = _sampleHeight *
                                        (1f - Mathf.Clamp(visual.Height01, 0f, 1.25f));
                worldPosition = (placement.IsValid
                                    ? placement.RootPosition
                                    : _surface + _up * _slabThickness) -
                                _up * (emergenceBurial + burialDepth * visualSink01) +
                                lateral;
            }
            else
            {
                float burialDepth = Mathf.Max(_fullScale.y * 0.72f, 0.42f);
                float visibleHeight = Mathf.Max(0.012f, _fullScale.y * visual.Height01);
                worldScale = new Vector3(
                    _fullScale.x * visual.Width01,
                    visibleHeight,
                    _fullScale.z * visual.Width01);
                MeshFilter filter = GetComponent<MeshFilter>();
                EarthSurfacePlacementResult placement = ResolveFullRisePlacement(
                    filter != null ? filter.sharedMesh : null,
                    _surface - _up * (burialDepth * visualSink01),
                    _up,
                    rotation,
                    worldScale,
                    FoundationBurialRatio);
                worldPosition = (placement.IsValid ? placement.RootPosition : _surface) + lateral;
            }

            _instancedVisualMatrix = Matrix4x4.TRS(worldPosition, rotation, worldScale);
            _hasInstancedVisualMatrix = true;
            EmitSurfaceFeedback(localTime, visual.Height01);
            if (_visualProxy == null || _instancedRendering) return;
            _visualProxy.SetPositionAndRotation(worldPosition, rotation);
            Vector3 parentScale = _visualProxy.parent != null
                ? _visualProxy.parent.lossyScale
                : Vector3.one;
            _visualProxy.localScale = new Vector3(
                worldScale.x / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                worldScale.y / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                worldScale.z / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
        }

        private void EmitSurfaceFeedback(float localTime, float height)
        {
            if (_materialFeedback == null || _contactVertices == null || localTime < 0f ||
                localTime < _nextSurfaceFeedback) return;
            _nextSurfaceFeedback = localTime + .10f + (_stableId % 5) * .009f;
            bool moving = Mathf.Abs(height - _previousFeedbackHeight) > .00005f;
            _previousFeedbackHeight = height;
            if (!moving) return;
            using (SurfaceFeedbackMarker.Auto())
            {
                Matrix4x4 matrix = _instancedVisualMatrix;
                // Plane is expressed in the unchanged mesh's local space. Emissions
                // follow its actual current intersection, not a puff at the root.
                Vector3 n = new Vector3(Vector3.Dot(_up, matrix.MultiplyVector(Vector3.right)),
                    Vector3.Dot(_up, matrix.MultiplyVector(Vector3.up)), Vector3.Dot(_up, matrix.MultiplyVector(Vector3.forward)));
                float d = Vector3.Dot(_up, matrix.MultiplyPoint3x4(Vector3.zero) - _surface);
                int count = EarthWaveSurfaceContactSolver.Slice(_contactVertices, _contactTriangles,
                    new float4(n.x, n.y, n.z, d), _contactPoints, out _, out float highest);
                if (count == 0) return;
                bool descending = _profile != null && localTime >= _profile.AnimationTiming.Rise +
                    _profile.AnimationTiming.Settle + _profile.AnimationTiming.Hold;
                bool burst = !descending ? !_surfaceRiseBurst : !_surfaceRetreatBurst && highest < .20f;
                if (burst) { if (descending) _surfaceRetreatBurst = true; else _surfaceRiseBurst = true; }
                int samples = Mathf.Min(burst ? 8 : 4, count);
                _lastSurfacePointCount = samples;
                for (int i = 0; i < samples; i++)
                {
                    int index = (i * count / samples + _contactSequence) % count;
                    Vector3 point = matrix.MultiplyPoint3x4(_contactPoints[index]);
                    _lastSurfacePoints[i] = point;
                    _materialFeedback.Emit(burst ? EarthMaterialFeedbackKind.WaveSurfaceBurst : EarthMaterialFeedbackKind.WaveSurfaceContact,
                        point, _up, 1f, .14f, _stableId, _generation, burst ? 20 : 5, burst ? 5 : 1);
                    SurfaceContactEmissions++;
                }
                _contactSequence++;
            }
        }

        private void EmitLastSurfaceRetreatBurst()
        {
            // A very short retreat can cross the surface between contact samples.
            if (_surfaceRetreatBurst || _lastSurfacePointCount == 0 || _materialFeedback == null) return;
            _surfaceRetreatBurst = true;
            for (int i = 0; i < _lastSurfacePointCount; i++)
                _materialFeedback.Emit(EarthMaterialFeedbackKind.WaveSurfaceBurst, _lastSurfacePoints[i], _up,
                    1f, .14f, _stableId, _generation, 20, 5);
        }

        private void SetVisualVisible(bool visible)
        {
            _visualVisible = visible;
            bool directRendering = visible && (!_instancedRendering || _magicDetached);
            if (_visualProxyRenderer != null)
            {
                bool useVisualProxy = !_magicDetached;
                _visualProxyRenderer.enabled = directRendering && useVisualProxy;
                if (_renderer != null) _renderer.enabled = directRendering && !useVisualProxy;
            }
            else if (_renderer != null)
            {
                _renderer.enabled = directRendering;
            }
        }

        private void OnDestroy()
        {
            _renderBevels.Clear();
            if (_visualProxy != null)
            {
                if (Application.isPlaying) Destroy(_visualProxy.gameObject);
                else DestroyImmediate(_visualProxy.gameObject);
                _visualProxy = null;
            }
            if (_ownedCellMesh == null) return;
            if (Application.isPlaying) Destroy(_ownedCellMesh);
            else DestroyImmediate(_ownedCellMesh);
            _ownedCellMesh = null;
        }
    }
}
