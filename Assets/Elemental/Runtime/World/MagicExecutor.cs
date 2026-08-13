using System;
using System.Collections.Generic;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Bending;
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

        [SerializeField] private VoxelPlanetBehaviour voxelPlanet;
        [SerializeField] private EarthFragmentPool fragmentPool;
        [SerializeField] private EarthWallPool wallPool;
        [SerializeField] private EarthTelekinesisController telekinesis;
        [SerializeField] private EarthVectorFieldProfile vectorFieldProfile;
        [SerializeField] private EarthPlatformPool platformPool;
        [SerializeField] private EarthGravityWellProfile gravityWellProfile;
        [SerializeField] private Transform planetCenter;
        [SerializeField] private Transform heldFragmentAnchor;
        [SerializeField, Min(1f)] private float earthMaterialDensity = 120f;
        [SerializeField, Min(0.1f)] private float wallMaximumHeight = 6.25f;
        [SerializeField, Min(0.1f)] private float wallMinimumHeight = 1.25f;
        [SerializeField, Min(1f)] private float wallMaxLength = 22f;
        [SerializeField, Min(0.05f)] private float wallThickness = 0.55f;
        [SerializeField, Min(1f)] private float wallPushLeverage = 12f;
        [SerializeField, Min(0.1f)] private float minimumThrowVelocityChange = 6f;
        [SerializeField, Min(0.1f)] private float maximumThrowVelocityChange = 18f;

        private readonly Dictionary<AbilityId, CompiledAbilityRecipe> _recipes =
            new Dictionary<AbilityId, CompiledAbilityRecipe>();
        private readonly List<float3> _platformPathScratch = new List<float3>(32);
        private EarthFragment _heldFragment;
        private IEarthPhysicalTarget _vectorFieldTarget;
        private Vector3 _vectorFieldPoint;
        private Vector3 _vectorFieldDirection;
        private float _vectorFieldCharge;
        private readonly Collider[] _gravityWellHits = new Collider[64];
        private readonly HashSet<Rigidbody> _gravityWellBodies = new HashSet<Rigidbody>();
        private readonly EarthGravityGripSession _gravityGripSession = new EarthGravityGripSession(48);
        private readonly IEarthPhysicalTarget[] _gravityWellStructureTargets = new IEarthPhysicalTarget[48];
        private bool _gravityWellActive;
        private Vector3 _gravityWellFocus;
        private Vector3 _gravityWellUp;
        private float _gravityWellElapsed;
        private EarthWall _gravityWellWall;
        private EarthPlatform _gravityWellPlatform;
        private bool _gravityWellFracturedStructure;

        public MagicWorldEvents Events { get; } = new MagicWorldEvents();
        public MagicReplayRecorder Recorder { get; } = new MagicReplayRecorder();
        public int SuccessfulCommandCount { get; private set; }
        public EarthFragment HeldFragment => _heldFragment != null && _heldFragment.IsHeld ? _heldFragment : null;
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
        public Vector3 VectorFieldDirection => _vectorFieldDirection;
        public Vector3 VectorFieldPoint => _vectorFieldTarget != null && _vectorFieldTarget.Body != null
            ? _vectorFieldTarget.Body.worldCenterOfMass
            : _vectorFieldPoint;
        public float VectorFieldCharge => _vectorFieldCharge;
        public float VectorFieldMass => _vectorFieldTarget != null ? _vectorFieldTarget.EarthMass : 0f;
        public bool IsGravityWellActive => _gravityWellActive;
        public Vector3 GravityWellFocus => _gravityWellFocus;
        public float GravityWellStrength => _gravityWellActive
            ? Mathf.Clamp01(_gravityWellElapsed / GravityFractureDelay)
            : 0f;
        public float GravityWellRadius => gravityWellProfile != null ? gravityWellProfile.Radius : 7.5f;
        public float GravityWellFocusLift => gravityWellProfile != null ? gravityWellProfile.FocusLift : 0.75f;
        public int GravityWellCapturedCount => _gravityGripSession.Count;
        public int GravityWellMaximumCapturedTargets => GravityMaximumCapturedTargets;

        public bool TryBeginGravityWell(Collider aimedCollider, Vector3 focus, Vector3 localUp)
        {
            CancelGravityWell();
            if (aimedCollider == null) return false;
            _gravityWellWall = aimedCollider.GetComponentInParent<EarthWall>();
            if (_gravityWellWall == null)
                _gravityWellWall = aimedCollider.GetComponentInParent<EarthWallPiece>()?.Owner;
            if (_gravityWellWall != null)
                _gravityWellWall.Collapsed += HandleGravityWellWallFractured;
            _gravityWellPlatform = aimedCollider.GetComponentInParent<EarthPlatform>();
            if (_gravityWellPlatform == null)
                _gravityWellPlatform = aimedCollider.GetComponentInParent<EarthPlatformPiece>()?.Owner;
            _gravityWellFocus = focus;
            _gravityWellUp = SafeDirection(localUp);
            _gravityWellElapsed = 0f;
            _gravityWellFracturedStructure = false;
            _gravityWellActive = true;
            return true;
        }

        public void UpdateGravityWell(Vector3 focus, Vector3 localUp)
        {
            if (!_gravityWellActive) return;
            _gravityWellFocus = focus;
            if (localUp.sqrMagnitude > 0.001f) _gravityWellUp = localUp.normalized;
        }

        public void CancelGravityWell()
        {
            if (_gravityWellWall != null)
                _gravityWellWall.Collapsed -= HandleGravityWellWallFractured;
            _gravityGripSession.ReleaseAll(EarthMagicGripKind.GravityWell);
            _gravityWellBodies.Clear();
            _gravityWellActive = false;
            _gravityWellElapsed = 0f;
            _gravityWellWall = null;
            _gravityWellPlatform = null;
            _gravityWellFracturedStructure = false;
        }

        private void HandleGravityWellWallFractured(EarthWall wall)
        {
            if (!_gravityWellActive || wall == null || wall != _gravityWellWall) return;
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
            return true;
        }

        public void BeginHeldEarthControl(
            Vector3 target,
            Vector3 velocity,
            float charge01,
            in Elemental.Simulation.Bending.BendTuning tuning)
        {
            if (HeldFragment != null)
                HeldFragment.BeginBendControl(target, velocity, charge01, in tuning);
            else
                telekinesis?.UpdateTarget(target, velocity, charge01);
        }

        public void UpdateHeldEarthTarget(Vector3 target, Vector3 velocity, float charge01)
        {
            if (HeldFragment != null)
                HeldFragment.UpdateBendTarget(target, velocity, charge01);
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
            if (HeldFragment != null)
                return ReleaseHeldFragment(
                    aimDirection, gestureVelocity, charge01, tick, out releaseVelocity);
            releaseVelocity = Vector3.zero;
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

        public void CancelHeldEarthControl()
        {
            if (_heldFragment != null)
            {
                _heldFragment.StopBendControl();
                _heldFragment = null;
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
            IEarthPhysicalTarget target = ResolveEarthTarget(hitCollider, body);
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

        public bool ReleaseVectorField()
        {
            IEarthPhysicalTarget target = _vectorFieldTarget;
            if (target == null) return false;
            bool valid = target.IsEarthTargetValid && target.Body != null;
            float mass = Mathf.Max(0.01f, target.EarthMass);
            float velocityChange = 0f;
            if (valid)
            {
                float impulse = EarthVectorFieldSolver.FinalImpulse(
                    _vectorFieldCharge,
                    VectorMinimumReleaseImpulse,
                    VectorMaximumReleaseImpulse);
                float multiplier = target.TargetKind == EarthPhysicalTargetKind.Wall
                    ? VectorWallForceMultiplier
                    : 1f;
                float speedLimit = target.TargetKind == EarthPhysicalTargetKind.Wall
                    ? VectorWallSpeedLimit
                    : VectorRockSpeedLimit;
                Vector3 direction = FieldDirectionFor(target);
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
                LastMagicPushVelocityChange = velocityChange;
                MagicPushEvent pushed = new MagicPushEvent(
                    unchecked((uint)Time.frameCount),
                    ToFloat3(_vectorFieldPoint),
                    _vectorFieldCharge,
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
                if (body == null || body.isKinematic) return false;
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
            if (_gravityWellActive) ApplyGravityWell();
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
                    ? VectorWallSpeedLimit
                    : VectorRockSpeedLimit;
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

        public void Configure(
            VoxelPlanetBehaviour configuredVoxelPlanet,
            EarthFragmentPool configuredPool,
            Transform configuredPlanetCenter,
            EarthWallPool configuredWallPool = null,
            Transform configuredHeldFragmentAnchor = null)
        {
            if (wallPool != null) wallPool.WallCollapsed -= HandleWallCollapsed;
            voxelPlanet = configuredVoxelPlanet;
            fragmentPool = configuredPool;
            planetCenter = configuredPlanetCenter;
            wallPool = configuredWallPool;
            heldFragmentAnchor = configuredHeldFragmentAnchor;
            if (wallPool != null) wallPool.WallCollapsed += HandleWallCollapsed;
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
            platformPool = configuredPlatformPool;
            gravityWellProfile = configuredGravityWellProfile;
        }

        private void ApplyGravityWell()
        {
            using (GravityWellMarker.Auto())
            {
                _gravityWellElapsed += Time.fixedDeltaTime;
                StressGravityWellStructure();
                _gravityWellBodies.Clear();
                CaptureFracturedStructureTargets();
                int hitCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                    _gravityWellFocus,
                    GravityRadius,
                    _gravityWellHits,
                    ~0,
                    QueryTriggerInteraction.Ignore);
                Vector3 planetPosition = planetCenter != null ? planetCenter.position : Vector3.zero;
                for (int index = 0; index < hitCount; index++)
                {
                    Collider hit = _gravityWellHits[index];
                    if (hit == null) continue;
                    Rigidbody body = hit.attachedRigidbody;
                    IEarthPhysicalTarget target = ResolveExplicitGravityTarget(hit);
                    if (target == null || !target.IsEarthTargetValid) continue;
                    if (body == null) body = target.Body;
                    if (body == null || body.isKinematic ||
                        body.GetComponent<PlanetMotor>() != null ||
                        body.GetComponent<ActiveRagdollPuppet>() != null) continue;
                    TryLatchGravityTarget(target);
                }

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
                    float3 offset = EarthGravityGripSolver.SlotOffset(
                        target.StableEarthId,
                        GravityClusterOrbitRadius,
                        ToFloat3(localUp));
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

        private void CaptureFracturedStructureTargets()
        {
            IEarthFractureSource source = _gravityWellWall != null
                ? (IEarthFractureSource)_gravityWellWall
                : _gravityWellPlatform;
            if (source == null || !source.IsFractured) return;
            int count = source.CopyActiveTargetsNonAlloc(_gravityWellStructureTargets);
            for (int index = 0; index < count; index++)
            {
                TryLatchGravityTarget(_gravityWellStructureTargets[index]);
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
            if (!_gravityWellFracturedStructure && _gravityWellElapsed >= GravityFractureDelay)
            {
                Vector3 direction = SafeDirection(_gravityWellFocus -
                    (_gravityWellWall != null ? _gravityWellWall.transform.position :
                     _gravityWellPlatform != null ? _gravityWellPlatform.transform.position :
                     _gravityWellFocus - _gravityWellUp));
                bool fractured = _gravityWellWall != null &&
                                  _gravityWellWall.ApplyStructureImpact(
                                      _gravityWellFocus, direction, GravityFractureImpulse);
                fractured |= _gravityWellPlatform != null &&
                              _gravityWellPlatform.ApplyStructureImpact(
                                  _gravityWellFocus, direction, GravityFractureImpulse);
                _gravityWellFracturedStructure = fractured ||
                                                  (_gravityWellWall == null && _gravityWellPlatform == null);
            }
            if (!_gravityWellFracturedStructure) return;
            float impulse = GravitySustainedImpulse * Time.fixedDeltaTime;
            Vector3 pull = SafeDirection(_gravityWellFocus -
                (_gravityWellWall != null ? _gravityWellWall.transform.position :
                 _gravityWellPlatform != null ? _gravityWellPlatform.transform.position :
                 _gravityWellFocus - _gravityWellUp));
            _gravityWellWall?.ApplyStructureImpact(_gravityWellFocus, pull, impulse);
            _gravityWellPlatform?.ApplyStructureImpact(_gravityWellFocus, pull, impulse);
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
            PhysicalImpactTarget physical = hitCollider.GetComponentInParent<PhysicalImpactTarget>();
            return physical;
        }

        public void ConfigureWallProfile(float minimumHeight, float maximumHeight, float maximumLength = 22f)
        {
            wallMinimumHeight = Mathf.Max(0.1f, minimumHeight);
            wallMaximumHeight = Mathf.Max(wallMinimumHeight, maximumHeight);
            wallMaxLength = Mathf.Max(1f, maximumLength);
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

                for (int index = 0; index < recipe.Operators.Length; index++)
                {
                    if (!ExecuteOperator(recipe.Operators[index], recipe, command))
                    {
                        return false;
                    }
                }

                Recorder.Record(in command);
                SuccessfulCommandCount++;
                return true;
            }
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
            Collider terrainCollider = planetCenter != null ? planetCenter.GetComponent<Collider>() : null;
            bool terrainHit = terrainCollider != null && collision.collider != null &&
                              (collision.collider == terrainCollider ||
                               collision.collider.transform.IsChildOf(terrainCollider.transform));
            wall?.ApplyRockImpact(contact.point, direction, impulse);
            platform?.ApplyStructureImpact(contact.point, direction, impulse);
            // A controlled rock touching the ground is the accretion gesture. It must not
            // self-destruct from the PD controller's contact impulse; only released rocks
            // shatter and carve craters on impact.
            if (terrainHit && !fragment.IsHeld)
                fragment.TryShatter(contact.point, contact.normal, impulse);
            ApplyFragmentImpact(
                fragment, contact.point, contact.normal, impulse, target, direction,
                terrainHit && wall == null);
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
            EarthWall wall = wallPool.Acquire(
                start,
                end,
                planetCenter.position,
                committedHeight,
                wallThickness,
                command.Tick);
            WallRaisedEvent raised = new WallRaisedEvent(
                command.Tick,
                wall.WallId,
                footprint[0],
                footprint[footprint.Length - 1],
                committedHeight,
                wallThickness);
            Events.Emit(in raised);
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

        private float VectorContinuousForce => vectorFieldProfile != null ? vectorFieldProfile.ContinuousForce : 4200f;
        private float VectorMinimumReleaseImpulse => vectorFieldProfile != null ? vectorFieldProfile.MinimumReleaseImpulse : 260f;
        private float VectorMaximumReleaseImpulse => vectorFieldProfile != null ? vectorFieldProfile.MaximumReleaseImpulse : 2400f;
        private float VectorRockSpeedLimit => vectorFieldProfile != null ? vectorFieldProfile.RockSpeedLimit : 32f;
        private float VectorWallSpeedLimit => vectorFieldProfile != null ? vectorFieldProfile.WallSpeedLimit : 14f;
        private float VectorWallForceMultiplier => vectorFieldProfile != null ? vectorFieldProfile.WallForceMultiplier : 3.4f;
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
