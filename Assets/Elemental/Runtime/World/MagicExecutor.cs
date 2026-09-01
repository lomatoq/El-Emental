using System;
using System.Collections.Generic;
using Elemental.Runtime.Physics;
using Elemental.Runtime.Matter;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Matter;
using Elemental.Simulation.Structures;
using Elemental.Simulation.Combat;
using Elemental.Simulation.Voxel;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using Elemental.Runtime.Characters;

namespace Elemental.Runtime.World
{
    [DisallowMultipleComponent]
    public sealed class MagicExecutor : MonoBehaviour, IMagicCommandSink
    {
        private static readonly ProfilerMarker ExecuteMarker = new ProfilerMarker("Elemental.Magic.Execute");
        private static readonly ProfilerMarker VectorFieldMarker = new ProfilerMarker("Elemental.Magic.VectorField");
        private static readonly ProfilerMarker GravityWellMarker = new ProfilerMarker("Elemental.Magic.GravityWell");
        private static readonly ProfilerMarker TerrainCommitMarker =
            new ProfilerMarker("Elemental.Terrain.Commit");

        [SerializeField] private VoxelPlanetBehaviour voxelPlanet;
        [SerializeField] private EarthFragmentPool fragmentPool;
        [SerializeField] private EarthWallPool wallPool;
        [SerializeField] private EarthTelekinesisController telekinesis;
        [SerializeField] private EarthVectorFieldProfile vectorFieldProfile;
        [SerializeField] private EarthPlatformPool platformPool;
        [SerializeField] private EarthGravityWellProfile gravityWellProfile;
        [SerializeField] private Transform planetCenter;
        [SerializeField] private Transform heldFragmentAnchor;
        [SerializeField] private EarthMatterKernelBehaviour matterKernel;
        [SerializeField] private EarthMatterReturnController matterReturnController;
        [SerializeField] private EarthTechniqueComboRuntime comboRuntime;
        [SerializeField, Min(1f)] private float earthMaterialDensity = 120f;
        [SerializeField, Min(0.1f)] private float wallMaximumHeight = 4.0f;
        [SerializeField, Min(0.1f)] private float wallMinimumHeight = 1.5f;
        [SerializeField, Min(1f)] private float wallMaxLength = 22f;
        [SerializeField, Min(0.05f)] private float wallThickness = 0.95f;
        [SerializeField, Min(1f)] private float wallPushLeverage = 12f;
        [SerializeField, Min(0.1f)] private float minimumThrowVelocityChange = 6f;
        [SerializeField, Min(0.1f)] private float maximumThrowVelocityChange = 18f;

        private readonly Dictionary<AbilityId, CompiledAbilityRecipe> _recipes =
            new Dictionary<AbilityId, CompiledAbilityRecipe>();
        private readonly List<float3> _platformPathScratch = new List<float3>(32);
        private readonly List<TerrainExtractionTransaction> _pendingExtractions =
            new List<TerrainExtractionTransaction>(4);
        private readonly RaycastHit[] _extractionSurfaceHits = new RaycastHit[12];
        private EarthFragment _heldFragment;
        private Vector3 _pendingExtractionTarget;
        private Vector3 _pendingExtractionVelocity;
        private float _pendingExtractionCharge;
        private BendTuning _pendingExtractionTuning = BendTuning.Default;
        private bool _pendingExtractionRelease;
        private bool _pendingExtractionCancel;
        private Vector3 _pendingReleaseAim;
        private Vector3 _pendingReleaseGestureVelocity;
        private float _pendingReleaseCharge;
        private uint _pendingReleaseTick;
        private IEarthPhysicalTarget _vectorFieldTarget;
        private Vector3 _vectorFieldPoint;
        private Vector3 _vectorFieldDirection;
        private float _vectorFieldCharge;
        private readonly HashSet<Rigidbody> _gravityWellBodies = new HashSet<Rigidbody>();
        private readonly EarthGravityGripSession _gravityGripSession = new EarthGravityGripSession(48);
        private readonly IEarthPhysicalTarget[] _gravityWellStructureTargets = new IEarthPhysicalTarget[48];
        private readonly IEarthPhysicalTarget[] _comboFractureTargets = new IEarthPhysicalTarget[48];
        private readonly EarthMatterIdentity[] _gravityReturnIdentities = new EarthMatterIdentity[48];
        private bool _gravityWellActive;
        private Vector3 _gravityWellFocus;
        private Vector3 _gravityWellUp;
        private Vector3 _gravityWellViewForward;
        private float _gravityWellElapsed;
        private EarthWall _gravityWellWall;
        private EarthPlatform _gravityWellPlatform;
        private IEarthFractureSource _gravityFractureSource;
        private bool _gravityWellFracturedStructure;
        private IEarthRepairController _repairController;
        private bool _gravityGestureControlled;
        private EarthGravityStructureIntent _gravityStructureIntent;
        private float _gravityStructurePhase;
        private float _gravityPlatformDisassemblyPhase;
        private bool _gravityThrowCharging;
        private float _gravityThrowStartedAt;
        private float _gravityThrowCharge01;
        private Vector3 _gravityThrowDirection;
        private readonly EarthFragment[] _heldFractureCluster = new EarthFragment[4];
        private readonly Vector3[] _heldFractureOffsets = new Vector3[4];
        private int _heldFractureCount;
        private bool _heldFractureThrowCharging;
        private float _heldFractureThrowStartedAt;
        private Vector3 _heldFractureThrowDirection;
        private Vector3 _heldFractureCenter;

        public MagicWorldEvents Events { get; } = new MagicWorldEvents();
        public MagicReplayRecorder Recorder { get; } = new MagicReplayRecorder();
        public int SuccessfulCommandCount { get; private set; }
        public EarthFragmentPool FragmentPool => fragmentPool;
        public EarthWallPool WallPool => wallPool;
        public EarthPlatformPool PlatformPool => platformPool;
        public VoxelPlanetBehaviour VoxelPlanet => voxelPlanet;
        public EarthMatterKernelBehaviour MatterKernel => matterKernel;
        public EarthMatterReturnController MatterReturnController => matterReturnController;
        public EarthTechniqueComboRuntime ComboRuntime => comboRuntime;
        public float EarthMaterialDensity => Mathf.Max(1f, earthMaterialDensity);
        public Transform PlanetCenterTransform => planetCenter;
        public EarthFragment HeldFragment => _heldFragment != null && _heldFragment.IsHeld ? _heldFragment : null;
        public EarthFragment ReservedOrHeldFragment => _heldFragment;
        public bool HasPendingExtraction => _pendingExtractions.Count > 0;
        public Rigidbody HeldBody => HeldFragment != null
            ? HeldFragment.Body
            : (telekinesis != null ? telekinesis.Body : null);
        public float HeldMass => HeldBody != null ? HeldBody.mass : 0f;
        public Vector3 HeldControlError => HeldFragment != null
            ? HeldFragment.LastControlError
            : (telekinesis != null ? telekinesis.LastControlError : Vector3.zero);
        public Vector3 HeldControlForce => HeldFragment != null
            ? HeldFragment.LastAppliedControlForce
            : (telekinesis != null ? telekinesis.LastAppliedControlForce : Vector3.zero);
        public bool HeldControlForceWasClamped => HeldFragment != null
            ? HeldFragment.LastControlForceWasClamped
            : telekinesis != null && telekinesis.LastControlForceWasClamped;
        public float LastLaunchVelocityChange { get; private set; }
        public float LastMagicPushVelocityChange { get; private set; }
        public ulong LastPreviewGeometryHash { get; private set; }
        public ulong LastCommittedGeometryHash { get; private set; }
        public bool IsVectorFieldActive => _vectorFieldTarget != null && _vectorFieldTarget.IsEarthTargetValid;
        public Rigidbody VectorFieldBody => _vectorFieldTarget != null ? _vectorFieldTarget.Body : null;
        public Vector3 VectorFieldDirection => _vectorFieldDirection;
        public Vector3 VectorFieldPoint => _vectorFieldTarget != null && _vectorFieldTarget.Body != null
            ? _vectorFieldTarget.Body.worldCenterOfMass
            : _vectorFieldPoint;
        public float VectorFieldCharge => _vectorFieldCharge;
        public float VectorFieldMass => _vectorFieldTarget != null ? _vectorFieldTarget.EarthMass : 0f;
        public bool IsGravityWellActive => _gravityWellActive;
        public bool IsRepairActive => _repairController != null && _repairController.IsRepairing;
        public Vector3 GravityWellFocus => _gravityWellFocus;
        public float GravityWellStrength => _gravityWellActive
            ? Mathf.Clamp01(_gravityWellElapsed / GravityFractureDelay)
            : 0f;
        public float GravityWellRadius => gravityWellProfile != null ? gravityWellProfile.Radius : 7.5f;
        public float GravityWellFocusLift => gravityWellProfile != null ? gravityWellProfile.FocusLift : 0.75f;
        public int GravityWellCapturedCount => _gravityGripSession.Count;
        public int GravityWellMaximumCapturedTargets => GravityMaximumCapturedTargets;
        public bool HasGravityStructureTarget => _gravityFractureSource != null;
        public EarthGravityStructureIntent GravityStructureIntent => _gravityStructureIntent;
        public float GravityStructurePhase => _gravityStructurePhase;
        public bool IsGravityClusterThrowCharging => _gravityThrowCharging;
        public float GravityClusterThrowCharge01 => _gravityThrowCharge01;
        public bool HasHeldFractureCluster => _heldFractureCount > 0;
        public int HeldFractureClusterCount => _heldFractureCount;

        public bool TryFractureHeldBoulder()
        {
            EarthFragment source = HeldFragment;
            if (source == null || source.Body == null || fragmentPool == null ||
                HasPendingExtraction || _heldFractureCount > 0) return false;

            const int desiredCount = 4;
            float sourceMass = Mathf.Max(0.4f, source.Mass);
            float chunkMass = sourceMass / desiredCount;
            float chunkRadius = Mathf.Max(0.12f, source.Radius * 0.58f);
            Vector3 center = source.BendTargetPosition;
            _heldFractureCenter = center;
            Vector3 up = planetCenter != null
                ? SafeDirection(center - planetCenter.position)
                : SafeDirection(transform.up);
            Vector3 right = Vector3.ProjectOnPlane(source.transform.right, up).normalized;
            if (right.sqrMagnitude < 0.5f) right = Vector3.Cross(up, Vector3.forward).normalized;
            Vector3 forward = Vector3.Cross(right, up).normalized;
            _heldFractureOffsets[0] = (up * 0.16f - right * 0.14f) * chunkRadius;
            _heldFractureOffsets[1] = (right * 0.72f + up * 0.12f) * chunkRadius;
            _heldFractureOffsets[2] = (-right * 0.38f + forward * 0.66f - up * 0.18f) * chunkRadius;
            _heldFractureOffsets[3] = (-right * 0.34f - forward * 0.64f - up * 0.10f) * chunkRadius;

            source.transform.localScale *= 0.66f;
            source.Body.mass = chunkMass;
            _heldFractureCluster[0] = source;
            _heldFractureCount = 1;
            for (int index = 1; index < desiredCount; index++)
            {
                EarthFragment chunk = fragmentPool.Acquire(
                    this,
                    center + _heldFractureOffsets[index],
                    chunkRadius,
                    chunkMass,
                    null);
                if (chunk == null) break;
                chunk.BeginBendControl(
                    center + _heldFractureOffsets[index], Vector3.zero, 0.45f, BendTuning.Default);
                _heldFractureCluster[index] = chunk;
                _heldFractureCount++;
            }
            for (int index = 0; index < _heldFractureCluster.Length; index++)
                _heldFractureCluster[index]?.SetFormationKinematic(true);
            return _heldFractureCount >= 2;
        }

        public bool BeginHeldFractureThrow(Vector3 direction)
        {
            if (!HasHeldFractureCluster || _heldFractureThrowCharging) return false;
            _heldFractureThrowCharging = true;
            _heldFractureThrowStartedAt = Time.unscaledTime;
            _heldFractureThrowDirection = SafeDirection(direction);
            return true;
        }

        public void UpdateHeldFractureThrow(Vector3 direction)
        {
            if (_heldFractureThrowCharging)
                _heldFractureThrowDirection = SafeDirection(direction);
        }

