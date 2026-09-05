using System.Collections.Generic;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Geometry;
using Elemental.Runtime.Matter;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Combat;
using Elemental.Simulation.Matter;
using Elemental.Simulation.Structures;
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
        [SerializeField] private EarthEffectsTuningProfile effectsProfile;
        [SerializeField] private EarthStoneBevelProfile stoneBevelProfile;
        [SerializeField] private Material material;
        [SerializeField] private Material dustMaterial;

        private EarthSurfSession _session;
        private Rigidbody _boardBody;
        private BoxCollider _boardCollider;
        private MeshFilter _boardFilter;
        private MeshFilter _boardVisualFilter;
        private MeshRenderer _boardRenderer;
        private Transform _boardVisualRoot;
        private TrailRenderer _cutTrack;
        private ParticleSystem _dust;
        private readonly EarthCosmeticMaterialCache cosmeticMaterials = new();
        private readonly SurfChip[] _chips = new SurfChip[64];
        private readonly SurfCellView[] _cells = new SurfCellView[EarthSurfCellGraph.CellCount];
        private readonly SurfCellView[] _releasedCells = new SurfCellView[48];
        private Mesh _chipMesh;
        private Collider _lastSupportCollider;
        private Vector3 _lastSupportPoint;
        private Vector3 _lastSupportNormal;
        private float _lastSupportDamageAt;
        private bool _hasSupportSample;
        private EarthSurfIntegrityState _integrityState = EarthSurfIntegrityState.Initial;
        private Vector3 _forward;
        private Vector3 _up;
        private Vector3 _previousPosition;
        private Quaternion _previousRotation = Quaternion.identity;
        private Vector3 _angularVelocity;
        private float _surfaceRadius;
        private uint _generation;
        private Collider _lastImpactCollider;
        private float _lastImpactAt;
        private float _lastImpactMissingSince;
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
        private string _lastIntegrityTargetName;
        private float _cutChipDistance;
        private float _wakeDustDistance;
        private float _assemblyStartedAt;
        private bool _visualSessionActive;
        private uint _trailEmissionIndex;
        private Vector3 _pillarJumpScatterVelocity;

        private sealed class SurfChip
        {
            public Transform Transform;
            public Vector3 Velocity;
            public float Life;
            public float FullLife;
            public Vector3 FullScale;
        }

        private sealed class SurfCellView
        {
            public Transform Transform;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public Vector3 AttachedLocalPosition;
            public Quaternion AttachedLocalRotation;
            public Vector3 Velocity;
            public Vector3 AngularVelocity;
            public float Life;
            public bool Detached;
            public Vector3 AssemblyStartLocal;
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
        public float BankDegrees => _bankDegrees;
        public float RiderDriftMeters { get; private set; }
        public float BoardIntegrity => _integrityState.Integrity;
        public Transform BoardTransform => _boardBody != null ? _boardBody.transform : null;
        public ushort AttachedCellMask => _integrityState.AttachedMask;
        public ushort OccupiedSupportCellMask => _integrityState.OccupiedSupportMask;
        public string LastIntegrityTargetName => _lastIntegrityTargetName;
        public int DetachedOuterCellCount { get; private set; }
        public uint PillarJumpBreakSequence { get; private set; }
        public float PillarJumpCharge01 => profile != null ? profile.PillarJumpCharge01 : 0.24f;
        public EarthMatterId MatterId => _boardMatter != null ? _boardMatter.MatterId : default;
        public int CopyReleasedStonePositionsNonAlloc(Vector3[] destination)
        {
            if (destination == null) return 0;
            int copied = 0;
            for (int index = 0; index < _releasedCells.Length && copied < destination.Length; index++)
            {
                SurfCellView cell = _releasedCells[index];
                if (cell?.Transform != null && cell.Transform.gameObject.activeSelf)
                    destination[copied++] = cell.Transform.position;
            }
            return copied;
        }
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
        /// <summary>
        /// Render-clock pose used only by animation contacts. Rigidbody
        /// interpolation is visible through Transform while SupportFrame remains the
        /// canonical fixed-tick snapshot consumed by gameplay physics.
        /// </summary>
        public SupportFrameSnapshot PresentationSupportFrame
        {
            get
            {
                Vector3 position = _boardBody != null ? _boardBody.transform.position : _previousPosition;
                Quaternion rotation = _boardBody != null ? _boardBody.transform.rotation : _previousRotation;
                // Rigidbody interpolation can expose the board's pooled pose for
                // one rendered frame immediately after Begin teleports the
                // kinematic body onto the rider. Capturing a support-local foot
                // anchor against that stale origin made the next frame resolve at
                // roughly twice the planet radius. Use the canonical fixed pose
                // until the interpolated Transform is inside a physically bounded
                // render lead from it.
                float maximumRenderLead = Mathf.Max(
                    0.35f,
                    SurfaceVelocity.magnitude * Time.fixedDeltaTime * 2f + 0.10f);
                if (Vector3.Distance(position, _previousPosition) > maximumRenderLead ||
                    Quaternion.Angle(rotation, _previousRotation) > 35f)
                {
                    position = _previousPosition;
                    rotation = _previousRotation;
                }
                Vector3 up = rotation * Vector3.up;
                return new SupportFrameSnapshot(
                    SurfaceId,
                    _generation == 0u ? 1u : _generation,
                    ToFloat3(position),
                    ToMathQuaternion(rotation),
                    ToFloat3(SurfaceVelocity),
                    ToFloat3(_angularVelocity),
                    ToFloat3(SurfaceVelocity),
                    ToFloat3(up),
                    IsEmerging);
            }
        }
        public MovingSupportSnapshot Snapshot => new MovingSupportSnapshot(SupportFrame);

        public void Configure(
            Rigidbody configuredCaster,
            PlanetMotor configuredMotor,
            Transform configuredPlanetCenter,
            EarthSurfProfile configuredProfile,
            Material configuredMaterial,
            Material configuredDustMaterial = null,
            EarthEffectsTuningProfile configuredEffectsProfile = null)
        {
            casterBody = configuredCaster;
            motor = configuredMotor;
            planetCenter = configuredPlanetCenter;
            profile = configuredProfile;
            effectsProfile = configuredEffectsProfile;
            material = configuredMaterial;
            dustMaterial = configuredDustMaterial;
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
            _integrityState = EarthSurfIntegrityState.Initial;
            DetachedOuterCellCount = 0;
            _lastSupportCollider = null;
            _lastSupportPoint = Vector3.zero;
            _lastSupportNormal = Vector3.zero;
            _lastSupportDamageAt = float.NegativeInfinity;
            _hasSupportSample = false;
            _lastIntegrityTargetName = string.Empty;
            _lastImpactCollider = null;
            _lastImpactAt = float.NegativeInfinity;
            _lastImpactMissingSince = float.PositiveInfinity;
            _cutChipDistance = 0f;
            _wakeDustDistance = 0f;
            _assemblyStartedAt = Time.fixedUnscaledTime;
            _visualSessionActive = true;
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
            _boardRenderer.enabled = false;
            ResetSurfCells(true);
            BeginCellAssembly();
            _boardCollider.enabled = true;
            RegisterBoardMatter(position);
            if (_cutTrack != null)
            {
                _cutTrack.Clear();
                _cutTrack.emitting = profile != null && profile.RibbonEnabled;
            }
            // UnityEngine.Object overloads == null after destruction; null-conditional
            // access does not use that overload and can throw for a stale particle handle.
            if (_dust != null) _dust.Play(true);
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

        public bool BreakForPillarJump(Vector3 launchUp)
        {
            if (!IsActive || !_visualSessionActive) return false;
            Vector3 safeUp = launchUp.sqrMagnitude > 0.5f ? launchUp.normalized : _up;
            Vector3 point = _boardBody != null ? _boardBody.worldCenterOfMass : transform.position;
            float scatterSpeed = profile != null ? profile.PillarJumpScatterSpeed : 3.2f;
            _pillarJumpScatterVelocity = safeUp * scatterSpeed;
            ApplyIntegrityEvent(
                EarthSurfDamageKind.NoseCrash,
                14f,
                0f,
                0f,
                point,
                safeUp - _forward * 0.45f);
            PillarJumpBreakSequence++;
            Cancel();
            _pillarJumpScatterVelocity = Vector3.zero;
            return true;
        }

        public void Cancel()
        {
            if (_visualSessionActive && isActiveAndEnabled)
                ReleaseBoardVisuals();
            _visualSessionActive = false;
            _session?.Cancel();
            Speed = 0f;
            SurfaceVelocity = Vector3.zero;
            if (_boardRenderer != null) _boardRenderer.enabled = false;
            if (_boardCollider != null) _boardCollider.enabled = false;
            SetSurfCellVisibility(false);
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
            UpdatePloughDebris(_previousPosition);
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
            EvaluateSupportTransfer(next);
            SweepNose(next, rotation);
            EmitCutTrailChip(next);
            EmitBoardWake(next);
            UpdateCellAssembly();
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
            if (_lastImpactCollider != null)
            {
                bool stillOverlapping = false;
                for (int index = 0; index < count; index++)
                {
                    if (_impactHits[index].collider != _lastImpactCollider) continue;
                    stillOverlapping = true;
                    break;
                }
                if (stillOverlapping)
                {
                    _lastImpactMissingSince = float.PositiveInfinity;
                }
                else if (float.IsPositiveInfinity(_lastImpactMissingSince))
                {
                    _lastImpactMissingSince = Time.fixedTime;
                }
                else if (Time.fixedTime - _lastImpactMissingSince >= 0.30f)
                {
                    _lastImpactCollider = null;
                    _lastImpactMissingSince = float.PositiveInfinity;
                }
            }
            for (int index = 0; index < count; index++)
            {
                Collider collider = _impactHits[index].collider;
                if (collider == null || collider == _boardCollider || IsCasterCollider(collider)) continue;
                // The forward box overlaps the curved riding surface by design.
                // Treating that same support as a wall crash repeatedly consumed
                // integrity and released an otherwise healthy board.
                if (collider == _lastSupportCollider) continue;
                if (Vector3.Dot(_impactHits[index].normal, _up) > 0.35f) continue;
                // One physical contact episode produces one integrity event. A
                // timer alone repeatedly damaged the board while it remained
                // overlapped with the same bot/wall.
                if (collider == _lastImpactCollider) continue;
                if (Time.fixedTime - _lastImpactAt < 0.75f) continue;
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
                EarthArenaStructure arena = wall == null && platform == null
                    ? collider.GetComponentInParent<EarthArenaStructure>()
                    : null;
                bool applied = wall != null
                    ? wall.ApplySurfLowerBandImpact(in impact, Speed)
                    : platform != null && Speed >= 5f &&
                      platform.ApplySurfBreach(in impact, _boardCollider);
                if (!applied && arena != null)
                    applied = arena.ApplyEarthImpact(in impact);
                bool damagesBoard = applied && (wall != null || platform != null || arena != null);
                EarthSurfDamageKind boardDamageKind = EarthSurfDamageKind.NoseCrash;
                EarthCharacterImpactTarget characterTarget =
                    collider.GetComponentInParent<EarthCharacterImpactTarget>();
                if (!applied && characterTarget != null)
                {
                    float targetMass = characterTarget.Body != null ? characterTarget.Body.mass : 42f;
                    float characterImpulse = Mathf.Max(0.01f, targetMass) * Speed;
                    characterTarget.ApplyImpact(
                        _impactHits[index].point,
                        _forward + _up * 0.08f,
                        characterImpulse,
                        EarthCharacterImpactSourceKind.SurfNose,
                        SurfaceId,
                        Speed,
                        _ramp01);
                    applied = true;
                }
                EarthDestructibleDecorRock decorRock =
                    collider.GetComponentInParent<EarthDestructibleDecorRock>();
                if (!applied && decorRock != null)
                {
                    decorRock.ApplyImpact(
                        _impactHits[index].point,
                        _forward + _up * 0.08f,
                        Mathf.Max(850f, impulse));
                    applied = true;
                    damagesBoard = collider.bounds.extents.magnitude >= BoardWidth * 0.72f;
                    boardDamageKind = damagesBoard
                        ? EarthSurfDamageKind.NoseCrash
                        : EarthSurfDamageKind.Bump;
                }
                Rigidbody body = collider.attachedRigidbody;
                if (!applied && body != null && !body.isKinematic && body != casterBody)
                {
                    body.AddForceAtPosition(_forward * Mathf.Min(28f, Speed * 2f), _impactHits[index].point, ForceMode.VelocityChange);
                    applied = true;
                    damagesBoard = body.mass >= 120f;
                    boardDamageKind = damagesBoard
                        ? EarthSurfDamageKind.NoseCrash
                        : EarthSurfDamageKind.Bump;
                }
                // Static support seams and the protected arena floor can enter the
                // forward box on a curved world. If no gameplay target accepted the
                // impact, this is not a destructive nose event and cannot consume a
                // finite cell.
                if (!applied) continue;
                _lastIntegrityTargetName = collider.name;
                if (!damagesBoard)
                {
                    _lastImpactCollider = collider;
                    _lastImpactAt = Time.fixedTime;
                    _lastImpactMissingSince = float.PositiveInfinity;
                    _ploughImpulseQueued = false;
                    break;
                }
                float localContactX = _boardBody != null
                    ? _boardBody.transform.InverseTransformPoint(_impactHits[index].point).x /
                      Mathf.Max(0.1f, BoardWidth * 0.5f)
                    : 0f;
                ApplyIntegrityEvent(
                    boardDamageKind,
                    Speed,
                    0f,
                    localContactX,
                    _impactHits[index].point,
                    -_forward + _up * 0.35f);
                _lastImpactCollider = collider;
                _lastImpactAt = Time.fixedTime;
                _lastImpactMissingSince = float.PositiveInfinity;
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
            _boardRenderer.enabled = false;
            for (int index = 0; index < _cells.Length; index++)
            {
                EarthSurfCellDefinition definition = EarthSurfCellGraph.GetDefinition(index);
                GameObject cellObject = new GameObject($"Surf Cell {index:00} {definition.Role}");
                cellObject.transform.SetParent(_boardVisualRoot, false);
                var cell = new SurfCellView
                {
                    Transform = cellObject.transform,
                    Filter = cellObject.AddComponent<MeshFilter>(),
                    Renderer = cellObject.AddComponent<MeshRenderer>()
                };
                cell.Renderer.sharedMaterial = material;
                cell.Renderer.enabled = false;
                _cells[index] = cell;
            }
            _boardCollider = board.AddComponent<BoxCollider>();
            _boardCollider.center = new Vector3(0f, 0.08f, -BoardLength * 0.08f);
            _boardCollider.size = new Vector3(BoardWidth * 0.88f, 0.34f, BoardLength * 0.72f);
            _boardBody = board.AddComponent<Rigidbody>();
            _boardBody.useGravity = false;
            _boardBody.isKinematic = true;
            _boardBody.interpolation = RigidbodyInterpolation.Interpolate;
            ConfigurePloughEffects(board);
            _boardCollider.enabled = false;
        }

        private void ConfigurePloughEffects(GameObject board)
        {
            EarthSurfEffectsTuning tuning = effectsProfile != null ? effectsProfile.Surf : null;
            _cutTrack = board.AddComponent<TrailRenderer>();
            _cutTrack.sharedMaterial = effectsProfile != null
                ? effectsProfile.Materials.SurfTrail
                : material;
            _cutTrack.time = tuning != null ? tuning.TrailLifetime : 0.85f;
            _cutTrack.minVertexDistance = 0.12f;
            _cutTrack.startWidth = BoardWidth * 0.82f;
            _cutTrack.endWidth = tuning != null ? tuning.TrailEndWidth : 0.34f;
            _cutTrack.startColor = tuning != null
                ? tuning.TrailStartColor
                : new Color(0.24f, 0.13f, 0.065f, 0.72f);
            _cutTrack.endColor = tuning != null
                ? tuning.TrailEndColor
                : new Color(0.16f, 0.075f, 0.03f, 0f);
            _cutTrack.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _cutTrack.emitting = false;

            _dust = board.AddComponent<ParticleSystem>();
            if (tuning != null)
                EarthParticleSystemTuningApplier.ApplyDust(
                    _dust, tuning.Dust, effectsProfile.Materials.SurfDust);
            ParticleSystem.MainModule main = _dust.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            if (tuning == null)
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.32f, 0.68f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 2.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.34f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.30f, 0.18f, 0.10f, 0.62f),
                    new Color(0.52f, 0.35f, 0.20f, 0.34f));
                main.maxParticles = 512;
            }
            ParticleSystem.EmissionModule emission = _dust.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = tuning != null ? tuning.RateOverDistance : 29f;
            ParticleSystem.ShapeModule shape = _dust.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(BoardWidth * 0.92f, 0.16f, 0.42f);
            ParticleSystemRenderer dustRenderer = _dust.GetComponent<ParticleSystemRenderer>();
            dustRenderer.sharedMaterial = effectsProfile != null
                ? effectsProfile.Materials.SurfDust
                : dustMaterial != null ? dustMaterial : material;
            dustRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dustRenderer.receiveShadows = false;
            dustRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            EarthEffectRenderOrder.ApplyDustRenderer(dustRenderer);

            _chipMesh = CreateOwnedChamferedRenderMesh(EarthWebWaveCellMeshFactory.Create(997), stoneBevelProfile);
            for (int index = 0; index < _chips.Length; index++)
            {
                GameObject chipObject = new GameObject($"Surf Cut Chip {index + 1:00}");
                chipObject.AddComponent<MeshFilter>().sharedMesh = _chipMesh;
                EarthEffectRenderOrder.ApplyCosmeticRenderer(chipObject.AddComponent<MeshRenderer>(),
                    cosmeticMaterials.Get(material));
                chipObject.SetActive(false);
                _chips[index] = new SurfChip { Transform = chipObject.transform };
            }
            for (int index = 0; index < _releasedCells.Length; index++)
            {
                GameObject debris = new GameObject($"Surf Released Stone {index:00}");
                var view = new SurfCellView { Transform = debris.transform,
                    Filter = debris.AddComponent<MeshFilter>(), Renderer = debris.AddComponent<MeshRenderer>() };
                view.Renderer.sharedMaterial = material;
                debris.SetActive(false);
                _releasedCells[index] = view;
            }
        }

        private void UpdatePloughDebris(Vector3 boardPosition)
        {
            Vector3 center = planetCenter != null ? planetCenter.position : Vector3.zero;
            float delta = Time.fixedDeltaTime;
            for (int index = 0; index < _releasedCells.Length; index++)
            {
                SurfCellView cell = _releasedCells[index];
                if (cell?.Transform == null || !cell.Transform.gameObject.activeSelf) continue;
                Vector3 localUp = cell.Transform.position - center;
                localUp = localUp.sqrMagnitude > 0.01f ? localUp.normalized : _up;
                cell.Velocity -= localUp * (11.5f * delta);
                cell.Transform.position += cell.Velocity * delta;
                cell.Transform.Rotate(cell.AngularVelocity * delta, Space.Self);
                cell.Life -= delta;
                cell.Transform.localScale = Vector3.one * Mathf.Clamp01(cell.Life / 0.25f);
                if (cell.Life > 0f) continue;
                cell.Transform.gameObject.SetActive(false);
                if (cell.Mesh != null) DestroyOwned(cell.Mesh);
                cell.Mesh = null;
                cell.Filter.sharedMesh = null;
            }
            for (int index = 0; index < _cells.Length; index++)
            {
                SurfCellView cell = _cells[index];
                if (cell?.Transform == null || !cell.Detached || !cell.Transform.gameObject.activeSelf) continue;
                Vector3 cellUp = cell.Transform.position - center;
                cellUp = cellUp.sqrMagnitude > 0.01f ? cellUp.normalized : _up;
                cell.Velocity -= cellUp * (11.5f * delta);
                cell.Transform.position += cell.Velocity * delta;
                cell.Transform.Rotate(cell.AngularVelocity * delta, Space.Self);
                cell.Life -= delta;
                if (cell.Life > 0f) continue;
                cell.Transform.gameObject.SetActive(false);
            }
            for (int index = 0; index < _chips.Length; index++)
            {
                SurfChip chip = _chips[index];
                if (chip?.Transform == null || !chip.Transform.gameObject.activeSelf) continue;
                Vector3 up = chip.Transform.position - center;
                up = up.sqrMagnitude > 0.01f ? up.normalized : _up;
                chip.Velocity -= up * (9.5f * delta);
                chip.Transform.position += chip.Velocity * delta;
                uint spinSeed = (uint)index * 193u + _generation * 97u;
                Vector3 spin = new Vector3(Mathf.Lerp(-320f, 320f, Hash01(spinSeed)),
                    Mathf.Lerp(-420f, 420f, Hash01(spinSeed + 17u)),
                    Mathf.Lerp(-280f, 280f, Hash01(spinSeed + 31u)));
                chip.Transform.Rotate(spin * delta, Space.Self);
                chip.Life -= delta;
                float scale01 = Mathf.Clamp01(chip.Life / Mathf.Max(0.01f, chip.FullLife * 0.58f));
                chip.Transform.localScale = chip.FullScale * scale01;
                if (chip.Life <= 0f) chip.Transform.gameObject.SetActive(false);
            }
        }

        private void EvaluateSupportTransfer(Vector3 boardPosition)
        {
            int count = UnityEngine.Physics.RaycastNonAlloc(
                boardPosition + _up * 1.15f,
                -_up,
                _supportHits,
                2.6f,
                ~0,
                QueryTriggerInteraction.Ignore);
            RaycastHit best = default;
            bool found = false;
            float bestDistance = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = _supportHits[index];
                Collider collider = hit.collider;
                if (collider == null || collider == _boardCollider || IsCasterCollider(collider)) continue;
                if (Vector3.Dot(hit.normal, _up) < 0.30f || hit.distance >= bestDistance) continue;
                best = hit;
                bestDistance = hit.distance;
                found = true;
            }
            if (!found)
            {
                _hasSupportSample = false;
                _lastSupportCollider = null;
                return;
            }

            if (_hasSupportSample &&
                Time.fixedUnscaledTime - _lastSupportDamageAt >= 0.12f)
            {
                bool supportSwap = best.collider != _lastSupportCollider;
                float heightStep = Mathf.Abs(Vector3.Dot(
                    best.point - _lastSupportPoint,
                    _up));
                float normalAngle = Vector3.Angle(_lastSupportNormal, best.normal);
                float normalSpeed = heightStep / Mathf.Max(0.0001f, Time.fixedDeltaTime);
                if (supportSwap || heightStep > 0.12f || normalAngle > 12f)
                {
                    EarthSurfDamageKind kind = supportSwap
                        ? EarthSurfDamageKind.SupportTransfer
                        : EarthSurfDamageKind.Bump;
                    Vector3 localContact = _boardBody != null
                        ? _boardBody.transform.InverseTransformPoint(best.point)
                        : Vector3.zero;
                    Vector3 right = Vector3.Cross(_up, _forward).normalized;
                    if (ApplyIntegrityEvent(
                            kind,
                            normalSpeed,
                            normalAngle,
                            localContact.x / Mathf.Max(0.1f, BoardWidth * 0.5f),
                            best.point + right * Mathf.Sign(localContact.x == 0f ? 1f : localContact.x) *
                            BoardWidth * 0.42f,
                            -_forward + _up * 0.55f + right * Mathf.Sign(localContact.x) * 0.35f))
                    {
                        _lastSupportDamageAt = Time.fixedUnscaledTime;
                    }
                }
            }

            _hasSupportSample = true;
            _lastSupportCollider = best.collider;
            _lastSupportPoint = best.point;
            _lastSupportNormal = best.normal;
        }

        public bool ApplyIntegrityEvent(
            EarthSurfDamageKind kind,
            float relativeNormalSpeed,
            float normalDiscontinuityDegrees,
            float contactLocalX,
            Vector3 point,
            Vector3 ejectDirection)
        {
            var damageEvent = new EarthSurfDamageEvent(
                kind,
                relativeNormalSpeed,
                normalDiscontinuityDegrees,
                contactLocalX,
                SurfaceId);
            EarthSurfIntegrityDecision decision = EarthSurfIntegritySolver.Resolve(
                in _integrityState,
                in damageEvent);
            if (decision.Damage <= 0f) return false;
            ApplyIntegrityDecision(in decision, point, ejectDirection);
            return true;
        }

        private void ApplyIntegrityDecision(
            in EarthSurfIntegrityDecision decision,
            Vector3 point,
            Vector3 ejectDirection)
        {
            if (decision.Damage <= 0f) return;
            _integrityState = decision.State;
            if (decision.DetachedOuterCells > 0)
            {
                DetachedOuterCellCount += decision.DetachedOuterCells;
                DetachSurfCells(decision.DetachedCellMask, point, ejectDirection, decision.Damage);
                EmitSurfChips(point, ejectDirection, decision.DetachedOuterCells, decision.Damage);
            }
            if (decision.Collapse && _session != null && !_session.Releasing)
                _session.Release(Time.fixedUnscaledTime);
        }

        private void RebuildSurfCells()
        {
            if (_boardVisualRoot == null) return;
            for (int index = 0; index < _cells.Length; index++)
            {
                SurfCellView cell = _cells[index];
                if (cell?.Filter == null) continue;
                EarthSurfCellDefinition definition = EarthSurfCellGraph.GetDefinition(index);
                Mesh old = cell.Mesh;
                bool core = (EarthSurfCellGraph.SupportCoreMask & (1 << index)) != 0;
                float width = definition.Size01.x * BoardWidth * (core ? 1.10f : 1.28f);
                float length = definition.Size01.y * BoardLength * (core ? 1.08f : 1.22f);
                float x = definition.Center01.x * BoardWidth;
                float z = definition.Center01.y * BoardLength;
                if (_family == EarthSurfSilhouetteFamily.CrescentPlough &&
                    (definition.Role == EarthSurfCellRole.Nose || definition.Role == EarthSurfCellRole.OuterRail))
                    x *= 1.08f;
                else if (_family == EarthSurfSilhouetteFamily.SplitRail && definition.Role == EarthSurfCellRole.FootBridge)
                    width *= 0.72f;
                else if (_family == EarthSurfSilhouetteFamily.BrokenWedge)
                {
                    x += Mathf.Lerp(-0.06f, 0.06f, Hash01((uint)index + SurfaceId * 7u));
                    z += Mathf.Lerp(-0.05f, 0.05f, Hash01((uint)index + SurfaceId * 13u));
                }

                uint seed = SurfaceId ^ (uint)(index * 0x45D9F3B);
                if (!core)
                {
                    x += Mathf.Lerp(-0.11f, 0.11f, Hash01(seed + 11u));
                    z += Mathf.Lerp(-0.16f, 0.16f, Hash01(seed + 17u));
                }
                float thickness = core ? 0.30f : Mathf.Lerp(0.23f, 0.52f, Hash01(seed + 29u));
                cell.Mesh = BuildSemanticCellMesh(
                    width,
                    length,
                    thickness,
                    seed,
                    stoneBevelProfile);
                cell.Filter.sharedMesh = cell.Mesh;
                cell.AttachedLocalPosition = new Vector3(x, core ? 0.03f : Mathf.Lerp(-0.04f, 0.10f, Hash01(seed + 31u)), z);
                cell.AttachedLocalRotation = Quaternion.Euler(
                    Mathf.Lerp(-7f, 7f, Hash01(seed + 41u)) * (core ? 0.20f : 1f),
                    Mathf.Lerp(-26f, 26f, Hash01(seed + 43u)),
                    Mathf.Lerp(-9f, 9f, Hash01(seed + 47u)) * (core ? 0.20f : 1f));
                if (cell.Renderer != null) cell.Renderer.sharedMaterial = material;
                if (old != null) DestroyOwned(old);
            }
            ResetSurfCells(IsActive);
        }

        private float BoardTopHeight(float localZ)
        {
            float z01 = Mathf.InverseLerp(-BoardLength * 0.5f, BoardLength * 0.5f, localZ);
            return Mathf.Lerp(0.04f, NoseHeight, Mathf.Pow(z01, 1.25f));
        }

        private void ResetSurfCells(bool visible)
        {
            for (int index = 0; index < _cells.Length; index++)
            {
                SurfCellView cell = _cells[index];
                if (cell?.Transform == null) continue;
                cell.Transform.SetParent(_boardVisualRoot, false);
                cell.Transform.localPosition = cell.AttachedLocalPosition;
                cell.Transform.localRotation = cell.AttachedLocalRotation;
                cell.Transform.localScale = Vector3.one;
                cell.Detached = false;
                cell.Velocity = Vector3.zero;
                cell.AngularVelocity = Vector3.zero;
                cell.Life = 0f;
                cell.Transform.gameObject.SetActive(visible);
                if (cell.Renderer != null) cell.Renderer.enabled = visible;
            }
        }

        private void BeginCellAssembly()
        {
            for (int index = 0; index < _cells.Length; index++)
            {
                SurfCellView cell = _cells[index];
                Vector3 side = new Vector3(cell.AttachedLocalPosition.x, 0f, cell.AttachedLocalPosition.z);
                side = side.sqrMagnitude > 0.01f ? side.normalized : Vector3.forward;
                Vector3 localStart = cell.AttachedLocalPosition + side * Mathf.Lerp(0.45f, 0.95f,
                    Hash01((uint)index + SurfaceId)) - Vector3.up * 0.55f;
                Vector3 worldStart = _boardVisualRoot.TransformPoint(localStart);
                int count = UnityEngine.Physics.RaycastNonAlloc(worldStart + _up * 1.5f, -_up,
                    _supportHits, 3f, ~0, QueryTriggerInteraction.Ignore);
                float nearest = float.PositiveInfinity;
                for (int hitIndex = 0; hitIndex < count; hitIndex++)
                {
                    RaycastHit hit = _supportHits[hitIndex];
                    if (hit.collider == _boardCollider || IsCasterCollider(hit.collider) || hit.distance >= nearest) continue;
                    nearest = hit.distance;
                    worldStart = hit.point - _up * 0.12f;
                }
                cell.AssemblyStartLocal = _boardVisualRoot.InverseTransformPoint(worldStart);
                cell.Transform.localPosition = cell.AssemblyStartLocal;
                EmitLooseDust(worldStart, _up * 1.4f, 5);
            }
        }

        private void UpdateCellAssembly()
        {
            float duration = profile != null ? profile.AssemblySeconds : 0.30f;
            float elapsed = Time.fixedUnscaledTime - _assemblyStartedAt;
            if (elapsed > duration + 0.08f) return;
            for (int index = 0; index < _cells.Length; index++)
            {
                SurfCellView cell = _cells[index];
                if (cell.Detached) continue;
                float delay = (EarthSurfCellGraph.SupportCoreMask & (1 << index)) != 0 ? 0f
                    : Hash01((uint)index + SurfaceId) * 0.07f;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - delay) / Mathf.Max(0.05f, duration - delay)));
                cell.Transform.localPosition = Vector3.Lerp(cell.AssemblyStartLocal, cell.AttachedLocalPosition, t) +
                    Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.16f);
            }
        }

        private void ReleaseBoardVisuals()
        {
            for (int index = 0; index < _cells.Length; index++)
            {
                SurfCellView cell = _cells[index];
                if (cell?.Mesh == null || !cell.Transform.gameObject.activeSelf) continue;
                SurfCellView released = null;
                for (int slot = 0; slot < _releasedCells.Length; slot++)
                    if (!_releasedCells[slot].Transform.gameObject.activeSelf) { released = _releasedCells[slot]; break; }
                if (released == null) continue; // Bounded cosmetic pool; never reclaim a visible generation.
                if (released.Mesh != null) DestroyOwned(released.Mesh);
                released.Mesh = cell.Mesh;
                released.Filter.sharedMesh = cell.Mesh;
                released.Renderer.sharedMaterial = material;
                cell.Mesh = null;
                cell.Filter.sharedMesh = null;
                released.Transform.SetPositionAndRotation(cell.Transform.position, cell.Transform.rotation);
                released.Transform.localScale = Vector3.one;
                released.Velocity = (cell.Detached ? cell.Velocity : SurfaceVelocity * 0.65f +
                    (_up + (cell.Transform.position - _previousPosition).normalized) * 1.8f) +
                    _pillarJumpScatterVelocity;
                released.AngularVelocity = new Vector3(83f + index * 13f, -97f + index * 17f, 67f);
                released.Life = profile != null ? profile.ReleaseDebrisSeconds : 1f;
                released.Transform.gameObject.SetActive(true);
                EmitLooseDust(cell.Transform.position, _up * 1.2f, 7);
            }
            int remaining = 12;
            Vector3 right = Vector3.Cross(_up, _forward).normalized;
            for (int index = 0; index < _chips.Length && remaining > 0; index++)
            {
                SurfChip chip = _chips[index];
                if (chip?.Transform == null || chip.Transform.gameObject.activeSelf) continue;
                uint seed = (uint)index + SurfaceId * 71u;
                float angle = Hash01(seed) * Mathf.PI * 2f;
                Vector3 radial = right * Mathf.Cos(angle) + _forward * Mathf.Sin(angle);
                chip.Transform.SetPositionAndRotation(_previousPosition + radial * 0.7f + _up * 0.15f,
                    Quaternion.LookRotation(radial, _up));
                float size = Mathf.Lerp(0.09f, 0.19f, Hash01(seed + 13u));
                chip.FullScale = new Vector3(size * 1.2f, size * 0.7f, size);
                chip.Transform.localScale = chip.FullScale;
                chip.Velocity = SurfaceVelocity * 0.3f + radial * Mathf.Lerp(1.5f, 3.2f, Hash01(seed + 17u)) + _up * 2.1f;
                chip.FullLife = chip.Life = 0.85f;
                chip.Transform.gameObject.SetActive(true);
                remaining--;
            }
        }

        private void EmitLooseDust(Vector3 point, Vector3 velocity, int count)
        {
            if (_dust == null) return;
            var emit = new ParticleSystem.EmitParams { position = point, velocity = velocity };
            _dust.Emit(emit, count);
        }

        private void SetSurfCellVisibility(bool visible)
        {
            for (int index = 0; index < _cells.Length; index++)
            {
                SurfCellView cell = _cells[index];
                if (cell?.Transform == null) continue;
                cell.Transform.gameObject.SetActive(visible);
                if (cell.Renderer != null) cell.Renderer.enabled = visible;
            }
        }

        private void DetachSurfCells(
            ushort detachedMask,
            Vector3 impactPoint,
            Vector3 ejectDirection,
            float damage)
        {
            Vector3 safeEject = ejectDirection.sqrMagnitude > 0.001f
                ? ejectDirection.normalized
                : -_forward + _up * 0.4f;
            float damage01 = Mathf.InverseLerp(1f, 50f, damage);
            for (int index = 0; index < _cells.Length; index++)
            {
                ushort bit = (ushort)(1 << index);
                if ((detachedMask & bit) == 0 || (EarthSurfCellGraph.SupportCoreMask & bit) != 0) continue;
                SurfCellView cell = _cells[index];
                if (cell?.Transform == null || cell.Detached) continue;
                Vector3 away = Vector3.ProjectOnPlane(cell.Transform.position - impactPoint, _up);
                away = away.sqrMagnitude > 0.001f ? away.normalized : safeEject;
                cell.Transform.SetParent(null, true);
                cell.Detached = true;
                cell.Velocity = safeEject * Mathf.Lerp(2.2f, 5.4f, damage01) +
                                away * Mathf.Lerp(1.1f, 3.4f, Hash01((uint)index + SurfaceId * 31u)) +
                                _up * Mathf.Lerp(1.5f, 3.8f, Hash01((uint)index + SurfaceId * 37u));
                cell.AngularVelocity = new Vector3(
                    Mathf.Lerp(110f, 260f, Hash01((uint)index + SurfaceId * 41u)),
                    Mathf.Lerp(90f, 240f, Hash01((uint)index + SurfaceId * 43u)),
                    Mathf.Lerp(120f, 280f, Hash01((uint)index + SurfaceId * 47u)));
                cell.Life = Mathf.Lerp(0.68f, 0.96f, Hash01((uint)index + SurfaceId * 53u));
                cell.Transform.gameObject.SetActive(true);
                if (cell.Renderer != null) cell.Renderer.enabled = true;
            }
        }

        private void EmitSurfChips(
            Vector3 origin,
            Vector3 ejectDirection,
            int requested,
            float damage)
        {
            Vector3 safeEject = ejectDirection.sqrMagnitude > 0.001f
                ? ejectDirection.normalized
                : -_forward + _up * 0.4f;
            Vector3 right = Vector3.Cross(_up, _forward).normalized;
            int emitted = 0;
            for (int index = 0; index < _chips.Length; index++)
            {
                SurfChip chip = _chips[index];
                if (chip == null || chip.Transform == null || chip.Transform.gameObject.activeSelf) continue;
                float side = ((index & 1) == 0 ? -1f : 1f) * Mathf.Lerp(
                    BoardWidth * 0.30f, BoardWidth * 0.58f,
                    Hash01((uint)index + _generation * 17u));
                chip.Transform.SetPositionAndRotation(
                    origin + right * side * 0.42f + _up * 0.13f,
                    Quaternion.LookRotation(_forward, _up) * Quaternion.Euler(index * 29f, index * 47f, 0f));
                float damage01 = Mathf.InverseLerp(1f, 50f, damage);
                float size = Mathf.Lerp(0.18f, 0.52f, damage01) *
                             Mathf.Lerp(0.82f, 1.18f, Hash01((uint)index * 31u + _generation));
                chip.FullScale = new Vector3(size * 1.35f, size * 0.52f, size);
                chip.Transform.localScale = chip.FullScale;
                chip.Velocity = safeEject * Mathf.Lerp(1.8f, 4.2f, damage01) +
                                right * Mathf.Sign(side) * Mathf.Lerp(1.6f, 3.2f, Hash01((uint)index + 121u)) +
                                _up * Mathf.Lerp(1.2f, 3.4f, Hash01((uint)index + 173u));
                chip.FullLife = chip.Life = Mathf.Lerp(0.48f, 0.78f, Hash01((uint)index + 251u));
                chip.Transform.gameObject.SetActive(true);
                emitted++;
                if (emitted >= Mathf.Clamp(requested, 1, 3)) break;
            }

            if (_dust != null)
            {
                float damage01 = Mathf.InverseLerp(1f, 50f, damage);
                EarthSurfEffectsTuning tuning = effectsProfile != null ? effectsProfile.Surf : null;
                var coarse = new ParticleSystem.EmitParams
                {
                    position = origin,
                    velocity = safeEject * Mathf.Lerp(2.2f, 4.8f, damage01) + _up * 1.2f,
                    startLifetime = Mathf.Lerp(0.45f, 0.78f, damage01),
                    startSize = Mathf.Lerp(0.18f, 0.34f, damage01),
                    startColor = new Color(1f, 1f, 1f, 0.78f)
                };
                Vector2 coarseRange = tuning != null ? tuning.CoarseCount : new Vector2(4f, 14f);
                _dust.Emit(coarse, Mathf.RoundToInt(Mathf.Lerp(coarseRange.x, coarseRange.y, damage01)));
                var body = coarse;
                body.velocity = safeEject * Mathf.Lerp(1.2f, 3.2f, damage01) + _up * 0.72f;
                body.startLifetime = Mathf.Lerp(0.72f, 1.28f, damage01);
                body.startSize = Mathf.Lerp(0.24f, 0.58f, damage01);
                body.startColor = new Color(1f, 1f, 1f, 0.62f);
                Vector2 bodyRange = tuning != null ? tuning.BodyCount : new Vector2(14f, 44f);
                _dust.Emit(body, Mathf.RoundToInt(Mathf.Lerp(bodyRange.x, bodyRange.y, damage01)));
                var veil = body;
                veil.velocity = safeEject * Mathf.Lerp(0.55f, 1.65f, damage01) + _up * 0.38f;
                veil.startLifetime = Mathf.Lerp(1.05f, 1.65f, damage01);
                veil.startSize = Mathf.Lerp(0.38f, 0.86f, damage01);
                veil.startColor = new Color(1f, 1f, 1f, 0.34f);
                Vector2 veilRange = tuning != null ? tuning.VeilCount : new Vector2(18f, 54f);
                _dust.Emit(veil, Mathf.RoundToInt(Mathf.Lerp(veilRange.x, veilRange.y, damage01)));
            }
        }

        private void EmitCutTrailChip(Vector3 boardPosition)
        {
            _cutChipDistance += Speed * Time.fixedDeltaTime * (profile != null ? profile.TrailChipsPerMeter : 5f) *
                (profile != null ? profile.WakeChipMultiplier : 2.4f);
            int requested = Mathf.Min(6, Mathf.FloorToInt(_cutChipDistance));
            _cutChipDistance -= requested;
            Vector3 right = Vector3.Cross(_up, _forward).normalized;
            for (int index = 0; index < _chips.Length; index++)
            {
                if (requested <= 0) break;
                SurfChip chip = _chips[index];
                if (chip == null || chip.Transform == null || chip.Transform.gameObject.activeSelf) continue;
                int lane = (int)(_trailEmissionIndex++ % 3u);
                float side = lane == 0 ? -1f : lane == 1 ? 1f : Mathf.Lerp(-1f, 1f, Hash01((uint)index + _generation * 61u));
                bool nose = lane != 2;
                chip.Transform.SetPositionAndRotation(
                    boardPosition + _forward * (BoardLength * (nose ? 0.43f : -0.30f)) +
                    right * side * BoardWidth * 0.38f + _up * 0.04f,
                    Quaternion.LookRotation(_forward, _up) * Quaternion.Euler(index * 23f, index * 41f, 0f));
                float size = Mathf.Lerp(0.11f, 0.23f, Hash01((uint)index + _generation * 67u));
                chip.FullScale = new Vector3(size * 1.25f, size * 0.48f, size);
                chip.Transform.localScale = chip.FullScale;
                float angle = (profile != null ? profile.NoseSpreadDegrees : 35f) * Mathf.Deg2Rad;
                Vector3 backwardsV = -_forward * Mathf.Cos(angle) + right * side * Mathf.Sin(angle);
                chip.Velocity = backwardsV * Mathf.Lerp(2f, 5f, Mathf.Clamp01(Speed / 13f)) +
                                _up * Mathf.Lerp(1f, 2.5f, Hash01((uint)index + 83u));
                chip.FullLife = chip.Life = Mathf.Lerp(0.46f, 0.72f, Hash01((uint)index + 89u));
                chip.Transform.gameObject.SetActive(true);
                EmitLooseDust(chip.Transform.position, backwardsV * 2.4f + _up * 0.8f, nose ? 5 : 3);
                requested--;
            }
        }

        private void EmitBoardWake(Vector3 boardPosition)
        {
            if (_dust == null || Speed < 0.3f) return;
            _wakeDustDistance += Speed * Time.fixedDeltaTime * (profile != null ? profile.WakeDustPerMeter : 48f);
            int count = Mathf.Min(24, Mathf.FloorToInt(_wakeDustDistance));
            _wakeDustDistance = Mathf.Min(24f, _wakeDustDistance - count);
            Vector3 right = Vector3.Cross(_up, _forward).normalized;
            float frontShare = profile != null ? profile.WakeFrontShare : .45f;
            for (int i = 0; i < count; i++)
            {
                uint seed = ++_trailEmissionIndex * 71u + _generation;
                float lane = Hash01(seed);
                float side = Mathf.Lerp(-1f, 1f, Hash01(seed + 17u));
                bool front = lane < frontShare;
                Vector3 offset = front
                    ? _forward * (BoardLength * .48f) + right * (side * BoardWidth * .45f)
                    : right * ((side < 0f ? -1f : 1f) * BoardWidth * .48f) +
                      _forward * (Mathf.Lerp(-.35f, .35f, Hash01(seed + 31u)) * BoardLength);
                if (lane > .88f) offset *= .45f; // A lighter layer over the stone deck.
                float size = Mathf.Lerp(.18f, .40f, Hash01(seed + 47u));
                var emit = new ParticleSystem.EmitParams
                {
                    position = boardPosition + offset + _up * (size * .55f + .12f),
                    velocity = -_forward * Mathf.Lerp(.8f, 2.2f, Hash01(seed + 53u)) +
                        right * side * 1.2f + _up * Mathf.Lerp(.6f, 1.8f, Hash01(seed + 59u)),
                    startSize = size,
                    startLifetime = Mathf.Lerp(.4f, .85f, Hash01(seed + 67u)),
                    startColor = new Color(1f, 1f, 1f, .48f)
                };
                _dust.Emit(emit, 1);
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
        private void OnDisable()
        {
            _visualSessionActive = false;
            Cancel();
            for (int index = 0; index < _releasedCells.Length; index++)
                if (_releasedCells[index]?.Transform != null) _releasedCells[index].Transform.gameObject.SetActive(false);
            for (int index = 0; index < _chips.Length; index++)
                if (_chips[index]?.Transform != null) _chips[index].Transform.gameObject.SetActive(false);
        }
        private void OnDestroy()
        {
            cosmeticMaterials.Dispose();
            if (_boardBody != null) DestroyOwned(_boardBody.gameObject);
            for (int index = 0; index < _cells.Length; index++)
            {
                SurfCellView cell = _cells[index];
                if (cell?.Transform != null &&
                    (_boardBody == null || !cell.Transform.IsChildOf(_boardBody.transform)))
                    DestroyOwned(cell.Transform.gameObject);
                if (cell?.Mesh != null) DestroyOwned(cell.Mesh);
            }
            for (int index = 0; index < _chips.Length; index++)
                if (_chips[index]?.Transform != null) DestroyOwned(_chips[index].Transform.gameObject);
            for (int index = 0; index < _releasedCells.Length; index++)
            {
                if (_releasedCells[index]?.Mesh != null) DestroyOwned(_releasedCells[index].Mesh);
                if (_releasedCells[index]?.Transform != null) DestroyOwned(_releasedCells[index].Transform.gameObject);
            }
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
            RebuildSurfCells();
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
            var colors = new List<Color>(128);
            float jitter = Mathf.Lerp(-0.08f, 0.08f, Hash01(seed ^ 0xA341316Cu));
            switch (family)
            {
                case EarthSurfSilhouetteFamily.MantaSlab:
                    AppendBeveledPrism(vertices, triangles, uv, colors, new[]
                    {
                        new Vector2(-halfWidth * 0.38f, -halfLength), new Vector2(halfWidth * 0.38f, -halfLength),
                        new Vector2(halfWidth * 0.62f, -halfLength * 0.30f), new Vector2(halfWidth, halfLength * 0.28f),
                        new Vector2(halfWidth * 0.72f, halfLength), new Vector2(-halfWidth * 0.72f, halfLength),
                        new Vector2(-halfWidth, halfLength * 0.28f), new Vector2(-halfWidth * 0.62f, -halfLength * 0.30f)
                    }, noseHeight, 0.09f, jitter);
                    break;
                case EarthSurfSilhouetteFamily.CrescentPlough:
                    AppendBeveledPrism(vertices, triangles, uv, colors, new[]
                    {
                        new Vector2(-halfWidth * 0.58f, -halfLength), new Vector2(halfWidth * 0.58f, -halfLength),
                        new Vector2(halfWidth, halfLength * 0.42f), new Vector2(halfWidth * 0.62f, halfLength),
                        new Vector2(-halfWidth * 0.62f, halfLength), new Vector2(-halfWidth, halfLength * 0.42f)
                    }, noseHeight * 1.04f, 0.11f, jitter);
                    break;
                case EarthSurfSilhouetteFamily.SplitRail:
                    AppendBeveledPrism(vertices, triangles, uv, colors, Rectangle(
                        -halfWidth * 0.55f, halfWidth * 0.42f, halfLength), noseHeight, 0.08f, jitter);
                    AppendBeveledPrism(vertices, triangles, uv, colors, Rectangle(
                        halfWidth * 0.55f, halfWidth * 0.42f, halfLength), noseHeight * 0.94f, 0.08f, -jitter);
                    AppendBeveledPrism(vertices, triangles, uv, colors, Rectangle(
                        0f, halfWidth * 0.12f, halfLength * 0.76f), noseHeight * 1.12f, 0.12f, 0f);
                    break;
                default:
                    AppendBeveledPrism(vertices, triangles, uv, colors, new[]
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
            mesh.SetColors(colors);
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

        private static Mesh BuildSemanticCellMesh(
            float width,
            float length,
            float localNoseHeight,
            uint seed,
            EarthStoneBevelProfile bevelProfile)
        {
            var mesh = new Mesh { name = $"Earth Surf Irregular Stone {seed:X8}" };
            var vertices = new List<Vector3>(24);
            var triangles = new List<int>(48);
            var uv = new List<Vector2>(24);
            var colors = new List<Color>(24);
            float halfWidth = Mathf.Max(0.08f, width * 0.5f);
            float halfLength = Mathf.Max(0.10f, length * 0.5f);
            float jitter = Mathf.Lerp(-0.055f, 0.055f, Hash01(seed ^ 0xB5297A4Du));
            var footprint = new Vector2[7];
            for (int index = 0; index < footprint.Length; index++)
            {
                float angle = index * Mathf.PI * 2f / footprint.Length;
                float radius = Mathf.Lerp(0.80f, 1.05f, Hash01(seed + (uint)index * 31u));
                footprint[index] = new Vector2(Mathf.Cos(angle) * halfWidth * radius,
                    Mathf.Sin(angle) * halfLength * radius);
            }
            AppendBeveledPrism(
                vertices,
                triangles,
                uv,
                colors,
                footprint,
                Mathf.Max(0.065f, localNoseHeight),
                0.09f,
                jitter);
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0, true);
            mesh.SetUVs(0, uv);
            mesh.SetColors(colors);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EarthMeshIntegrityGate.ValidateInPlaceOrUseFallback(
                mesh,
                EarthMeshIntegrityPolicy.ClosedHero,
                mesh.name,
                mesh.bounds);
            Mesh faceted = EarthHardSurfaceMeshUtility.CreateFlatShadedCopy(mesh, mesh.name);
            DestroyOwned(mesh);
            return CreateOwnedChamferedRenderMesh(faceted, bevelProfile);
        }

        private static Mesh CreateOwnedChamferedRenderMesh(Mesh source, EarthStoneBevelProfile bevelProfile)
        {
            // These sources belong only to prepared visual cells/chips, never to
            // the board collider. Each procedural generation transfers its final
            // mesh to the released-cell pool, so a source-key cache would retain
            // dead generations. Keep exactly one owned finished mesh instead.
            Mesh chamfered = EarthFractureBevelMeshBuilder.Create(source, bevelProfile);
            if (chamfered != null && chamfered != source)
            {
                chamfered.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
                DestroyOwned(source);
                return chamfered;
            }
            return source;
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
            List<Color> colors,
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
            Color faceColor = new Color(0.62f, 0.60f, 0.57f, 0.38f);
            Color bevelColor = new Color(0.67f, 0.64f, 0.59f, 0.72f);
            int bottomCenter = vertices.Count;
            AddVertex(vertices, uv, colors, new Vector3(center.x, bottom, center.y), faceColor);
            int bottomRing = vertices.Count;
            for (int index = 0; index < count; index++)
                AddVertex(vertices, uv, colors, new Vector3(footprint[index].x, bottom, footprint[index].y), faceColor);
            int shoulderRing = vertices.Count;
            for (int index = 0; index < count; index++)
            {
                float z01 = Mathf.InverseLerp(minimumZ, maximumZ, footprint[index].y);
                float top = Mathf.Lerp(0.04f, noseHeight, Mathf.Pow(z01, 1.25f)) +
                            heightJitter * Mathf.Sin(index * 2.17f);
                AddVertex(vertices, uv, colors, new Vector3(footprint[index].x, top - bevelHeight, footprint[index].y), faceColor);
            }
            int topCenter = vertices.Count;
            float centerZ01 = Mathf.InverseLerp(minimumZ, maximumZ, center.y);
            AddVertex(vertices, uv, colors,
                new Vector3(center.x, Mathf.Lerp(0.04f, noseHeight, centerZ01), center.y), faceColor);
            int topRing = vertices.Count;
            for (int index = 0; index < count; index++)
            {
                Vector2 inset = Vector2.Lerp(footprint[index], center, Mathf.Clamp01(bevel01));
                float z01 = Mathf.InverseLerp(minimumZ, maximumZ, footprint[index].y);
                float top = Mathf.Lerp(0.04f, noseHeight, Mathf.Pow(z01, 1.25f)) +
                            heightJitter * Mathf.Sin(index * 2.17f);
                AddVertex(vertices, uv, colors, new Vector3(inset.x, top, inset.y), bevelColor);
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

        private static void AddVertex(
            List<Vector3> vertices,
            List<Vector2> uv,
            List<Color> colors,
            Vector3 value,
            Color color)
        {
            vertices.Add(value);
            uv.Add(new Vector2(value.x * 0.3f + 0.5f, value.z * 0.2f + 0.5f));
            colors.Add(color);
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
