using System;
using Elemental.Simulation.Structures;
using Elemental.Simulation.Bending;
using Elemental.Runtime.World;
using Elemental.Runtime.Geometry;
using Elemental.Runtime.Matter;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    public readonly struct EarthArenaFracturePulse
    {
        public EarthArenaFracturePulse(
            Vector3 point,
            Vector3 direction,
            float impulse,
            int releasedPieces)
        {
            Point = point;
            Direction = direction;
            Impulse = Mathf.Max(0f, impulse);
            ReleasedPieces = Mathf.Max(1, releasedPieces);
        }

        public Vector3 Point { get; }
        public Vector3 Direction { get; }
        public float Impulse { get; }
        public int ReleasedPieces { get; }
    }

    [DisallowMultipleComponent]
    public sealed class EarthArenaStructure : MonoBehaviour, IEarthFractureSource,
        IEarthDamageableStructure, IEarthPluckableStructure
    {
        private static readonly ProfilerMarker ImpactMarker =
            new ProfilerMarker("Elemental.Earth.ArenaFracture.Impact");
        private static readonly ProfilerMarker ProxyMarker =
            new ProfilerMarker("Elemental.Earth.ArenaFracture.ProxySwap");
        private static readonly ProfilerMarker SupportMarker =
            new ProfilerMarker("Elemental.Earth.ArenaFracture.ReleaseUnsupported");

        [SerializeField] private ScriptableObject fractureAssetObject;
        [SerializeField] private Transform coordinateRoot;
        [SerializeField] private Transform fractureRoot;
        [SerializeField] private Renderer intactRenderer;
        [SerializeField] private Collider intactCollider;
        [SerializeField] private Transform[] pieces = Array.Empty<Transform>();
        [SerializeField] private GravityWorldBehaviour gravityWorld;
        [SerializeField] private Material pieceMaterial;
        [SerializeField] private Material pieceInteriorMaterial;
        [SerializeField] private uint structureId;
        [SerializeField] private bool ordinaryDamageEnabled = true;
        [SerializeField] private bool repairable = true;
        [SerializeField] private EarthMaterialFeedbackHub materialFeedback;
        [SerializeField] private EarthStoneBevelProfile stoneBevelProfile;
        [SerializeField] private EarthRockDebrisPool rockDebrisPool;
        [SerializeField, Min(1f)] private float cumulativeFractureImpulse = 95f;
        public void ConfigureRockBreakup(EarthRockDebrisPool pool) => rockDebrisPool = pool;
        private EarthImpactDamage _impactDamage;
        private EarthImpactDamage[] _pieceImpactDamage;
        private bool[] _shattered;
        private EarthMatterIdentity[] _pieceIdentities;
        private uint[] _pieceLastSource;
        private float[] _pieceLastHitAt, _pieceArmedAt;
        public float AccumulatedImpactImpulse => _impactDamage.Impulse;
        public int ShatteredPieceCount { get; private set; }
        public bool HasMaterialFeedback => materialFeedback != null;
        private readonly EarthContactFrictionFeedback _frictionFeedback = new();
        public void ReportPieceFriction(int pieceIndex, Collision collision) =>
            _frictionFeedback.Emit(materialFeedback, collision,
                pieceIndex >= 0 && pieceIndex < _pieceTargets.Length && _pieceTargets[pieceIndex] != null
                    ? _pieceTargets[pieceIndex].StableEarthId : structureId, _generation);
        public void ConfigureMaterialFeedback(EarthMaterialFeedbackHub hub) => materialFeedback = hub;

        private IEarthFractureAssetRuntimeData _asset;
        private Mesh[] _beveledRenderMeshes;
        private MeshFilter[] _pieceFilters;
        private Renderer[] _pieceRenderers;
        private Material[][] _restMaterials;
        private EarthPieceDefinition[] _pieceDefinitions = Array.Empty<EarthPieceDefinition>();
        private EarthBondDefinition[] _bondDefinitions = Array.Empty<EarthBondDefinition>();
        private EarthPieceState[] _pieceStates = Array.Empty<EarthPieceState>();
        private EarthBondState[] _bondStates = Array.Empty<EarthBondState>();
        private EarthBondId[] _brokenOutput = Array.Empty<EarthBondId>();
        private int[] _islandByPiece = Array.Empty<int>();
        private bool[] _islandSupported = Array.Empty<bool>();
        private int[] _islandPieceCounts = Array.Empty<int>();
        private int[] _traversalQueue = Array.Empty<int>();
        private EarthArenaPiece[] _pieceTargets = Array.Empty<EarthArenaPiece>();
        private Rigidbody[] _pieceBodies = Array.Empty<Rigidbody>();
        private Collider[] _pieceColliders = Array.Empty<Collider>();
        private EarthArenaMeshPicking[] _piecePicking = Array.Empty<EarthArenaMeshPicking>();
        private GravityBody[] _pieceGravity = Array.Empty<GravityBody>();
        private bool[] _released = Array.Empty<bool>();
        private MaterialPropertyBlock _fractureShadingProperties;
        private uint _generation = 1u;
        private bool _configured;
        private bool _fractured;
        private int _releasedCount;
        private int _repairStartReleased;
        private uint _lastImpactSourceId;
        private float _lastImpactTime = float.NegativeInfinity;

        public event Action<IEarthFractureSource> TargetsActivated;
        public event Action<EarthArenaFracturePulse> FracturePresented;

        public uint StructureId => structureId;
        public uint Generation => _generation;
        public bool IsFractured => _fractured;
        public bool CameraSuppressed { get; private set; }
        public bool OrdinaryDamageEnabled => ordinaryDamageEnabled;
        public bool Repairable => repairable;
        public int PieceCount => pieces?.Length ?? 0;
        public int ReleasedPieceCount => _releasedCount;

        public void SetCameraSuppressed(bool suppressed) => CameraSuppressed = suppressed;

        public bool Configure(
            ScriptableObject configuredAsset,
            Transform configuredCoordinateRoot,
            Transform configuredFractureRoot,
            Renderer configuredIntactRenderer,
            Collider configuredIntactCollider,
            Transform[] configuredPieces,
            GravityWorldBehaviour configuredGravity,
            Material configuredMaterial,
            Material configuredInteriorMaterial,
            uint configuredStructureId,
            bool configuredOrdinaryDamage,
            bool configuredRepairable)
        {
            fractureAssetObject = configuredAsset;
            coordinateRoot = configuredCoordinateRoot;
            fractureRoot = configuredFractureRoot;
            intactRenderer = configuredIntactRenderer;
            intactCollider = configuredIntactCollider;
            pieces = configuredPieces ?? Array.Empty<Transform>();
            gravityWorld = configuredGravity;
            pieceMaterial = configuredMaterial;
            pieceInteriorMaterial = configuredInteriorMaterial;
            structureId = configuredStructureId != 0u ? configuredStructureId : 1u;
            ordinaryDamageEnabled = configuredOrdinaryDamage;
            repairable = configuredRepairable;
            return InitializeRuntime(true);
        }

        public bool ApplyEarthImpact(in EarthStructureImpact impact)
        {
            if (!ordinaryDamageEnabled || !_configured || PieceCount <= _releasedCount) return false;
            if (impact.SourceId != 0u && impact.SourceId == _lastImpactSourceId &&
                Time.time - _lastImpactTime < 0.35f) return false;
            if (!_impactDamage.Add(impact.Impulse)) return false;
            _lastImpactSourceId = impact.SourceId;
            _lastImpactTime = Time.time;
            float threshold = Mathf.Max(1f, cumulativeFractureImpulse);
            if (_impactDamage.Impulse < threshold) return false;
            float combined = _impactDamage.Impulse;
            EarthArenaFractureDecision decision = EarthArenaFractureGate.Resolve(
                ordinaryDamageEnabled,
                EarthArenaFractureTrigger.OrdinaryImpact,
                combined * (EarthArenaFractureGate.MinimumOrdinaryImpulse / threshold),
                PieceCount - _releasedCount);
            if (!decision.Accepted) return false;
            bool released = ReleaseNearestPieces(impact.Point, impact.Direction, combined, decision.ReleaseCount);
            if (released) _impactDamage.Consume(combined);
            return released;
        }

        public bool TryPluckCell(Vector3 point, out IEarthPhysicalTarget target)
        {
            target = null;
            EarthArenaFractureDecision decision = EarthArenaFractureGate.Resolve(
                ordinaryDamageEnabled,
                EarthArenaFractureTrigger.MagicPluck,
                0f,
                PieceCount - _releasedCount);
            if (!decision.Accepted) return false;
            int index = FindNearestAvailablePiece(point);
            if (index < 0 || !ReleasePiece(index, point, Vector3.zero, 0f)) return false;
            target = _pieceTargets[index];
            TargetsActivated?.Invoke(this);
            return target != null && target.IsEarthTargetValid;
        }

        public bool SetMagicDisassemblyProgress(float phase01, Vector3 focus, Vector3 direction)
        {
            if (!ordinaryDamageEnabled || !_configured || PieceCount == 0) return false;
            _repairStartReleased = 0;
            int desiredReleased = Mathf.Clamp(
                Mathf.CeilToInt(Mathf.Clamp01(phase01) * PieceCount), 1, PieceCount);
            int requested = desiredReleased - _releasedCount;
            if (requested <= 0) return _fractured;
            return ReleaseNearestPieces(focus, direction, 0f, requested);
        }

        public bool SetMagicRepairProgress(float phase01)
        {
            if (!repairable || !_fractured || _releasedCount <= 0) return false;
            if (_repairStartReleased <= 0) _repairStartReleased = _releasedCount;
            int targetRepaired = Mathf.Clamp(
                Mathf.FloorToInt(Mathf.Clamp01(phase01) * _repairStartReleased),
                0,
                _repairStartReleased);
            int alreadyRepaired = _repairStartReleased - _releasedCount;
            while (alreadyRepaired < targetRepaired)
            {
                int index = FindReleasedPiece();
                if (index < 0) break;
                ReattachPiece(index);
                materialFeedback?.Emit(EarthMaterialFeedbackKind.RepairSeat, pieces[index].position,
                    coordinateRoot.up, 0.7f, 0.4f, structureId, _generation);
                alreadyRepaired++;
            }
            if (_releasedCount == 0)
            {
                materialFeedback?.Emit(EarthMaterialFeedbackKind.RepairComplete,
                    intactRenderer.bounds.center, coordinateRoot.up, 1f,
                    Mathf.Min(3f, intactRenderer.bounds.extents.magnitude), structureId, _generation);
                ResetToIntact();
                _repairStartReleased = 0;
            }
            return true;
        }

        public bool TriggerMeteorImpact(Vector3 point, Vector3 direction, float impulse)
        {
            EarthArenaFractureDecision decision = EarthArenaFractureGate.Resolve(
                ordinaryDamageEnabled,
                EarthArenaFractureTrigger.MeteorImpact,
                impulse,
                PieceCount - _releasedCount);
            return decision.Accepted && ReleaseNearestPieces(
                point, direction, Mathf.Max(impulse, 1800f), decision.ReleaseCount);
        }

        public int CopyActiveTargetsNonAlloc(IEarthPhysicalTarget[] destination)
        {
            if (destination == null || !_fractured) return 0;
            int output = 0;
            for (int index = 0; index < _pieceTargets.Length && output < destination.Length; index++)
            {
                EarthArenaPiece target = _pieceTargets[index];
                if (target == null || !target.IsEarthTargetValid) continue;
                destination[output++] = target;
            }
            return output;
        }

        public bool IsPieceReleased(int index) =>
            index >= 0 && index < _released.Length && _released[index] && !_shattered[index];

        public bool TryAcquirePiece(int index)
        {
            if (index < 0 || index >= PieceCount) return false;
            bool newlyReleased = !_released[index];
            if (newlyReleased && !ReleasePiece(
                    index,
                    pieces[index] != null ? pieces[index].position : transform.position,
                    Vector3.zero,
                    0f)) return false;
            if (newlyReleased) TargetsActivated?.Invoke(this);
            return _pieceTargets[index] != null && _pieceTargets[index].IsEarthTargetValid;
        }

        public void NotifyPieceMagicReleased(int index)
        {
            if (!IsPieceReleased(index)) return;
            // The detachment grace period prevents newly exposed neighbouring cells
            // from breaking each other while PhysX resolves the fractured proxy. A
            // deliberate magic release is a different ownership boundary: the next
            // contact is the thrown piece's gameplay impact and must not be discarded.
            _pieceArmedAt[index] = Mathf.Min(_pieceArmedAt[index], Time.time);
        }

        public void HandlePieceCollision(int pieceIndex, Collision collision)
        {
            if (collision == null || collision.contactCount == 0 || collision.collider == null) return;
            ContactPoint effectContact = collision.GetContact(0);
            if (collision.relativeVelocity.sqrMagnitude >= 0.5625f)
                materialFeedback?.Emit(EarthMaterialFeedbackKind.Impact, effectContact.point,
                    effectContact.normal, Mathf.Clamp(collision.relativeVelocity.magnitude / 8f, 0.3f, 2f),
                    0.5f, _pieceTargets[pieceIndex].StableEarthId, _generation);
            ContactPoint contact = collision.GetContact(0);
            if (collision.relativeVelocity.magnitude < .75f) return;
            var incomingArmor = collision.collider.GetComponentInParent<EarthArmorPiece>();
            var incomingFragment = collision.collider.GetComponentInParent<EarthFragment>();
            uint incomingId = incomingArmor != null ? incomingArmor.ImpactSourceId :
                incomingFragment != null ? incomingFragment.FragmentId :
                collision.collider.GetComponentInParent<IEarthPhysicalTarget>()?.StableEarthId ?? 0u;
            float collisionImpulse = Mathf.Max(collision.impulse.magnitude,
                collision.relativeVelocity.magnitude * Mathf.Min(_pieceBodies[pieceIndex].mass,
                    collision.rigidbody != null ? collision.rigidbody.mass : _pieceBodies[pieceIndex].mass));
            var ownImpact = new EarthStructureImpact(contact.point, -contact.normal, collisionImpulse,
                EarthStructureImpactKind.Projectile, incomingId);
            Vector3 direction = _pieceBodies[pieceIndex] != null &&
                                _pieceBodies[pieceIndex].linearVelocity.sqrMagnitude > 0.01f
                ? _pieceBodies[pieceIndex].linearVelocity.normalized
                : -contact.normal;
            var impact = new EarthStructureImpact(
                contact.point,
                direction,
                collisionImpulse,
                EarthStructureImpactKind.Projectile,
                _pieceTargets[pieceIndex] != null ? _pieceTargets[pieceIndex].StableEarthId : 0u);
            EarthArenaPiece otherPiece = collision.collider.GetComponentInParent<EarthArenaPiece>();
            EarthArenaStructure other = otherPiece != null ? otherPiece.Owner : collision.collider.GetComponentInParent<EarthArenaStructure>();
            if (other != this) EarthStructureImpactRouter.Apply(collision.collider, in impact);
            ApplyReleasedPieceImpact(pieceIndex, in ownImpact);
        }

        public bool ApplyReleasedPieceImpact(int index, in EarthStructureImpact impact)
        {
            if (!IsPieceReleased(index) || rockDebrisPool == null || Time.time < _pieceArmedAt[index]) return false;
            if (impact.SourceId != 0 && impact.SourceId == _pieceLastSource[index] &&
                Time.time - _pieceLastHitAt[index] < .20f) return false;
            if (!_pieceImpactDamage[index].Add(impact.Impulse)) return false;
            _pieceLastSource[index] = impact.SourceId;
            _pieceLastHitAt[index] = Time.time;
            Rigidbody body = _pieceBodies[index];
            Vector3 size = _pieceColliders[index].bounds.size;
            float radius = Mathf.Max(.1f, Mathf.Pow(size.x * size.y * size.z * .2387324f, 1f / 3f));
            var decision = rockDebrisPool.ResolveBreak(radius, body.mass, _pieceImpactDamage[index].Impulse);
            if (!decision.Breaks || !rockDebrisPool.TryEmitBreak(impact.Point, -impact.Direction,
                    body.linearVelocity, radius, body.mass, _pieceTargets[index].StableEarthId,
                    decision, 0, _pieceIdentities[index])) return false;
            _shattered[index] = true;
            ShatteredPieceCount++;
            var state = _pieceStates[index];
            state.Phase = EarthPiecePhase.Missing;
            _pieceStates[index] = state;
            pieces[index].gameObject.SetActive(false);
            SolveIslands();
            return true;
        }

        private void Awake()
        {
            InitializeRuntime(false);
        }

        private void Start()
        {
            // Runtime builders assign fracture data immediately after AddComponent;
            // validate after that construction window instead of reporting a false
            // wiring error from Awake.
            if (!_configured && !InitializeRuntime(false))
                Debug.LogError(
                    $"[Elemental] Broken Crown structure '{name}' has invalid fracture wiring: " +
                    $"asset={fractureAssetObject != null}, coordinateRoot={coordinateRoot != null}, " +
                    $"fractureRoot={fractureRoot != null}, renderer={intactRenderer != null}, " +
                    $"collider={intactCollider != null}, pieces={pieces?.Length ?? 0}.",
                    this);
        }

        private bool InitializeRuntime(bool resetProxy)
        {
            _asset = fractureAssetObject as IEarthFractureAssetRuntimeData;
            if (_asset == null || coordinateRoot == null || fractureRoot == null ||
                intactRenderer == null || intactCollider == null ||
                pieces == null || _asset.PieceCount != pieces.Length ||
                _asset.PieceCount <= 0 || _asset.PieceCount > EarthBondGraph.MaxPieceCount ||
                _asset.BondCount > EarthBondGraph.MaxBondCount)
            {
                _configured = false;
                return false;
            }

            int pieceCount = _asset.PieceCount;
            int bondCount = _asset.BondCount;
            _pieceDefinitions = new EarthPieceDefinition[pieceCount];
            _bondDefinitions = new EarthBondDefinition[bondCount];
            if (!_asset.CopyDefinitions(_pieceDefinitions, _bondDefinitions)) return false;
            EarthGraphValidationResult validation = EarthBondGraph.Validate(
                _pieceDefinitions, pieceCount, _bondDefinitions, bondCount);
            if (!validation.IsValid) return false;

            _pieceStates = new EarthPieceState[pieceCount];
            _bondStates = new EarthBondState[bondCount];
            _brokenOutput = new EarthBondId[Mathf.Max(1, bondCount)];
            _islandByPiece = new int[pieceCount];
            _islandSupported = new bool[pieceCount];
            _islandPieceCounts = new int[pieceCount];
            _traversalQueue = new int[pieceCount];
            _pieceTargets = new EarthArenaPiece[pieceCount];
            _pieceBodies = new Rigidbody[pieceCount];
            _pieceColliders = new Collider[pieceCount];
            _piecePicking = new EarthArenaMeshPicking[pieceCount];
            _pieceGravity = new GravityBody[pieceCount];
            _released = new bool[pieceCount];
            _shattered = new bool[pieceCount];
            _pieceImpactDamage = new EarthImpactDamage[pieceCount];
            _pieceIdentities = new EarthMatterIdentity[pieceCount];
            _pieceLastSource = new uint[pieceCount];
            _pieceLastHitAt = new float[pieceCount];
            _pieceArmedAt = new float[pieceCount];
            _pieceFilters = new MeshFilter[pieceCount];
            _pieceRenderers = new Renderer[pieceCount];
            _restMaterials = new Material[pieceCount][];

            _fractureShadingProperties ??= new MaterialPropertyBlock();
            if (_beveledRenderMeshes == null || _beveledRenderMeshes.Length != pieceCount)
                _beveledRenderMeshes = new Mesh[pieceCount];
            float4x4 intactLocalToStructure = EarthArenaFractureShading.ToFloat4x4(
                coordinateRoot.worldToLocalMatrix * intactRenderer.transform.localToWorldMatrix);
            if (!ApplyFractureShadingFrame(intactRenderer, intactLocalToStructure)) return false;

            for (int index = 0; index < pieceCount; index++)
            {
                Transform piece = pieces[index];
                if (piece == null) return false;
                Renderer renderer = piece.GetComponent<Renderer>();
                MeshFilter filter = piece.GetComponent<MeshFilter>();
                Mesh bakedRenderMesh = _asset.GetPieceRenderMesh(index);
                if (filter != null && bakedRenderMesh != null)
                {
                    _beveledRenderMeshes[index] ??= EarthFractureBevelMeshBuilder.Create(bakedRenderMesh, stoneBevelProfile);
                    filter.sharedMesh = _beveledRenderMeshes[index];
                }
                if (renderer != null && pieceMaterial != null)
                {
                    bool hasInteriorSlot = filter != null && filter.sharedMesh != null &&
                                           filter.sharedMesh.subMeshCount > 1 &&
                                           pieceInteriorMaterial != null;
                    renderer.sharedMaterials = hasInteriorSlot
                        ? new[] { pieceMaterial, pieceInteriorMaterial }
                        : new[] { pieceMaterial };
                }
                if (renderer != null)
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
                EarthPieceDefinition definition = _pieceDefinitions[index];
                float4x4 restLocalToStructure = float4x4.TRS(
                    definition.RestLocalPosition,
                    definition.RestLocalRotation,
                    definition.RestLocalScale);
                if (!ApplyFractureShadingFrame(renderer, restLocalToStructure)) return false;
                _pieceFilters[index] = filter;
                _pieceRenderers[index] = renderer;
                _restMaterials[index] = renderer != null ? renderer.sharedMaterials : System.Array.Empty<Material>();
                MeshCollider collider = piece.GetComponent<MeshCollider>();
                if (collider == null) collider = piece.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = _asset.GetPieceColliderMesh(index);
                _piecePicking[index] = new EarthArenaMeshPicking(collider.sharedMesh);
                collider.convex = true;
                if (Application.isPlaying) rockDebrisPool?.PrepareFracture(collider);
                collider.enabled = false;
                Rigidbody body = piece.GetComponent<Rigidbody>();
                if (body == null) body = piece.gameObject.AddComponent<Rigidbody>();
                body.mass = Mathf.Clamp(_pieceDefinitions[index].Mass, 8f, 1800f);
                body.useGravity = false;
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                GravityBody gravity = piece.GetComponent<GravityBody>();
                if (gravity == null) gravity = piece.gameObject.AddComponent<GravityBody>();
                gravity.Configure(gravityWorld, body);
                gravity.enabled = false;
                EarthArenaPiece target = piece.GetComponent<EarthArenaPiece>();
                if (target == null) target = piece.gameObject.AddComponent<EarthArenaPiece>();
                target.Configure(this, index, _pieceDefinitions[index].Id, body, collider, gravity);
                _pieceIdentities[index] = piece.GetComponent<EarthMatterIdentity>();
                if (_pieceIdentities[index] == null) _pieceIdentities[index] = piece.gameObject.AddComponent<EarthMatterIdentity>();
                _pieceBodies[index] = body;
                _pieceColliders[index] = collider;
                _pieceGravity[index] = gravity;
                _pieceTargets[index] = target;
            }

            _configured = true;
            if (resetProxy) ResetToIntact();
            else ResetCanonicalState();
            return true;
        }

        private void OnDestroy()
        {
            if (_beveledRenderMeshes == null) return;
            for (int i = 0; i < _beveledRenderMeshes.Length; i++)
                if (_beveledRenderMeshes[i] != null &&
                    (_asset == null || _beveledRenderMeshes[i] != _asset.GetPieceRenderMesh(i)))
                {
                    if (Application.isPlaying) Destroy(_beveledRenderMeshes[i]);
                    else DestroyImmediate(_beveledRenderMeshes[i]);
                }
        }

        private bool ApplyFractureShadingFrame(Renderer renderer, float4x4 localToStructure)
        {
            EarthFractureMappingFrame frame =
                EarthFractureMappingFrameSolver.Resolve(localToStructure);
            return EarthArenaFractureShading.Apply(
                renderer,
                in frame,
                _fractureShadingProperties);
        }

        private void ResetToIntact()
        {
            _generation = _generation == uint.MaxValue ? 1u : _generation + 1u;
            ResetCanonicalState();
        }

        private void ResetCanonicalState()
        {
            _fractured = false;
            _releasedCount = 0;
            _repairStartReleased = 0;
            _impactDamage = default;
            ShatteredPieceCount = 0;
            for (int index = 0; index < _pieceStates.Length; index++)
            {
                _pieceStates[index] = EarthPieceState.Intact;
                _released[index] = false;
                _shattered[index] = false;
                _pieceImpactDamage[index] = default;
                Rigidbody body = _pieceBodies[index];
                if (body != null)
                {
                    if (!body.isKinematic)
                    {
                        body.linearVelocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                    }
                    body.isKinematic = true;
                    body.detectCollisions = false;
                }
                if (_pieceColliders[index] != null) _pieceColliders[index].enabled = false;
                if (_pieceGravity[index] != null) _pieceGravity[index].enabled = false;
                if (pieces[index] != null) pieces[index].gameObject.SetActive(false);
            }
            for (int index = 0; index < _bondStates.Length; index++)
                _bondStates[index] = EarthBondState.Healthy;
            if (fractureRoot != null) fractureRoot.gameObject.SetActive(false);
            if (intactRenderer != null) intactRenderer.enabled = true;
            if (intactCollider != null) intactCollider.enabled = true;
        }

        private bool ReleaseNearestPieces(
            Vector3 point,
            Vector3 direction,
            float impulse,
            int requestedCount)
        {
            if (!_configured || requestedCount <= 0) return false;
            using (ImpactMarker.Auto())
            {
                bool releasedAny = false;
                for (int count = 0; count < requestedCount; count++)
                {
                    int index = FindNearestAvailablePiece(point);
                    if (index < 0) break;
                    if (!ReleasePiece(index, point, direction, impulse)) continue;
                    releasedAny = true;
                    point += SafeDirection(direction) * 0.08f;
                }
                if (releasedAny) TargetsActivated?.Invoke(this);
                return releasedAny;
            }
        }

        private bool ReleasePiece(
            int pieceIndex,
            Vector3 point,
            Vector3 direction,
            float impulse,
            bool releaseUnsupported = true)
        {
            if (pieceIndex < 0 || pieceIndex >= PieceCount || _released[pieceIndex]) return false;
            EnsureFracturedProxy();
            uint tick = unchecked((uint)Mathf.Max(1, Time.frameCount));
            for (int bondIndex = 0; bondIndex < _bondDefinitions.Length; bondIndex++)
            {
                EarthBondDefinition bond = _bondDefinitions[bondIndex];
                if (bond.PieceA != pieceIndex && bond.PieceB != pieceIndex) continue;
                EarthBondState state = _bondStates[bondIndex];
                state.Phase = EarthBondPhase.Broken;
                state.AccumulatedDamage = 1f;
                state.LastChangedTick = tick;
                _bondStates[bondIndex] = state;
            }
            EarthPieceState pieceState = _pieceStates[pieceIndex];
            pieceState.Phase = EarthPiecePhase.Dynamic;
            pieceState.LastChangedTick = tick;
            _pieceStates[pieceIndex] = pieceState;
            _released[pieceIndex] = true;
            _releasedCount++;
            _pieceArmedAt[pieceIndex] = Time.time + .20f;
            // Detachment preserves the authored cell, its bevel and its fracture mapping.
            // Only a subsequent physical split may replace it with smaller contained stones.

            Rigidbody body = _pieceBodies[pieceIndex];
            Collider collider = _pieceColliders[pieceIndex];
            GravityBody gravity = _pieceGravity[pieceIndex];
            if (collider != null) collider.enabled = true;
            if (body != null)
            {
                body.detectCollisions = true;
                body.isKinematic = false;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.WakeUp();
                if (impulse > 0f)
                {
                    Vector3 safeDirection = SafeDirection(direction);
                    float boundedImpulse = Mathf.Min(impulse * 0.72f, body.mass * 6.5f);
                    body.AddForceAtPosition(safeDirection * boundedImpulse, point, ForceMode.Impulse);
                }
            }
            if (gravity != null) gravity.enabled = true;
            if (releaseUnsupported) ReleaseUnsupportedPieces();
            materialFeedback?.Emit(EarthMaterialFeedbackKind.Fracture, point,
                coordinateRoot != null ? coordinateRoot.up : transform.up, 1f, 0.65f,
                structureId ^ (uint)(pieceIndex + 1), _generation, 80, 18);
            FracturePresented?.Invoke(new EarthArenaFracturePulse(
                point,
                direction,
                impulse,
                1));
            return true;
        }

        private void ReleaseUnsupportedPieces()
        {
            using (SupportMarker.Auto())
            {
                SolveIslands();
                // The solved graph is a snapshot. Releasing an unsupported island
                // cannot remove a path to a supported one, so one bounded pass is enough.
                bool changed = false;
                for (int i = 0; i < _pieceStates.Length; i++)
                {
                    int island = _islandByPiece[i];
                    if (_released[i] || island < 0 || _islandSupported[island]) continue;
                    changed |= ReleasePiece(i, pieces[i].position, Vector3.zero, 0f, false);
                }
                if (changed) SolveIslands();
            }
        }

        private void EnsureFracturedProxy()
        {
            if (_fractured) return;
            using (ProxyMarker.Auto())
            {
                _fractured = true;
                if (intactRenderer != null) intactRenderer.enabled = false;
                if (intactCollider != null) intactCollider.enabled = false;
                if (fractureRoot != null) fractureRoot.gameObject.SetActive(true);
                for (int index = 0; index < pieces.Length; index++)
                {
                    Transform piece = pieces[index];
                    if (piece == null) continue;
                    piece.gameObject.SetActive(true);
                    Rigidbody body = _pieceBodies[index];
                    if (body != null)
                    {
                        body.isKinematic = true;
                        body.detectCollisions = true;
                    }
                    if (_pieceColliders[index] != null) _pieceColliders[index].enabled = true;
                }
            }
        }

        private int FindNearestAvailablePiece(Vector3 point)
        {
            int best = -1;
            float bestDistance = float.PositiveInfinity;
            float bestCenterDistance = float.PositiveInfinity;
            for (int index = 0; index < pieces.Length; index++)
            {
                if (_released[index] || pieces[index] == null) continue;
                // Separated cells can share Blender origins. Use cached authored
                // triangles, independent of dormant collider activation/cooking.
                Transform piece = pieces[index];
                float distance = _piecePicking[index].SquaredDistance(point,
                    piece.localToWorldMatrix, piece.worldToLocalMatrix, out float centerDistance);
                if (distance > bestDistance || distance == bestDistance && centerDistance >= bestCenterDistance) continue;
                bestDistance = distance;
                bestCenterDistance = centerDistance;
                best = index;
            }
            return best;
        }

        private int FindReleasedPiece()
        {
            for (int index = _released.Length - 1; index >= 0; index--)
                if (_released[index] && !_shattered[index] && CanSeatOnAttachedSupport(index)) return index;
            return -1;
        }

        private bool CanSeatOnAttachedSupport(int index)
        {
            // Repair grows from world foundations through cells that are already seated.
            // A free foundation cell retains its authoring flag but cannot support a
            // different kinematic cell until it has itself been reattached.
            if ((_pieceDefinitions[index].Flags & EarthPieceFlags.Foundation) != 0) return true;
            for (int bondIndex = 0; bondIndex < _bondDefinitions.Length; bondIndex++)
            {
                EarthBondDefinition bond = _bondDefinitions[bondIndex];
                int neighbor = bond.PieceA == index ? bond.PieceB : bond.PieceB == index ? bond.PieceA : -2;
                if (neighbor == EarthBondGraph.WorldPieceIndex) return true;
                if (neighbor < 0 || _released[neighbor] || _shattered[neighbor]) continue;
                int island = _islandByPiece[neighbor];
                if (island >= 0 && _islandSupported[island]) return true;
            }
            return false;
        }

        private void ReattachPiece(int index)
        {
            if (index < 0 || index >= PieceCount || !_released[index] || _shattered[index]) return;
            if (_pieceFilters[index] != null) _pieceFilters[index].sharedMesh = _beveledRenderMeshes[index];
            if (_pieceRenderers[index] != null) _pieceRenderers[index].sharedMaterials = _restMaterials[index];
            Rigidbody body = _pieceBodies[index];
            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;
                body.detectCollisions = true;
            }
            if (_pieceGravity[index] != null) _pieceGravity[index].enabled = false;
            EarthPieceDefinition definition = _pieceDefinitions[index];
            Transform piece = pieces[index];
            ApplyFractureShadingFrame(_pieceRenderers[index], float4x4.TRS(
                definition.RestLocalPosition, definition.RestLocalRotation, definition.RestLocalScale));
            piece.localPosition = new Vector3(
                definition.RestLocalPosition.x,
                definition.RestLocalPosition.y,
                definition.RestLocalPosition.z);
            quaternion rotation = definition.RestLocalRotation;
            piece.localRotation = new Quaternion(
                rotation.value.x,
                rotation.value.y,
                rotation.value.z,
                rotation.value.w);
            piece.localScale = new Vector3(
                definition.RestLocalScale.x,
                definition.RestLocalScale.y,
                definition.RestLocalScale.z);
            _released[index] = false;
            _releasedCount--;
            EarthPieceState state = _pieceStates[index];
            state.Phase = EarthPiecePhase.Welded;
            state.LastChangedTick = unchecked((uint)Mathf.Max(1, Time.frameCount));
            _pieceStates[index] = state;
            for (int bondIndex = 0; bondIndex < _bondDefinitions.Length; bondIndex++)
            {
                EarthBondDefinition bond = _bondDefinitions[bondIndex];
                if (bond.PieceA != index && bond.PieceB != index) continue;
                int neighbor = bond.PieceA == index ? bond.PieceB : bond.PieceA;
                // Never manufacture graph connections to still-free/missing bodies.
                if (neighbor >= 0 && (_released[neighbor] || _shattered[neighbor])) continue;
                EarthBondState bondState = _bondStates[bondIndex];
                bondState.Phase = EarthBondPhase.Repaired;
                bondState.AccumulatedDamage = 0f;
                bondState.LastChangedTick = state.LastChangedTick;
                _bondStates[bondIndex] = bondState;
            }
            SolveIslands();
        }

        private void SolveIslands()
        {
            EarthFractureBatchRunner.SolveIslands(
                _pieceDefinitions,
                _pieceStates,
                _pieceStates.Length,
                _bondDefinitions,
                _bondStates,
                _bondStates.Length,
                _islandByPiece,
                _islandSupported,
                _islandPieceCounts,
                _traversalQueue);
        }

        private static Vector3 SafeDirection(Vector3 direction) =>
            direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
    }
}