        public int ReleaseHeldFractureThrow(Vector3 direction)
        {
            if (!_heldFractureThrowCharging || !HasHeldFractureCluster) return 0;
            UpdateHeldFractureThrow(direction);
            float heldSeconds = Mathf.Max(0f, Time.unscaledTime - _heldFractureThrowStartedAt);
            _heldFractureThrowCharging = false;
            Vector3 forward = SafeDirection(_heldFractureThrowDirection);
            if (heldSeconds <= 0.22f)
            {
                for (int index = _heldFractureCluster.Length - 1; index >= 0; index--)
                {
                    EarthFragment chunk = _heldFractureCluster[index];
                    if (chunk == null) continue;
                    LaunchHeldFractureChunk(chunk, forward * 13.5f);
                    _heldFractureCluster[index] = null;
                    _heldFractureCount--;
                    return 1;
                }
                return 0;
            }

            float charge01 = Mathf.Clamp01((heldSeconds - 0.22f) / 0.83f);
            float speed = Mathf.Lerp(11.5f, 18f, charge01);
            Vector3 up = planetCenter != null
                ? SafeDirection((_heldFragment != null ? _heldFragment.transform.position : transform.position) -
                                planetCenter.position)
                : SafeDirection(transform.up);
            Vector3 side = Vector3.Cross(up, forward).normalized;
            int launched = 0;
            for (int index = 0; index < _heldFractureCluster.Length; index++)
            {
                EarthFragment chunk = _heldFractureCluster[index];
                if (chunk == null) continue;
                float spread = (index - 1.5f) * 0.11f;
                Vector3 velocity = (forward + side * spread + up * (0.04f + index * 0.015f)).normalized * speed;
                LaunchHeldFractureChunk(chunk, velocity);
                _heldFractureCluster[index] = null;
                launched++;
            }
            _heldFractureCount = 0;
            _heldFragment = null;
            LastLaunchVelocityChange = speed;
            return launched;
        }

        private void LaunchHeldFractureChunk(EarthFragment chunk, Vector3 velocity)
        {
            if (chunk == null || chunk.Body == null) return;
            chunk.SetFormationKinematic(false);
            chunk.StopBendControl();
            chunk.Body.linearVelocity = velocity;
            chunk.Body.angularVelocity = Vector3.Cross(velocity.normalized, chunk.transform.up) * 2.4f;
            chunk.Body.WakeUp();
            if (chunk == _heldFragment) _heldFragment = null;
            LastLaunchVelocityChange = velocity.magnitude;
        }

        private void Awake()
        {
            AttachExtractionCommitListener();
            if (matterKernel == null) matterKernel = EarthMatterKernelBehaviour.FindOrCreate(this);
            if (matterReturnController == null)
                matterReturnController = GetComponent<EarthMatterReturnController>() ??
                                         gameObject.AddComponent<EarthMatterReturnController>();
            matterReturnController.Configure(voxelPlanet, matterKernel, EarthMaterialDensity);
            matterReturnController.ReturnStageChanged -= HandleReturnStageForCombo;
            matterReturnController.ReturnStageChanged += HandleReturnStageForCombo;
            if (comboRuntime == null)
                comboRuntime = GetComponent<EarthTechniqueComboRuntime>() ??
                               gameObject.AddComponent<EarthTechniqueComboRuntime>();
            if (platformPool != null)
            {
                platformPool.PlatformFractured -= HandlePlatformFractured;
                platformPool.PlatformFractured += HandlePlatformFractured;
            }
        }

        public bool TryReturnMatter(IEarthPhysicalTarget target, Vector3 fallbackSurfaceWorld)
        {
            if (target == null || target.Body == null || matterReturnController == null) return false;
            EarthMatterIdentity identity = target.Body.GetComponent<EarthMatterIdentity>() ??
                                           target.Body.GetComponentInParent<EarthMatterIdentity>();
            return identity != null && matterReturnController.TryBeginReturn(identity, fallbackSurfaceWorld);
        }

        public int TryReturnGravityCaptured(Vector3 fallbackSurfaceWorld)
        {
            if (matterReturnController == null || _gravityGripSession.Count <= 0) return 0;
            int identityCount = 0;
            for (int index = 0; index < _gravityGripSession.Count &&
                                identityCount < _gravityReturnIdentities.Length; index++)
            {
                IEarthPhysicalTarget target = _gravityGripSession.GetTarget(index);
                if (target == null || target.Body == null) continue;
                EarthMatterIdentity identity = target.Body.GetComponent<EarthMatterIdentity>() ??
                                               target.Body.GetComponentInParent<EarthMatterIdentity>();
                if (identity != null && identity.IsRegistered)
                    _gravityReturnIdentities[identityCount++] = identity;
            }
            if (identityCount <= 0) return 0;

            // Release the grip first so physical targets leave Controlled cleanly;
            // the return controller then atomically claims them as CapturedForReturn.
            CancelGravityWell();
            int started = matterReturnController.TryBeginReturnsNonAlloc(
                _gravityReturnIdentities, identityCount, fallbackSurfaceWorld);
            for (int index = 0; index < identityCount; index++) _gravityReturnIdentities[index] = null;
            return started;
        }

        public bool ReverseMatterReturnBeforeCommit() =>
            matterReturnController != null && matterReturnController.ReverseBeforeCommit();

        public bool TryBeginGravityWell(
            Collider aimedCollider,
            Vector3 focus,
            Vector3 localUp,
            bool gestureControlled = false)
        {
            CancelGravityWell();
            if (aimedCollider == null) return false;
            _gravityGestureControlled = gestureControlled;
            _gravityStructureIntent = EarthGravityStructureIntent.Neutral;
            _gravityStructurePhase = 0f;
            _gravityPlatformDisassemblyPhase = 0f;
            _gravityWellWall = aimedCollider.GetComponentInParent<EarthWall>();
            if (_gravityWellWall == null)
                _gravityWellWall = aimedCollider.GetComponentInParent<EarthWallPiece>()?.Owner;
            if (!gestureControlled && _gravityWellWall != null && _gravityWellWall.IsCollapsing &&
                _gravityWellWall.Reassembly != null)
            {
                _gravityWellFocus = focus;
                _gravityWellUp = SafeDirection(localUp);
                _gravityWellElapsed = 0f;
                _repairController = _gravityWellWall.Reassembly;
                _gravityWellActive = _repairController.TryBeginRepair(
                    unchecked((uint)Time.frameCount));
                if (_gravityWellActive) return true;
                _repairController = null;
                _gravityWellWall = null;
                return false;
            }
            if (_gravityWellWall != null)
                _gravityWellWall.Collapsed += HandleGravityWellWallFractured;
            _gravityWellPlatform = aimedCollider.GetComponentInParent<EarthPlatform>();
            if (_gravityWellPlatform == null)
                _gravityWellPlatform = aimedCollider.GetComponentInParent<EarthPlatformPiece>()?.Owner;
            EarthArenaStructure arenaStructure = aimedCollider.GetComponentInParent<EarthArenaStructure>();
            if (arenaStructure == null)
                arenaStructure = aimedCollider.GetComponentInParent<EarthArenaPiece>()?.Owner;
            _gravityFractureSource = _gravityWellWall != null
                ? (IEarthFractureSource)_gravityWellWall
                : _gravityWellPlatform != null
                    ? _gravityWellPlatform
                    : arenaStructure;
            if (_gravityFractureSource != null)
                _gravityFractureSource.TargetsActivated += HandleGravityTargetsActivated;
            _gravityWellFocus = focus;
            _gravityWellUp = SafeDirection(localUp);
            _gravityWellViewForward = Vector3.ProjectOnPlane(focus - transform.position, _gravityWellUp).normalized;
            _gravityWellElapsed = 0f;
            _gravityWellFracturedStructure = false;
            _gravityWellActive = true;
            // MMB is a press-owned session. Capture the explicitly aimed target once;
            // newly fractured children join through IEarthFractureSource.TargetsActivated.
            // Never grow the selection from an overlap query while the button is held.
            IEarthPhysicalTarget aimedTarget = ResolveExplicitGravityTarget(aimedCollider);
            TryLatchGravityTarget(aimedTarget);
            return true;
        }

        public void SetGravityStructureGesture(EarthGravityStructureIntent intent, float phase01)
        {
            if (!_gravityWellActive || !_gravityGestureControlled) return;
            _gravityStructureIntent = intent;
            _gravityStructurePhase = Mathf.Clamp01(phase01);
            if (intent == EarthGravityStructureIntent.Repair)
                ApplyGestureRepair();
            else if (intent == EarthGravityStructureIntent.Disassemble)
                ApplyGestureDisassembly();
        }

        public void UpdateGravityWell(Vector3 focus, Vector3 localUp)
        {
            if (!_gravityWellActive) return;
            _gravityWellFocus = focus;
            if (localUp.sqrMagnitude > 0.001f) _gravityWellUp = localUp.normalized;
        }

        public void UpdateGravityWell(Vector3 focus, Vector3 localUp, Vector3 viewForward)
        {
            UpdateGravityWell(focus, localUp);
            Vector3 tangentForward = Vector3.ProjectOnPlane(viewForward, _gravityWellUp);
            if (tangentForward.sqrMagnitude > 0.01f)
                _gravityWellViewForward = tangentForward.normalized;
        }

        public bool BeginGravityClusterThrow(Vector3 aimDirection)
        {
            if (!_gravityWellActive || _repairController != null || _gravityGripSession.Count <= 0)
                return false;
            _gravityThrowCharging = true;
            _gravityThrowStartedAt = Time.unscaledTime;
            _gravityThrowCharge01 = 0f;
            _gravityThrowDirection = SafeDirection(aimDirection);
            return true;
        }

        public void UpdateGravityClusterThrow(Vector3 aimDirection)
        {
            if (!_gravityThrowCharging) return;
            _gravityThrowDirection = SafeDirection(aimDirection);
            _gravityThrowCharge01 = EarthGravityClusterThrowSolver.Charge01(
                Mathf.Max(0f, Time.unscaledTime - _gravityThrowStartedAt), 1.05f);
        }

        public int ReleaseGravityClusterThrow(Vector3 aimDirection)
        {
            if (!_gravityThrowCharging || !_gravityWellActive) return 0;
            UpdateGravityClusterThrow(aimDirection);
            float heldSeconds = Mathf.Max(0f, Time.unscaledTime - _gravityThrowStartedAt);
            EarthGravityClusterReleaseMode mode = heldSeconds <= 0.22f
                ? EarthGravityClusterReleaseMode.Direct
                : EarthGravityClusterReleaseMode.CompressionBlast;
            EarthGravityClusterThrowTuning tuning = EarthGravityClusterThrowTuning.Default;
            Vector3 direction = SafeDirection(_gravityThrowDirection);
            Vector3 up = SafeDirection(_gravityWellUp);
            int launched = 0;
            int targetCount = _gravityGripSession.Count;
            for (int index = 0; index < targetCount; index++)
            {
                IEarthPhysicalTarget target = _gravityGripSession.GetTarget(index);
                if (target == null || target.Body == null || !target.IsEarthTargetValid) continue;
                Rigidbody body = target.Body;
                EarthGravityClusterLaunchSample sample = EarthGravityClusterThrowSolver.Solve(
                    target.StableEarthId,
                    index,
                    targetCount,
                    Mathf.Max(0.1f, target.EarthMass),
                    ToFloat3(direction),
                    ToFloat3(up),
                    mode,
                    _gravityThrowCharge01,
                    in tuning);
                body.linearVelocity = ToVector3(sample.Velocity);
                body.angularVelocity = ToVector3(sample.AngularVelocity);
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                EarthLaunchCollisionGrace grace = body.GetComponent<EarthLaunchCollisionGrace>() ??
                                                  body.gameObject.AddComponent<EarthLaunchCollisionGrace>();
                grace.Begin(transform, direction);
                body.WakeUp();
                launched++;
            }
            LastLaunchVelocityChange = mode == EarthGravityClusterReleaseMode.Direct
                ? tuning.DirectSpeed
                : Mathf.Lerp(tuning.MinimumBlastSpeed, tuning.MaximumBlastSpeed, _gravityThrowCharge01);
            CancelGravityWell();
            return launched;
        }

        public void CancelGravityWell()
        {
            if (_repairController != null && _repairController.IsRepairing)
                _repairController.Interrupt(
                    EarthRepairInterruptReason.Released,
                    unchecked((uint)Time.frameCount));
            if (_gravityWellWall != null)
                _gravityWellWall.Collapsed -= HandleGravityWellWallFractured;
            if (_gravityFractureSource != null)
                _gravityFractureSource.TargetsActivated -= HandleGravityTargetsActivated;
            _gravityGripSession.ReleaseAll(EarthMagicGripKind.GravityWell);
            _gravityWellBodies.Clear();
            _gravityWellActive = false;
            _gravityWellElapsed = 0f;
            _gravityWellViewForward = Vector3.zero;
            _gravityWellWall = null;
            _gravityWellPlatform = null;
            _gravityFractureSource = null;
            _gravityWellFracturedStructure = false;
            _repairController = null;
            _gravityGestureControlled = false;
            _gravityStructureIntent = EarthGravityStructureIntent.Neutral;
            _gravityStructurePhase = 0f;
            _gravityPlatformDisassemblyPhase = 0f;
            _gravityThrowCharging = false;
            _gravityThrowCharge01 = 0f;
            _gravityThrowDirection = Vector3.zero;
        }

