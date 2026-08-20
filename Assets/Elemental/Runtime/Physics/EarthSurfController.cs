using System.Collections.Generic;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Geometry;
using Elemental.Runtime.Matter;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Matter;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    public sealed class EarthSurfController : MonoBehaviour, IMovingSurface
    {
        private readonly RaycastHit[] _impactHits = new RaycastHit[16];
        private readonly RaycastHit[] _supportHits = new RaycastHit[8];
        private readonly Collider[] _casterColliders = new Collider[32];
        [SerializeField] private Rigidbody casterBody;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private Transform planetCenter;
        [SerializeField] private EarthSurfProfile profile;
        [SerializeField] private Material material;

        private EarthSurfSession _session;
        private Rigidbody _boardBody;
        private BoxCollider _boardCollider;
        private MeshFilter _boardFilter;
        private MeshFilter _boardVisualFilter;
        private MeshRenderer _boardRenderer;
        private Transform _boardVisualRoot;
        private TrailRenderer _cutTrack;
        private ParticleSystem _dust;
        private readonly SurfChip[] _chips = new SurfChip[28];
        private Mesh _chipMesh;
        private float _nextChipAt;
        private Vector3 _forward;
        private Vector3 _up;
        private Vector3 _previousPosition;
        private Quaternion _previousRotation = Quaternion.identity;
        private Vector3 _angularVelocity;
        private float _surfaceRadius;
        private uint _generation;
        private Collider _lastImpactCollider;
        private float _lastImpactAt;
        private EarthMatterKernelBehaviour _matterKernel;
        private EarthMatterIdentity _boardMatter;
        private EarthSurfSilhouetteFamily _family = EarthSurfSilhouetteFamily.BrokenWedge;
        private EarthSurfSilhouetteFamily _previousFamily = EarthSurfSilhouetteFamily.BrokenWedge;
        private float _ramp01;
        private float _brake01;
        private float _bankDegrees;
        private float _speedMultiplier = 1f;
        private bool _rampCommitted;
        private bool _ploughImpulseQueued;
        private bool _ploughBraceHeld;
        private Vector3 _riderAnchorLocal;

        private sealed class SurfChip
        {
            public Transform Transform;
            public Vector3 Velocity;
            public float Life;
            public float FullLife;
            public Vector3 FullScale;
        }

        public uint SurfaceId => 0x5F000000u + _generation;
        public Vector3 SurfaceVelocity { get; private set; }
        public Vector3 SurfaceUp => _up;
        public bool IsEmerging => _session != null && _session.Active && !_session.Releasing;
        public float Speed { get; private set; }
        public bool IsActive => _session != null && _session.Active;
        public EarthSurfSilhouetteFamily SilhouetteFamily => _family;
        public float Ramp01 => _ramp01;
        public float Brake01 => _brake01;
        public float RiderDriftMeters { get; private set; }
        public EarthMatterId MatterId => _boardMatter != null ? _boardMatter.MatterId : default;
        public SupportFrameSnapshot SupportFrame => new SupportFrameSnapshot(
            SurfaceId,
            _generation == 0u ? 1u : _generation,
            ToFloat3(_previousPosition),
            ToMathQuaternion(_previousRotation),
            ToFloat3(SurfaceVelocity),
            ToFloat3(_angularVelocity),
            ToFloat3(SurfaceVelocity),
            ToFloat3(_up),
            IsEmerging);
        public MovingSupportSnapshot Snapshot => new MovingSupportSnapshot(SupportFrame);

        public void Configure(
            Rigidbody configuredCaster,
            PlanetMotor configuredMotor,
            Transform configuredPlanetCenter,
            EarthSurfProfile configuredProfile,
            Material configuredMaterial)
        {
            casterBody = configuredCaster;
            motor = configuredMotor;
            planetCenter = configuredPlanetCenter;
            profile = configuredProfile;
            material = configuredMaterial;
            EnsureBoard();
            RebuildBoardMesh();
            RecreateSession();
        }

        public bool Begin(float now, Vector3 forward)
        {
            EnsureBoard();
            if (!_session.Begin(now) || casterBody == null) return false;
            // Sample() marks the session inactive on the completion tick before
            // Cancel() performs presentation cleanup. A previous board can therefore
            // still own a live matter record even though the session is no longer
            // Active. Retire it before reusing the single pooled board.
            _boardMatter?.RetireTransientRepresentation();
            _generation = _generation == uint.MaxValue ? 1u : _generation + 1u;
            _family = EarthSurfControlSolver.SelectFamily(
                SurfaceId ^ unchecked((uint)Mathf.RoundToInt(casterBody.worldCenterOfMass.sqrMagnitude * 31f)),
                _previousFamily);
            _previousFamily = _family;
            _ramp01 = 0f;
            _brake01 = 0f;
            _bankDegrees = 0f;
            _speedMultiplier = 1f;
            _rampCommitted = false;
            _ploughImpulseQueued = false;
            _ploughBraceHeld = false;
            RebuildBoardMesh();
            _up = CurrentUp(casterBody.worldCenterOfMass);
            _forward = Vector3.ProjectOnPlane(forward, _up).normalized;
            if (_forward.sqrMagnitude < 0.5f) _forward = Vector3.ProjectOnPlane(transform.forward, _up).normalized;
            Vector3 foot = casterBody.worldCenterOfMass - _up * 0.92f;
            _surfaceRadius = planetCenter != null
                ? Vector3.Distance(planetCenter.position, foot)
                : Mathf.Max(1f, foot.magnitude);
            Vector3 position = foot - _up * 0.46f;
            _boardBody.position = position;
            _boardBody.rotation = Quaternion.LookRotation(_forward, _up);
            _previousPosition = position;
            _previousRotation = _boardBody.rotation;
            _riderAnchorLocal = Quaternion.Inverse(_boardBody.rotation) *
                                (casterBody.worldCenterOfMass - position);
            RiderDriftMeters = 0f;
            _angularVelocity = Vector3.zero;
            _boardRenderer.enabled = true;
            _boardCollider.enabled = true;
            RegisterBoardMatter(position);
            if (_cutTrack != null)
            {
                _cutTrack.Clear();
                _cutTrack.emitting = true;
            }
            // UnityEngine.Object overloads == null after destruction; null-conditional
            // access does not use that overload and can throw for a stale particle handle.
            if (_dust != null) _dust.Play(true);
            _nextChipAt = Time.fixedUnscaledTime;
            IgnoreCasterCollisions();
            return true;
        }

        public bool HasNearbyStartSurface()
        {
            if (casterBody == null) return false;
            Vector3 up = CurrentUp(casterBody.worldCenterOfMass);
            Vector3 foot = casterBody.worldCenterOfMass - up * 0.88f;
            int count = UnityEngine.Physics.RaycastNonAlloc(
                foot + up * 0.32f,
                -up,
                _supportHits,
                1.25f,
                ~0,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < count; index++)
            {
                Collider collider = _supportHits[index].collider;
                if (collider == null || collider == _boardCollider || IsCasterCollider(collider)) continue;
                if (Vector3.Dot(_supportHits[index].normal, up) < 0.32f) continue;
                return true;
            }
            return false;
        }

        public void Continue(Vector2 move, Vector3 facing)
        {
            Continue(move, facing, 0f, false, false);
        }

        public void Continue(Vector2 move, Vector3 facing, float wheel, bool forcePressed, bool forceHeld)
        {
            if (_session == null || !_session.Active || _session.Releasing) return;
            Vector3 desired = Vector3.ProjectOnPlane(facing, _up).normalized;
            if (desired.sqrMagnitude > 0.5f)
                _forward = Vector3.Slerp(_forward, desired, Mathf.Clamp01(Time.deltaTime * 2.8f));
            EarthSurfControlSample control = EarthSurfControlSolver.Solve(
                move.x,
                Mathf.Sign(wheel),
                _ramp01,
                _brake01,
                Time.unscaledDeltaTime);
            _bankDegrees = control.BankDegrees;
            _ramp01 = control.Ramp01;
            _brake01 = control.Brake01;
            _speedMultiplier = control.SpeedMultiplier;
            float normalizedWheel = Mathf.Abs(wheel) >= 2f ? wheel / 120f : wheel;
            if (normalizedWheel > 0.01f)
            {
                _ramp01 = Mathf.Clamp01(_ramp01 + normalizedWheel * 0.31f);
                _brake01 = Mathf.MoveTowards(_brake01, 0f, Mathf.Abs(normalizedWheel) * 0.42f);
            }
            else if (normalizedWheel < -0.01f)
            {
                _brake01 = Mathf.Clamp01(_brake01 - normalizedWheel * 0.34f);
                _ramp01 = Mathf.MoveTowards(_ramp01, 0f, Mathf.Abs(normalizedWheel) * 0.38f);
                _speedMultiplier = Mathf.Lerp(1f, 0.38f, _brake01);
            }
            _ploughImpulseQueued |= forcePressed;
            _ploughBraceHeld = forceHeld;
            if (_boardVisualRoot != null)
            {
                _boardVisualRoot.localRotation = Quaternion.Euler(-_ramp01 * 8.5f, 0f, -_bankDegrees);
                _boardVisualRoot.localPosition = new Vector3(0f, _ramp01 * 0.05f, 0f);
            }
            if (!_rampCommitted && wheel > 0.01f && _ramp01 >= 0.92f && casterBody != null)
            {
                _rampCommitted = true;
                casterBody.AddForce(_forward * 2.4f + _up * 5.8f, ForceMode.VelocityChange);
                _session.Release(Time.unscaledTime);
            }
        }

        public void Release(float now) => _session?.Release(now);

        public void Cancel()
        {
            _session?.Cancel();
            Speed = 0f;
            SurfaceVelocity = Vector3.zero;
            if (_boardRenderer != null) _boardRenderer.enabled = false;
            if (_boardCollider != null) _boardCollider.enabled = false;
            if (_cutTrack != null) _cutTrack.emitting = false;
            if (_dust != null) _dust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (_boardVisualRoot != null)
            {
                _boardVisualRoot.localPosition = Vector3.zero;
                _boardVisualRoot.localRotation = Quaternion.identity;
            }
            _boardMatter?.RetireTransientRepresentation();
        }

        private void RegisterBoardMatter(Vector3 position)
        {
            _matterKernel ??= EarthMatterKernelBehaviour.FindOrCreate(this);
            float volume = Mathf.Max(0.05f, BoardWidth * BoardLength * NoseHeight * 0.34f);
            Vector3 center = planetCenter != null ? planetCenter.position : Vector3.zero;
            Vector3 local = position - center;
            ushort generation = (ushort)Mathf.Clamp((int)_generation, 1, ushort.MaxValue);
            var source = new EarthSourceProvenance(
                EarthSourceKind.TerrainEdit,
                SurfaceId,
                generation,
                -1,
                unchecked((uint)Time.frameCount),
                new float3(local.x, local.y, local.z),
                volume,
                EarthProvenanceFlags.VolumeReserved);
            _boardMatter = EarthMatterRuntimeBridge.EnsureIdentity(
                _boardBody,
                _matterKernel,
                _boardBody,
                EarthMatterPhase.Forming,
                EarthRepresentationTier.HeroPhysical,
                EarthMaterialKind.Stone,
                EarthShapeSemantic.Wedge,
                volume,
                volume * 170f,
                in source);
            _boardMatter?.TryTransition(EarthMatterPhase.Controlled);
        }

        private void FixedUpdate()
        {
            if (_session == null || !_session.Active || _boardBody == null) return;
            EarthSurfSample sample = _session.Sample(Time.fixedUnscaledTime);
            if (sample.Complete)
            {
                Cancel();
                return;
            }
            Speed = sample.Speed * _speedMultiplier;
            Vector3 center = planetCenter != null ? planetCenter.position : Vector3.zero;
            Vector3 radial = _boardBody.position - center;
            _up = radial.sqrMagnitude > 0.1f ? radial.normalized : _up;
            _forward = Vector3.ProjectOnPlane(_forward, _up).normalized;
            Vector3 tangentStep = _forward * Speed * Time.fixedDeltaTime;
            Vector3 nextRadial = radial + tangentStep;
            float targetRadius = _surfaceRadius - Mathf.Lerp(0.38f, 0.02f, sample.Emergence01);
            Vector3 next = center + nextRadial.normalized * targetRadius;
            Quaternion rotation = Quaternion.LookRotation(_forward, nextRadial.normalized);
            SurfaceVelocity = (next - _previousPosition) / Mathf.Max(0.0001f, Time.fixedDeltaTime);
            _angularVelocity = ToVector3(MovingSurfaceSolver.AngularVelocity(
                ToMathQuaternion(_previousRotation),
                ToMathQuaternion(rotation),
                Time.fixedDeltaTime));
            _boardBody.MovePosition(next);
            _boardBody.MoveRotation(rotation);
            _previousPosition = next;
            _previousRotation = rotation;
            if (!sample.Releasing && motor != null && motor.AcceptsMovingSupport)
            {
                Vector3 top = next + _up * Mathf.Max(0.38f, NoseHeight * 0.5f);
                float carryAcceleration = profile != null ? profile.CarryAcceleration : 95f;
                motor.ApplyMovingSupport(SupportFrame, top, 16f, carryAcceleration);
                Vector3 riderAnchor = next + rotation * _riderAnchorLocal;
                RiderDriftMeters = Vector3.ProjectOnPlane(
                    riderAnchor - casterBody.worldCenterOfMass, _up).magnitude;
                motor.ApplyMovingSupportAnchorCorrection(
                    riderAnchor,
                    38f,
                    carryAcceleration);
            }
            SweepNose(next, rotation);
            UpdatePloughDebris(next);
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
        private static quaternion ToMathQuaternion(Quaternion value) =>
            new quaternion(value.x, value.y, value.z, value.w);

        private void SweepNose(Vector3 position, Quaternion rotation)
        {
            if (Speed < 1f) return;
            int count = UnityEngine.Physics.BoxCastNonAlloc(
                position + _forward * (BoardLength * 0.41f) + _up * (NoseHeight * 0.18f),
                new Vector3(BoardWidth * 0.46f, NoseHeight * 0.52f, 0.28f),
                _forward,
                _impactHits,
                rotation,
                Mathf.Max(0.18f, Speed * Time.fixedDeltaTime + 0.12f),
                ~0,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < count; index++)
            {
                Collider collider = _impactHits[index].collider;
                if (collider == null || collider == _boardCollider || IsCasterCollider(collider)) continue;
                if (collider == _lastImpactCollider && Time.fixedTime - _lastImpactAt < 0.18f) continue;
                float impulse = (profile != null ? profile.NoseImpactImpulse : 2400f) *
                                Mathf.InverseLerp(4f, 13f, Speed) *
                                (_ploughBraceHeld ? 1.65f : 1f) *
                                (_ploughImpulseQueued ? 1.55f : 1f);
                var impact = new EarthStructureImpact(
                    _impactHits[index].point,
                    _forward + _up * 0.08f,
                    Mathf.Max(850f, impulse),
                    EarthStructureImpactKind.Surf,
                    SurfaceId);
                EarthWall wall = collider.GetComponentInParent<EarthWall>();
                EarthPlatform platform = wall == null ? collider.GetComponentInParent<EarthPlatform>() : null;
                bool applied = wall != null
                    ? wall.ApplyEarthImpact(in impact)
                    : platform != null && platform.ApplyEarthImpact(in impact);
                Rigidbody body = collider.attachedRigidbody;
                if (!applied && body != null && !body.isKinematic && body != casterBody)
                    body.AddForceAtPosition(_forward * Mathf.Min(28f, Speed * 2f), _impactHits[index].point, ForceMode.VelocityChange);
                _lastImpactCollider = collider;
                _lastImpactAt = Time.fixedTime;
                _ploughImpulseQueued = false;
                break;
            }
        }

        private void EnsureBoard()
        {
            if (_boardBody != null) return;
            GameObject board = new GameObject("Earth Surf Plough");
            board.transform.SetParent(null, false);
            Mesh mesh = BuildHeroMesh(_family, BoardWidth, BoardLength, NoseHeight, SurfaceId);
            _boardFilter = board.AddComponent<MeshFilter>();
            _boardFilter.sharedMesh = mesh;
            GameObject visual = new GameObject("Hero Visual Shell");
            visual.transform.SetParent(board.transform, false);
            _boardVisualRoot = visual.transform;
            _boardVisualFilter = visual.AddComponent<MeshFilter>();
            _boardVisualFilter.sharedMesh = mesh;
            _boardRenderer = visual.AddComponent<MeshRenderer>();
            _boardRenderer.sharedMaterial = material;
            _boardCollider = board.AddComponent<BoxCollider>();
            _boardCollider.center = new Vector3(0f, 0.08f, -BoardLength * 0.08f);
            _boardCollider.size = new Vector3(BoardWidth * 0.88f, 0.34f, BoardLength * 0.72f);
            _boardBody = board.AddComponent<Rigidbody>();
            _boardBody.useGravity = false;
            _boardBody.isKinematic = true;
            _boardBody.interpolation = RigidbodyInterpolation.Interpolate;
            ConfigurePloughEffects(board);
            _boardRenderer.enabled = false;
            _boardCollider.enabled = false;
        }

        private void ConfigurePloughEffects(GameObject board)
        {
            _cutTrack = board.AddComponent<TrailRenderer>();
            _cutTrack.sharedMaterial = material;
            _cutTrack.time = 0.85f;
            _cutTrack.minVertexDistance = 0.12f;
            _cutTrack.startWidth = BoardWidth * 0.82f;
            _cutTrack.endWidth = 0.34f;
            _cutTrack.startColor = new Color(0.24f, 0.13f, 0.065f, 0.72f);
            _cutTrack.endColor = new Color(0.16f, 0.075f, 0.03f, 0f);
            _cutTrack.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _cutTrack.emitting = false;

            _dust = board.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = _dust.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.32f, 0.68f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.34f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.30f, 0.18f, 0.10f, 0.62f),
                new Color(0.52f, 0.35f, 0.20f, 0.34f));
            main.maxParticles = 128;
            ParticleSystem.EmissionModule emission = _dust.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 29f;
            ParticleSystem.ShapeModule shape = _dust.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(BoardWidth * 0.92f, 0.16f, 0.42f);
            ParticleSystemRenderer dustRenderer = _dust.GetComponent<ParticleSystemRenderer>();
            dustRenderer.sharedMaterial = material;
            dustRenderer.renderMode = ParticleSystemRenderMode.Billboard;

            _chipMesh = EarthWebWaveCellMeshFactory.Create(997);
            for (int index = 0; index < _chips.Length; index++)
            {
                GameObject chipObject = new GameObject($"Surf Cut Chip {index + 1:00}");
                chipObject.AddComponent<MeshFilter>().sharedMesh = _chipMesh;
                chipObject.AddComponent<MeshRenderer>().sharedMaterial = material;
                chipObject.SetActive(false);
                _chips[index] = new SurfChip { Transform = chipObject.transform };
            }
        }

        private void UpdatePloughDebris(Vector3 boardPosition)
        {
            Vector3 center = planetCenter != null ? planetCenter.position : Vector3.zero;
            float delta = Time.fixedDeltaTime;
            for (int index = 0; index < _chips.Length; index++)
            {
                SurfChip chip = _chips[index];
                if (chip?.Transform == null || !chip.Transform.gameObject.activeSelf) continue;
                Vector3 up = chip.Transform.position - center;
                up = up.sqrMagnitude > 0.01f ? up.normalized : _up;
                chip.Velocity -= up * (9.5f * delta);
                chip.Transform.position += chip.Velocity * delta;
                chip.Transform.Rotate(new Vector3(95f, 137f, 73f) * delta, Space.Self);
                chip.Life -= delta;
                float scale01 = Mathf.Clamp01(chip.Life / Mathf.Max(0.01f, chip.FullLife * 0.58f));
                chip.Transform.localScale = chip.FullScale * scale01;
                if (chip.Life <= 0f) chip.Transform.gameObject.SetActive(false);
            }
            if (_session == null || _session.Releasing || Speed < 3.5f ||
                Time.fixedUnscaledTime < _nextChipAt) return;
            _nextChipAt = Time.fixedUnscaledTime + Mathf.Lerp(0.07f, 0.032f, Mathf.InverseLerp(4f, 13f, Speed));
            int emitted = 0;
            for (int index = 0; index < _chips.Length; index++)
            {
                SurfChip chip = _chips[index];
                if (chip == null || chip.Transform == null || chip.Transform.gameObject.activeSelf) continue;
                float side = ((index & 1) == 0 ? -1f : 1f) * Mathf.Lerp(
                    BoardWidth * 0.30f, BoardWidth * 0.58f,
                    Hash01((uint)index + _generation * 17u));
                Vector3 right = Vector3.Cross(_up, _forward).normalized;
                chip.Transform.SetPositionAndRotation(
                    boardPosition + _forward * (BoardLength * 0.45f) + right * side + _up * 0.13f,
                    Quaternion.LookRotation(_forward, _up) * Quaternion.Euler(index * 29f, index * 47f, 0f));
                float size = Mathf.Lerp(0.18f, 0.44f, Hash01((uint)index * 31u + _generation));
                chip.FullScale = new Vector3(size * 1.35f, size * 0.52f, size);
                chip.Transform.localScale = chip.FullScale;
                chip.Velocity = -_forward * Mathf.Lerp(1.1f, 2.8f, Hash01((uint)index + 91u)) +
                                right * Mathf.Sign(side) * Mathf.Lerp(1.6f, 3.2f, Hash01((uint)index + 121u)) +
                                _up * Mathf.Lerp(1.2f, 3.4f, Hash01((uint)index + 173u));
                chip.FullLife = chip.Life = Mathf.Lerp(0.48f, 0.78f, Hash01((uint)index + 251u));
                chip.Transform.gameObject.SetActive(true);
                emitted++;
                if (emitted >= 3) break;
            }
        }

        private void IgnoreCasterCollisions()
        {
            if (casterBody == null || _boardCollider == null) return;
            Collider[] discovered = casterBody.GetComponentsInChildren<Collider>(false);
            int count = Mathf.Min(discovered.Length, _casterColliders.Length);
            for (int index = 0; index < count; index++)
            {
                _casterColliders[index] = discovered[index];
                if (discovered[index] != null)
                    UnityEngine.Physics.IgnoreCollision(_boardCollider, discovered[index], true);
            }
        }

        private bool IsCasterCollider(Collider collider)
        {
            if (collider == null) return false;
            return collider.attachedRigidbody == casterBody || collider.transform.IsChildOf(transform);
        }

        private Vector3 CurrentUp(Vector3 position)
        {
            Vector3 center = planetCenter != null ? planetCenter.position : Vector3.zero;
            Vector3 up = position - center;
            return up.sqrMagnitude > 0.1f ? up.normalized : transform.up;
        }

        private void Awake()
        {
            if (casterBody == null) casterBody = GetComponent<Rigidbody>();
            if (motor == null) motor = GetComponent<PlanetMotor>();
            EnsureBoard();
            RecreateSession();
        }
        private void OnDisable() => Cancel();
        private void OnDestroy()
        {
            if (_boardBody != null) DestroyOwned(_boardBody.gameObject);
            for (int index = 0; index < _chips.Length; index++)
                if (_chips[index]?.Transform != null) DestroyOwned(_chips[index].Transform.gameObject);
            if (_chipMesh != null) DestroyOwned(_chipMesh);
        }
        private void RecreateSession()
        {
            EarthSurfProfileData data = profile != null ? profile.Data : EarthSurfProfileData.Default;
            _session = new EarthSurfSession(in data);
        }

        private void RebuildBoardMesh()
        {
            if (_boardCollider == null || _boardRenderer == null) return;
            Mesh old = _boardVisualFilter != null ? _boardVisualFilter.sharedMesh : null;
            Mesh next = BuildHeroMesh(_family, BoardWidth, BoardLength, NoseHeight, SurfaceId);
            if (_boardFilter != null) _boardFilter.sharedMesh = next;
            if (_boardVisualFilter != null) _boardVisualFilter.sharedMesh = next;
            _boardCollider.center = new Vector3(0f, 0.08f, -BoardLength * 0.08f);
            _boardCollider.size = new Vector3(BoardWidth * 0.88f, 0.34f, BoardLength * 0.72f);
            if (old != null) DestroyOwned(old);
            if (_cutTrack != null) _cutTrack.startWidth = BoardWidth * 0.82f;
        }

        private static void DestroyOwned(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        public static Mesh BuildHeroMesh(
            EarthSurfSilhouetteFamily family,
            float width,
            float length,
            float noseHeight,
            uint seed)
        {
            var mesh = new Mesh { name = $"Earth Surf {family}" };
            float halfWidth = Mathf.Max(0.8f, width * 0.5f);
            float halfLength = Mathf.Max(1.4f, length * 0.5f);
            var vertices = new List<Vector3>(128);
            var triangles = new List<int>(256);
            var uv = new List<Vector2>(128);
            float jitter = Mathf.Lerp(-0.08f, 0.08f, Hash01(seed ^ 0xA341316Cu));
            switch (family)
            {
                case EarthSurfSilhouetteFamily.MantaSlab:
                    AppendBeveledPrism(vertices, triangles, uv, new[]
                    {
                        new Vector2(-halfWidth * 0.38f, -halfLength), new Vector2(halfWidth * 0.38f, -halfLength),
                        new Vector2(halfWidth * 0.62f, -halfLength * 0.30f), new Vector2(halfWidth, halfLength * 0.28f),
                        new Vector2(halfWidth * 0.72f, halfLength), new Vector2(-halfWidth * 0.72f, halfLength),
                        new Vector2(-halfWidth, halfLength * 0.28f), new Vector2(-halfWidth * 0.62f, -halfLength * 0.30f)
                    }, noseHeight, 0.09f, jitter);
                    break;
                case EarthSurfSilhouetteFamily.CrescentPlough:
                    AppendBeveledPrism(vertices, triangles, uv, new[]
                    {
                        new Vector2(-halfWidth * 0.58f, -halfLength), new Vector2(halfWidth * 0.58f, -halfLength),
                        new Vector2(halfWidth, halfLength * 0.42f), new Vector2(halfWidth * 0.62f, halfLength),
                        new Vector2(-halfWidth * 0.62f, halfLength), new Vector2(-halfWidth, halfLength * 0.42f)
                    }, noseHeight * 1.04f, 0.11f, jitter);
                    break;
                case EarthSurfSilhouetteFamily.SplitRail:
                    AppendBeveledPrism(vertices, triangles, uv, Rectangle(
                        -halfWidth * 0.55f, halfWidth * 0.42f, halfLength), noseHeight, 0.08f, jitter);
                    AppendBeveledPrism(vertices, triangles, uv, Rectangle(
                        halfWidth * 0.55f, halfWidth * 0.42f, halfLength), noseHeight * 0.94f, 0.08f, -jitter);
                    AppendBeveledPrism(vertices, triangles, uv, Rectangle(
                        0f, halfWidth * 0.12f, halfLength * 0.76f), noseHeight * 1.12f, 0.12f, 0f);
                    break;
                default:
                    AppendBeveledPrism(vertices, triangles, uv, new[]
                    {
                        new Vector2(-halfWidth * 0.72f, -halfLength), new Vector2(halfWidth * 0.42f, -halfLength * 0.92f),
                        new Vector2(halfWidth, -halfLength * 0.18f), new Vector2(halfWidth * 0.76f, halfLength * 0.82f),
                        new Vector2(halfWidth * 0.12f, halfLength), new Vector2(-halfWidth, halfLength * 0.56f)
                    }, noseHeight * 1.08f, 0.10f, jitter);
                    break;
            }
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0, true);
            mesh.SetUVs(0, uv);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EarthMeshIntegrityGate.ValidateInPlaceOrUseFallback(
                mesh,
                EarthMeshIntegrityPolicy.ClosedHero,
                mesh.name,
                mesh.bounds);
            return mesh;
        }

        private static Vector2[] Rectangle(float centerX, float halfWidth, float halfLength) => new[]
        {
            new Vector2(centerX - halfWidth, -halfLength),
            new Vector2(centerX + halfWidth, -halfLength),
            new Vector2(centerX + halfWidth, halfLength),
            new Vector2(centerX - halfWidth, halfLength)
        };

        private static void AppendBeveledPrism(
            List<Vector3> vertices,
            List<int> triangles,
            List<Vector2> uv,
            Vector2[] footprint,
            float noseHeight,
            float bevel01,
            float heightJitter)
        {
            int count = footprint.Length;
            Vector2 center = Vector2.zero;
            float minimumZ = float.PositiveInfinity;
            float maximumZ = float.NegativeInfinity;
            for (int index = 0; index < count; index++)
            {
                center += footprint[index];
                minimumZ = Mathf.Min(minimumZ, footprint[index].y);
                maximumZ = Mathf.Max(maximumZ, footprint[index].y);
            }
            center /= count;
            float bottom = -0.24f;
            float bevelHeight = 0.11f;
            int bottomCenter = vertices.Count;
            AddVertex(vertices, uv, new Vector3(center.x, bottom, center.y));
            int bottomRing = vertices.Count;
            for (int index = 0; index < count; index++)
                AddVertex(vertices, uv, new Vector3(footprint[index].x, bottom, footprint[index].y));
            int shoulderRing = vertices.Count;
            for (int index = 0; index < count; index++)
            {
                float z01 = Mathf.InverseLerp(minimumZ, maximumZ, footprint[index].y);
                float top = Mathf.Lerp(0.04f, noseHeight, Mathf.Pow(z01, 1.25f)) +
                            heightJitter * Mathf.Sin(index * 2.17f);
                AddVertex(vertices, uv, new Vector3(footprint[index].x, top - bevelHeight, footprint[index].y));
            }
            int topCenter = vertices.Count;
            float centerZ01 = Mathf.InverseLerp(minimumZ, maximumZ, center.y);
            AddVertex(vertices, uv, new Vector3(center.x, Mathf.Lerp(0.04f, noseHeight, centerZ01), center.y));
            int topRing = vertices.Count;
            for (int index = 0; index < count; index++)
            {
                Vector2 inset = Vector2.Lerp(footprint[index], center, Mathf.Clamp01(bevel01));
                float z01 = Mathf.InverseLerp(minimumZ, maximumZ, footprint[index].y);
                float top = Mathf.Lerp(0.04f, noseHeight, Mathf.Pow(z01, 1.25f)) +
                            heightJitter * Mathf.Sin(index * 2.17f);
                AddVertex(vertices, uv, new Vector3(inset.x, top, inset.y));
            }
            for (int index = 0; index < count; index++)
            {
                int next = (index + 1) % count;
                triangles.Add(bottomCenter); triangles.Add(bottomRing + index); triangles.Add(bottomRing + next);
                triangles.Add(topCenter); triangles.Add(topRing + next); triangles.Add(topRing + index);
                AddQuad(triangles, bottomRing + index, shoulderRing + index, shoulderRing + next, bottomRing + next);
                AddQuad(triangles, shoulderRing + index, topRing + index, topRing + next, shoulderRing + next);
            }
        }

        private static void AddVertex(List<Vector3> vertices, List<Vector2> uv, Vector3 value)
        {
            vertices.Add(value);
            uv.Add(new Vector2(value.x * 0.3f + 0.5f, value.z * 0.2f + 0.5f));
        }

        private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(d);
        }

        private float BoardWidth => profile != null ? profile.BoardWidth : 2.35f;
        private float BoardLength => profile != null ? profile.BoardLength : 3.9f;
        private float NoseHeight => profile != null ? profile.NoseHeight : 0.82f;

        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }
    }
}
