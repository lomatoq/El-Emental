using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Structures;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class EarthPlatform : MonoBehaviour, IEarthFractureSource, IMovingSurface
    {
        private const int MaximumPieces = 20;

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

        public uint PlatformId { get; private set; }
        public float Area { get; private set; }
        public float Height { get; private set; }
        public bool IsFractured => _fractured;
        public uint StructureId => PlatformId;
        public uint Generation => _generation;
        public uint SurfaceId => PlatformId;
        public Vector3 SurfaceVelocity => _surfaceVelocity;
        public Vector3 SurfaceUp => _surfaceUp;
        public bool IsEmerging => !_fractured && _emergence < 1f;
        public MovingSupportSnapshot Snapshot => new MovingSupportSnapshot(
            PlatformId,
            ToFloat3(_surfaceVelocity),
            ToFloat3(_surfaceUp),
            IsEmerging);
        public int ActivePieceCount { get; private set; }
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
            Height = Mathf.Max(0.1f, height);
            _embedDepth = Mathf.Max(0.08f, embedDepth);
            _polygon = geometry.Polygon;
            _fractured = false;
            _fractureElapsed = 0f;
            ActivePieceCount = 0;
            _cohesion.ResetCohesion();
            _surfacePosition = ToVector3(geometry.Center);
            _surfaceUp = ToVector3(geometry.Up).normalized;
            _planetCenter = _surfacePosition - (_surfaceUp * geometry.SurfaceRadius);
            _surfaceRotation = Quaternion.LookRotation(ToVector3(geometry.Forward), _surfaceUp);
            _buriedPosition = _surfacePosition -
                              (_surfaceUp * (Height + _embedDepth + 0.12f));
            _emergence = 0f;
            _settledAt = float.PositiveInfinity;
            transform.SetPositionAndRotation(_buriedPosition, _surfaceRotation);
            _previousFixedPosition = _buriedPosition;
            _surfaceVelocity = Vector3.zero;
            transform.localScale = Vector3.one;
            BuildPrismMesh(_polygon, Height, _embedDepth);
            _filter.sharedMesh = _solidMesh;
            _collider.sharedMesh = null;
            _collider.sharedMesh = _solidMesh;
            _collider.convex = false;
            _collider.enabled = false;
            _renderer.enabled = true;
            _body.isKinematic = true;
            HidePieces();
            gameObject.SetActive(true);
        }

        public bool ApplyStructureImpact(Vector3 point, Vector3 direction, float impulse)
        {
            if (_fractured || impulse < FractureImpulse) return false;
            BeginFracture(point, direction, impulse);
            return true;
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
            _renderer.enabled = false;
            _collider.enabled = false;

            Bounds bounds = _solidMesh.bounds;
            int requested = Mathf.Clamp(_profile != null ? _profile.FracturePieceCount : 12, 6, MaximumPieces);
            int columns = Mathf.CeilToInt(Mathf.Sqrt(requested * Mathf.Max(0.5f, bounds.size.x / Mathf.Max(0.2f, bounds.size.z))));
            int rows = Mathf.CeilToInt(requested / (float)Mathf.Max(1, columns));
            Vector3 localImpact = transform.InverseTransformPoint(point);
            Vector3 worldDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.up;
            int output = 0;
            for (int row = 0; row < rows && output < requested; row++)
            for (int column = 0; column < columns && output < requested; column++)
            {
                float jitterX = Mathf.Lerp(-0.16f, 0.16f, Hash01(PlatformId, output * 2));
                float jitterZ = Mathf.Lerp(-0.16f, 0.16f, Hash01(PlatformId, output * 2 + 1));
                float x01 = (column + 0.5f + jitterX) / columns;
                float z01 = (row + 0.5f + jitterZ) / rows;
                Vector2 center = new Vector2(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, x01),
                    Mathf.Lerp(bounds.min.z, bounds.max.z, z01));
                if (!Contains(_polygon, center)) continue;
                EarthPlatformPiece piece = _pieces[output];
                piece.transform.SetParent(transform, false);
                piece.transform.localPosition = new Vector3(
                    center.x, (Height - _embedDepth) * 0.5f, center.y);
                piece.transform.localRotation = Quaternion.Euler(
                    Mathf.Lerp(-4f, 4f, Hash01(PlatformId ^ 0x51u, output)),
                    Mathf.Lerp(-7f, 7f, Hash01(PlatformId ^ 0xA7u, output)),
                    Mathf.Lerp(-4f, 4f, Hash01(PlatformId ^ 0xD3u, output)));
                float width = bounds.size.x / columns * Mathf.Lerp(0.78f, 1.08f, Hash01(PlatformId, output + 31));
                float depth = bounds.size.z / rows * Mathf.Lerp(0.76f, 1.06f, Hash01(PlatformId, output + 59));
                float pieceHeight = (Height + _embedDepth) *
                                    Mathf.Lerp(0.72f, 1.02f, Hash01(PlatformId, output + 83));
                piece.transform.localScale = new Vector3(width, pieceHeight, depth);
                piece.gameObject.SetActive(true);
                piece.transform.SetParent(transform.parent, true);
                _pieceFullScale[output] = piece.transform.localScale;
                _pieceReleasedAt[output] = 0f;
                Rigidbody pieceBody = piece.Body;
                pieceBody.mass = Mathf.Max(1f, Area * Height * 110f / requested);
                pieceBody.isKinematic = false;
                pieceBody.detectCollisions = true;
                pieceBody.linearVelocity = Vector3.zero;
                pieceBody.angularVelocity = Vector3.zero;
                float distance = Vector3.Distance(new Vector3(center.x, 0f, center.y), new Vector3(localImpact.x, 0f, localImpact.z));
                float falloff = Mathf.Clamp01(1f - distance / Mathf.Max(bounds.size.x, bounds.size.z));
                pieceBody.AddForceAtPosition(
                    (worldDirection + (transform.up * 0.22f)).normalized * impulse *
                    Mathf.Lerp(0.025f, 0.12f, falloff),
                    point,
                    ForceMode.Impulse);
                output++;
            }
            ActivePieceCount = output;
            if (output == 0) gameObject.SetActive(false);
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
        }

        private void UpdateEmergence()
        {
            if (_emergence >= 1f) return;
            float duration = _profile != null ? _profile.EmergenceSeconds : 0.52f;
            _emergence = Mathf.Min(1f, _emergence + (Time.fixedDeltaTime / Mathf.Max(0.05f, duration)));
            float eased = 1f - Mathf.Pow(1f - _emergence, 3f);
            float tremorEnvelope = Mathf.Sin(_emergence * Mathf.PI) * (1f - (_emergence * 0.45f));
            float lateral = (Mathf.Sin((Time.fixedTime * 39f) + PlatformId) * 0.035f) * tremorEnvelope;
            float settle = Mathf.Sin(_emergence * Mathf.PI * 2.4f) * 0.045f * tremorEnvelope;
            Vector3 right = _surfaceRotation * Vector3.right;
            Vector3 next = Vector3.LerpUnclamped(_buriedPosition, _surfacePosition, eased) +
                           (right * lateral) + (_surfaceUp * settle);
            _surfaceVelocity = (next - _previousFixedPosition) / Mathf.Max(0.0001f, Time.fixedDeltaTime);
            _body.MovePosition(next);
            _body.MoveRotation(_surfaceRotation);
            _previousFixedPosition = next;
            _collider.enabled = _emergence >= 0.52f;
            if (_emergence < 1f) return;
            _body.MovePosition(_surfacePosition);
            _body.MoveRotation(_surfaceRotation);
            _surfaceVelocity = (_surfacePosition - _previousFixedPosition) / Mathf.Max(0.0001f, Time.fixedDeltaTime);
            _previousFixedPosition = _surfacePosition;
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
                if (motor == null) continue;
                Vector3 local = Quaternion.Inverse(_surfaceRotation) * (body.worldCenterOfMass - _surfacePosition);
                if (!ContainsExpanded(_polygon, new Vector2(local.x, local.z), tolerance)) continue;
                _riderBodies[riderCount++] = body;
                Vector3 top = transform.position + _surfaceUp * Height;
                motor.ApplyMovingSupport(Snapshot, top, CarryMaximumSpeed, CarryMaximumAcceleration);
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
                if (_temporarilyIgnoredRiders[index] == rider) return;
            if (_temporarilyIgnoredRiderCount >= _temporarilyIgnoredRiders.Length) return;
            UnityEngine.Physics.IgnoreCollision(_collider, rider, true);
            _temporarilyIgnoredRiders[_temporarilyIgnoredRiderCount++] = rider;
        }

        private void RestoreRiderCollisionsWhenSafe()
        {
            if (_temporarilyIgnoredRiderCount == 0 || _emergence < 1f ||
                Time.time < _settledAt + SupportGraceSeconds) return;
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

        public void OnEarthMagicGrabbed(EarthMagicGripKind grip) => Owner?.AcquirePiece(PieceIndex);
        public void OnEarthMagicReleased(EarthMagicGripKind grip) => Owner?.ReleasePiece(PieceIndex);
    }
}