        private void HandleGravityWellWallFractured(EarthWall wall)
        {
            if (!_gravityWellActive || wall == null || wall != _gravityWellWall) return;
            _gravityWellFracturedStructure = true;
            CaptureFracturedStructureTargets();
        }

        private void HandleGravityTargetsActivated(IEarthFractureSource source)
        {
            if (!_gravityWellActive || source == null || source != _gravityFractureSource) return;
            _gravityWellFracturedStructure = true;
            CaptureFracturedStructureTargets();
        }

        public bool ReleaseHeldFragment(
            Vector3 aimDirection,
            Vector3 gestureVelocity,
            float charge01,
            uint tick,
            out Vector3 releaseVelocity)
        {
            releaseVelocity = Vector3.zero;
            if (_heldFragment == null || !_heldFragment.gameObject.activeSelf)
                return false;

            EarthFragment fragment = _heldFragment;
            float boundedCharge = Mathf.Clamp01(charge01);
            float reportedVelocityChange = Mathf.Lerp(
                minimumThrowVelocityChange,
                maximumThrowVelocityChange,
                boundedCharge);
            releaseVelocity = fragment.ReleaseBend(aimDirection, gestureVelocity, boundedCharge);
            LastLaunchVelocityChange = reportedVelocityChange;
            Vector3 direction = releaseVelocity.sqrMagnitude > 0.0001f
                ? releaseVelocity.normalized
                : aimDirection.normalized;
            FragmentLaunchedEvent launched = new FragmentLaunchedEvent(
                tick,
                fragment.FragmentId,
                fragment.Mass,
                ToFloat3(fragment.transform.position),
                ToFloat3(direction),
                reportedVelocityChange);
            Events.Emit(in launched);
            _heldFragment = null;
            return true;
        }

        public bool TryAcquireExistingEarthBody(
            Rigidbody body,
            Vector3 initialTarget,
            in Elemental.Simulation.Bending.BendTuning tuning,
            uint tick,
            IEarthPhysicalTarget earthTarget = null)
        {
            if (HeldBody != null || telekinesis == null ||
                !telekinesis.TryAcquire(body, initialTarget, in tuning, earthTarget)) return false;
            var grabbed = new EarthBodyGrabbedEvent(
                tick,
                telekinesis.BodyId,
                body.mass,
                ToFloat3(body.worldCenterOfMass));
            Events.Emit(in grabbed);
            SuccessfulCommandCount++;
            return true;
        }

        public void BeginHeldEarthControl(
            Vector3 target,
            Vector3 velocity,
            float charge01,
            in Elemental.Simulation.Bending.BendTuning tuning)
        {
            if (HasHeldFractureCluster)
            {
                _heldFractureCenter = target;
                UpdateHeldFractureFormation();
            }
            else if (HeldFragment != null)
                HeldFragment.BeginBendControl(target, velocity, charge01, in tuning);
            else if (HasPendingExtraction)
            {
                _pendingExtractionTarget = target;
                _pendingExtractionVelocity = velocity;
                _pendingExtractionCharge = Mathf.Clamp01(charge01);
                _pendingExtractionTuning = tuning;
            }
            else
                telekinesis?.UpdateTarget(target, velocity, charge01);
        }

        public void UpdateHeldEarthTarget(Vector3 target, Vector3 velocity, float charge01)
        {
            if (HasHeldFractureCluster)
            {
                _heldFractureCenter = target;
                UpdateHeldFractureFormation();
            }
            else if (HeldFragment != null)
                HeldFragment.UpdateBendTarget(target, velocity, charge01);
            else if (HasPendingExtraction)
            {
                _pendingExtractionTarget = target;
                _pendingExtractionVelocity = velocity;
                _pendingExtractionCharge = Mathf.Clamp01(charge01);
            }
            else
                telekinesis?.UpdateTarget(target, velocity, charge01);
        }

        public bool ReleaseHeldEarth(
            Vector3 aimDirection,
            Vector3 gestureVelocity,
            float charge01,
            uint tick,
            out Vector3 releaseVelocity)
        {
            releaseVelocity = Vector3.zero;
            if (HeldFragment != null)
                return ReleaseHeldFragment(
                    aimDirection, gestureVelocity, charge01, tick, out releaseVelocity);
            if (HasPendingExtraction)
            {
                _pendingExtractionRelease = true;
                _pendingReleaseAim = aimDirection;
                _pendingReleaseGestureVelocity = gestureVelocity;
                _pendingReleaseCharge = Mathf.Clamp01(charge01);
                _pendingReleaseTick = tick;
                return true;
            }
            if (telekinesis == null || telekinesis.Body == null) return false;
            uint bodyId = telekinesis.BodyId;
            float mass = telekinesis.Body.mass;
            if (!telekinesis.Release(
                    aimDirection, gestureVelocity, charge01, out releaseVelocity)) return false;
            var released = new EarthBodyReleasedEvent(
                tick, bodyId, mass, ToFloat3(releaseVelocity));
            Events.Emit(in released);
            return true;
        }

        public bool ReleaseHeldEarthAtSpeed(
            Vector3 direction,
            float targetSpeed,
            uint tick,
            out Vector3 releaseVelocity)
        {
            releaseVelocity = Vector3.zero;
            EarthFragment fragment = HeldFragment;
            if (fragment == null || fragment.Body == null) return false;
            Vector3 safeDirection = SafeDirection(direction);
            // Quick Stone owns its launch envelope through EarthQuickCastProfile.
            // Reusing the slower vector-field cap silently flattened the authored
            // 30..38 m/s lane to 32 m/s and would also defeat its 2x power tuning.
            // The high ceiling is only a numerical/CCD guard; ordinary values stay
            // completely profile-driven.
            releaseVelocity = safeDirection * Mathf.Clamp(
                targetSpeed,
                1f,
                EarthQuickCastProfile.MaximumProjectileSpeed);
            EarthTypedCombatProjectile typedProjectile =
                fragment.GetComponent<EarthTypedCombatProjectile>();
            typedProjectile?.Arm(fragment, EarthCharacterImpactSourceKind.QuickStone);
            fragment.ReleaseControlledProjectile(releaseVelocity);
            LastLaunchVelocityChange = releaseVelocity.magnitude;
            FragmentLaunchedEvent launched = new FragmentLaunchedEvent(
                tick,
                fragment.FragmentId,
                fragment.Mass,
                ToFloat3(fragment.transform.position),
                ToFloat3(safeDirection),
                releaseVelocity.magnitude);
            Events.Emit(in launched);
            _heldFragment = null;
            return true;
        }

        public void CancelHeldEarthControl()
        {
            if (_heldFragment != null)
            {
                if (HasPendingExtraction)
                {
                    // The SDF transaction already owns this reserved fragment. Do not
                    // orphan it while neighbouring chunks are still staging; commit it
                    // as a normal dynamic rock and then release executor ownership.
                    _pendingExtractionCancel = true;
                    _pendingExtractionRelease = false;
                }
                else
                {
                    _heldFragment.StopBendControl();
                    _heldFragment = null;
                }
            }
            telekinesis?.Clear();
        }

        public bool TryBeginVectorField(
            Collider hitCollider,
            Rigidbody body,
            Vector3 point,
            Vector3 direction)
        {
            CancelVectorField();
            if (IsCharacterBody(body)) return false;
            IEarthPhysicalTarget target = ResolveEarthTarget(hitCollider, body);
            if (target == null && hitCollider != null)
            {
                EarthArenaStructure arena = hitCollider.GetComponentInParent<EarthArenaStructure>();
                if (arena != null)
                {
                    arena.TryPluckCell(point, out target);
                    body = target?.Body;
                }
            }
            if (target == null || !target.IsEarthTargetValid || target.Body == null) return false;
            _vectorFieldTarget = target;
            _vectorFieldPoint = point;
            _vectorFieldDirection = SafeDirection(direction);
            _vectorFieldCharge = 0f;
            target.OnEarthMagicGrabbed(EarthMagicGripKind.VectorField);
            return target.IsEarthTargetValid;
        }

        public void UpdateVectorField(Vector3 direction, float charge01)
        {
            if (_vectorFieldTarget == null) return;
            _vectorFieldDirection = SafeDirection(direction);
            _vectorFieldCharge = Mathf.Clamp01(charge01);
        }

        public bool ReleaseVectorField() => ReleaseVectorField(
            EarthVectorReleaseIntent.ChargedPulse,
            _vectorFieldDirection,
            _vectorFieldCharge);

        public bool ReleaseVectorField(
            EarthVectorReleaseIntent intent,
            Vector3 releaseDirection,
            float strength01)
        {
            IEarthPhysicalTarget target = _vectorFieldTarget;
            if (target == null) return false;
            bool valid = target.IsEarthTargetValid && target.Body != null;
            float mass = Mathf.Max(0.01f, target.EarthMass);
            float velocityChange = 0f;
            if (valid && intent != EarthVectorReleaseIntent.Controlled)
            {
                _vectorFieldDirection = SafeDirection(releaseDirection);
                float releaseStrength = intent switch
                {
                    EarthVectorReleaseIntent.QuickPulse => Mathf.Lerp(0.18f, 0.46f, Mathf.Clamp01(strength01)),
                    EarthVectorReleaseIntent.ProjectileFlick => Mathf.Lerp(0.62f, 1f, Mathf.Clamp01(strength01)),
                    _ => Mathf.Max(_vectorFieldCharge, Mathf.Clamp01(strength01))
                };
                Vector3 direction = FieldDirectionFor(target);
                if (intent == EarthVectorReleaseIntent.ProjectileFlick && target is EarthWall wall)
                {
                    float sizeResponse = Mathf.Clamp(Mathf.Sqrt(2400f / mass), 0.82f, 1.45f);
                    float targetSpeed = Mathf.Min(
                        VectorWallSpeedLimit,
                        Mathf.Lerp(8f, 11f, releaseStrength) * sizeResponse);
                    velocityChange = wall.ApplyMagicLaunchVelocity(direction, targetSpeed);
                }
                else
                {
                    float impulse = EarthVectorFieldSolver.FinalImpulse(
                        releaseStrength,
                        target.TargetKind == EarthPhysicalTargetKind.Wall
                            ? VectorMinimumWallReleaseImpulse
                            : VectorMinimumReleaseImpulse,
                        VectorMaximumReleaseImpulse);
                    float multiplier = target.TargetKind == EarthPhysicalTargetKind.Wall
                        ? VectorWallForceMultiplier
                        : 1f;
                    float speedLimit = target.TargetKind == EarthPhysicalTargetKind.Wall
                        ? VectorWallSpeedLimit
                        : VectorRockSpeedLimit;
                    EarthVectorFieldSample sample = EarthVectorFieldSolver.Solve(
                        ToFloat3(target.Body.linearVelocity),
                        mass,
                        ToFloat3(direction),
                        1f,
                        impulse * multiplier / Mathf.Max(0.0001f, Time.fixedDeltaTime),
                        speedLimit,
                        Time.fixedDeltaTime);
                    Vector3 delta = ToVector3(sample.VelocityChange);
                    target.Body.AddForce(delta, ForceMode.VelocityChange);
                    velocityChange = delta.magnitude;
                }
                LastMagicPushVelocityChange = velocityChange;
                MagicPushEvent pushed = new MagicPushEvent(
                    unchecked((uint)Time.frameCount),
                    ToFloat3(_vectorFieldPoint),
                    releaseStrength,
                    mass,
                    velocityChange,
                    target.TargetKind == EarthPhysicalTargetKind.Wall);
                Events.Emit(in pushed);
            }
            target.OnEarthMagicReleased(EarthMagicGripKind.VectorField);
            _vectorFieldTarget = null;
            _vectorFieldCharge = 0f;
            return valid;
        }

        public void CancelVectorField()
        {
            if (_vectorFieldTarget != null)
                _vectorFieldTarget.OnEarthMagicReleased(EarthMagicGripKind.VectorField);
            _vectorFieldTarget = null;
            _vectorFieldCharge = 0f;
        }

