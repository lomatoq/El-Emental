using System;
using Elemental.Simulation.Structures;
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

        private IEarthFractureAssetRuntimeData _asset;
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
            if (impact.SourceId != 0u && impact.SourceId == _lastImpactSourceId &&
                Time.time - _lastImpactTime < 0.35f) return false;
            EarthArenaFractureDecision decision = EarthArenaFractureGate.Resolve(
                ordinaryDamageEnabled,
                EarthArenaFractureTrigger.OrdinaryImpact,
                impact.Impulse,
                PieceCount - _releasedCount);
            if (!decision.Accepted) return false;
            _lastImpactSourceId = impact.SourceId;
            _lastImpactTime = Time.time;
            return ReleaseNearestPieces(
                impact.Point, impact.Direction, impact.Impulse, decision.ReleaseCount);
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
                alreadyRepaired++;
            }
            if (_releasedCount == 0)
            {
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
            index >= 0 && index < _released.Length && _released[index];

        public bool TryAcquirePiece(int index)
        {
            if (index < 0 || index >= PieceCount) return false;
            if (!_released[index] && !ReleasePiece(
                    index,
                    pieces[index] != null ? pieces[index].position : transform.position,
                    Vector3.zero,
                    0f)) return false;
            return _pieceTargets[index] != null && _pieceTargets[index].IsEarthTargetValid;
        }

        public void HandlePieceCollision(int pieceIndex, Collision collision)
        {
            if (collision == null || collision.contactCount == 0 || collision.collider == null) return;
            EarthArenaStructure other = collision.collider.GetComponentInParent<EarthArenaStructure>();
            if (other == null || other == this) return;
            ContactPoint contact = collision.GetContact(0);
            Vector3 direction = _pieceBodies[pieceIndex] != null &&
                                _pieceBodies[pieceIndex].linearVelocity.sqrMagnitude > 0.01f
                ? _pieceBodies[pieceIndex].linearVelocity.normalized
                : -contact.normal;
            var impact = new EarthStructureImpact(
                contact.point,
                direction,
                collision.impulse.magnitude,
                EarthStructureImpactKind.Projectile,
                _pieceTargets[pieceIndex] != null ? _pieceTargets[pieceIndex].StableEarthId : 0u);
            other.ApplyEarthImpact(in impact);
        }

        private void Awake()
        {
            if (!InitializeRuntime(false))
                Debug.LogError("[Elemental] Broken Crown structure has invalid fracture wiring.", this);
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
            _pieceGravity = new GravityBody[pieceCount];
            _released = new bool[pieceCount];

            _fractureShadingProperties ??= new MaterialPropertyBlock();
            float4x4 intactLocalToStructure = EarthArenaFractureShading.ToFloat4x4(
                coordinateRoot.worldToLocalMatrix * intactRenderer.transform.localToWorldMatrix);
            if (!ApplyFractureShadingFrame(intactRenderer, intactLocalToStructure)) return false;

            for (int index = 0; index < pieceCount; index++)
            {
                Transform piece = pieces[index];
                if (piece == null) return false;
                Renderer renderer = piece.GetComponent<Renderer>();
                if (renderer != null && pieceMaterial != null)
                {
                    MeshFilter filter = piece.GetComponent<MeshFilter>();
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
                MeshCollider collider = piece.GetComponent<MeshCollider>();
                if (collider == null) collider = piece.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = _asset.GetPieceColliderMesh(index);
                collider.convex = true;
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
            for (int index = 0; index < _pieceStates.Length; index++)
            {
                _pieceStates[index] = EarthPieceState.Intact;
                _released[index] = false;
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
            float impulse)
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
            SolveIslands();
            FracturePresented?.Invoke(new EarthArenaFracturePulse(
                point,
                direction,
                impulse,
                1));
            return true;
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
            for (int index = 0; index < pieces.Length; index++)
            {
                if (_released[index] || pieces[index] == null) continue;
                float distance = Vector3.SqrMagnitude(pieces[index].position - point);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = index;
            }
            return best;
        }

        private int FindReleasedPiece()
        {
            for (int index = _released.Length - 1; index >= 0; index--)
                if (_released[index]) return index;
            return -1;
        }

        private void ReattachPiece(int index)
        {
            if (index < 0 || index >= PieceCount || !_released[index]) return;
            Rigidbody body = _pieceBodies[index];
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.detectCollisions = true;
            }
            if (_pieceGravity[index] != null) _pieceGravity[index].enabled = false;
            EarthPieceDefinition definition = _pieceDefinitions[index];
            Transform piece = pieces[index];
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
