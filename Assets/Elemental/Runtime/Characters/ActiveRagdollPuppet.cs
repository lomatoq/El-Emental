using System;
using Elemental.Core.IDs;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Combat;
using Elemental.Simulation.Gravity;
using Elemental.Simulation.Magic;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ActiveRagdollPuppet : MonoBehaviour
    {
        private static readonly ProfilerMarker FixedTickMarker = new ProfilerMarker("Elemental.ActiveRagdoll.FixedTick");
        private static readonly ProfilerMarker PoweredAssistMarker =
            new ProfilerMarker("Elemental.ActiveRagdoll.PoweredAssist");
        private const int GroundHitCapacity = 8;
        private const int SemanticProbeCapacity = 8;

        [Header("Authority")]
        [SerializeField, Min(1)] private uint actorId = 1u;
        [SerializeField] private GravityWorldBehaviour gravityWorld;
        [SerializeField] private Rigidbody rootBody;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private PhysicalImpactTarget impactTarget;

        [Header("Puppet")]
        [SerializeField] private Transform chest;
        [SerializeField] private ActiveRagdollJoint[] joints = Array.Empty<ActiveRagdollJoint>();
        [SerializeField] private Collider[] selfColliders = Array.Empty<Collider>();
        [SerializeField] private Behaviour[] disabledDuringRagdoll = Array.Empty<Behaviour>();

        [Header("Balance")]
        [SerializeField, Min(0.1f)] private float groundProbeDistance = 2.2f;
        [SerializeField, Min(0f)] private float balanceGain = 160f;
        [SerializeField, Min(0f)] private float maximumBalanceTorque = 220f;
        [SerializeField, Min(0f)] private float startupImpactGraceSeconds = 0.8f;
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Powered Physical Assist")]
        [SerializeField] private EarthPhysicalAnimationProfile physicalAnimationProfile;
        [SerializeField] private Transform leftFoot;
        [SerializeField] private Transform rightFoot;
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;
        [SerializeField, Min(0.02f)] private float supportFootHalfLength = 0.16f;
        [SerializeField, Min(0.02f)] private float supportFootHalfWidth = 0.09f;
        [SerializeField, Min(0.01f)] private float semanticProbeRadius = 0.08f;
        [SerializeField] private LayerMask semanticProbeMask = ~0;

        private readonly RaycastHit[] _groundHits = new RaycastHit[GroundHitCapacity];
        private readonly RaycastHit[] _semanticHits = new RaycastHit[SemanticProbeCapacity];
        private readonly EarthPoweredPhysicalAssist _poweredAssist =
            new EarthPoweredPhysicalAssist();
        private CharacterPhysicalController _controller;
        private Vector3 _gravityUp = Vector3.up;
        private Vector3 _supportCenter;
        private int _contactCount;
        private CharacterPhysicalMode _lastPublishedMode;
        private bool _hasPublishedMode;
        private float _suppressImpactsUntil;
        private bool _rootConstraintsCaptured;
        private RigidbodyConstraints _motorRootConstraints;
        private RigidbodyConstraints _ragdollRootConstraints;
        private float _forcedRagdollUntil;
        private bool[] _externalBodyWasKinematic = Array.Empty<bool>();
        private bool[] _externalBodyDetectedCollisions = Array.Empty<bool>();
        private bool[] _externalColliderWasEnabled = Array.Empty<bool>();
        private Vector3 _lastPoweredImpactDirection;
        private bool _leftFootPlanted;
        private bool _rightFootPlanted;
        private int _poweredFootContactFrame = -1;
        private bool _poweredAssistOperationalThisTick;
        private bool _reportedInvalidPoweredJointBinding;

        public CharacterPhysicalState CurrentState { get; private set; }
        public CharacterPhysicalMode CanonicalMode =>
            _controller != null ? _controller.Mode : CurrentState.Mode;
        public Vector3 LastBalanceTorque { get; private set; }
        public float MaximumJointError { get; private set; }
        public PhysicalCollisionImpact LastCollisionImpact { get; private set; }
        public bool LastCollisionWasSupport { get; private set; }
        public bool UsePoweredPhysicalAssist =>
            physicalAnimationProfile != null &&
            physicalAnimationProfile.UsePoweredPhysicalAssist;
        public bool PoweredAssistConfigurationValid =>
            HasValidPoweredJointBindings();
        public EarthPoweredImpactDecision LastPoweredImpactDecision { get; private set; }
        public EarthPoweredAssistOutput LastPoweredAssistOutput { get; private set; }
        public int PoweredActionRequestCount { get; private set; }
        public event Action<CharacterPhysicalState> StateChanged;
        public event Action<Vector3, float> ImpactObserved;
        public event Action<EarthPhysicalActionRequest> PhysicalActionRequested;
        public bool IsExternalRagdollAuthority { get; private set; }
        public bool IsExternalRecoveryAuthority { get; private set; }

        public bool OwnsCollider(Collider candidate) => IsSelfCollider(candidate);

        public int CopySelfCollidersNonAlloc(Collider[] destination)
        {
            if (destination == null) return 0;
            int count = Mathf.Min(destination.Length, selfColliders.Length);
            for (int index = 0; index < count; index++) destination[index] = selfColliders[index];
            return count;
        }

        public void SuppressImpacts(float seconds)
        {
            _suppressImpactsUntil = Mathf.Max(_suppressImpactsUntil, Time.time + Mathf.Max(0f, seconds));
        }

        public void ApplyUniformVelocityChange(Vector3 velocityChange)
        {
            if (!IsFinite(velocityChange) || velocityChange.sqrMagnitude <= 0f) return;
            if (rootBody != null && !rootBody.isKinematic)
                rootBody.AddForce(velocityChange, ForceMode.VelocityChange);
            for (int index = 0; index < joints.Length; index++)
            {
                Rigidbody body = joints[index] != null ? joints[index].Body : null;
                if (body != null && !body.isKinematic)
                    body.AddForce(velocityChange, ForceMode.VelocityChange);
            }
        }

        public void ForceKnockout(Vector3 launchVelocityChange, float holdSeconds)
        {
            EnsureController();
            _forcedRagdollUntil = Mathf.Max(
                _forcedRagdollUntil,
                Time.time + Mathf.Max(0.1f, holdSeconds));
            TryBeginExternalFullRagdoll();
            ApplyUniformVelocityChange(launchVelocityChange);
        }

        public bool TryBeginExternalFullRagdoll()
        {
            EnsureController();
            IsExternalRecoveryAuthority = false;
            _controller.ForceFullRagdoll();
            PublishExternalMode(
                CharacterPhysicalMode.FullRagdoll,
                0f,
                RecoveryCandidate.None);
            return _controller.Mode == CharacterPhysicalMode.FullRagdoll;
        }

        public bool TryBeginExternalRecovery(RecoveryCandidate recovery)
        {
            EnsureController();
            if (!_controller.TryForceRecovery(recovery)) return false;
            IsExternalRecoveryAuthority = true;
            PublishExternalMode(CharacterPhysicalMode.Recovery, 0f, recovery);
            return true;
        }

        public bool TryCompleteExternalRecovery()
        {
            EnsureController();
            if (_controller.Mode != CharacterPhysicalMode.Recovery) return false;
            _controller.Reset();
            IsExternalRecoveryAuthority = false;
            PublishExternalMode(
                CharacterPhysicalMode.AnimatedMotor,
                1f,
                RecoveryCandidate.None);
            return true;
        }

        public void SetExternalRagdollAuthority(bool active)
        {
            if (IsExternalRagdollAuthority == active) return;
            if (active)
            {
                if (_externalBodyWasKinematic.Length != joints.Length)
                {
                    _externalBodyWasKinematic = new bool[joints.Length];
                    _externalBodyDetectedCollisions = new bool[joints.Length];
                }
                if (_externalColliderWasEnabled.Length != selfColliders.Length)
                    _externalColliderWasEnabled = new bool[selfColliders.Length];
                for (int index = 0; index < joints.Length; index++)
                {
                    Rigidbody body = joints[index] != null ? joints[index].Body : null;
                    if (body == null) continue;
                    _externalBodyWasKinematic[index] = body.isKinematic;
                    _externalBodyDetectedCollisions[index] = body.detectCollisions;
                    if (!body.isKinematic)
                    {
                        body.linearVelocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                    }
                    body.detectCollisions = false;
                    body.isKinematic = true;
                }
                for (int index = 0; index < selfColliders.Length; index++)
                {
                    Collider collider = selfColliders[index];
                    if (collider == null) continue;
                    _externalColliderWasEnabled[index] = collider.enabled;
                    collider.enabled = false;
                }
            }
            else
            {
                for (int index = 0; index < joints.Length; index++)
                {
                    Rigidbody body = joints[index] != null ? joints[index].Body : null;
                    if (body == null) continue;
                    body.isKinematic = index < _externalBodyWasKinematic.Length &&
                                       _externalBodyWasKinematic[index];
                    body.detectCollisions = index < _externalBodyDetectedCollisions.Length &&
                                            _externalBodyDetectedCollisions[index];
                    if (!body.isKinematic)
                    {
                        body.linearVelocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                    }
                }
                for (int index = 0; index < selfColliders.Length; index++)
                {
                    Collider collider = selfColliders[index];
                    if (collider != null)
                        collider.enabled = index < _externalColliderWasEnabled.Length &&
                                           _externalColliderWasEnabled[index];
                }
            }
            IsExternalRagdollAuthority = active;
        }

        public void ResetPhysicalState(Vector3 worldPosition, Quaternion worldRotation)
        {
            if (rootBody == null || !IsFinite(worldPosition)) return;
            EnsureController();

            // Enter Play Mode can keep non-serialized runtime state when domain and
            // scene reloads are disabled. Never carry visible-ragdoll authority into
            // a fresh gameplay run.
            if (IsExternalRagdollAuthority) SetExternalRagdollAuthority(false);
            IsExternalRecoveryAuthority = false;

            Vector3 previousPosition = rootBody.position;
            Quaternion previousRotation = rootBody.rotation;
            Quaternion deltaRotation = worldRotation * Quaternion.Inverse(previousRotation);
            for (int index = 0; index < joints.Length; index++)
            {
                Rigidbody body = joints[index] != null ? joints[index].Body : null;
                if (body == null) continue;
                Vector3 offset = body.position - previousPosition;
                body.position = worldPosition + deltaRotation * offset;
                body.rotation = deltaRotation * body.rotation;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }

            rootBody.position = worldPosition;
            rootBody.rotation = worldRotation;
            if (!rootBody.isKinematic)
            {
                rootBody.linearVelocity = Vector3.zero;
                rootBody.angularVelocity = Vector3.zero;
            }
            _forcedRagdollUntil = 0f;
            _poweredAssist.ResetTemporalState();
            _lastPoweredImpactDirection = Vector3.zero;
            _leftFootPlanted = false;
            _rightFootPlanted = false;
            _poweredFootContactFrame = -1;
            _poweredAssistOperationalThisTick = false;
            LastPoweredImpactDecision = default;
            LastPoweredAssistOutput = default;
            _controller.Reset();
            _supportCenter = worldPosition - worldRotation * Vector3.up * 0.9f;
            _contactCount = 0;
            _hasPublishedMode = false;
            CurrentState = new CharacterPhysicalState(
                new ActorId(Math.Max(1u, actorId)),
                CharacterPhysicalMode.AnimatedMotor,
                float3.zero,
                float3.zero,
                ToFloat3(worldRotation * Vector3.up),
                0f,
                0f,
                1f,
                RecoveryCandidate.None);
            ApplyControl(CurrentState);
            SuppressImpacts(0.75f);
            impactTarget?.SuppressImpacts(0.75f);
            UnityEngine.Physics.SyncTransforms();
            PublishStateIfChanged();
        }

        public void Configure(
            uint configuredActorId,
            GravityWorldBehaviour configuredGravityWorld,
            Rigidbody configuredRootBody,
            PlanetMotor configuredMotor,
            PhysicalImpactTarget configuredImpactTarget,
            Transform configuredChest,
            ActiveRagdollJoint[] configuredJoints,
            Collider[] configuredSelfColliders)
        {
            actorId = Math.Max(1u, configuredActorId);
            gravityWorld = configuredGravityWorld;
            rootBody = configuredRootBody;
            motor = configuredMotor;
            impactTarget = configuredImpactTarget;
            chest = configuredChest;
            joints = configuredJoints ?? Array.Empty<ActiveRagdollJoint>();
            selfColliders = configuredSelfColliders ?? Array.Empty<Collider>();
            Initialize();
        }

        public void ConfigureControlBehaviours(params Behaviour[] behaviours)
        {
            disabledDuringRagdoll = behaviours ?? Array.Empty<Behaviour>();
        }

        /// <summary>
        /// Wires the default-off powered-assist adapter. The supplied limb
        /// transforms are read-only support/probe inputs; Animator and foot IK
        /// remain owned by Presentation.
        /// </summary>
        public void ConfigurePoweredPhysicalAssist(
            EarthPhysicalAnimationProfile profile,
            Transform configuredLeftFoot,
            Transform configuredRightFoot,
            Transform configuredHead,
            Transform configuredLeftHand,
            Transform configuredRightHand)
        {
            physicalAnimationProfile = profile;
            leftFoot = configuredLeftFoot;
            rightFoot = configuredRightFoot;
            head = configuredHead;
            leftHand = configuredLeftHand;
            rightHand = configuredRightHand;
            _poweredAssist.ResetTemporalState();
            LastPoweredImpactDecision = default;
            LastPoweredAssistOutput = default;
            PoweredActionRequestCount = 0;
            _leftFootPlanted = false;
            _rightFootPlanted = false;
            _poweredFootContactFrame = -1;
            _poweredAssistOperationalThisTick = false;
            _reportedInvalidPoweredJointBinding = false;
        }

        /// <summary>
        /// Consumes the presentation contact owner's current semantic planted
        /// state. This does not capture anchors or write IK; stale samples expire.
        /// </summary>
        public void SetPoweredFootContactState(bool leftPlanted, bool rightPlanted)
        {
            _leftFootPlanted = leftPlanted;
            _rightFootPlanted = rightPlanted;
            _poweredFootContactFrame = Time.frameCount;
        }

        /// <summary>
        /// Accepted-hit ingress for the Director-owned response fanout. This
        /// method never writes Animator/IK, never applies impulse, and never
        /// requests full ragdoll; heavy ownership stays on HumanoidRagdollRig.
        /// </summary>
        public EarthPoweredImpactDecision ReceiveAcceptedWorldResponse(
            in EarthWorldResponseEvent response)
        {
            if (!UsePoweredPhysicalAssist || response.Response == EarthCharacterImpactResponse.Ignore)
                return default;
            if (response.ResponseId == 0u)
                return default;

            EnsureController();
            if (response.Response != EarthCharacterImpactResponse.Stagger)
            {
                LastPoweredImpactDecision = _poweredAssist.RouteAcceptedResponse(
                    response.ResponseId,
                    response.Response,
                    response.Intensity01,
                    response.Direction,
                    true,
                    EarthPoweredAssistRejection.None);
                return LastPoweredImpactDecision;
            }

            if (_poweredAssist.IsResponseKnown(response.ResponseId))
            {
                LastPoweredImpactDecision = _poweredAssist.RouteAcceptedResponse(
                    response.ResponseId,
                    response.Response,
                    response.Intensity01,
                    response.Direction,
                    false,
                    EarthPoweredAssistRejection.None);
                return LastPoweredImpactDecision;
            }

            EarthPoweredAssistRejection rejection;
            if (!ValidatePoweredJointBindings(true))
            {
                rejection = EarthPoweredAssistRejection.InvalidBodyRegionBinding;
            }
            else
            {
                EarthSupportPolygon polygon = BuildPoweredSupportPolygon(
                    out bool feetConfigured,
                    out bool leftPlanted,
                    out bool rightPlanted);
                rejection = EarthPoweredAssistEligibility.Evaluate(
                    _controller.Mode,
                    motor != null && motor.HasStableSupport,
                    feetConfigured,
                    leftPlanted,
                    rightPlanted,
                    polygon.IsValid);
            }

            bool controllerAccepted = rejection == EarthPoweredAssistRejection.None &&
                                      _controller.TryRequestPoweredAssist(true);
            if (rejection == EarthPoweredAssistRejection.None && !controllerAccepted)
                rejection = EarthPoweredAssistRejection.ControllerRejected;
            LastPoweredImpactDecision = _poweredAssist.RouteAcceptedResponse(
                response.ResponseId,
                response.Response,
                response.Intensity01,
                response.Direction,
                controllerAccepted,
                rejection);
            if (!controllerAccepted)
                return LastPoweredImpactDecision;

            _lastPoweredImpactDirection = ToVector3(response.Direction);
            return LastPoweredImpactDecision;
        }

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (impactTarget != null)
            {
                impactTarget.ImpactApplied += HandleImpact;
                impactTarget.CollisionImpactApplied += HandleCollisionImpact;
            }
            if (gravityWorld != null)
                _suppressImpactsUntil = Mathf.Max(
                    _suppressImpactsUntil,
                    Time.time + Mathf.Max(0f, startupImpactGraceSeconds));
        }

        private void OnDisable()
        {
            if (impactTarget != null)
            {
                impactTarget.ImpactApplied -= HandleImpact;
                impactTarget.CollisionImpactApplied -= HandleCollisionImpact;
            }
        }

        public void InjectImpact(float impulse)
        {
            if (Time.time < _suppressImpactsUntil) return;
            EnsureController();
            _controller.ApplyImpact(Mathf.Max(0f, impulse), Mathf.Max(rootBody.mass, 0.01f));
        }

        public void ReceiveImpact(in ImpactEvent impact)
        {
            InjectImpact(impact.Impulse);
            ImpactObserved?.Invoke(ToVector3(impact.Point), impact.Impulse);
        }

        private void FixedUpdate()
        {
            if (rootBody == null ||
                IsExternalRagdollAuthority ||
                IsExternalRecoveryAuthority)
            {
                return;
            }

            using (FixedTickMarker.Auto())
            {
                EnsureController();
                if (Time.time < _forcedRagdollUntil) _controller.ForceFullRagdoll();
                SampleGravity();
                _poweredAssistOperationalThisTick = UsePoweredPhysicalAssist &&
                                                    ValidatePoweredJointBindings(true);
                bool poweredSupportAuthority = _poweredAssistOperationalThisTick &&
                                               _controller.Mode != CharacterPhysicalMode.FullRagdoll &&
                                               _controller.Mode != CharacterPhysicalMode.Recovery;
                if (poweredSupportAuthority) UpdatePoweredSupport();
                else UpdateSupport();
                Transform chestTransform = chest != null ? chest : transform;
                var frame = new CharacterPhysicalFrame(
                    Time.fixedDeltaTime,
                    ToFloat3(_gravityUp),
                    ToFloat3(rootBody.worldCenterOfMass),
                    ToFloat3(_supportCenter),
                    _contactCount,
                    ToFloat3(rootBody.linearVelocity),
                    ToFloat3(rootBody.angularVelocity),
                    ToFloat3(chestTransform.up),
                    ToFloat3(chestTransform.right));
                CurrentState = _controller.Step(in frame);
                PublishStateIfChanged();
                if (_poweredAssistOperationalThisTick)
                {
                    using (PoweredAssistMarker.Auto())
                        UpdatePoweredAssist();
                }
                ApplyControl(CurrentState);
            }
        }

        private void ApplyControl(CharacterPhysicalState state)
        {
            bool motorAllowed = state.Mode == CharacterPhysicalMode.AnimatedMotor ||
                                state.Mode == CharacterPhysicalMode.PhysicalAssist ||
                                state.Mode == CharacterPhysicalMode.Stagger;
            if (rootBody != null)
            {
                CaptureRootConstraints();
                RigidbodyConstraints required = motorAllowed
                    ? _motorRootConstraints
                    : _ragdollRootConstraints;
                if (rootBody.constraints != required) rootBody.constraints = required;
            }
            if (motor != null)
            {
                motor.enabled = motorAllowed;
            }
            for (int index = 0; index < disabledDuringRagdoll.Length; index++)
            {
                Behaviour behaviour = disabledDuringRagdoll[index];
                if (behaviour != null && behaviour.enabled != motorAllowed)
                    behaviour.enabled = motorAllowed;
            }

            MaximumJointError = 0f;
            bool poweredJointOwnership = _poweredAssistOperationalThisTick &&
                                         !IsExternalRagdollAuthority &&
                                         !IsExternalRecoveryAuthority;
            EarthMuscleProfile poweredProfile = poweredJointOwnership
                ? physicalAnimationProfile.ResolveMuscleProfile(
                    LastPoweredAssistOutput.Profile)
                : default;
            for (int index = 0; index < joints.Length; index++)
            {
                ActiveRagdollJoint joint = joints[index];
                if (joint == null)
                {
                    continue;
                }

                if (poweredJointOwnership)
                {
                    EarthMuscleRegionTuning tuning = poweredProfile.For(joint.BodyRegion);
                    joint.ApplyPoweredPose(
                        in tuning,
                        LastPoweredAssistOutput.ResponseWeight,
                        Time.fixedDeltaTime);
                }
                else
                {
                    joint.ApplyPose(state.MuscleStrength);
                }
                MaximumJointError = Mathf.Max(MaximumJointError, joint.JointErrorDegrees);
            }

            LastBalanceTorque = Vector3.zero;
            if (poweredJointOwnership) return;
            if (_contactCount <= 0 ||
                (state.Mode != CharacterPhysicalMode.PhysicalAssist && state.Mode != CharacterPhysicalMode.Stagger))
            {
                return;
            }

            float3 torque = BalanceControllerMath.ComputeCorrectiveTorque(
                ToFloat3(rootBody.worldCenterOfMass),
                ToFloat3(_supportCenter),
                ToFloat3(_gravityUp),
                balanceGain,
                maximumBalanceTorque);
            LastBalanceTorque = ToVector3(torque);
            rootBody.AddTorque(LastBalanceTorque, ForceMode.Acceleration);
        }

        private void PublishExternalMode(
            CharacterPhysicalMode mode,
            float muscleStrength,
            RecoveryCandidate recovery)
        {
            Vector3 linearVelocity = rootBody != null
                ? rootBody.linearVelocity
                : Vector3.zero;
            Vector3 angularVelocity = rootBody != null
                ? rootBody.angularVelocity
                : Vector3.zero;
            CurrentState = new CharacterPhysicalState(
                new ActorId(Math.Max(1u, actorId)),
                mode,
                ToFloat3(linearVelocity),
                ToFloat3(angularVelocity),
                ToFloat3(_gravityUp),
                CurrentState.BalanceError,
                CurrentState.StaggerDebt,
                muscleStrength,
                recovery);
            ApplyControl(CurrentState);
            PublishStateIfChanged();
        }

        private void SampleGravity()
        {
            if (gravityWorld == null || !gravityWorld.IsReady)
            {
                _gravityUp = transform.up;
                return;
            }

            Vector3 position = rootBody.worldCenterOfMass;
            GravitySample sample = gravityWorld.World.Sample(ToFloat3(position), 0u);
            if (sample.IsFinite && math.lengthsq(sample.Up) > 0.5f)
            {
                _gravityUp = ToVector3(math.normalizesafe(sample.Up, new float3(0f, 1f, 0f)));
            }
        }

        private void UpdateSupport()
        {
            Vector3 origin = rootBody.worldCenterOfMass + (_gravityUp * 0.15f);
            int count = UnityEngine.Physics.RaycastNonAlloc(
                origin,
                -_gravityUp,
                _groundHits,
                groundProbeDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);
            float bestDistance = float.PositiveInfinity;
            _contactCount = 0;
            _supportCenter = rootBody.worldCenterOfMass - (_gravityUp * 0.9f);
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = _groundHits[index];
                if (hit.rigidbody == rootBody || IsSelfCollider(hit.collider) || hit.distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = hit.distance;
                _supportCenter = hit.point;
                _contactCount = 1;
            }
        }

        private void UpdatePoweredSupport()
        {
            bool stable = motor != null && motor.HasStableSupport;
            bool freshContacts = HasFreshPoweredFootContacts();
            bool leftPlanted = freshContacts && _leftFootPlanted && leftFoot != null;
            bool rightPlanted = freshContacts && _rightFootPlanted && rightFoot != null;
            _contactCount = stable ? (leftPlanted ? 1 : 0) + (rightPlanted ? 1 : 0) : 0;
            if (!stable || _contactCount == 0)
            {
                _supportCenter = rootBody.worldCenterOfMass - _gravityUp * 0.9f;
                return;
            }

            if (leftPlanted && rightPlanted)
                _supportCenter = (leftFoot.position + rightFoot.position) * 0.5f;
            else if (leftPlanted)
                _supportCenter = leftFoot.position;
            else if (rightPlanted)
                _supportCenter = rightFoot.position;
            else
                _supportCenter = motor.SupportFeetPoint(_gravityUp);
        }

        private void UpdatePoweredAssist()
        {
            EarthSupportPolygon polygon = BuildPoweredSupportPolygon(
                out bool feetConfigured,
                out bool leftPlanted,
                out bool rightPlanted);
            EarthPoweredAssistRejection eligibility = EarthPoweredAssistEligibility.Evaluate(
                CurrentState.Mode,
                motor != null && motor.HasStableSupport,
                feetConfigured,
                leftPlanted,
                rightPlanted,
                polygon.IsValid);
            bool livePlantedSupport = eligibility == EarthPoweredAssistRejection.None;
            bool probeSurfaces = CurrentState.Mode == CharacterPhysicalMode.PhysicalAssist ||
                                 CurrentState.Mode == CharacterPhysicalMode.Stagger;
            EarthPhysicalSurfaceProbe braceProbe = default;
            EarthPhysicalSurfaceProbe reachProbe = default;
            EarthPhysicalSurfaceProbe fallArrestProbe = default;
            if (probeSurfaces)
            {
                Vector3 probeDirection = _lastPoweredImpactDirection.sqrMagnitude > 0.0001f
                    ? -_lastPoweredImpactDirection.normalized
                    : -transform.forward;
                braceProbe = ProbeSemanticSurface(
                    chest != null ? chest.position : rootBody.worldCenterOfMass,
                    probeDirection,
                    EarthSemanticSurfaceKind.Braceable);
                Vector3 reachOrigin = leftHand != null && rightHand != null
                    ? (leftHand.position + rightHand.position) * 0.5f
                    : rootBody.worldCenterOfMass;
                Vector3 reachDirection = rootBody.linearVelocity.sqrMagnitude > 0.01f
                    ? rootBody.linearVelocity.normalized
                    : motor != null ? motor.FacingForward : transform.forward;
                reachProbe = ProbeSemanticSurface(
                    reachOrigin,
                    reachDirection,
                    EarthSemanticSurfaceKind.ReachableSupport);
                fallArrestProbe = ProbeSemanticSurface(
                    head != null ? head.position : rootBody.worldCenterOfMass,
                    -_gravityUp,
                    EarthSemanticSurfaceKind.FallArrest);
            }
            var input = new EarthPoweredAssistInput(
                Time.fixedDeltaTime,
                CurrentState.Mode,
                ToFloat3(_gravityUp),
                ToFloat3(motor != null ? motor.FacingForward : transform.forward),
                ToFloat3(rootBody.worldCenterOfMass),
                ToFloat3(rootBody.linearVelocity),
                livePlantedSupport,
                feetConfigured && (leftPlanted || rightPlanted),
                in polygon,
                in braceProbe,
                in reachProbe,
                in fallArrestProbe);
            LastPoweredAssistOutput = _poweredAssist.Step(in input);
            if (!LastPoweredAssistOutput.EmitAction) return;
            PoweredActionRequestCount++;
            PhysicalActionRequested?.Invoke(LastPoweredAssistOutput.Action);
        }

        private EarthSupportPolygon BuildPoweredSupportPolygon(
            out bool feetConfigured,
            out bool leftPlanted,
            out bool rightPlanted)
        {
            feetConfigured = leftFoot != null && rightFoot != null;
            bool freshContacts = HasFreshPoweredFootContacts();
            leftPlanted = freshContacts && _leftFootPlanted;
            rightPlanted = freshContacts && _rightFootPlanted;
            if (!feetConfigured || (!leftPlanted && !rightPlanted))
                return default;

            Vector3 facing = motor != null ? motor.FacingForward : transform.forward;
            return EarthSupportPolygon.FromPlantedFeet(
                ToFloat3(leftFoot.position),
                ToFloat3(rightFoot.position),
                leftPlanted,
                rightPlanted,
                ToFloat3(_gravityUp),
                ToFloat3(facing),
                supportFootHalfLength,
                supportFootHalfWidth);
        }

        private bool HasFreshPoweredFootContacts()
        {
            if (_poweredFootContactFrame < 0) return false;
            int age = Time.frameCount - _poweredFootContactFrame;
            return age >= 0 && age <= 1;
        }

        private bool ValidatePoweredJointBindings(bool reportFailure)
        {
            bool valid = HasValidPoweredJointBindings();
            if (valid)
            {
                _reportedInvalidPoweredJointBinding = false;
                return true;
            }
            if (reportFailure && !_reportedInvalidPoweredJointBinding)
            {
                _reportedInvalidPoweredJointBinding = true;
                Debug.LogError(
                    $"{nameof(ActiveRagdollPuppet)} on '{name}' disabled powered assist. " +
                    "Every configured active-ragdoll joint requires an explicit body-region binding.",
                    this);
            }
            return false;
        }

        private bool HasValidPoweredJointBindings()
        {
            if (joints == null || joints.Length == 0) return false;
            for (int index = 0; index < joints.Length; index++)
            {
                ActiveRagdollJoint joint = joints[index];
                if (joint == null || !joint.HasConfiguredBodyRegion) return false;
            }
            return true;
        }

        private EarthPhysicalSurfaceProbe ProbeSemanticSurface(
            Vector3 origin,
            Vector3 direction,
            EarthSemanticSurfaceKind kind)
        {
            if (direction.sqrMagnitude <= 0.0001f) return default;
            float maximumReach = EarthPoweredPhysicalAssist.MaximumSemanticReach;
            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
                origin,
                semanticProbeRadius,
                direction.normalized,
                _semanticHits,
                maximumReach,
                semanticProbeMask,
                QueryTriggerInteraction.Ignore);
            int bestIndex = -1;
            float bestDistance = float.PositiveInfinity;
            for (int index = 0; index < hitCount && index < _semanticHits.Length; index++)
            {
                RaycastHit hit = _semanticHits[index];
                if (hit.collider == null || hit.rigidbody == rootBody ||
                    IsSelfCollider(hit.collider) || hit.distance >= bestDistance)
                    continue;
                CharacterSupportCandidate candidate = CharacterSupportRuntimeAdapter.Classify(
                    hit.collider,
                    hit.distance,
                    Vector3.Dot(hit.normal, _gravityUp));
                bool semanticMatch = kind == EarthSemanticSurfaceKind.Braceable
                    ? candidate.IsValid &&
                      candidate.Kind != CharacterSupportKind.DynamicDebris &&
                      candidate.Kind != CharacterSupportKind.ReleasedFracture
                    : candidate.IsValid && candidate.IsWalkable;
                if (!semanticMatch) continue;
                bestDistance = hit.distance;
                bestIndex = index;
            }
            if (bestIndex < 0) return default;
            RaycastHit selected = _semanticHits[bestIndex];
            return new EarthPhysicalSurfaceProbe(
                kind,
                ToFloat3(selected.point),
                ToFloat3(selected.normal),
                selected.distance,
                true);
        }

        private bool IsSelfCollider(Collider candidate)
        {
            for (int index = 0; index < selfColliders.Length; index++)
            {
                if (selfColliders[index] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private void HandleImpact(Vector3 point, float impulse)
        {
            InjectImpact(impulse);
            ImpactObserved?.Invoke(point, impulse);
        }

        private void HandleCollisionImpact(PhysicalCollisionImpact impact)
        {
            if (Time.time < _suppressImpactsUntil || rootBody == null) return;
            LastCollisionImpact = impact;
            LastCollisionWasSupport = CharacterSupportImpactSolver.IsSupportContact(
                    ToFloat3(_gravityUp),
                    ToFloat3(rootBody.worldCenterOfMass),
                    ToFloat3(impact.Point),
                    ToFloat3(impact.Normal),
                    impact.OtherBodyIsDynamic);
            if (LastCollisionWasSupport) return;
            InjectImpact(impact.Impulse);
            ImpactObserved?.Invoke(impact.Point, impact.Impulse);
        }

        private void PublishStateIfChanged()
        {
            if (_hasPublishedMode && CurrentState.Mode == _lastPublishedMode)
            {
                return;
            }

            _hasPublishedMode = true;
            _lastPublishedMode = CurrentState.Mode;
            StateChanged?.Invoke(CurrentState);
        }

        private void Initialize()
        {
            if (rootBody == null)
            {
                rootBody = GetComponent<Rigidbody>();
            }

            if (motor == null)
            {
                motor = GetComponent<PlanetMotor>();
            }

            if (impactTarget == null)
            {
                impactTarget = GetComponent<PhysicalImpactTarget>();
            }

            EnsureController();
            _poweredAssist.ResetTemporalState();
            _lastPoweredImpactDirection = Vector3.zero;
            LastPoweredImpactDecision = default;
            LastPoweredAssistOutput = default;
            CaptureRootConstraints();
            ConfigureSelfCollisionFiltering();
            // Scene rebuilds and domain reloads must always start with gameplay
            // authority on the motor. The controller's default enum value happens
            // to be AnimatedMotor, but relying on that left the Rigidbody/control
            // behaviours in whatever serialized or interrupted ragdoll state they
            // previously had until the first physics tick.
            _controller.Reset();
            CurrentState = new CharacterPhysicalState(
                new ActorId(Math.Max(1u, actorId)),
                CharacterPhysicalMode.AnimatedMotor,
                float3.zero,
                float3.zero,
                ToFloat3(transform.up),
                0f,
                0f,
                1f,
                RecoveryCandidate.None);
            if (rootBody != null)
            {
                rootBody.isKinematic = false;
                rootBody.detectCollisions = true;
            }
            ApplyControl(CurrentState);
        }

        private void CaptureRootConstraints()
        {
            if (_rootConstraintsCaptured || rootBody == null) return;
            _rootConstraintsCaptured = true;
            _ragdollRootConstraints = rootBody.constraints & ~RigidbodyConstraints.FreezeRotation;
            _motorRootConstraints = _ragdollRootConstraints | RigidbodyConstraints.FreezeRotation;
        }

        private void EnsureController()
        {
            if (_controller == null)
            {
                _controller = new CharacterPhysicalController(
                    new ActorId(Math.Max(1u, actorId)),
                    CharacterPhysicalTuning.Default);
            }
        }

        private void ConfigureSelfCollisionFiltering()
        {
            for (int first = 0; first < selfColliders.Length; first++)
            {
                Collider a = selfColliders[first];
                if (a == null)
                {
                    continue;
                }

                for (int second = first + 1; second < selfColliders.Length; second++)
                {
                    Collider b = selfColliders[second];
                    if (b != null)
                    {
                        UnityEngine.Physics.IgnoreCollision(a, b, true);
                    }
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (rootBody == null)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(rootBody.worldCenterOfMass, 0.08f);
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(_supportCenter, 0.08f);
            Gizmos.DrawLine(rootBody.worldCenterOfMass, _supportCenter);
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(rootBody.worldCenterOfMass, _gravityUp);

            for (int index = 0; index < joints.Length; index++)
            {
                ActiveRagdollJoint joint = joints[index];
                if (joint == null || joint.TargetPose == null)
                {
                    continue;
                }

                Gizmos.color = Color.Lerp(Color.green, Color.red, Mathf.Clamp01(joint.JointErrorDegrees / 90f));
                Gizmos.DrawLine(joint.transform.position, joint.TargetPose.position);
            }
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