        public bool TryApplyMagicPush(
            Rigidbody body,
            EarthWall wall,
            Vector3 point,
            Vector3 direction,
            float charge)
        {
            float boundedCharge = Mathf.Clamp01(charge);
            float impulse = Mathf.Lerp(145f, 1150f, Mathf.Pow(boundedCharge, 1.45f));
            float mass;
            float velocityChange;
            bool pushedWall = wall != null;
            if (pushedWall)
            {
                mass = wall.EstimatedMass;
                velocityChange = wall.ApplyMagicPush(direction, impulse * wallPushLeverage);
            }
            else
            {
                if (body == null || body.isKinematic || IsCharacterBody(body)) return false;
                mass = Mathf.Max(0.01f, body.mass);
                velocityChange = impulse / mass;
                body.AddForceAtPosition(direction.normalized * impulse, point, ForceMode.Impulse);
            }

            LastMagicPushVelocityChange = velocityChange;
            MagicPushEvent pushed = new MagicPushEvent(
                unchecked((uint)Time.frameCount),
                ToFloat3(point),
                boundedCharge,
                mass,
                velocityChange,
                pushedWall);
            Events.Emit(in pushed);
            return true;
        }

        private void FixedUpdate()
        {
            UpdateHeldFractureFormation();
            if (_gravityWellActive && _repairController == null) ApplyGravityWell();
            IEarthPhysicalTarget target = _vectorFieldTarget;
            if (target == null) return;
            if (!target.IsEarthTargetValid || target.Body == null)
            {
                CancelVectorField();
                return;
            }

            using (VectorFieldMarker.Auto())
            {
                float multiplier = target.TargetKind == EarthPhysicalTargetKind.Wall
                    ? VectorWallForceMultiplier
                    : 1f;
                float speedLimit = target.TargetKind == EarthPhysicalTargetKind.Wall
                    ? VectorControlledWallSpeedLimit
                    : VectorControlledRockSpeedLimit;
                Vector3 direction = FieldDirectionFor(target);
                EarthVectorFieldSample sample = EarthVectorFieldSolver.Solve(
                    ToFloat3(target.Body.linearVelocity),
                    target.EarthMass,
                    ToFloat3(direction),
                    _vectorFieldCharge,
                    VectorContinuousForce * multiplier,
                    speedLimit,
                    Time.fixedDeltaTime);
                Vector3 delta = ToVector3(sample.VelocityChange);
                if (delta.sqrMagnitude > 0f) target.Body.AddForce(delta, ForceMode.VelocityChange);
                LastMagicPushVelocityChange = delta.magnitude / Mathf.Max(0.0001f, Time.fixedDeltaTime);
            }
        }

        private void UpdateHeldFractureFormation()
        {
            if (!HasHeldFractureCluster) return;
            for (int index = 0; index < _heldFractureCluster.Length; index++)
            {
                EarthFragment chunk = _heldFractureCluster[index];
                if (chunk == null) continue;
                chunk.UpdateBendTarget(
                    _heldFractureCenter + _heldFractureOffsets[index], Vector3.zero, 0.55f);
            }
        }

        public void Configure(
            VoxelPlanetBehaviour configuredVoxelPlanet,
            EarthFragmentPool configuredPool,
            Transform configuredPlanetCenter,
            EarthWallPool configuredWallPool = null,
            Transform configuredHeldFragmentAnchor = null)
        {
            if (wallPool != null) wallPool.WallCollapsed -= HandleWallCollapsed;
            if (voxelPlanet != null) voxelPlanet.EditCommitted -= HandleExtractionEditCommitted;
            voxelPlanet = configuredVoxelPlanet;
            fragmentPool = configuredPool;
            planetCenter = configuredPlanetCenter;
            wallPool = configuredWallPool;
            heldFragmentAnchor = configuredHeldFragmentAnchor;
            if (wallPool != null) wallPool.WallCollapsed += HandleWallCollapsed;
            AttachExtractionCommitListener();
            matterReturnController?.Configure(voxelPlanet, matterKernel, EarthMaterialDensity);
        }

        private void AttachExtractionCommitListener()
        {
            if (voxelPlanet == null) return;
            // Unity serializes the VoxelPlanet reference, not C# event subscriptions.
            // Rebind idempotently whenever the runtime wakes or authoring replaces it.
            voxelPlanet.EditCommitted -= HandleExtractionEditCommitted;
            voxelPlanet.EditCommitted += HandleExtractionEditCommitted;
        }

        public void ConfigureTelekinesis(EarthTelekinesisController configuredTelekinesis)
        {
            telekinesis = configuredTelekinesis;
        }

        public void ConfigureEarthExtensions(
            EarthVectorFieldProfile configuredVectorFieldProfile,
            EarthPlatformPool configuredPlatformPool,
            EarthGravityWellProfile configuredGravityWellProfile = null)
        {
            vectorFieldProfile = configuredVectorFieldProfile;
            if (platformPool != null) platformPool.PlatformFractured -= HandlePlatformFractured;
            platformPool = configuredPlatformPool;
            if (platformPool != null) platformPool.PlatformFractured += HandlePlatformFractured;
            gravityWellProfile = configuredGravityWellProfile;
        }

        private void OnDestroy()
        {
            if (wallPool != null) wallPool.WallCollapsed -= HandleWallCollapsed;
            if (platformPool != null) platformPool.PlatformFractured -= HandlePlatformFractured;
            if (matterReturnController != null)
                matterReturnController.ReturnStageChanged -= HandleReturnStageForCombo;
            if (voxelPlanet != null) voxelPlanet.EditCommitted -= HandleExtractionEditCommitted;
        }

        private void ApplyGravityWell()
        {
            using (GravityWellMarker.Auto())
            {
                _gravityWellElapsed += Time.fixedDeltaTime;
                if (!_gravityGestureControlled) StressGravityWellStructure();
                _gravityWellBodies.Clear();
                CaptureFracturedStructureTargets();
                Vector3 planetPosition = planetCenter != null ? planetCenter.position : Vector3.zero;
                for (int targetIndex = _gravityGripSession.Count - 1; targetIndex >= 0; targetIndex--)
                {
                    IEarthPhysicalTarget target = _gravityGripSession.GetTarget(targetIndex);
                    if (target == null || !target.IsEarthTargetValid || target.Body == null)
                    {
                        target?.OnEarthMagicReleased(EarthMagicGripKind.GravityWell);
                        _gravityGripSession.RemoveAtSwapBack(targetIndex);
                        continue;
                    }
                    Rigidbody body = target.Body;
                    if (!_gravityWellBodies.Add(body)) continue;
                    Vector3 localUp = body.worldCenterOfMass - planetPosition;
                    if (localUp.sqrMagnitude < 0.01f) localUp = _gravityWellUp;
                    localUp.Normalize();
                    float clusterRadius = _gravityThrowCharging
                        ? EarthGravityClusterThrowSolver.CompressedRadius(
                            GravityClusterOrbitRadius, _gravityThrowCharge01)
                        : GravityClusterOrbitRadius;
                    Vector3 viewForward = _gravityWellViewForward.sqrMagnitude > 0.01f
                        ? _gravityWellViewForward
                        : Vector3.ProjectOnPlane(_gravityWellFocus - transform.position, localUp).normalized;
                    float objectClearance = Mathf.Lerp(
                        0.04f,
                        0.62f,
                        Mathf.InverseLerp(12f, 1100f, Mathf.Max(0f, body.mass)));
                    float3 offset = EarthGravityGripSolver.CameraAwareSlotOffset(
                        target.StableEarthId,
                        clusterRadius,
                        ToFloat3(localUp),
                        ToFloat3(viewForward),
                        objectClearance);
                    EarthGravityGripSample sample = EarthGravityGripSolver.Solve(
                        ToFloat3(body.worldCenterOfMass),
                        ToFloat3(body.linearVelocity),
                        ToFloat3(body.angularVelocity),
                        ToFloat3(_gravityWellFocus) + offset,
                        ToFloat3(-localUp * 11.5f),
                        GravityClusterStiffness,
                        GravityClusterDamping,
                        GravityClusterAngularDamping,
                        GravityClusterMaximumAcceleration,
                        GravityMaximumSpeed,
                        Time.fixedDeltaTime);
                    body.AddForce(ToVector3(sample.Acceleration), ForceMode.Acceleration);
                    body.AddTorque(ToVector3(sample.AngularAcceleration), ForceMode.Acceleration);
                    body.WakeUp();
                }
            }
        }

        private void ApplyGestureRepair()
        {
            if (_gravityFractureSource is EarthArenaStructure arenaStructure)
            {
                _gravityGripSession.ReleaseAll(EarthMagicGripKind.GravityWell);
                _gravityWellBodies.Clear();
                arenaStructure.SetMagicRepairProgress(_gravityStructurePhase);
                return;
            }
            bool wallRepairable = _gravityWellWall != null && _gravityWellWall.IsCollapsing;
            bool platformRepairable = _gravityWellPlatform != null && _gravityWellPlatform.IsFractured;
            if ((!wallRepairable && !platformRepairable) || _gravityStructurePhase <= 0f) return;
            if (_repairController == null)
            {
                _gravityGripSession.ReleaseAll(EarthMagicGripKind.GravityWell);
                _gravityWellBodies.Clear();
                _repairController = wallRepairable
                    ? (IEarthRepairController)_gravityWellWall.Reassembly
                    : _gravityWellPlatform.RepairController;
                if (_repairController == null || !_repairController.TryBeginRepair(
                        unchecked((uint)Time.frameCount), _gravityStructurePhase))
                {
                    _repairController = null;
                    return;
                }
            }
            else
            {
                _repairController.SetTargetProgress(
                    _gravityStructurePhase,
                    unchecked((uint)Time.frameCount));
            }
        }

        private void ApplyGestureDisassembly()
        {
            if (_gravityStructurePhase <= 0f) return;
            if (_repairController != null)
            {
                _repairController.Interrupt(
                    EarthRepairInterruptReason.ExplicitCancel,
                    unchecked((uint)Time.frameCount));
                _repairController = null;
            }
            Component genericComponent = _gravityFractureSource as Component;
            Vector3 structurePosition = _gravityWellWall != null
                ? _gravityWellWall.transform.position
                : _gravityWellPlatform != null
                    ? _gravityWellPlatform.transform.position
                    : genericComponent != null
                        ? genericComponent.transform.position
                        : _gravityWellFocus - _gravityWellUp;
            Vector3 direction = SafeDirection(_gravityWellFocus - structurePosition);
            if (_gravityWellWall != null)
            {
                _gravityWellWall.SetMagicDisassemblyProgress(
                    _gravityStructurePhase, _gravityWellFocus, direction);
                _gravityWellFracturedStructure = _gravityWellWall.IsCollapsing;
                CaptureFracturedStructureTargets();
                return;
            }
            if (_gravityFractureSource is EarthArenaStructure arenaStructure)
            {
                arenaStructure.SetMagicDisassemblyProgress(
                    _gravityStructurePhase, _gravityWellFocus, direction);
                _gravityWellFracturedStructure = arenaStructure.IsFractured;
                CaptureFracturedStructureTargets();
                return;
            }
            if (_gravityWellPlatform == null ||
                _gravityStructurePhase <= _gravityPlatformDisassemblyPhase) return;
            _gravityPlatformDisassemblyPhase = _gravityStructurePhase;
            _gravityWellFracturedStructure |= _gravityWellPlatform.ApplyStructureImpact(
                _gravityWellFocus,
                direction,
                Mathf.Lerp(
                    GravityFractureImpulse * 0.20f,
                    GravityFractureImpulse * 1.25f,
                    Mathf.Pow(_gravityStructurePhase, 0.72f)));
            if (_gravityWellFracturedStructure) CaptureFracturedStructureTargets();
        }

        private void CaptureFracturedStructureTargets()
        {
            IEarthFractureSource source = _gravityFractureSource;
            if (source == null || !source.IsFractured) return;
            int count = source.CopyActiveTargetsNonAlloc(_gravityWellStructureTargets);
            for (int index = 0; index < count; index++)
            {
                IEarthPhysicalTarget target = _gravityWellStructureTargets[index];
                if (_gravityGestureControlled &&
                    _gravityStructureIntent == EarthGravityStructureIntent.Disassemble &&
                    target is EarthWallPiece wallPiece &&
                    _gravityWellWall != null &&
                    _gravityWellWall.IsPieceStructurallySupported(wallPiece.PieceIndex))
                {
                    _gravityWellStructureTargets[index] = null;
                    continue;
                }
                TryLatchGravityTarget(target);
                _gravityWellStructureTargets[index] = null;
            }
        }

