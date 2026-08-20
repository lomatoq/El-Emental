using System.Collections.Generic;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Matter;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Matter;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

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

        [SerializeField, Range(64, 96)] private int capacity = 96;
        [SerializeField] private Mesh columnMesh;
        [SerializeField] private Mesh[] columnMeshVariants;
        [SerializeField, Tooltip("Compatibility only. Production waves use deterministic polygonal web-cell meshes.")]
        private bool useLegacyColumnMeshes;
        [SerializeField] private Material columnMaterial;
        [SerializeField] private Transform planetCenter;
        [SerializeField] private EarthPillarWaveProfile profile;

        private readonly List<EarthPillarWaveColumn> _columns = new List<EarthPillarWaveColumn>(96);
        private int _reuseCursor;
        private uint _nextPulseId = 1u;
        private int _nextTopologySeed;
        private EarthWaveSemanticFamily _lastSemanticFamily = EarthWaveSemanticFamily.RollingTerraces;
        private EarthMatterKernelBehaviour _matterKernel;
        private readonly Mesh[] _webCellMeshes = new Mesh[6];
        private readonly Matrix4x4[][] _webMatrices = new Matrix4x4[6][];
        private readonly int[] _webMatrixCounts = new int[6];

        public event System.Action<EarthPillarWavePulse> ColumnBurst;
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
                    filter.sharedMesh = mesh;
                    MeshCollider meshCollider = _columns[index].GetComponent<MeshCollider>();
                    if (meshCollider != null) meshCollider.sharedMesh = mesh;
                }
            }
        }

        private void Awake()
        {
            _matterKernel = EarthMatterKernelBehaviour.FindOrCreate(this);
            if (!useLegacyColumnMeshes)
            {
                for (int family = 0; family < _webCellMeshes.Length; family++)
                {
                    _webCellMeshes[family] = EarthWebWaveCellMeshFactory.Create(family);
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
                    _webCellMeshes[family],
                    0,
                    _webMatrices[family],
                    count);
            }
        }

        private void OnDestroy()
        {
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
            _nextTopologySeed = (_nextTopologySeed + 1) % 6;
            Vector3 center = planetCenter != null ? planetCenter.position : Vector3.zero;
            float planetRadius = Mathf.Max(1f, Vector3.Distance(surfaceOrigin, center));
            Vector3 up = localUp.sqrMagnitude > 0.5f ? localUp.normalized : (surfaceOrigin - center).normalized;
            Vector3 tangentForward = Vector3.ProjectOnPlane(forward, up).normalized;
            if (tangentForward.sqrMagnitude < 0.5f) tangentForward = Vector3.Cross(up, Vector3.right).normalized;
            float impulse = Mathf.Lerp(
                profile != null ? profile.MinimumImpulse : 85f,
                profile != null ? profile.MaximumImpulse : 420f,
                Mathf.Clamp01(powerCharge01));
            for (int index = 0; index < topology.Cells.Length; index++)
            {
                EarthWebWaveCell cell = topology.Cells[index];
                EarthPillarWaveSample sample = cell.Sample;
                Vector3 tangentDirection = Quaternion.AngleAxis(sample.AngleDegrees, up) * tangentForward;
                float arcRadians = sample.ArcDistance / planetRadius;
                Vector3 radial = up * Mathf.Cos(arcRadians) + tangentDirection * Mathf.Sin(arcRadians);
                Vector3 columnUp = radial.normalized;
                Vector3 surface = center + (columnUp * planetRadius);
                Vector3 columnForward = Vector3.ProjectOnPlane(tangentDirection, columnUp).normalized;
                EarthPillarWaveColumn column = Acquire();
                column.Schedule(
                    this,
                    surface,
                    columnUp,
                    columnForward,
                    sample.Height,
                    sample.Width,
                    sample.Depth,
                    sample.StartDelay,
                    sample.HoldDuration,
                    sample.Crest01,
                    _nextPulseId++,
                    impulse,
                    caster,
                    profile,
                    ImpactHits,
                    sample.ShapeSides,
                    sample.ShapeAreaScale,
                    sample.SpiralPhase01,
                    cell.Footprint,
                    cell.Area);
            }
            return topology.Cells.Length;
        }

        private EarthPillarWaveColumn Acquire()
        {
            for (int index = 0; index < _columns.Count; index++)
                if (!_columns[index].gameObject.activeSelf) return _columns[index];
            EarthPillarWaveColumn column = _columns[_reuseCursor];
            _reuseCursor = (_reuseCursor + 1) % _columns.Count;
            column.ResetColumn();
            return column;
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
            filter.sharedMesh = cellMesh;
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
            column.SetInstancedRendering(!useLegacyColumnMeshes);
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
        private Rigidbody _body;
        private Collider _collider;
        private MeshRenderer _renderer;
        private EarthPillarWaveProfile _profile;
        private Collider[] _impactHits;
        private Rigidbody _caster;
        private ActiveRagdollPuppet _casterPuppet;
        private Vector3 _surface;
        private Vector3 _up;
        private Vector3 _outward;
        private Vector3 _fullScale;
        private float _delay;
        private float _holdDuration;
        private float _impulse;
        private float _elapsed;
        private bool _impacted;
        private EarthPillarWavePool _owner;
        private Quaternion _baseRotation;
        private float _crest01;
        private uint _stableId;
        private uint _generation;
        private bool _magicDetached;
        private bool _instancedRendering;
        private bool _visualVisible;
        private int _magicGripCount;
        private float _detachedElapsed;
        private bool _polygonCell;
        private float _sampleHeight;
        private float _slabThickness;
        private float _footprintArea;
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

        public void SetInstancedRendering(bool value)
        {
            _instancedRendering = value;
            Resolve();
            _renderer.enabled = _visualVisible && (!_instancedRendering || _magicDetached);
        }

        public bool TryGetInstancedRenderMatrix(out Matrix4x4 matrix)
        {
            matrix = default;
            if (!_instancedRendering || !_visualVisible || _magicDetached || !gameObject.activeInHierarchy)
                return false;
            matrix = transform.localToWorldMatrix;
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
            float footprintArea = 0f)
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
            _slabThickness = Mathf.Max(0.24f, _sampleHeight + 0.20f);
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
                    _slabThickness);
                MeshFilter filter = GetComponent<MeshFilter>();
                if (filter != null) filter.sharedMesh = _ownedCellMesh;
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
            }
            _surface = surface;
            _delay = Mathf.Max(0f, delay);
            _holdDuration = Mathf.Max(0.05f, holdDuration);
            _impulse = impulse;
            _crest01 = Mathf.Clamp01(crest01);
            _stableId = stableId;
            _generation++;
            if (_generation == 0u) _generation = 1u;
            _elapsed = 0f;
            _impacted = false;
            _magicDetached = false;
            _magicGripCount = 0;
            _detachedElapsed = 0f;
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
                _baseRotation = Quaternion.AngleAxis(geologicalYaw, _up) *
                                Quaternion.LookRotation(forward, _up);
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
            _body.WakeUp();
            MatterIdentity?.TryTransition(EarthMatterPhase.Controlled);
        }

        public void OnEarthMagicReleased(EarthMagicGripKind grip)
        {
            _magicGripCount = Mathf.Max(0, _magicGripCount - 1);
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
            if (_elapsed < _delay) return;
            float localTime = _elapsed - _delay;
            float rise = _profile != null ? _profile.ColumnRiseSeconds : 0.36f;
            float hold = _holdDuration;
            float retreat = _profile != null ? _profile.ColumnRetreatSeconds : 0.46f;
            EarthPillarWaveMotionSample motion = EarthPillarWaveSolver.EvaluateMotion(
                localTime, rise, hold, retreat);
            if (motion.Complete)
            {
                ResetColumn();
                return;
            }
            if (!_visualVisible)
            {
                SetVisualVisible(true);
            }
            float rise01 = Mathf.Clamp01(localTime / Mathf.Max(0.05f, rise));
            float tremorEnvelope = Mathf.Sin(rise01 * Mathf.PI) * (1f - motion.Sink01);
            float phase = (_stableId * 0.6180339f) + (localTime * 34f);
            // Shared-boundary Voronoi cells must translate coherently. Per-cell
            // lateral shake reopened their exact seams and made the web read as a
            // pile of independent boxes; the vertical overshoot already carries mass.
            Vector3 lateral = _polygonCell
                ? Vector3.zero
                : transform.right * (Mathf.Sin(phase) * 0.028f * tremorEnvelope);
            if (_polygonCell)
            {
                // The complete underground rock volume translates upward. No axis
                // scaling means the silhouette remains a real Voronoi plate instead
                // of turning back into a stretched rectangular pillar.
                float lift = _sampleHeight * motion.Height01;
                _body.MovePosition(_surface + (_up * lift) + lateral);
            }
            else
            {
                float visibleHeight = Mathf.Max(0.012f, _fullScale.y * motion.Height01);
                float sink = _fullScale.y * 0.18f * motion.Sink01;
                _body.MovePosition(_surface - (_up * sink) + (_up * visibleHeight * 0.5f) + lateral);
            }
            // Geological cells rise along local gravity. A tiny yaw vibration sells
            // mass without making every tooth lean back toward the caster.
            float yawTremor = _polygonCell
                ? 0f
                : Mathf.Sin(phase * 0.73f) * 0.8f * tremorEnvelope;
            _body.MoveRotation(Quaternion.AngleAxis(yawTremor, _up) * _baseRotation);
            if (_polygonCell)
                transform.localScale = Vector3.one;
            else
            {
                float visibleHeight = Mathf.Max(0.012f, _fullScale.y * motion.Height01);
                transform.localScale = new Vector3(
                    _fullScale.x * motion.Width01,
                    visibleHeight,
                    _fullScale.z * motion.Width01);
            }
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
                wall?.ApplyStructureImpact(transform.position, _outward + _up, _impulse);
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

        private void SetVisualVisible(bool visible)
        {
            _visualVisible = visible;
            if (_renderer != null)
                _renderer.enabled = visible && (!_instancedRendering || _magicDetached);
        }

        private void OnDestroy()
        {
            if (_ownedCellMesh == null) return;
            if (Application.isPlaying) Destroy(_ownedCellMesh);
            else DestroyImmediate(_ownedCellMesh);
            _ownedCellMesh = null;
        }
    }
}
