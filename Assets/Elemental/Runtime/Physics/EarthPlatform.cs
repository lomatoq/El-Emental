using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Structures;
using Elemental.Runtime.Geometry;
using Elemental.Runtime.Matter;
using Elemental.Simulation.Matter;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class EarthPlatform : MonoBehaviour, IEarthPhysicalTarget, IEarthReassemblableStructure, IMovingSurface, IEarthRepairController, IEarthDamageableStructure, IEarthPluckableStructure
    {
        private const int MaximumPieces = 48;
        private const int MaximumStructuralBonds = 768;

        private struct PlatformStructuralBond
        {
            public int PieceA;
            public int PieceB;
            public bool Foundation;
            public bool Broken;
        }

        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private MeshCollider _collider;
        private Rigidbody _body;
        private EarthCohesiveStructure _cohesion;
        private EarthPlatformProfile _profile;
        private Mesh _solidMesh;
        private EarthPlatformPiece[] _pieces;
        private float[] _pieceReleasedAt;
        private Vector3[] _pieceFullScale;
        private float _fractureElapsed;
        private float2[] _polygon;
        private bool _fractured;
        private Vector3 _surfacePosition;
        private Vector3 _buriedPosition;
        private Quaternion _surfaceRotation;
        private Vector3 _surfaceUp;
        private float _embedDepth;
        private float _emergence;
        private float _emergenceSpeed;
        private Vector3 _planetCenter;
        private Vector3 _previousFixedPosition;
        private Vector3 _surfaceVelocity;
        private float _settledAt;
        private readonly Collider[] _riderHits = new Collider[16];
        private readonly Rigidbody[] _riderBodies = new Rigidbody[8];
        private readonly Collider[] _puppetColliderScratch = new Collider[16];
        private readonly Collider[] _temporarilyIgnoredRiders = new Collider[24];
        private int _temporarilyIgnoredRiderCount;
        private uint _generation;
        private Mesh[] _pieceMeshVariants;
        private Mesh _fallbackPieceMesh;
        private Mesh[] _generatedPieceMeshes;
        private Vector3[] _pieceRestLocalPosition;
        private float[] _pieceVolume;
        private EarthVolumetricFracturePlan _fracturePlan;
        private EarthStructureFractureProfile _fractureProfile;
        private bool _repairing;
        private float _repairTarget01;
        private bool[] _repairAcquired;
        private bool[] _pieceReleased;
        private readonly PlatformStructuralBond[] _structuralBonds =
            new PlatformStructuralBond[MaximumStructuralBonds];
        private readonly bool[] _supportedPieces = new bool[MaximumPieces];
        private readonly bool[] _impactSelectedPieces = new bool[MaximumPieces];
        private readonly int[] _supportQueue = new int[MaximumPieces];
        private int _structuralBondCount;
        private EarthMatterRecord[] _matterChildren;
        private EarthMatterId[] _matterChildIds;
        private EarthMatterId[] _matterMergeIds;
        private EarthMatterId _matterConsumedRoot;

        public event System.Action<IEarthFractureSource> TargetsActivated;
        public event System.Action<EarthPlatform> Fractured;

        public uint PlatformId { get; private set; }
        public float Area { get; private set; }
        public float Height { get; private set; }
        public float Stability01 { get; private set; }
        public float CostMultiplier { get; private set; }
        public bool IsFractured => _fractured;
        public uint StructureId => PlatformId;
        public Rigidbody Body => _body;
        public uint StableEarthId => PlatformId;
        public EarthPhysicalTargetHandle TargetHandle => new EarthPhysicalTargetHandle(PlatformId, _generation);
        public float EarthMass => _body != null ? Mathf.Max(1f, _body.mass) : Mathf.Max(1f, Area * Height * 170f);
        public EarthPhysicalTargetKind TargetKind => EarthPhysicalTargetKind.Platform;
        public bool IsEarthTargetValid => gameObject.activeInHierarchy && !_fractured && _body != null;
        public uint Generation => _generation;
        public uint SurfaceId => PlatformId;
        public Vector3 SurfaceVelocity => _surfaceVelocity;
        public Vector3 SurfaceUp => _surfaceUp;
        public bool IsEmerging => !_fractured && _emergence < 1f;
        public bool IsEmergenceComplete => !_fractured && _emergence >= 1f;
        public float Emergence01 => _emergence;
        public bool IsSurfaceAvailable => gameObject.activeInHierarchy && !_fractured;
        public Collider SurfaceCollider => _collider;
        public Vector3 SurfaceTopPoint => transform.position + (_surfaceUp * Height);
        public SupportFrameSnapshot SupportFrame => new SupportFrameSnapshot(
            PlatformId,
            _generation == 0u ? 1u : _generation,
            ToFloat3(transform.position),
            new quaternion(_surfaceRotation.x, _surfaceRotation.y, _surfaceRotation.z, _surfaceRotation.w),
            ToFloat3(_surfaceVelocity),
            float3.zero,
            ToFloat3(_surfaceVelocity),
            ToFloat3(_surfaceUp),
            IsEmerging);
        public MovingSupportSnapshot Snapshot => new MovingSupportSnapshot(SupportFrame);
        public int ActivePieceCount { get; private set; }
        public IEarthRepairController RepairController => this;
        public bool IsRepairing => _repairing;
        public EarthPlatformPiece FirstActivePiece
        {
            get
            {
                if (_pieces == null) return null;
                for (int index = 0; index < _pieces.Length; index++)
                    if (_pieces[index] != null && _pieces[index].gameObject.activeSelf) return _pieces[index];
                return null;
            }
        }

        public int CopyActiveTargetsNonAlloc(IEarthPhysicalTarget[] destination)
        {
            if (destination == null || _pieces == null || !_fractured) return 0;
            int output = 0;
            for (int index = 0; index < _pieces.Length && output < destination.Length; index++)
            {
                EarthPlatformPiece target = _pieces[index];
                if (target == null || !target.IsEarthTargetValid) continue;
                destination[output++] = target;
            }
            return output;
        }

        public bool TrySampleTopSurface(Ray ray, float maximumDistance, out Vector3 point, out float distance)
        {
            point = default;
            distance = 0f;
            if (!IsSurfaceAvailable || _polygon == null || _polygon.Length < 3) return false;
            float denominator = Vector3.Dot(ray.direction, _surfaceUp);
            if (Mathf.Abs(denominator) < 0.0001f) return false;
            float travel = Vector3.Dot(SurfaceTopPoint - ray.origin, _surfaceUp) / denominator;
            if (travel < 0f || travel > maximumDistance) return false;
            Vector3 candidate = ray.GetPoint(travel);
            Vector3 local = Quaternion.Inverse(_surfaceRotation) * (candidate - transform.position);
            if (!ContainsExpanded(_polygon, new Vector2(local.x, local.z), 0.015f)) return false;
            point = candidate;
            distance = travel;
            return true;
        }

        public void Configure(
            Material material,
            EarthPlatformProfile profile,
            EarthPhysicsFeelProfile physicsFeelProfile = null,
            Mesh[] pieceMeshVariants = null)
        {
            Resolve();
            _profile = profile;
            _renderer.sharedMaterial = material;
            _pieceMeshVariants = pieceMeshVariants;
            if (_pieces != null)
            {
                ConfigurePieceMeshes(pieceMeshVariants);
                return;
            }
            _pieces = new EarthPlatformPiece[MaximumPieces];
            _pieceReleasedAt = new float[MaximumPieces];
            _pieceFullScale = new Vector3[MaximumPieces];
            _generatedPieceMeshes = new Mesh[MaximumPieces];
            _pieceRestLocalPosition = new Vector3[MaximumPieces];
            _pieceVolume = new float[MaximumPieces];
            _repairAcquired = new bool[MaximumPieces];
            _pieceReleased = new bool[MaximumPieces];
            for (int index = 0; index < MaximumPieces; index++)
            {
                GameObject pieceObject = new GameObject();
                pieceObject.name = $"Platform Piece {index + 1:00}";
                pieceObject.transform.SetParent(transform, false);
                MeshFilter filter = pieceObject.AddComponent<MeshFilter>();
                filter.sharedMesh = ResolvePieceMesh(index);
                MeshRenderer renderer = pieceObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                MeshCollider collider = pieceObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = true;
                Rigidbody pieceBody = pieceObject.AddComponent<Rigidbody>();
                pieceBody.useGravity = false;
                pieceBody.isKinematic = true;
                pieceBody.detectCollisions = false;
                pieceBody.interpolation = RigidbodyInterpolation.Interpolate;
                pieceBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                physicsFeelProfile?.Apply(
                    pieceBody,
                    collider,
                    EarthPhysicsBodyClass.HeavyBlock);
                EarthPlatformPiece piece = pieceObject.AddComponent<EarthPlatformPiece>();
                piece.Configure(this, index);
                pieceObject.SetActive(false);
                _pieces[index] = piece;
            }
            _cohesion.Configure(MaximumPieces);
        }

        public void ConfigurePieceMeshes(Mesh[] configuredVariants)
        {
            if (configuredVariants != null && configuredVariants.Length > 0)
                _pieceMeshVariants = configuredVariants;
            if (_pieces == null) return;
            for (int index = 0; index < _pieces.Length; index++)
            {
                EarthPlatformPiece piece = _pieces[index];
                if (piece == null) continue;
                Mesh mesh = ResolvePieceMesh(index);
                piece.GetComponent<MeshFilter>().sharedMesh = mesh;
                MeshCollider collider = piece.GetComponent<MeshCollider>();
                collider.sharedMesh = null;
                collider.sharedMesh = mesh;
                collider.convex = true;
            }
        }

        public void ConfigureFractureProfile(EarthStructureFractureProfile configuredProfile) =>
            _fractureProfile = configuredProfile;

        public void Initialize(
            uint id,
            in EarthPlatformGeometry geometry,
            float height,
            float embedDepth)
        {
            Resolve();
            PlatformId = id;
            _generation = _generation == uint.MaxValue ? 1u : _generation + 1u;
            Area = geometry.Area;
            EarthPlatformBudgetSample budget = EarthPlatformGeometrySolver.EvaluateHeightBudget(
                in geometry,
                height,
                _profile != null ? _profile.SoftHeightLimit : 8f,
                _profile != null ? _profile.MaximumHeight : 22f,
                _profile != null ? _profile.HeightCostExponent : 1.65f,
                _profile != null ? _profile.AspectCost : 0.18f);
            Height = budget.AcceptedHeight;
            Stability01 = budget.Stability01;
            CostMultiplier = budget.CostMultiplier;
            _embedDepth = Mathf.Max(0.08f, embedDepth);
            _polygon = geometry.Polygon;
            _fractured = false;
            _repairing = false;
            _repairTarget01 = 0f;
            _fractureElapsed = 0f;
            ActivePieceCount = 0;
            _cohesion.ResetCohesion();
            ResetStructuralBonds();
            _surfacePosition = ToVector3(geometry.Center);
            _surfaceUp = ToVector3(geometry.Up).normalized;
            _planetCenter = _surfacePosition - (_surfaceUp * geometry.SurfaceRadius);
            _surfaceRotation = Quaternion.LookRotation(ToVector3(geometry.Forward), _surfaceUp);
            _buriedPosition = _surfacePosition -
                              (_surfaceUp * (Height + _embedDepth + 0.12f));
            _emergence = 0f;
            _emergenceSpeed = 0f;
            _settledAt = float.PositiveInfinity;
            transform.SetPositionAndRotation(_buriedPosition, _surfaceRotation);
            _previousFixedPosition = _buriedPosition;
            _surfaceVelocity = Vector3.zero;
            transform.localScale = Vector3.one;
            BuildPrismMesh(_polygon, Height, _embedDepth);
            PrepareFracturePlan();
            _filter.sharedMesh = _solidMesh;
            _collider.sharedMesh = null;
            _collider.sharedMesh = _solidMesh;
            _collider.convex = false;
            // Starts below the ground, yet is already a valid support/projectile
            // surface when its first visible physics step begins.
            _collider.enabled = true;
            _renderer.enabled = true;
            _body.isKinematic = true;
            HidePieces();
            gameObject.SetActive(true);
        }

        public bool ApplyStructureImpact(Vector3 point, Vector3 direction, float impulse)
        {
            if (impulse < FractureImpulse) return false;
            if (_fractured)
                return ReleaseLocalPieces(point, direction, impulse);
            BeginFracture(point, direction, impulse);
            return true;
        }

        public bool ApplyEarthImpact(in EarthStructureImpact impact) =>
            ApplyStructureImpact(impact.Point, impact.Direction, impact.Impulse);

        public bool TryPluckCell(Vector3 point, out IEarthPhysicalTarget target)
        {
            target = null;
            if (!_fractured) BeginFracture(point, _surfaceUp, FractureImpulse * 1.05f);
            if (_pieces == null) return false;
            int best = -1;
            float bestDistance = float.PositiveInfinity;
            for (int index = 0; index < _pieces.Length; index++)
            {
                EarthPlatformPiece piece = _pieces[index];
                if (piece == null || !piece.gameObject.activeSelf) continue;
                float distance = Vector3.SqrMagnitude(piece.Body.worldCenterOfMass - point);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = index;
            }
            if (best < 0 || !AcquirePiece(best)) return false;
            target = _pieces[best];
            return target.IsEarthTargetValid;
        }

        public bool TryBeginRepair(uint tick) => TryBeginRepair(tick, 1f);

        public bool TryBeginRepair(uint tick, float targetProgress01)
        {
            if (!_fractured || _pieces == null || ActivePieceCount <= 0) return false;
            _repairing = true;
            _repairTarget01 = Mathf.Clamp01(targetProgress01);
            if (_repairAcquired == null || _repairAcquired.Length != MaximumPieces)
                _repairAcquired = new bool[MaximumPieces];
            for (int index = 0; index < _pieces.Length; index++)
            {
                EarthPlatformPiece piece = _pieces[index];
                bool available = piece != null && piece.gameObject.activeSelf;
                _repairAcquired[index] = available && AcquirePiece(index);
            }
            return true;
        }

        public bool SetTargetProgress(float targetProgress01, uint tick = 0u)
        {
            if (!_repairing) return false;
            _repairTarget01 = Mathf.Clamp01(targetProgress01);
            return true;
        }

        public void Interrupt(EarthRepairInterruptReason reason, uint tick)
        {
            if (!_repairing) return;
            ReleaseRepairPieces();
            _repairing = false;
        }

        internal bool AcquirePiece(int pieceIndex)
        {
            if (!_fractured || !_cohesion.AcquirePiece(pieceIndex)) return false;
            EarthPlatformPiece piece = _pieces[pieceIndex];
            if (piece == null) return false;
            piece.transform.localScale = _pieceFullScale[pieceIndex];
            Rigidbody body = piece.Body;
            body.isKinematic = false;
            body.detectCollisions = true;
            body.WakeUp();
            _pieceReleased[pieceIndex] = true;
            _pieceReleasedAt[pieceIndex] = _fractureElapsed;
            BreakStructuralBonds(pieceIndex);
            piece.transform.SetParent(transform.parent, true);
            ReleaseUnsupportedIslands(body.worldCenterOfMass, _surfaceUp, 0f);
            return true;
        }

        internal void ReleasePiece(int pieceIndex)
        {
            _cohesion.ReleasePiece(pieceIndex);
            if (pieceIndex >= 0 && pieceIndex < _pieceReleasedAt.Length)
                _pieceReleasedAt[pieceIndex] = _fractureElapsed;
        }

        private void Awake() => Resolve();

        private void Update()
        {
            if (!_fractured) return;
            _fractureElapsed += Time.deltaTime;
            int active = 0;
            for (int index = 0; index < _pieces.Length; index++)
            {
                EarthPlatformPiece piece = _pieces[index];
                if (piece == null || !piece.gameObject.activeSelf) continue;
                active++;
                if (_cohesion.IsPieceHeld(index)) continue;
                if (_pieceReleased == null || !_pieceReleased[index]) continue;
                DynamicDebrisLifecycleSample lifecycle = DynamicDebrisLifecycle.Evaluate(
                    _fractureElapsed - _pieceReleasedAt[index],
                    DebrisRestSeconds,
                    DebrisShrinkSeconds);
                if (!lifecycle.Shrinking) continue;
                if (!lifecycle.Complete)
                {
                    piece.transform.localScale = _pieceFullScale[index] *
                                                 Mathf.Max(0.0125f, lifecycle.Scale01);
                    piece.Body.WakeUp();
                    continue;
                }
                piece.Body.detectCollisions = false;
                piece.Body.isKinematic = true;
                piece.gameObject.SetActive(false);
                active--;
            }
            ActivePieceCount = active;
            if (active > 0) return;
            HidePieces();
            gameObject.SetActive(false);
        }

        private void BeginFracture(Vector3 point, Vector3 direction, float impulse)
        {
            transform.SetPositionAndRotation(_surfacePosition, _surfaceRotation);
            _fractured = true;
            _fractureElapsed = 0f;
            _cohesion.BeginFracture();
            _repairing = false;
            int requested = Mathf.Min(_fracturePlan.Cells.Length, MaximumPieces);
            Vector3 localImpact = transform.InverseTransformPoint(point);
            Vector3 worldDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.up;
            // Make every prepared collider live before retiring the solid shell. This
            // keeps a high-speed projectile from finding a one-tick collision hole.
            for (int output = 0; output < requested; output++)
            {
                EarthPlatformPiece piece = _pieces[output];
                piece.transform.SetParent(transform, false);
                piece.transform.localPosition = _pieceRestLocalPosition[output];
                piece.transform.localRotation = Quaternion.identity;
                piece.transform.localScale = Vector3.one;
                piece.gameObject.SetActive(true);
                _pieceFullScale[output] = Vector3.one;
                _pieceReleasedAt[output] = 0f;
                _pieceReleased[output] = false;
                Rigidbody pieceBody = piece.Body;
                pieceBody.mass = Mathf.Max(1f, _pieceVolume[output] * 135f);
                if (!pieceBody.isKinematic)
                {
                    pieceBody.linearVelocity = Vector3.zero;
                    pieceBody.angularVelocity = Vector3.zero;
                }
                pieceBody.isKinematic = true;
                pieceBody.detectCollisions = true;
            }
            RegisterMatterFracture(requested);
            Fractured?.Invoke(this);
            _renderer.enabled = false;
            _collider.enabled = false;
            ActivePieceCount = requested;
            TargetsActivated?.Invoke(this);
            ReleaseLocalPieces(point, worldDirection, impulse);
            if (requested == 0) gameObject.SetActive(false);
        }

        private bool ReleaseLocalPieces(Vector3 point, Vector3 direction, float impulse)
        {
            if (_fracturePlan.Cells == null || _pieces == null) return false;
            int total = Mathf.Min(_fracturePlan.Cells.Length, MaximumPieces);
            if (total <= 0) return false;
            Vector3 localImpact = transform.InverseTransformPoint(point);
            int releaseBudget = impulse >= FractureImpulse * 4f
                ? Mathf.Min(total, Mathf.CeilToInt(total * 0.72f))
                : impulse >= FractureImpulse * 2f
                    ? (_fractureProfile != null ? _fractureProfile.MediumImpactPieceLimit : 8)
                    : (_fractureProfile != null ? _fractureProfile.LightImpactPieceLimit : 4);
            bool damaged = false;
            System.Array.Clear(_impactSelectedPieces, 0, _impactSelectedPieces.Length);
            Vector3 worldDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.up;
            for (int released = 0; released < releaseBudget; released++)
            {
                int best = -1;
                float bestDistance = float.PositiveInfinity;
                for (int index = 0; index < total; index++)
                {
                    EarthPlatformPiece candidate = _pieces[index];
                    if (candidate == null || !candidate.gameObject.activeSelf || _pieceReleased[index] ||
                        _impactSelectedPieces[index]) continue;
                    EarthVolumetricFractureCell cell = _fracturePlan.Cells[index];
                    float foundationPenalty = cell.Foundation && impulse < FractureImpulse * 3.25f ? 1000f : 0f;
                    float distance = math.lengthsq(cell.Centroid - ToFloat3(localImpact)) + foundationPenalty;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    best = index;
                }
                if (best < 0 || bestDistance >= 999f) break;
                _impactSelectedPieces[best] = true;
                BreakStructuralBonds(best);
                damaged = true;
            }
            bool releasedAny = damaged && ReleaseUnsupportedIslands(point, worldDirection, impulse);
            if (releasedAny) TargetsActivated?.Invoke(this);
            return releasedAny;
        }

        internal void ReportPieceImpact(int pieceIndex, Collision collision)
        {
            if (!_fractured || collision == null || collision.contactCount == 0) return;
            if (pieceIndex < 0 || pieceIndex >= _pieceReleased.Length || _pieceReleased[pieceIndex]) return;
            float impulse = collision.impulse.magnitude;
            if (impulse < FractureImpulse) return;
            Vector3 direction = collision.relativeVelocity.sqrMagnitude > 0.01f
                ? -collision.relativeVelocity.normalized
                : -collision.GetContact(0).normal;
            ApplyStructureImpact(collision.GetContact(0).point, direction, impulse);
        }

        private void FixedUpdate()
        {
            if (!_fractured)
            {
                UpdateEmergence();
                CarryRiders();
                RestoreRiderCollisionsWhenSafe();
                return;
            }
            if (_pieces == null) return;
            if (_repairing)
            {
                UpdateRepair();
                return;
            }
            for (int index = 0; index < _pieces.Length; index++)
            {
                EarthPlatformPiece piece = _pieces[index];
                if (piece == null || !piece.gameObject.activeSelf || piece.Body.isKinematic) continue;
                Vector3 inward = _planetCenter - piece.Body.worldCenterOfMass;
                if (inward.sqrMagnitude < 0.01f) inward = -_surfaceUp;
                piece.Body.AddForce(inward.normalized * 11.5f, ForceMode.Acceleration);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_fractured || collision.contactCount == 0) return;
            if ((_emergence < 1f || Time.time < _settledAt + SupportGraceSeconds) &&
                collision.collider != null &&
                (collision.collider.GetComponentInParent<Elemental.Runtime.Characters.PlanetMotor>() != null ||
                 collision.collider.GetComponentInParent<Elemental.Runtime.Characters.ActiveRagdollPuppet>() != null))
                return;
            Vector3 direction = collision.relativeVelocity.sqrMagnitude > 0.01f
                ? -collision.relativeVelocity.normalized
                : -collision.GetContact(0).normal;
            ApplyStructureImpact(collision.GetContact(0).point, direction, collision.impulse.magnitude);
        }

        private void Resolve()
        {
            if (_filter == null) _filter = GetComponent<MeshFilter>();
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
            if (_collider == null) _collider = GetComponent<MeshCollider>();
            if (_body == null) _body = GetComponent<Rigidbody>();
            if (_body == null)
            {
                _body = gameObject.AddComponent<Rigidbody>();
                _body.useGravity = false;
                _body.isKinematic = true;
            }
            if (_cohesion == null) _cohesion = GetComponent<EarthCohesiveStructure>();
            if (_cohesion == null) _cohesion = gameObject.AddComponent<EarthCohesiveStructure>();
            if (_solidMesh == null) _solidMesh = new Mesh { name = "Runtime Earth Platform" };
        }

        private Mesh ResolvePieceMesh(int index)
        {
            if (_pieceMeshVariants != null && _pieceMeshVariants.Length > 0)
            {
                Mesh authored = _pieceMeshVariants[index % _pieceMeshVariants.Length];
                if (authored != null) return authored;
            }
            if (_fallbackPieceMesh == null) _fallbackPieceMesh = BuildFallbackPieceMesh();
            return _fallbackPieceMesh;
        }

        private static Mesh BuildFallbackPieceMesh()
        {
            var mesh = new Mesh { name = "Debug Platform Piece" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.42f, -0.46f), new Vector3(0.46f, -0.5f, -0.4f),
                new Vector3(0.5f, 0.39f, -0.5f), new Vector3(-0.43f, 0.5f, -0.41f),
                new Vector3(-0.48f, -0.5f, 0.4f), new Vector3(0.5f, -0.4f, 0.5f),
                new Vector3(0.42f, 0.5f, 0.43f), new Vector3(-0.5f, 0.42f, 0.5f)
            };
            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5,
                2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EarthMeshIntegrityGate.ValidateInPlaceOrUseFallback(
                mesh,
                EarthMeshIntegrityPolicy.ConvexCollider,
                mesh.name,
                mesh.bounds);
            return mesh;
        }

        private void OnDestroy()
        {
            if (_solidMesh != null)
            {
                if (Application.isPlaying) Destroy(_solidMesh);
                else DestroyImmediate(_solidMesh);
            }
            if (_fallbackPieceMesh != null)
            {
                if (Application.isPlaying) Destroy(_fallbackPieceMesh);
                else DestroyImmediate(_fallbackPieceMesh);
            }
            DestroyGeneratedPieceMeshes();
        }

        private void PrepareFracturePlan()
        {
            int requested = Mathf.Clamp(
                _fractureProfile != null
                    ? _fractureProfile.PlatformCellCount
                    : _profile != null ? _profile.FracturePieceCount : 36,
                28,
                MaximumPieces);
            _fracturePlan = EarthVolumetricFractureSolver.BuildClosedConvexPrism(
                PlatformId ^ 0xC011AB1Eu,
                _polygon,
                -_embedDepth,
                Height,
                requested);
            BuildStructuralBonds();
            DestroyGeneratedPieceMeshes();
            if (_generatedPieceMeshes == null || _generatedPieceMeshes.Length != MaximumPieces)
                _generatedPieceMeshes = new Mesh[MaximumPieces];
            if (_pieceRestLocalPosition == null || _pieceRestLocalPosition.Length != MaximumPieces)
                _pieceRestLocalPosition = new Vector3[MaximumPieces];
            if (_pieceVolume == null || _pieceVolume.Length != MaximumPieces)
                _pieceVolume = new float[MaximumPieces];

            for (int index = 0; index < _fracturePlan.Cells.Length && index < MaximumPieces; index++)
            {
                EarthVolumetricFractureCell cell = _fracturePlan.Cells[index];
                Mesh mesh = BuildPlatformPieceMesh(cell, index);
                _generatedPieceMeshes[index] = mesh;
                _pieceRestLocalPosition[index] = ToVector3(cell.Centroid);
                _pieceVolume[index] = cell.Volume;
                EarthPlatformPiece piece = _pieces[index];
                if (piece == null) continue;
                piece.GetComponent<MeshFilter>().sharedMesh = mesh;
                MeshCollider collider = piece.GetComponent<MeshCollider>();
                collider.sharedMesh = null;
                collider.sharedMesh = mesh;
                collider.convex = true;
                UnityEngine.Physics.BakeMesh(mesh.GetEntityId(), true);
            }
        }

        private void UpdateRepair()
        {
            int total = Mathf.Min(_fracturePlan.Cells.Length, MaximumPieces);
            int targetCount = Mathf.Clamp(Mathf.CeilToInt(total * _repairTarget01), 0, total);
            int available = 0;
            int seated = 0;
            for (int index = 0; index < total; index++)
            {
                EarthPlatformPiece piece = _pieces[index];
                if (piece == null || !piece.gameObject.activeSelf) continue;
                available++;
                Rigidbody body = piece.Body;
                if (index >= targetCount)
                {
                    if (_repairAcquired[index])
                    {
                        body.isKinematic = false;
                        body.detectCollisions = true;
                    }
                    continue;
                }
                body.isKinematic = true;
                body.detectCollisions = false;
                Vector3 targetPosition = _surfacePosition +
                                         (_surfaceRotation * _pieceRestLocalPosition[index]);
                Quaternion targetRotation = _surfaceRotation;
                float speed = Mathf.Lerp(7f, 18f, _repairTarget01);
                Vector3 next = Vector3.MoveTowards(body.position, targetPosition, speed * Time.fixedDeltaTime);
                Quaternion nextRotation = Quaternion.RotateTowards(
                    body.rotation, targetRotation, 540f * Time.fixedDeltaTime);
                body.MovePosition(next);
                body.MoveRotation(nextRotation);
                piece.transform.localScale = Vector3.MoveTowards(
                    piece.transform.localScale, Vector3.one, 4f * Time.fixedDeltaTime);
                if (Vector3.Distance(next, targetPosition) <= 0.025f &&
                    Quaternion.Angle(nextRotation, targetRotation) <= 0.75f)
                {
                    body.position = targetPosition;
                    body.rotation = targetRotation;
                    seated++;
                }
            }
            if (_repairTarget01 < 0.98f || targetCount < total || seated < total || available < total) return;
            CompletePhysicalRepair();
        }

        private void CompletePhysicalRepair()
        {
            RestoreMatterAfterRepair();
            for (int index = 0; index < _pieces.Length; index++)
            {
                EarthPlatformPiece piece = _pieces[index];
                if (piece == null) continue;
                Rigidbody body = piece.Body;
                body.detectCollisions = false;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;
                piece.gameObject.SetActive(false);
                piece.transform.SetParent(transform, false);
            }
            _fractured = false;
            _repairing = false;
            _repairTarget01 = 0f;
            _cohesion.ResetCohesion();
            ResetStructuralBonds();
            transform.SetPositionAndRotation(_surfacePosition, _surfaceRotation);
            _body.position = _surfacePosition;
            _body.rotation = _surfaceRotation;
            _body.isKinematic = true;
            _renderer.enabled = true;
            _collider.enabled = true;
            _emergence = 1f;
            _surfaceVelocity = Vector3.zero;
            _settledAt = Time.time;
            ActivePieceCount = 0;
            System.Array.Clear(_repairAcquired, 0, _repairAcquired.Length);
            System.Array.Clear(_pieceReleased, 0, _pieceReleased.Length);
        }

        private void RegisterMatterFracture(int count)
        {
            EarthMatterIdentity root = GetComponent<EarthMatterIdentity>();
            if (root == null || !root.TryRead(out EarthMatterRecord parent) ||
                parent.Phase == EarthMatterPhase.Consumed || count <= 0) return;
            if (_matterChildren == null || _matterChildren.Length != MaximumPieces)
            {
                _matterChildren = new EarthMatterRecord[MaximumPieces];
                _matterChildIds = new EarthMatterId[MaximumPieces];
                _matterMergeIds = new EarthMatterId[MaximumPieces];
            }
            float volumeTotal = 0f;
            for (int index = 0; index < count; index++) volumeTotal += Mathf.Max(0.0001f, _pieceVolume[index]);
            for (int index = 0; index < count; index++)
            {
                float fraction = Mathf.Max(0.0001f, _pieceVolume[index]) /
                                 Mathf.Max(0.0001f, volumeTotal);
                Rigidbody body = _pieces[index].Body;
                Quaternion rotation = body.rotation;
                var pose = new EarthMatterPose(ToFloat3(body.worldCenterOfMass),
                    new quaternion(rotation.x, rotation.y, rotation.z, rotation.w));
                _matterChildren[index] = new EarthMatterRecord
                {
                    Phase = EarthMatterPhase.Sleeping,
                    Representation = EarthRepresentationTier.SecondaryPhysical,
                    Material = parent.Material,
                    Volume = parent.Volume * fraction,
                    Mass = parent.Mass * fraction,
                    Integrity = Mathf.Clamp01(parent.Integrity),
                    Source = new EarthSourceProvenance(
                        EarthSourceKind.StructureCell,
                        parent.Id.StableId,
                        parent.Id.Generation,
                        index,
                        unchecked((uint)Time.frameCount),
                        parent.Source.SourceLocalPoint,
                        parent.Volume * fraction,
                        EarthProvenanceFlags.SourceStructureAlive |
                        EarthProvenanceFlags.VolumeReserved),
                    Owner = parent.Owner,
                    Shape = EarthShapeSemantic.PlatformCell,
                    RestPose = pose,
                    CurrentPose = pose,
                    LinearVelocity = float3.zero,
                    AngularVelocity = float3.zero
                };
            }
            EarthMatterKernelBehaviour kernel = root.Kernel;
            if (!kernel.Registry.TrySplit(root.MatterId, _matterChildren, count, _matterChildIds))
            {
                Debug.LogError($"[EarthMatter] Platform {PlatformId} fracture split rejected: {kernel.Registry.LastFailure}", this);
                return;
            }
            _matterConsumedRoot = root.MatterId;
            for (int index = 0; index < count; index++)
                EarthMatterRuntimeBridge.BindExistingRecord(
                    _pieces[index], kernel, _matterChildIds[index], _pieces[index].Body);
        }

        private void RestoreMatterAfterRepair()
        {
            if (!_matterConsumedRoot.IsValid || _matterMergeIds == null) return;
            EarthMatterIdentity root = GetComponent<EarthMatterIdentity>();
            if (root == null || root.Kernel == null ||
                !root.Kernel.Registry.TryGet(_matterConsumedRoot, out EarthMatterRecord consumed)) return;
            int total = Mathf.Min(_fracturePlan.Cells?.Length ?? 0, MaximumPieces);
            for (int index = 0; index < total; index++)
            {
                EarthMatterIdentity child = _pieces[index].GetComponent<EarthMatterIdentity>();
                if (child == null || !child.MatterId.IsValid) return;
                _matterMergeIds[index] = child.MatterId;
            }
            Quaternion rotation = transform.rotation;
            var pose = new EarthMatterPose(ToFloat3(transform.position),
                new quaternion(rotation.x, rotation.y, rotation.z, rotation.w));
            consumed.Phase = EarthMatterPhase.Sleeping;
            consumed.Representation = EarthRepresentationTier.HeroPhysical;
            consumed.Integrity = 1f;
            consumed.RestPose = pose;
            consumed.CurrentPose = pose;
            consumed.LinearVelocity = float3.zero;
            consumed.AngularVelocity = float3.zero;
            if (!root.Kernel.Registry.TryMerge(
                    _matterConsumedRoot, _matterMergeIds, total, in consumed, out EarthMatterId restored))
            {
                Debug.LogError($"[EarthMatter] Platform {PlatformId} repair merge rejected: {root.Kernel.Registry.LastFailure}", this);
                return;
            }
            EarthMatterRuntimeBridge.BindExistingRecord(this, root.Kernel, restored, _body);
            _matterConsumedRoot = default;
        }

        public void OnEarthMagicGrabbed(EarthMagicGripKind grip)
        {
            if (_body != null) _body.WakeUp();
            EarthMatterIdentity identity = GetComponent<EarthMatterIdentity>();
            if (identity != null && identity.TryRead(out EarthMatterRecord record) &&
                (record.Phase == EarthMatterPhase.Forming || record.Phase == EarthMatterPhase.Sleeping ||
                 record.Phase == EarthMatterPhase.FreeDynamic))
                identity.TryTransition(EarthMatterPhase.Controlled);
        }

        public void OnEarthMagicReleased(EarthMagicGripKind grip)
        {
            EarthMatterIdentity identity = GetComponent<EarthMatterIdentity>();
            if (identity != null && identity.TryRead(out EarthMatterRecord record) &&
                record.Phase == EarthMatterPhase.Controlled)
                identity.TryTransition(EarthMatterPhase.FreeDynamic);
        }

        private void ReleaseRepairPieces()
        {
            if (_repairAcquired == null) return;
            for (int index = 0; index < _repairAcquired.Length; index++)
            {
                if (!_repairAcquired[index]) continue;
                EarthPlatformPiece piece = _pieces[index];
                if (piece != null && piece.gameObject.activeSelf)
                {
                    piece.Body.isKinematic = false;
                    piece.Body.detectCollisions = true;
                    ReleasePiece(index);
                }
                _repairAcquired[index] = false;
            }
        }

        private void DestroyGeneratedPieceMeshes()
        {
            if (_generatedPieceMeshes == null) return;
            for (int index = 0; index < _generatedPieceMeshes.Length; index++)
            {
                Mesh mesh = _generatedPieceMeshes[index];
                if (mesh == null) continue;
                if (Application.isPlaying) Destroy(mesh);
                else DestroyImmediate(mesh);
                _generatedPieceMeshes[index] = null;
            }
        }

        private void BuildStructuralBonds()
        {
            _structuralBondCount = 0;
            if (_fracturePlan.Cells == null) return;
            int count = Mathf.Min(_fracturePlan.Cells.Length, MaximumPieces);
            for (int pieceIndex = 0; pieceIndex < count; pieceIndex++)
            {
                EarthVolumetricFractureCell cell = _fracturePlan.Cells[pieceIndex];
                if (cell.Foundation)
                    AddStructuralBond(pieceIndex, -1, true);
                EarthVolumetricFractureFace[] faces = cell.Faces;
                for (int faceIndex = 0; faceIndex < faces.Length; faceIndex++)
                {
                    int neighbour = faces[faceIndex].NeighbourCellIndex;
                    if (neighbour <= pieceIndex || neighbour >= count) continue;
                    AddStructuralBond(pieceIndex, neighbour, false);
                }
            }
        }

        private void AddStructuralBond(int pieceA, int pieceB, bool foundation)
        {
            if (_structuralBondCount >= _structuralBonds.Length) return;
            _structuralBonds[_structuralBondCount++] = new PlatformStructuralBond
            {
                PieceA = pieceA,
                PieceB = pieceB,
                Foundation = foundation,
                Broken = false
            };
        }

        private void ResetStructuralBonds()
        {
            for (int index = 0; index < _structuralBondCount; index++)
            {
                PlatformStructuralBond bond = _structuralBonds[index];
                bond.Broken = false;
                _structuralBonds[index] = bond;
            }
            System.Array.Clear(_supportedPieces, 0, _supportedPieces.Length);
        }

        private void BreakStructuralBonds(int pieceIndex)
        {
            for (int index = 0; index < _structuralBondCount; index++)
            {
                PlatformStructuralBond bond = _structuralBonds[index];
                if (bond.PieceA != pieceIndex && bond.PieceB != pieceIndex) continue;
                bond.Broken = true;
                _structuralBonds[index] = bond;
            }
        }

        private bool ReleaseUnsupportedIslands(Vector3 point, Vector3 direction, float impulse)
        {
            int count = Mathf.Min(_fracturePlan.Cells?.Length ?? 0, MaximumPieces);
            if (count <= 0) return false;
            System.Array.Clear(_supportedPieces, 0, _supportedPieces.Length);
            int read = 0;
            int write = 0;
            for (int bondIndex = 0; bondIndex < _structuralBondCount; bondIndex++)
            {
                PlatformStructuralBond bond = _structuralBonds[bondIndex];
                if (bond.Broken || !bond.Foundation || bond.PieceA < 0 || bond.PieceA >= count) continue;
                if (_supportedPieces[bond.PieceA]) continue;
                _supportedPieces[bond.PieceA] = true;
                _supportQueue[write++] = bond.PieceA;
            }

            while (read < write)
            {
                int piece = _supportQueue[read++];
                for (int bondIndex = 0; bondIndex < _structuralBondCount; bondIndex++)
                {
                    PlatformStructuralBond bond = _structuralBonds[bondIndex];
                    if (bond.Broken || bond.Foundation) continue;
                    int neighbour = bond.PieceA == piece ? bond.PieceB :
                        bond.PieceB == piece ? bond.PieceA : -1;
                    if (neighbour < 0 || neighbour >= count || _supportedPieces[neighbour]) continue;
                    _supportedPieces[neighbour] = true;
                    _supportQueue[write++] = neighbour;
                }
            }

            bool released = false;
            for (int pieceIndex = 0; pieceIndex < count; pieceIndex++)
            {
                if (_supportedPieces[pieceIndex] || _pieceReleased[pieceIndex]) continue;
                EarthPlatformPiece piece = _pieces[pieceIndex];
                if (piece == null || !piece.gameObject.activeSelf) continue;
                _pieceReleased[pieceIndex] = true;
                _pieceReleasedAt[pieceIndex] = _fractureElapsed;
                piece.transform.SetParent(transform.parent, true);
                Rigidbody body = piece.Body;
                body.isKinematic = false;
                body.detectCollisions = true;
                body.WakeUp();
                float massShare = Mathf.Clamp01(_pieceVolume[pieceIndex] /
                    Mathf.Max(0.001f, _fracturePlan.SourceVolume));
                body.AddForceAtPosition(
                    (direction + transform.up * 0.12f).normalized * impulse *
                    Mathf.Lerp(0.018f, 0.055f, 1f - massShare),
                    point,
                    ForceMode.Impulse);
                released = true;
            }
            return released;
        }

        private static Mesh BuildPlatformPieceMesh(
            EarthVolumetricFractureCell cell,
            int pieceIndex)
        {
            var vertices = new Vector3[cell.Vertices.Length];
            for (int index = 0; index < vertices.Length; index++)
                vertices[index] = ToVector3(cell.Vertices[index] - cell.Centroid);
            var mesh = new Mesh { name = $"Earth Platform Volume {pieceIndex:00}" };
            mesh.vertices = vertices;
            mesh.triangles = cell.Triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EarthMeshIntegrityGate.ValidateInPlaceOrUseFallback(
                mesh,
                EarthMeshIntegrityPolicy.ConvexCollider,
                mesh.name,
                mesh.bounds);
            return mesh;
        }

        private void BuildPrismMesh(float2[] polygon, float height, float embed)
        {
            int count = polygon.Length;
            var vertices = new Vector3[count * 2];
            for (int index = 0; index < count; index++)
            {
                vertices[index] = new Vector3(polygon[index].x, -embed, polygon[index].y);
                vertices[count + index] = new Vector3(polygon[index].x, height, polygon[index].y);
            }
            var triangles = new int[((count - 2) * 6) + (count * 6)];
            int output = 0;
            for (int index = 1; index < count - 1; index++)
            {
                // Positive X/Z shoelace winding faces local -Y in Unity's axis
                // convention. Keep the lower cap in that order and reverse the top.
                triangles[output++] = 0;
                triangles[output++] = index;
                triangles[output++] = index + 1;
                triangles[output++] = count;
                triangles[output++] = count + index + 1;
                triangles[output++] = count + index;
            }
            for (int index = 0; index < count; index++)
            {
                int next = (index + 1) % count;
                triangles[output++] = index;
                triangles[output++] = count + index;
                triangles[output++] = count + next;
                triangles[output++] = index;
                triangles[output++] = count + next;
                triangles[output++] = next;
            }
            _solidMesh.Clear();
            _solidMesh.vertices = vertices;
            _solidMesh.triangles = triangles;
            _solidMesh.RecalculateNormals();
            _solidMesh.RecalculateBounds();
            EarthMeshIntegrityGate.ValidateInPlaceOrUseFallback(
                _solidMesh,
                EarthMeshIntegrityPolicy.ClosedHero,
                _solidMesh.name,
                _solidMesh.bounds);
        }

        private void UpdateEmergence()
        {
            if (_emergence >= 1f) return;
            float authoredDuration = _profile != null ? _profile.EmergenceSeconds : 0.52f;
            // Acceleration-limited motion responds in the first physics step but
            // remains inside the rider solver's speed and acceleration envelope.
            float travel = Vector3.Distance(_buriedPosition, _surfacePosition);
            float authoredSpeed = travel / Mathf.Max(0.05f, authoredDuration);
            // Leave a real acceleration reserve for the physical rider. The old
            // 0.64 ratio let the platform consume most of the 55 m/s² carry budget;
            // gravity and motor adhesion then left the feet about 15 cm inside the
            // cap. This envelope still reacts on the first tick and reaches full
            // height quickly, but the rider solver can remain ahead of the stone.
            float maximumSpeed = Mathf.Min(CarryMaximumSpeed * 0.25f, authoredSpeed * 1.35f);
            float maximumAcceleration = CarryMaximumAcceleration * 0.14f;
            float distance = _emergence * travel;
            float remaining = Mathf.Max(0f, travel - distance);
            float brakingSpeed = Mathf.Sqrt(2f * Mathf.Max(0.1f, maximumAcceleration) * remaining);
            float targetSpeed = Mathf.Min(Mathf.Max(0.2f, maximumSpeed), brakingSpeed);
            if (_emergenceSpeed <= 0f) _emergenceSpeed = 0.24f;
            _emergenceSpeed = Mathf.MoveTowards(
                _emergenceSpeed,
                targetSpeed,
                Mathf.Max(0.1f, maximumAcceleration) * Time.fixedDeltaTime);
            distance = Mathf.Min(travel, distance + _emergenceSpeed * Time.fixedDeltaTime);
            _emergence = travel > 0.0001f ? Mathf.Clamp01(distance / travel) : 1f;
            float tremorEnvelope = Mathf.Sin(_emergence * Mathf.PI) * (1f - (_emergence * 0.45f));
            float lateral = (Mathf.Sin((Time.fixedTime * 39f) + PlatformId) * 0.030f) * tremorEnvelope;
            float settle = Mathf.Sin(_emergence * Mathf.PI * 2.4f) * 0.012f * tremorEnvelope;
            Vector3 right = _surfaceRotation * Vector3.right;
            Vector3 next = Vector3.LerpUnclamped(_buriedPosition, _surfacePosition, _emergence) +
                           (right * lateral) + (_surfaceUp * settle);
            _surfaceVelocity = (next - _previousFixedPosition) / Mathf.Max(0.0001f, Time.fixedDeltaTime);
            _body.MovePosition(next);
            _body.MoveRotation(_surfaceRotation);
            _previousFixedPosition = next;
            _collider.enabled = true;
            if (_emergence < 1f) return;
            _body.MovePosition(_surfacePosition);
            _body.MoveRotation(_surfaceRotation);
            _surfaceVelocity = (_surfacePosition - _previousFixedPosition) / Mathf.Max(0.0001f, Time.fixedDeltaTime);
            _previousFixedPosition = _surfacePosition;
            _emergenceSpeed = 0f;
            _collider.enabled = true;
            if (float.IsPositiveInfinity(_settledAt)) _settledAt = Time.time;
        }

        private void CarryRiders()
        {
            if (_polygon == null || _polygon.Length < 3 || _emergence <= 0f) return;
            Bounds bounds = _solidMesh.bounds;
            float tolerance = RiderTolerance;
            Vector3 halfExtents = new Vector3(
                bounds.extents.x + tolerance,
                Mathf.Max(2f, Height + 2f),
                bounds.extents.z + tolerance);
            Vector3 center = _surfacePosition + _surfaceUp * (Height + 1f);
            int hitCount = UnityEngine.Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                _riderHits,
                _surfaceRotation,
                ~0,
                QueryTriggerInteraction.Ignore);
            int riderCount = 0;
            for (int index = 0; index < hitCount && riderCount < _riderBodies.Length; index++)
            {
                Collider candidate = _riderHits[index];
                Rigidbody body = candidate != null ? candidate.attachedRigidbody : null;
                if (body == null || body == _body) continue;
                bool duplicate = false;
                for (int existing = 0; existing < riderCount; existing++)
                    if (_riderBodies[existing] == body) duplicate = true;
                if (duplicate) continue;
                Elemental.Runtime.Characters.PlanetMotor motor = body.GetComponent<Elemental.Runtime.Characters.PlanetMotor>();
                if (motor == null) motor = body.GetComponentInParent<Elemental.Runtime.Characters.PlanetMotor>();
                if (motor == null || !motor.AcceptsMovingSupport) continue;
                Vector3 local = Quaternion.Inverse(_surfaceRotation) * (body.worldCenterOfMass - _surfacePosition);
                if (!ContainsExpanded(_polygon, new Vector2(local.x, local.z), tolerance)) continue;
                Vector3 top = SurfaceTopPoint;
                float footClearance = Vector3.Dot(motor.SupportFeetPoint(_surfaceUp) - top, _surfaceUp);
                float contactBand = Mathf.Max(0.35f, tolerance + 0.12f);
                // The platform can finish its own travel a few fixed ticks before
                // an acceleration-limited rider reaches the top. Keep that owned
                // rider in the carry solve until its feet have actually cleared the
                // surface. Dropping support at emergence==1 leaves the character
                // embedded and turns collision restoration into a depenetration pop.
                bool continuingRider = motor.MovingSurfaceId == PlatformId ||
                                       HasTemporarilyIgnoredRiderBody(body);
                if (!IsEmerging && !continuingRider &&
                    (footClearance < -0.14f || footClearance > contactBand)) continue;
                if (!IsEmerging && continuingRider && footClearance > contactBand) continue;
                _riderBodies[riderCount++] = body;
                motor.ApplyMovingSupport(SupportFrame, top, CarryMaximumSpeed, CarryMaximumAcceleration);
                Elemental.Runtime.Characters.ActiveRagdollPuppet puppet = body.GetComponent<Elemental.Runtime.Characters.ActiveRagdollPuppet>();
                if (puppet != null)
                {
                    puppet.SuppressImpacts(Time.fixedDeltaTime * 3f);
                    int selfCount = puppet.CopySelfCollidersNonAlloc(_puppetColliderScratch);
                    for (int selfIndex = 0; selfIndex < selfCount; selfIndex++)
                        IgnoreRiderCollision(_puppetColliderScratch[selfIndex]);
                }
                Elemental.Runtime.Physics.PhysicalImpactTarget impact =
                    body.GetComponent<Elemental.Runtime.Physics.PhysicalImpactTarget>();
                impact?.SuppressImpacts(Time.fixedDeltaTime * 3f);
                IgnoreRiderCollision(candidate);
            }
            for (int index = 0; index < riderCount; index++) _riderBodies[index] = null;
        }

        private void IgnoreRiderCollision(Collider rider)
        {
            if (rider == null || _collider == null ||
                (_emergence >= 1f && Time.time >= _settledAt + SupportGraceSeconds)) return;
            for (int index = 0; index < _temporarilyIgnoredRiderCount; index++)
            {
                if (_temporarilyIgnoredRiders[index] != rider) continue;
                // Unity can discard an ignore pair when a collider transitions
                // disabled -> enabled. Reassert it every carry tick before the
                // physics step that could otherwise depenetrate the rider.
                UnityEngine.Physics.IgnoreCollision(_collider, rider, true);
                return;
            }
            if (_temporarilyIgnoredRiderCount >= _temporarilyIgnoredRiders.Length) return;
            UnityEngine.Physics.IgnoreCollision(_collider, rider, true);
            _temporarilyIgnoredRiders[_temporarilyIgnoredRiderCount++] = rider;
        }

        private bool HasTemporarilyIgnoredRiderBody(Rigidbody body)
        {
            if (body == null) return false;
            for (int index = 0; index < _temporarilyIgnoredRiderCount; index++)
            {
                Collider ignored = _temporarilyIgnoredRiders[index];
                if (ignored != null && ignored.attachedRigidbody == body) return true;
            }
            return false;
        }

        private void RestoreRiderCollisionsWhenSafe()
        {
            if (_temporarilyIgnoredRiderCount == 0 || _emergence < 1f ||
                Time.time < _settledAt + SupportGraceSeconds) return;
            for (int index = 0; index < _temporarilyIgnoredRiderCount; index++)
            {
                Collider rider = _temporarilyIgnoredRiders[index];
                if (rider == null || _collider == null) continue;
                Elemental.Runtime.Characters.PlanetMotor motor =
                    rider.GetComponentInParent<Elemental.Runtime.Characters.PlanetMotor>();
                if (motor == null) continue;
                float footClearance = Vector3.Dot(
                    motor.SupportFeetPoint(_surfaceUp) - SurfaceTopPoint,
                    _surfaceUp);
                // Ragdoll feet are authored to overlap their support plane by a few
                // centimetres. Waiting for every child collider to clear the plane
                // left the ignore pair alive forever and made the settled platform
                // non-solid. The motor capsule is the authoritative rider envelope.
                if (footClearance < -Mathf.Max(0.12f, RiderTolerance * 0.5f)) return;
            }
            for (int index = 0; index < _temporarilyIgnoredRiderCount; index++)
            {
                Collider rider = _temporarilyIgnoredRiders[index];
                if (_collider != null && rider != null) UnityEngine.Physics.IgnoreCollision(_collider, rider, false);
                _temporarilyIgnoredRiders[index] = null;
            }
            _temporarilyIgnoredRiderCount = 0;
        }

        private void OnDisable()
        {
            for (int index = 0; index < _temporarilyIgnoredRiderCount; index++)
            {
                Collider rider = _temporarilyIgnoredRiders[index];
                if (_collider != null && rider != null) UnityEngine.Physics.IgnoreCollision(_collider, rider, false);
                _temporarilyIgnoredRiders[index] = null;
            }
            _temporarilyIgnoredRiderCount = 0;
        }

        private static bool ContainsExpanded(float2[] polygon, Vector2 point, float tolerance)
        {
            if (Contains(polygon, point)) return true;
            float toleranceSq = tolerance * tolerance;
            for (int index = 0; index < polygon.Length; index++)
            {
                Vector2 a = new Vector2(polygon[index].x, polygon[index].y);
                Vector2 b = new Vector2(polygon[(index + 1) % polygon.Length].x, polygon[(index + 1) % polygon.Length].y);
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                if ((point - (a + ab * t)).sqrMagnitude <= toleranceSq) return true;
            }
            return false;
        }

        private void HidePieces()
        {
            if (_pieces == null) return;
            for (int index = 0; index < _pieces.Length; index++)
            {
                EarthPlatformPiece piece = _pieces[index];
                if (piece == null) continue;
                Rigidbody body = piece.Body;
                body.detectCollisions = false;
                body.isKinematic = true;
                piece.gameObject.SetActive(false);
                piece.transform.SetParent(transform, false);
                piece.transform.localScale = Vector3.one;
            }
            ActivePieceCount = 0;
        }

        private static bool Contains(float2[] polygon, Vector2 point)
        {
            bool inside = false;
            for (int current = 0, previous = polygon.Length - 1; current < polygon.Length; previous = current++)
            {
                float2 a = polygon[current];
                float2 b = polygon[previous];
                float denominator = b.y - a.y;
                if (Mathf.Abs(denominator) < 0.00001f) denominator = denominator < 0f ? -0.00001f : 0.00001f;
                bool crosses = (a.y > point.y) != (b.y > point.y) &&
                               point.x < (b.x - a.x) * (point.y - a.y) / denominator + a.x;
                if (crosses) inside = !inside;
            }
            return inside;
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

        private float FractureImpulse => _profile != null ? _profile.FractureImpulse : 1150f;
        private float DebrisRestSeconds => _profile != null ? _profile.DebrisRestSeconds : 2.2f;
        private float DebrisShrinkSeconds => _profile != null ? _profile.DebrisShrinkSeconds : 1.4f;
        private float RiderTolerance => _profile != null ? _profile.RiderTolerance : 0.25f;
        private float CarryMaximumSpeed => _profile != null ? _profile.CarryMaximumSpeed : 8f;
        private float CarryMaximumAcceleration => _profile != null ? _profile.CarryMaximumAcceleration : 55f;
        private float SupportGraceSeconds => _profile != null ? _profile.SupportGraceSeconds : 0.35f;
        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
        private static Vector3 ToVector3(Unity.Mathematics.float3 value) => new Vector3(value.x, value.y, value.z);
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class EarthPlatformPiece : MonoBehaviour, IEarthPhysicalTarget
    {
        public EarthPlatform Owner { get; private set; }
        public int PieceIndex { get; private set; }
        public Rigidbody Body { get; private set; }
        public uint StableEarthId => Owner != null
            ? (Owner.PlatformId * 100u) + (uint)Mathf.Max(0, PieceIndex) + 1u
            : 0u;
        public EarthPhysicalTargetHandle TargetHandle => Owner != null
            ? new EarthPhysicalTargetHandle(StableEarthId, Owner.Generation)
            : default;
        public float EarthMass => Body != null ? Body.mass : 0f;
        public EarthPhysicalTargetKind TargetKind => EarthPhysicalTargetKind.PlatformPiece;
        public bool IsEarthTargetValid => Owner != null && Owner.IsFractured &&
                                          gameObject.activeSelf && Body != null;

        public void Configure(EarthPlatform owner, int pieceIndex)
        {
            Owner = owner;
            PieceIndex = pieceIndex;
            Body = GetComponent<Rigidbody>();
        }

        public void OnEarthMagicGrabbed(EarthMagicGripKind grip)
        {
            Owner?.AcquirePiece(PieceIndex);
            GetComponent<EarthMatterIdentity>()?.TryTransition(EarthMatterPhase.Controlled);
        }
        public void OnEarthMagicReleased(EarthMagicGripKind grip)
        {
            Owner?.ReleasePiece(PieceIndex);
            EarthMatterIdentity identity = GetComponent<EarthMatterIdentity>();
            if (identity != null && identity.TryRead(out EarthMatterRecord record) &&
                record.Phase == EarthMatterPhase.Controlled)
                identity.TryTransition(EarthMatterPhase.FreeDynamic);
        }

        private void OnCollisionEnter(Collision collision) => Owner?.ReportPieceImpact(PieceIndex, collision);
    }
}