        private void TryLatchGravityTarget(IEarthPhysicalTarget target)
        {
            if (target == null || !target.IsEarthTargetValid || target.Body == null ||
                _gravityGripSession.Count >= GravityMaximumCapturedTargets) return;
            if (_gravityGripSession.TryAdd(target, GravityMaximumCapturedTargets))
                target.OnEarthMagicGrabbed(EarthMagicGripKind.GravityWell);
        }

        private void StressGravityWellStructure()
        {
            IEarthDamageableStructure genericStructure =
                _gravityFractureSource as IEarthDamageableStructure;
            Component genericComponent = _gravityFractureSource as Component;
            if (!_gravityWellFracturedStructure && _gravityWellElapsed >= GravityFractureDelay)
            {
                Vector3 direction = SafeDirection(_gravityWellFocus -
                    (_gravityWellWall != null ? _gravityWellWall.transform.position :
                     _gravityWellPlatform != null ? _gravityWellPlatform.transform.position :
                     genericComponent != null ? genericComponent.transform.position :
                     _gravityWellFocus - _gravityWellUp));
                bool fractured = _gravityWellWall != null &&
                                  _gravityWellWall.ApplyStructureImpact(
                                      _gravityWellFocus, direction, GravityFractureImpulse);
                fractured |= _gravityWellPlatform != null &&
                              _gravityWellPlatform.ApplyStructureImpact(
                                  _gravityWellFocus, direction, GravityFractureImpulse);
                if (_gravityWellWall == null && _gravityWellPlatform == null && genericStructure != null)
                {
                    var impact = new EarthStructureImpact(
                        _gravityWellFocus,
                        direction,
                        GravityFractureImpulse,
                        EarthStructureImpactKind.Pluck);
                    fractured = genericStructure.ApplyEarthImpact(in impact);
                }
                _gravityWellFracturedStructure = fractured ||
                                                  (_gravityWellWall == null &&
                                                   _gravityWellPlatform == null &&
                                                   genericStructure == null);
            }
            if (!_gravityWellFracturedStructure) return;
            float impulse = GravitySustainedImpulse * Time.fixedDeltaTime;
            Vector3 pull = SafeDirection(_gravityWellFocus -
                (_gravityWellWall != null ? _gravityWellWall.transform.position :
                 _gravityWellPlatform != null ? _gravityWellPlatform.transform.position :
                 genericComponent != null ? genericComponent.transform.position :
                 _gravityWellFocus - _gravityWellUp));
            _gravityWellWall?.ApplyStructureImpact(_gravityWellFocus, pull, impulse);
            _gravityWellPlatform?.ApplyStructureImpact(_gravityWellFocus, pull, impulse);
            if (_gravityWellWall == null && _gravityWellPlatform == null && genericStructure != null)
            {
                var sustained = new EarthStructureImpact(
                    _gravityWellFocus,
                    pull,
                    impulse,
                    EarthStructureImpactKind.Pluck);
                genericStructure.ApplyEarthImpact(in sustained);
            }
        }

        private IEarthPhysicalTarget ResolveExplicitGravityTarget(Collider hitCollider)
        {
            if (hitCollider == null) return null;
            EarthWallPiece wallPiece = hitCollider.GetComponentInParent<EarthWallPiece>();
            if (wallPiece != null) return wallPiece;
            EarthPlatformPiece platformPiece = hitCollider.GetComponentInParent<EarthPlatformPiece>();
            if (platformPiece != null) return platformPiece;
            EarthFragment fragment = hitCollider.GetComponentInParent<EarthFragment>();
            if (fragment != null) return fragment;
            EarthPillarWaveColumn pillar = hitCollider.GetComponentInParent<EarthPillarWaveColumn>();
            if (pillar != null) return pillar;
            EarthArmorPiece armorPiece = hitCollider.GetComponentInParent<EarthArmorPiece>();
            if (armorPiece != null) return armorPiece;
            EarthArenaPiece arenaPiece = hitCollider.GetComponentInParent<EarthArenaPiece>();
            if (arenaPiece != null) return arenaPiece;
            EarthWall wall = hitCollider.GetComponentInParent<EarthWall>();
            if (wall != null) return wall;
            PhysicalImpactTarget physical = hitCollider.GetComponentInParent<PhysicalImpactTarget>();
            return physical;
        }

        public void ConfigureWallProfile(
            float minimumHeight,
            float maximumHeight,
            float maximumLength = 22f,
            float baseThickness = 0.95f)
        {
            wallMinimumHeight = Mathf.Max(0.1f, minimumHeight);
            wallMaximumHeight = Mathf.Max(wallMinimumHeight, maximumHeight);
            wallMaxLength = Mathf.Max(1f, maximumLength);
            wallThickness = Mathf.Max(0.05f, baseThickness);
        }

        public void ConfigureRecipes(CompiledAbilityRecipe[] recipes)
        {
            _recipes.Clear();
            if (recipes == null)
            {
                return;
            }

            for (int index = 0; index < recipes.Length; index++)
            {
                CompiledAbilityRecipe recipe = recipes[index];
                _recipes.Add(recipe.Id, recipe);
            }
        }

        public bool Execute(in MagicCommand command)
        {
            using (ExecuteMarker.Auto())
            {
                if (voxelPlanet == null || fragmentPool == null || planetCenter == null)
                {
                    return Reject(command, "Magic runtime is not configured.");
                }

                if (command.Element != ElementId.Earth)
                {
                    return Reject(command, "Only the Earth vertical slice is enabled in M3.");
                }

                if (!_recipes.TryGetValue(command.Ability, out CompiledAbilityRecipe recipe))
                {
                    return Reject(command, "Ability recipe is not registered.");
                }

                if (IsTerrainExtractionRecipe(recipe))
                {
                    if (!ExecuteTerrainExtraction(recipe, command)) return false;
                    Recorder.Record(in command);
                    SuccessfulCommandCount++;
                    comboRuntime?.RecordAbility(
                        command.Ability,
                        ResolveCommandMatter(command.Ability),
                        command.Tick,
                        command.Intensity,
                        command.Aim);
                    return true;
                }

                for (int index = 0; index < recipe.Operators.Length; index++)
                {
                    if (!ExecuteOperator(recipe.Operators[index], recipe, command))
                    {
                        return false;
                    }
                }

                Recorder.Record(in command);
                SuccessfulCommandCount++;
                comboRuntime?.RecordAbility(
                    command.Ability,
                    ResolveCommandMatter(command.Ability),
                    command.Tick,
                    command.Intensity,
                    command.Aim);
                return true;
            }
        }

        private EarthMatterId ResolveCommandMatter(AbilityId ability)
        {
            EarthMatterIdentity identity = null;
            if (ability == EarthAbilityIds.LineWall && wallPool != null && wallPool.LastAcquired != null)
                identity = wallPool.LastAcquired.GetComponent<EarthMatterIdentity>();
            else if (ability == EarthAbilityIds.RaisePlatform && platformPool != null && platformPool.LastAcquired != null)
                identity = platformPool.LastAcquired.GetComponent<EarthMatterIdentity>();
            else if (_heldFragment != null)
                identity = _heldFragment.MatterIdentity;
            return identity != null ? identity.MatterId : default;
        }

        public void BuildPreview(in MagicCommand command, List<Vector3> output)
        {
            output.Clear();
            LastPreviewGeometryHash = 0UL;
            if (!_recipes.TryGetValue(command.Ability, out CompiledAbilityRecipe recipe))
            {
                return;
            }

            if (command.Ability == EarthAbilityIds.LineWall)
            {
                FixedList4096Bytes<float3> footprint = EarthGeometryBuilder.BuildWallFootprint(
                    in command, wallMaxLength);
                LastPreviewGeometryHash = EarthGeometryBuilder.ComputeFootprintHash(footprint);
                for (int index = 0; index < footprint.Length; index++)
                {
                    float3 point = footprint[index];
                    float3 up = math.normalizesafe(point - ToFloat3(planetCenter.position), command.Aim);
                    output.Add(ToVector3(point + (up * 0.05f)));
                }
            }
            else if (command.Ability == EarthAbilityIds.RaisePlatform)
            {
                EarthPlatformGeometry geometry = BuildPlatformGeometry(in command);
                if (!geometry.IsValid) return;
                for (int index = 0; index < geometry.Polygon.Length; index++)
                {
                    float2 local = geometry.Polygon[index];
                    float3 point = geometry.Center + (geometry.Right * local.x) +
                                   (geometry.Forward * local.y) + (geometry.Up * 0.05f);
                    output.Add(ToVector3(point));
                }
                if (output.Count > 0) output.Add(output[0]);
            }
            else if (command.Ability == EarthAbilityIds.PullRock)
            {
                float extractionRadius = EarthGeometryBuilder.ExtractionRadius(
                    recipe.Radius, command.Intensity);
                EarthExtractionGeometry extraction = EarthGeometryBuilder.BuildExtraction(
                    in command, ToFloat3(planetCenter.position), extractionRadius);
                output.Add(ToVector3(extraction.SurfaceAnchor));
                output.Add(ToVector3(extraction.Center));
            }
            else if (command.Ability == EarthAbilityIds.FlickThrow && HeldFragment != null)
            {
                Vector3 start = HeldFragment.transform.position;
                Vector3 direction = ToVector3(command.Aim).normalized;
                float velocityChange = ThrowVelocityChange(recipe, command.Intensity);
                Vector3 velocity = direction * velocityChange;
                Vector3 up = (start - planetCenter.position).normalized;
                for (int index = 0; index < 12; index++)
                {
                    float time = index * (0.72f / 11f);
                    output.Add(start + (velocity * time) - (up * (0.5f * 14f * time * time)));
                }
            }
            else
            {
                float3 anchor = EarthGeometryBuilder.GetAnchor(in command);
                output.Add(ToVector3(anchor));
                output.Add(ToVector3(anchor + (command.Aim * recipe.Radius)));
            }
        }

        public bool TryGetPreviewMetrics(AbilityId ability, out MagicPreviewMetrics metrics)
        {
            metrics = default;
            if (!_recipes.TryGetValue(ability, out CompiledAbilityRecipe recipe)) return false;
            float mass = 0f;
            if (ability == EarthAbilityIds.PullRock)
            {
                float volume = (4f / 3f) * math.PI * recipe.Radius * recipe.Radius * recipe.Radius;
                mass = volume * earthMaterialDensity;
            }
            metrics = new MagicPreviewMetrics(ability, recipe.Radius, mass);
            return true;
        }

        public void HandleFragmentImpact(EarthFragment fragment, Collision collision, float impulse)
        {
            if (collision.contactCount == 0)
            {
                return;
            }

            ContactPoint contact = collision.GetContact(0);
            PhysicalImpactTarget target = collision.collider != null
                ? collision.collider.GetComponentInParent<PhysicalImpactTarget>()
                : null;
            EarthCharacterImpactTarget characterTarget = collision.collider != null
                ? collision.collider.GetComponentInParent<EarthCharacterImpactTarget>()
                : null;
            Vector3 direction = fragment.Body.linearVelocity.sqrMagnitude > 0.0001f
                ? fragment.Body.linearVelocity.normalized
                : -contact.normal;
            EarthWall wall = collision.collider != null
                ? collision.collider.GetComponentInParent<EarthWall>()
                : null;
            if (wall == null && collision.collider != null)
                wall = collision.collider.GetComponent<EarthWallPiece>()?.Owner;
            EarthPlatform platform = collision.collider != null
                ? collision.collider.GetComponentInParent<EarthPlatform>()
                : null;
            if (platform == null && collision.collider != null)
                platform = collision.collider.GetComponent<EarthPlatformPiece>()?.Owner;
            EarthArenaStructure arenaStructure = collision.collider != null
                ? collision.collider.GetComponentInParent<EarthArenaStructure>()
                : null;
            if (arenaStructure == null && collision.collider != null)
                arenaStructure = collision.collider.GetComponentInParent<EarthArenaPiece>()?.Owner;
            Collider terrainCollider = planetCenter != null ? planetCenter.GetComponent<Collider>() : null;
            bool terrainHit = terrainCollider != null && collision.collider != null &&
                              (collision.collider == terrainCollider ||
                               collision.collider.transform.IsChildOf(terrainCollider.transform));
            wall?.ApplyRockImpact(contact.point, direction, impulse);
            platform?.ApplyStructureImpact(contact.point, direction, impulse);
            if (arenaStructure != null)
            {
                var arenaImpact = new EarthStructureImpact(
                    contact.point,
                    direction,
                    impulse,
                    EarthStructureImpactKind.Projectile,
                    fragment.FragmentId);
                arenaStructure.ApplyEarthImpact(in arenaImpact);
            }
            EarthDestructibleDecorRock decorRock = collision.collider != null
                ? collision.collider.GetComponentInParent<EarthDestructibleDecorRock>()
                : null;
            if (decorRock != null)
            {
                decorRock.ApplyImpact(contact.point, direction, impulse);
                target = null;
            }
            // A controlled rock touching the ground is the accretion gesture. It must not
            // self-destruct from the PD controller's contact impulse; only released rocks
            // shatter and carve craters on impact.
            if (terrainHit && !fragment.IsHeld)
                fragment.TryShatter(contact.point, contact.normal, impulse);
            if (characterTarget != null)
            {
                characterTarget.ApplyImpact(
                    contact.point,
                    direction,
                    impulse,
                    EarthCharacterImpactSourceKind.LooseStone,
                    fragment.FragmentId,
                    fragment.Body != null ? fragment.Body.linearVelocity.magnitude : 0f);
                target = null;
            }
            ApplyFragmentImpact(
                fragment, contact.point, contact.normal, impulse, target, direction,
                terrainHit && wall == null);
        }

        public void HandleFragmentSweptImpact(
            EarthFragment fragment,
            Collider hitCollider,
            Vector3 point,
            Vector3 normal,
            float impulse)
        {
            if (fragment == null || hitCollider == null) return;
            Vector3 direction = fragment.Body != null && fragment.Body.linearVelocity.sqrMagnitude > 0.0001f
                ? fragment.Body.linearVelocity.normalized
                : -normal;
            EarthWall wall = hitCollider.GetComponentInParent<EarthWall>();
            if (wall == null) wall = hitCollider.GetComponent<EarthWallPiece>()?.Owner;
            EarthPlatform platform = hitCollider.GetComponentInParent<EarthPlatform>();
            if (platform == null) platform = hitCollider.GetComponent<EarthPlatformPiece>()?.Owner;
            EarthArenaStructure arenaStructure = hitCollider.GetComponentInParent<EarthArenaStructure>();
            if (arenaStructure == null)
                arenaStructure = hitCollider.GetComponentInParent<EarthArenaPiece>()?.Owner;
            PhysicalImpactTarget physical = hitCollider.GetComponentInParent<PhysicalImpactTarget>();
            EarthCharacterImpactTarget character = hitCollider.GetComponentInParent<EarthCharacterImpactTarget>();
            EarthDestructibleDecorRock decorRock =
                hitCollider.GetComponentInParent<EarthDestructibleDecorRock>();
            wall?.ApplyRockImpact(point, direction, impulse);
            platform?.ApplyStructureImpact(point, direction, impulse);
            if (arenaStructure != null)
            {
                var arenaImpact = new EarthStructureImpact(
                    point,
                    direction,
                    impulse,
                    EarthStructureImpactKind.Projectile,
                    fragment.FragmentId);
                arenaStructure.ApplyEarthImpact(in arenaImpact);
            }
            if (decorRock != null)
            {
                decorRock.ApplyImpact(point, direction, impulse);
                physical = null;
            }
            if (character != null)
            {
                character.ApplyImpact(
                    point,
                    direction,
                    impulse,
                    EarthCharacterImpactSourceKind.LooseStone,
                    fragment.FragmentId,
                    fragment.Body != null ? fragment.Body.linearVelocity.magnitude : 0f);
                physical = null;
            }
            ApplyFragmentImpact(fragment, point, normal, impulse, physical, direction, false);
        }

        public void TryAccreteHeldFragment(EarthFragment fragment)
        {
            if (fragment == null || fragment != HeldFragment || fragment.Profile == null ||
                fragmentPool == null || voxelPlanet == null || planetCenter == null) return;
            EarthRockProfile profile = fragment.Profile;
            if (fragment.Body == null ||
                fragment.Body.linearVelocity.magnitude > profile.MaximumAccretionSpeed) return;
            Collider terrainCollider = planetCenter.GetComponent<Collider>();
            if (terrainCollider == null) return;
            Vector3 localUp = (fragment.Body.worldCenterOfMass - planetCenter.position).normalized;
            if (localUp.sqrMagnitude < 0.5f) localUp = fragment.transform.up;
            Vector3 surfacePoint = terrainCollider.ClosestPoint(fragment.Body.worldCenterOfMass);
            float clearance = Vector3.Distance(fragment.Body.worldCenterOfMass, surfacePoint) - fragment.Radius;
            if (clearance > profile.AccretionSurfaceDistance ||
                !fragment.TryReserveAccretionPulse(Time.fixedTime, out float volume)) return;

            float extractionRadius = Mathf.Pow((volume * 3f) / (4f * Mathf.PI), 1f / 3f);
            Vector3 localPoint = voxelPlanet.transform.InverseTransformPoint(surfacePoint);
            voxelPlanet.ApplySphereEdit(localPoint, extractionRadius, false);
            fragmentPool.EmitAccretion(fragment, surfacePoint, localUp, volume);
            TerrainEditedEvent edited = new TerrainEditedEvent(
                unchecked((uint)Time.frameCount),
                EarthAbilityIds.PullRock,
                ToFloat3(surfacePoint),
                extractionRadius);
            Events.Emit(in edited);
        }

        public void ApplyFragmentImpact(
            EarthFragment fragment,
            Vector3 point,
            Vector3 normal,
            float impulse)
        {
            ApplyFragmentImpact(fragment, point, normal, impulse, null, -normal);
        }

        public void ApplyFragmentImpact(
            EarthFragment fragment,
            Vector3 point,
            Vector3 normal,
            float impulse,
            PhysicalImpactTarget physicalTarget,
            Vector3 impactDirection,
            bool editTerrain = true)
        {
            physicalTarget?.ApplyImpact(point, impactDirection, impulse);
            if (editTerrain)
            {
                float radius = fragment != null
                    ? fragment.ComputeCraterRadius(impulse)
                    : Mathf.Clamp(impulse * 0.0025f, 0.25f, 1.25f);
                Vector3 localPoint = voxelPlanet.transform.InverseTransformPoint(point);
                voxelPlanet.ApplySphereEdit(localPoint, radius, false);
            }
            ImpactEvent impactEvent = new ImpactEvent(
                unchecked((uint)Time.frameCount),
                fragment.FragmentId,
                impulse,
                ToFloat3(point),
                ToFloat3(normal));
            Events.Emit(in impactEvent);
            float mass = fragment != null ? fragment.Mass : 0f;
            float relativeSpeed = fragment != null && fragment.Body != null
                ? fragment.Body.linearVelocity.magnitude
                : (mass > 0.001f ? impulse / mass : 0f);
            EarthImpactEvent earthImpact = new EarthImpactEvent(
                unchecked((uint)Time.frameCount),
                fragment != null ? fragment.FragmentId : 0u,
                impulse,
                0.5f * mass * relativeSpeed * relativeSpeed,
                mass,
                relativeSpeed,
                ToFloat3(point),
                ToFloat3(normal),
                mass >= 250f ? EarthImpactMaterialKind.HeavyBlock : EarthImpactMaterialKind.LooseStone);
            Events.Emit(in earthImpact);
        }

        private bool ExecuteOperator(
            MagicOperatorKind operation,
            CompiledAbilityRecipe recipe,
            in MagicCommand command)
        {
            switch (operation)
            {
                case MagicOperatorKind.AddSolid:
                    return command.Ability == EarthAbilityIds.RaisePlatform
                        ? ExecutePlatform(command)
                        : ExecuteWall(recipe, command);
                case MagicOperatorKind.SubtractSolid:
                    return ExecuteSubtract(recipe, command);
                case MagicOperatorKind.SpawnFragment:
                    return ExecuteSpawnFragment(recipe, command);
                case MagicOperatorKind.ApplyImpulse:
                    return ExecuteFlick(recipe, command);
                default:
                    return Reject(command, $"Unsupported operator {operation}.");
            }
        }

        private bool ExecuteWall(CompiledAbilityRecipe recipe, in MagicCommand command)
        {
            if (wallPool == null)
            {
                return Reject(command, "Earth wall runtime is not configured.");
            }

            FixedList4096Bytes<float3> footprint = EarthGeometryBuilder.BuildWallFootprint(
                in command, wallMaxLength);
            if (footprint.Length < 2)
            {
                return Reject(command, "Line Wall needs at least two projected path points.");
            }
            LastCommittedGeometryHash = EarthGeometryBuilder.ComputeFootprintHash(footprint);

            Vector3 start = ToVector3(footprint[0]);
            Vector3 end = ToVector3(footprint[footprint.Length - 1]);
            float committedHeight = Mathf.Lerp(
                Mathf.Min(wallMinimumHeight, wallMaximumHeight),
                Mathf.Max(wallMinimumHeight, wallMaximumHeight),
                Mathf.Pow(command.Intensity, 0.78f));
            float thickness01 = command.Modifiers != 0u
                ? EarthTechniqueParameterCodec.UnpackSecondary(command.Modifiers)
                : 0.5f;
            float committedThickness = wallThickness * Mathf.Lerp(0.65f, 1.65f, thickness01);
            EarthWall wall = wallPool.Acquire(
                start,
                end,
                planetCenter.position,
                committedHeight,
                committedThickness,
                command.Tick);
            if (wall == null)
                return Reject(command, "Earth wall physical budget is full; return or destroy a structure first.");
            WallRaisedEvent raised = new WallRaisedEvent(
                command.Tick,
                wall.WallId,
                footprint[0],
                footprint[footprint.Length - 1],
                committedHeight,
                committedThickness);
            Events.Emit(in raised);
            return true;
        }

        public bool TryRaiseWallOnSurface(
            IReadOnlyList<float3> worldPath,
            Vector3 supportNormal,
            float height01,
            float thickness01,
            uint sourceTick,
            out EarthWall wall,
            uint supportStructureId = 0u,
            EarthSurfaceKind supportKind = EarthSurfaceKind.Invalid,
            uint supportGeneration = 0u,
            Vector3 supportTangent = default)
        {
            wall = null;
            if (wallPool == null || worldPath == null || worldPath.Count < 2 ||
                supportNormal.sqrMagnitude < 0.5f) return false;
            Vector3 start = ToVector3(worldPath[0]);
            Vector3 end = ToVector3(worldPath[worldPath.Count - 1]);
            Vector3 chord = end - start;
            if (chord.sqrMagnitude < 0.16f) return false;
            if (chord.magnitude > wallMaxLength) end = start + chord.normalized * wallMaxLength;
            float height = Mathf.Lerp(wallMinimumHeight, wallMaximumHeight, Mathf.Pow(Mathf.Clamp01(height01), 0.78f));
            float thickness = wallThickness * Mathf.Lerp(0.65f, 1.65f, Mathf.Clamp01(thickness01));
            wall = wallPool.Acquire(
                start,
                end,
                planetCenter != null ? planetCenter.position : Vector3.zero,
                height,
                thickness,
                sourceTick,
                supportNormal.normalized,
                supportStructureId);
            if (wall != null && supportStructureId != 0u)
            {
                IEarthFractureSource parent = supportKind == EarthSurfaceKind.WallSide ||
                                               supportKind == EarthSurfaceKind.WallTop
                    ? wallPool.FindActive(supportStructureId)
                    : platformPool != null ? platformPool.FindActive(supportStructureId) : null;
                if (parent != null && !ReferenceEquals(parent, wall))
                {
                    MonoBehaviour parentBehaviour = parent as MonoBehaviour;
                    Quaternion supportRotation = parentBehaviour != null
                        ? parentBehaviour.transform.rotation
                        : Quaternion.identity;
                    EarthConstructionFrameRuntime authoredFrame =
                        wall.GetComponent<EarthConstructionFrameRuntime>();
                    if (authoredFrame == null)
                        authoredFrame = wall.gameObject.AddComponent<EarthConstructionFrameRuntime>();
                    authoredFrame.Configure(
                        supportStructureId,
                        supportGeneration,
                        (start + end) * 0.5f,
                        supportNormal,
                        supportTangent.sqrMagnitude > 0.2f ? supportTangent : (end - start),
                        wall.transform.rotation,
                        supportRotation,
                        ConstructionOrientationMode.PreserveAuthoredFrame);
                    EarthStructureAttachment attachment = wall.GetComponent<EarthStructureAttachment>();
                    if (attachment == null) attachment = wall.gameObject.AddComponent<EarthStructureAttachment>();
                    attachment.Configure(wall, parent, (start + end) * 0.5f);
                }
            }
            if (wall == null) return false;
            SuccessfulCommandCount++;
            return true;
        }

        public bool TryRaisePlatformOnSurface(
            IReadOnlyList<float3> worldPath,
            Vector3 supportNormal,
            Vector3 supportTangent,
            float height01,
            uint sourceTick,
            out EarthPlatform platform,
            uint supportStructureId,
            uint supportGeneration,
            EarthSurfaceKind supportKind)
        {
            platform = null;
            if (platformPool == null || platformPool.Profile == null || worldPath == null ||
                worldPath.Count < 3 || supportNormal.sqrMagnitude < 0.5f) return false;
            Vector3 center = Vector3.zero;
            for (int index = 0; index < worldPath.Count; index++) center += ToVector3(worldPath[index]);
            center /= worldPath.Count;
            Vector3 planet = planetCenter != null ? planetCenter.position : Vector3.zero;
            Vector3 gravityUp = (center - planet).normalized;
            if (gravityUp.sqrMagnitude < 0.5f) gravityUp = Vector3.up;
            EarthPlatformProfile profile = platformPool.Profile;
            float requestedHeight = Mathf.Lerp(profile.MinimumHeight, profile.MaximumHeight, Mathf.Clamp01(height01));
            EarthPlatformGeometry geometry;
            float faceAlignment = Mathf.Abs(Vector3.Dot(supportNormal.normalized, gravityUp));
            if (faceAlignment >= 0.72f)
            {
                _platformPathScratch.Clear();
                for (int index = 0; index < worldPath.Count; index++) _platformPathScratch.Add(worldPath[index]);
                geometry = EarthPlatformGeometrySolver.Build(_platformPathScratch, ToFloat3(planet), 32);
            }
            else
            {
                Vector3 horizontal = Vector3.Cross(gravityUp, supportNormal).normalized;
                if (horizontal.sqrMagnitude < 0.5f)
                    horizontal = Vector3.ProjectOnPlane(supportTangent, gravityUp).normalized;
                float horizontalSpan = 0f;
                float verticalSpan = 0f;
                float minHorizontal = float.PositiveInfinity;
                float maxHorizontal = float.NegativeInfinity;
                float minVertical = float.PositiveInfinity;
                float maxVertical = float.NegativeInfinity;
                for (int index = 0; index < worldPath.Count; index++)
                {
                    Vector3 offset = ToVector3(worldPath[index]) - center;
                    float x = Vector3.Dot(offset, horizontal);
                    float y = Vector3.Dot(offset, gravityUp);
                    minHorizontal = Mathf.Min(minHorizontal, x);
                    maxHorizontal = Mathf.Max(maxHorizontal, x);
                    minVertical = Mathf.Min(minVertical, y);
                    maxVertical = Mathf.Max(maxVertical, y);
                }
                horizontalSpan = Mathf.Max(1.2f, maxHorizontal - minHorizontal);
                verticalSpan = Mathf.Max(0.35f, maxVertical - minVertical);
                requestedHeight = Mathf.Clamp(
                    Mathf.Lerp(0.46f, 1.15f, Mathf.Pow(Mathf.Clamp01(height01), 0.72f)),
                    profile.MinimumHeight,
                    Mathf.Min(profile.MaximumHeight, 1.4f));
                geometry = EarthCantileverPlatformSolver.Build(
                    ToFloat3(center),
                    ToFloat3(supportNormal),
                    ToFloat3(supportTangent),
                    ToFloat3(planet),
                    horizontalSpan,
                    verticalSpan,
                    requestedHeight);
            }
            if (!geometry.IsValid || geometry.Area < profile.MinimumArea || geometry.Area > profile.MaximumArea)
                return false;
            float embedDepth = faceAlignment >= 0.72f
                ? EarthPlatformGeometrySolver.RequiredChordEmbedDepth(
                    in geometry,
                    Mathf.Max(profile.MinimumEmbedDepth, profile.TopThickness * 0.45f),
                    profile.VisibleVoxelSafetyDepth)
                : Mathf.Max(0.12f, profile.MinimumEmbedDepth * 0.55f);
            platform = platformPool.Acquire(in geometry, requestedHeight, embedDepth);
            if (platform == null) return false;

            IEarthFractureSource parent = supportKind == EarthSurfaceKind.WallSide ||
                                           supportKind == EarthSurfaceKind.WallTop
                ? wallPool != null ? wallPool.FindActive(supportStructureId) : null
                : platformPool.FindActive(supportStructureId);
            if (parent != null && !ReferenceEquals(parent, platform))
            {
                MonoBehaviour parentBehaviour = parent as MonoBehaviour;
                Quaternion supportRotation = parentBehaviour != null
                    ? parentBehaviour.transform.rotation
                    : Quaternion.identity;
                EarthConstructionFrameRuntime authoredFrame =
                    platform.GetComponent<EarthConstructionFrameRuntime>();
                if (authoredFrame == null)
                    authoredFrame = platform.gameObject.AddComponent<EarthConstructionFrameRuntime>();
                authoredFrame.Configure(
                    supportStructureId,
                    supportGeneration,
                    center,
                    supportNormal,
                    supportTangent,
                    platform.transform.rotation,
                    supportRotation,
                    ConstructionOrientationMode.FollowSupportFrame);
                EarthStructureAttachment attachment = platform.GetComponent<EarthStructureAttachment>();
                if (attachment == null)
                    attachment = platform.gameObject.AddComponent<EarthStructureAttachment>();
                attachment.Configure(platform, parent, center);
            }
            SuccessfulCommandCount++;
            return true;
        }

        private bool ExecutePlatform(in MagicCommand command)
        {
            if (platformPool == null || platformPool.Profile == null)
                return Reject(command, "Earth platform runtime is not configured.");
            EarthPlatformGeometry geometry = BuildPlatformGeometry(in command);
            EarthPlatformProfile profile = platformPool.Profile;
            if (!geometry.IsValid || geometry.Area < profile.MinimumArea || geometry.Area > profile.MaximumArea)
                return Reject(command,
                    $"Platform area must stay between {profile.MinimumArea:0.0} and {profile.MaximumArea:0} m2.");
            float height = Mathf.Lerp(profile.MinimumHeight, profile.MaximumHeight, command.Intensity);
            float embedDepth = EarthPlatformGeometrySolver.RequiredChordEmbedDepth(
                in geometry,
                Mathf.Max(profile.MinimumEmbedDepth, profile.TopThickness * 0.45f),
                profile.VisibleVoxelSafetyDepth);
            EarthPlatform platform = platformPool.Acquire(
                in geometry,
                height,
                embedDepth);
            if (platform == null)
                return Reject(command, $"Platform limit reached ({profile.MaximumActivePlatforms}). Destroy one first.");
            return true;
        }

        private EarthPlatformGeometry BuildPlatformGeometry(in MagicCommand command)
        {
            _platformPathScratch.Clear();
            for (int index = 0; index < command.Path.Length; index++)
                _platformPathScratch.Add(command.Path[index]);
            return EarthPlatformGeometrySolver.Build(
                _platformPathScratch,
                ToFloat3(planetCenter != null ? planetCenter.position : Vector3.zero),
                32);
        }

        private void HandleWallCollapsed(EarthWall wall)
        {
            if (wall == null) return;
            WallCollapsedEvent collapsed = new WallCollapsedEvent(
                wall.SourceTick,
                wall.WallId,
                ToFloat3(wall.Start),
                ToFloat3(wall.End),
                wall.Height);
            Events.Emit(in collapsed);
            RecordFractureForCombo(wall, wall.SourceTick, wall.transform.forward);
        }

        private void HandlePlatformFractured(EarthPlatform platform)
        {
            if (platform == null) return;
            RecordFractureForCombo(
                platform,
                unchecked((uint)Time.frameCount),
                platform.transform.forward);
        }

        private void RecordFractureForCombo(
            IEarthFractureSource source,
            uint tick,
            Vector3 direction)
        {
            if (comboRuntime == null || source == null) return;
            int count = source.CopyActiveTargetsNonAlloc(_comboFractureTargets);
            EarthMatterId matter = default;
            for (int index = 0; index < count; index++)
            {
                IEarthPhysicalTarget target = _comboFractureTargets[index];
                _comboFractureTargets[index] = null;
                if (target == null || target.Body == null) continue;
                EarthMatterIdentity identity = target.Body.GetComponent<EarthMatterIdentity>() ??
                                               target.Body.GetComponentInParent<EarthMatterIdentity>();
                if (!matter.IsValid && identity != null && identity.MatterId.IsValid)
                    matter = identity.MatterId;
            }
            comboRuntime.RecordTechnique(
                EarthTechniqueId.FractureFan,
                matter,
                EarthEventTag.Fractured,
                tick,
                1f,
                direction);
        }

        private void HandleReturnStageForCombo(EarthReturnEvent value)
        {
            if (comboRuntime == null || value.Stage != EarthReturnEventStage.Completed) return;
            comboRuntime.RecordTechnique(
                EarthTechniqueId.SubsurfaceReturn,
                new EarthMatterId(value.MatterId, value.Generation),
                EarthEventTag.Reintegrated,
                value.Tick,
                value.Mass,
                Vector3.zero);
        }

        private bool ExecuteSubtract(CompiledAbilityRecipe recipe, in MagicCommand command)
        {
            float extractionRadius = EarthGeometryBuilder.ExtractionRadius(recipe.Radius, command.Intensity);
            EarthExtractionGeometry extraction = EarthGeometryBuilder.BuildExtraction(
                in command, ToFloat3(planetCenter.position), extractionRadius);
            Vector3 localAnchor = voxelPlanet.transform.InverseTransformPoint(ToVector3(extraction.Center));
            voxelPlanet.ApplySphereEdit(localAnchor, extractionRadius, false);
            TerrainEditedEvent edited = new TerrainEditedEvent(
                command.Tick, command.Ability, extraction.Center, extractionRadius);
            Events.Emit(in edited);
            return true;
        }

        private static bool IsTerrainExtractionRecipe(CompiledAbilityRecipe recipe)
        {
            return recipe.Operators.Length == 2 &&
                   recipe.Operators[0] == MagicOperatorKind.SubtractSolid &&
                   recipe.Operators[1] == MagicOperatorKind.SpawnFragment;
        }

        private bool ExecuteTerrainExtraction(
            CompiledAbilityRecipe recipe,
            in MagicCommand command)
        {
            if (_heldFragment != null || HasPendingExtraction)
                return Reject(command, "Finish controlling the current earth mass first.");

            float extractionRadius = EarthGeometryBuilder.ExtractionRadius(recipe.Radius, command.Intensity);
            EarthExtractionGeometry extraction = EarthGeometryBuilder.BuildExtraction(
                in command, ToFloat3(planetCenter.position), extractionRadius);
            float volume = (4f / 3f) * math.PI * extractionRadius * extractionRadius * extractionRadius;
            float mass = volume * earthMaterialDensity;
            Vector3 surface = ToVector3(extraction.SurfaceAnchor);
            Vector3 up = (surface - planetCenter.position).normalized;
            ResolveExtractionSurface(ref surface, up);
            Vector3 emergence = surface - up * Mathf.Max(0.05f, extractionRadius * 0.92f);

            // Reserve physical matter before touching the canonical SDF. A full pool
            // therefore rejects the cast without leaving a cavity behind.
            EarthFragment fragment = fragmentPool.ReserveExtraction(
                this, emergence, extractionRadius, mass);
            if (fragment == null)
                return Reject(command, "Earth matter physical budget is full; release or return a stone first.");

            Vector3 localAnchor = voxelPlanet.transform.InverseTransformPoint(
                ToVector3(extraction.Center));
            VoxelEditReceipt receipt = voxelPlanet.ApplySphereEditTransactional(
                localAnchor, extractionRadius, false);
            if (!receipt.IsValid)
            {
                fragment.MarkConsumedForPool();
                fragment.CompleteReintegration();
                return Reject(command, "Terrain extraction could not be staged.");
            }

            var transaction = new TerrainExtractionTransaction(
                receipt,
                fragment,
                command.Tick,
                command.Ability,
                ToVector3(extraction.Center),
                surface,
                up,
                emergence,
                extractionRadius,
                mass);
            _pendingExtractions.Add(transaction);
            _heldFragment = fragment;
            _pendingExtractionTarget = heldFragmentAnchor != null
                ? heldFragmentAnchor.position
                : emergence;
            _pendingExtractionVelocity = Vector3.zero;
            _pendingExtractionCharge = 0f;
            _pendingExtractionTuning = BendTuning.Default;
            _pendingExtractionRelease = false;
            _pendingExtractionCancel = false;
            return true;
        }

        private void HandleExtractionEditCommitted(VoxelEditReceipt receipt)
        {
            using (TerrainCommitMarker.Auto())
            {
                for (int index = _pendingExtractions.Count - 1; index >= 0; index--)
                {
                    TerrainExtractionTransaction transaction = _pendingExtractions[index];
                    if (!transaction.MarkVisualReady(receipt)) continue;
                    EarthFragment fragment = transaction.Fragment;
                    if (fragment == null)
                    {
                        transaction.MarkFailed();
                        _pendingExtractions.RemoveAt(index);
                        if (_heldFragment == fragment) _heldFragment = null;
                        return;
                    }

                    Vector3 emergenceSurface = transaction.SurfacePoint;
                    Collider emergenceCollider = ResolveExtractionSurface(
                        ref emergenceSurface, transaction.LocalUp);
                    fragment.CommitExtraction(
                        heldFragmentAnchor,
                        emergenceCollider != null
                            ? emergenceCollider
                            : planetCenter != null ? planetCenter.GetComponent<Collider>() : null,
                        emergenceSurface,
                        transaction.LocalUp,
                        transaction.Radius);
                    fragment.BeginBendControl(
                        _pendingExtractionTarget,
                        _pendingExtractionVelocity,
                        _pendingExtractionCharge,
                        in _pendingExtractionTuning);
                    transaction.MarkCommitted();
                    _pendingExtractions.RemoveAt(index);

                    TerrainEditedEvent edited = new TerrainEditedEvent(
                        transaction.Tick,
                        transaction.Ability,
                        ToFloat3(transaction.EditCenter),
                        transaction.Radius);
                    Events.Emit(in edited);
                    FragmentSpawnedEvent spawned = new FragmentSpawnedEvent(
                        transaction.Tick,
                        fragment.FragmentId,
                        transaction.Mass,
                        ToFloat3(transaction.EmergencePosition),
                        ToFloat3(transaction.SurfacePoint),
                        ToFloat3(transaction.EditCenter),
                        transaction.Radius);
                    Events.Emit(in spawned);

                    if (_pendingExtractionCancel)
                    {
                        _pendingExtractionCancel = false;
                        _pendingExtractionRelease = false;
                        fragment.StopBendControl();
                        if (_heldFragment == fragment) _heldFragment = null;
                    }
                    else if (_pendingExtractionRelease)
                    {
                        _pendingExtractionRelease = false;
                        ReleaseHeldFragment(
                            _pendingReleaseAim,
                            _pendingReleaseGestureVelocity,
                            _pendingReleaseCharge,
                            _pendingReleaseTick,
                            out _);
                    }
                    return;
                }
            }
        }

        private Collider ResolveExtractionSurface(ref Vector3 surface, Vector3 up)
        {
            if (up.sqrMagnitude < 0.5f) return null;
            up.Normalize();
            Ray ray = new Ray(surface + up * 4f, -up);
            int count = UnityEngine.Physics.RaycastNonAlloc(
                ray,
                _extractionSurfaceHits,
                8f,
                ~0,
                QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            Collider selected = null;
            Vector3 selectedPoint = surface;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = _extractionSurfaceHits[index];
                if (hit.collider == null || hit.distance >= nearest) continue;
                EarthArenaStructure structure =
                    hit.collider.GetComponentInParent<EarthArenaStructure>();
                if (structure == null) continue;
                nearest = hit.distance;
                selected = hit.collider;
                selectedPoint = hit.point;
            }
            if (selected != null) surface = selectedPoint;
            return selected;
        }

        private bool ExecuteSpawnFragment(CompiledAbilityRecipe recipe, in MagicCommand command)
        {
            float extractionRadius = EarthGeometryBuilder.ExtractionRadius(recipe.Radius, command.Intensity);
            EarthExtractionGeometry extraction = EarthGeometryBuilder.BuildExtraction(
                in command, ToFloat3(planetCenter.position), extractionRadius);
            float volume = (4f / 3f) * math.PI * extractionRadius * extractionRadius * extractionRadius;
            float mass = volume * earthMaterialDensity;
            Vector3 position = ToVector3(extraction.EmergencePosition);
            _heldFragment = fragmentPool.Acquire(
                this, position, extractionRadius, mass, heldFragmentAnchor);
            if (_heldFragment == null)
                return Reject(command, "Earth matter physical budget is full; release or return a stone first.");
            Vector3 surface = ToVector3(extraction.SurfaceAnchor);
            Vector3 up = (surface - planetCenter.position).normalized;
            _heldFragment.BeginSurfaceEmergence(
                planetCenter.GetComponent<Collider>(),
                surface,
                up,
                extractionRadius);
            FragmentSpawnedEvent spawned = new FragmentSpawnedEvent(
                command.Tick,
                _heldFragment.FragmentId,
                mass,
                ToFloat3(position),
                extraction.SurfaceAnchor,
                extraction.Center,
                extraction.Radius);
            Events.Emit(in spawned);
            return true;
        }

        private bool ExecuteFlick(CompiledAbilityRecipe recipe, in MagicCommand command)
        {
            if (_heldFragment == null || !_heldFragment.gameObject.activeSelf)
            {
                return Reject(command, "Flick Throw needs a held fragment from Pull Rock.");
            }

            float velocityChange = ThrowVelocityChange(recipe, command.Intensity);
            Vector3 direction = ToVector3(command.Aim).normalized;
            float normalizedCharge = Mathf.InverseLerp(
                minimumThrowVelocityChange,
                maximumThrowVelocityChange,
                velocityChange);
            return ReleaseHeldFragment(
                direction,
                Vector3.zero,
                normalizedCharge,
                command.Tick,
                out _);
        }

        private float ThrowVelocityChange(CompiledAbilityRecipe recipe, float intensity)
        {
            float raw = recipe.Strength * math.lerp(0.5f, 1.5f, math.saturate(intensity));
            return Mathf.Clamp(raw, minimumThrowVelocityChange, maximumThrowVelocityChange);
        }

        private bool Reject(in MagicCommand command, string reason)
        {
            AbilityRejectedEvent rejected = new AbilityRejectedEvent(command.Tick, command.Ability, reason);
            Events.Emit(in rejected);
            return false;
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private IEarthPhysicalTarget ResolveEarthTarget(Collider hitCollider, Rigidbody body)
        {
            if (hitCollider != null)
            {
                EarthWallPiece piece = hitCollider.GetComponentInParent<EarthWallPiece>();
                if (piece != null) return piece;
                EarthPlatformPiece platformPiece = hitCollider.GetComponentInParent<EarthPlatformPiece>();
                if (platformPiece != null) return platformPiece;
                EarthFragment fragment = hitCollider.GetComponentInParent<EarthFragment>();
                if (fragment != null) return fragment;
                EarthPillarWaveColumn pillar = hitCollider.GetComponentInParent<EarthPillarWaveColumn>();
                if (pillar != null) return pillar;
                EarthArmorPiece armorPiece = hitCollider.GetComponentInParent<EarthArmorPiece>();
                if (armorPiece != null) return armorPiece;
                EarthDestructibleDecorRock decorRock =
                    hitCollider.GetComponentInParent<EarthDestructibleDecorRock>();
                if (decorRock != null) return decorRock;
                EarthArenaPiece arenaPiece = hitCollider.GetComponentInParent<EarthArenaPiece>();
                if (arenaPiece != null) return arenaPiece;
                EarthWall wall = hitCollider.GetComponentInParent<EarthWall>();
                if (wall != null) return wall;
                PhysicalImpactTarget physical = hitCollider.GetComponentInParent<PhysicalImpactTarget>();
                if (physical != null) return physical;
            }
            return body != null ? new RigidbodyEarthTarget(body) : null;
        }

        private Vector3 FieldDirectionFor(IEarthPhysicalTarget target)
        {
            Vector3 direction = _vectorFieldDirection;
            if (target is EarthWall wall)
            {
                direction = Vector3.ProjectOnPlane(direction, wall.transform.up);
                if (direction.sqrMagnitude < 0.001f) direction = wall.transform.forward;
            }
            return SafeDirection(direction);
        }

        private static Vector3 SafeDirection(Vector3 direction) =>
            direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;

        private static bool IsCharacterBody(Rigidbody body) =>
            body != null &&
            (body.GetComponentInParent<PlanetMotor>() != null ||
             body.GetComponentInParent<ActiveRagdollPuppet>() != null);

        private float VectorContinuousForce => vectorFieldProfile != null ? vectorFieldProfile.ContinuousForce : 4200f;
        private float VectorMinimumReleaseImpulse => vectorFieldProfile != null ? vectorFieldProfile.MinimumReleaseImpulse : 260f;
        private float VectorMinimumWallReleaseImpulse => vectorFieldProfile != null ? vectorFieldProfile.MinimumWallReleaseImpulse : 650f;
        private float VectorMaximumReleaseImpulse => vectorFieldProfile != null ? vectorFieldProfile.MaximumReleaseImpulse : 2400f;
        private float VectorRockSpeedLimit => vectorFieldProfile != null ? vectorFieldProfile.RockSpeedLimit : 32f;
        private float VectorWallSpeedLimit => vectorFieldProfile != null ? vectorFieldProfile.WallSpeedLimit : 14f;
        private float VectorControlledRockSpeedLimit => vectorFieldProfile != null
            ? vectorFieldProfile.ControlledRockSpeedLimit
            : 9f;
        private float VectorControlledWallSpeedLimit => vectorFieldProfile != null
            ? vectorFieldProfile.ControlledWallSpeedLimit
            : 6.5f;
        private float VectorWallForceMultiplier => vectorFieldProfile != null ? vectorFieldProfile.WallForceMultiplier : 72f;
        private float GravityRadius => gravityWellProfile != null ? gravityWellProfile.Radius : 7.5f;
        private float GravityCoreRadius => gravityWellProfile != null ? gravityWellProfile.CoreRadius : 0.9f;
        private float GravityPullAcceleration => gravityWellProfile != null ? gravityWellProfile.PullAcceleration : 38f;
        private float GravityOrbitAcceleration => gravityWellProfile != null ? gravityWellProfile.OrbitAcceleration : 5.5f;
        private float GravityVelocityDamping => gravityWellProfile != null ? gravityWellProfile.VelocityDamping : 1.8f;
        private float GravityMaximumSpeed => gravityWellProfile != null ? gravityWellProfile.MaximumSpeed : 16f;
        private int GravityMaximumCapturedTargets => gravityWellProfile != null ? gravityWellProfile.MaximumCapturedTargets : 48;
        private float GravityClusterStiffness => gravityWellProfile != null ? gravityWellProfile.ClusterStiffness : 16f;
        private float GravityClusterDamping => gravityWellProfile != null ? gravityWellProfile.ClusterDamping : 5.5f;
        private float GravityClusterOrbitRadius => gravityWellProfile != null ? gravityWellProfile.ClusterOrbitRadius : 1.35f;
        private float GravityClusterAngularDamping => gravityWellProfile != null ? gravityWellProfile.ClusterAngularDamping : 6.5f;
        private float GravityClusterMaximumAcceleration => gravityWellProfile != null ? gravityWellProfile.ClusterMaximumAcceleration : 62f;
        private float GravityFractureDelay => gravityWellProfile != null ? gravityWellProfile.FractureDelaySeconds : 0.68f;
        private float GravityFractureImpulse => gravityWellProfile != null ? gravityWellProfile.FractureImpulse : 1450f;
        private float GravitySustainedImpulse => gravityWellProfile != null
            ? gravityWellProfile.SustainedDamageImpulsePerSecond
            : 680f;

        private sealed class RigidbodyEarthTarget : IEarthPhysicalTarget
        {
            public RigidbodyEarthTarget(Rigidbody body) => Body = body;
            public Rigidbody Body { get; }
            public uint StableEarthId => Body != null ? unchecked((uint)Body.GetHashCode()) : 0u;
            public EarthPhysicalTargetHandle TargetHandle => new EarthPhysicalTargetHandle(StableEarthId, 1u);
            public float EarthMass => Body != null ? Body.mass : 0f;
            public EarthPhysicalTargetKind TargetKind => EarthPhysicalTargetKind.Rock;
            public bool IsEarthTargetValid => Body != null && !Body.isKinematic && Body.gameObject.activeInHierarchy;
            public void OnEarthMagicGrabbed(EarthMagicGripKind grip) => Body?.WakeUp();
            public void OnEarthMagicReleased(EarthMagicGripKind grip) { }
        }
    }
}
