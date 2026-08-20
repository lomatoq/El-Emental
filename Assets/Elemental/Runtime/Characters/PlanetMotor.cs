using Elemental.Simulation.Characters;
using Elemental.Simulation.Gravity;
using Elemental.Runtime.Diagnostics;
using Elemental.Runtime.Physics;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class PlanetMotor : MonoBehaviour
    {
        private static readonly ProfilerMarker FixedTickMarker = new ProfilerMarker("Elemental.PlanetMotor.FixedTick");
        private const int GroundHitCapacity = 8;

        [Header("References")]
        [SerializeField] private GravityWorldBehaviour gravityWorld;
        [SerializeField] private Rigidbody targetBody;
        [SerializeField] private CapsuleCollider capsule;
        [SerializeField] private MonoBehaviour inputSourceBehaviour;
        [SerializeField] private Transform cameraFrame;
        [SerializeField] private PlanetMotorFeelProfile feelProfile;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float maxGroundSpeed = 8f;
        [SerializeField, Min(0.1f)] private float groundAcceleration = 45f;
        [SerializeField, Min(0.1f)] private float groundDeceleration = 58f;
        [SerializeField, Range(0f, 1f)] private float airControl = 0.35f;
        [SerializeField, Min(0.1f)] private float jumpSpeed = 8f;
        [SerializeField] private bool tankSteering;
        [SerializeField, Min(10f)] private float tankTurnRateDegrees = 145f;

        [Header("Grounding")]
        [SerializeField, Min(0.01f)] private float groundProbeDistance = 0.35f;
        [SerializeField, Range(0f, 89f)] private float maxSlopeAngle = 55f;
        [SerializeField, Min(0f)] private float adhesionSpring = 90f;
        [SerializeField, Min(0f)] private float adhesionDamping = 12f;
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Orientation")]
        [SerializeField, Min(0f)] private float orientationSpring = 35f;
        [SerializeField, Min(0f)] private float orientationDamping = 8f;
        [SerializeField, Min(0.1f)] private float maxOrientationTorque = 80f;
        [SerializeField, Min(30f)] private float maximumOrientationDegreesPerSecond = 540f;

        private readonly RaycastHit[] _groundHits = new RaycastHit[GroundHitCapacity];
        private IPlanetMotorInputSource _inputSource;
        private uint _tick;
        private int _ignoreGroundTicks;
        private byte _groundContactCount;
        private Vector3 _localUp = Vector3.up;
        private Vector3 _groundNormal = Vector3.up;
        private float _groundDistance;
        private Vector3 _lastGravityAcceleration;
        private Vector3 _aimForward;
        private bool _hasAimForward;
        private ActiveRagdollPuppet _puppet;
        private SupportFrameSnapshot _movingSupport;
        private int _movingSupportTicks;
        private uint _lastCarrySurfaceId;
        private uint _lastCarryGeneration;
        private Vector3 _lastCarrySurfaceVelocity;
        private PlanetJumpWindowState _jumpWindow;
        private float _castBrace01;
        private EarthMotionReproRecorder _motionRecorder;
        private MotionFaultKind _pendingMotionFaults;

        public bool IsGrounded { get; private set; }
        public Vector3 LocalUp => _localUp;
        public Vector3 GravityAcceleration => _lastGravityAcceleration;
        public Vector3 FacingForward => _hasAimForward ? _aimForward : transform.forward;
        public Rigidbody Body => targetBody;
        public CapsuleCollider Capsule => capsule;
        public LayerMask GroundMask => groundMask;
        public float MaximumSlopeAngle => maxSlopeAngle;
        public float GroundProbeDistance => groundProbeDistance;
        public PlanetMotorCommand LastCommand { get; private set; }
        public uint MovingSurfaceId => _movingSupportTicks > 0 ? _movingSupport.SurfaceId : 0u;
        public uint MovingSurfaceGeneration => _movingSupportTicks > 0 ? _movingSupport.Generation : 0u;
        public SupportFrameSnapshot CurrentSupportFrame => _movingSupportTicks > 0 ? _movingSupport : default;
        public bool HasStableSupport => IsGrounded || _movingSupportTicks > 0;
        public bool AcceptsMovingSupport => _ignoreGroundTicks <= 0;

        /// <summary>
        /// Removes residual player-authored tangential momentum without cancelling
        /// gravity or an active moving support. This is intentionally explicit and
        /// is used by editor proof/reset tooling after it releases synthetic input.
        /// </summary>
        public void SettleTangentialMotion()
        {
            if (targetBody == null) return;
            Vector3 up = _localUp.sqrMagnitude > 0.5f ? _localUp.normalized : transform.up;
            float radialSpeed = Vector3.Dot(targetBody.linearVelocity, up);
            Vector3 supportVelocity = _movingSupportTicks > 0
                ? ToVector3(_movingSupport.LinearVelocity)
                : Vector3.zero;
            Vector3 supportTangent = Vector3.ProjectOnPlane(supportVelocity, up);
            // On stable ground an outward residual is always numerical/support debt;
            // retain only a small inward component so contact can settle naturally.
            float settledRadial = HasStableSupport ? Mathf.Min(0f, radialSpeed) : radialSpeed;
            targetBody.linearVelocity = supportTangent + up * settledRadial;
            targetBody.angularVelocity = Vector3.zero;
            targetBody.WakeUp();
        }
        public PlanetLocomotionTelemetry Telemetry { get; private set; }
        public PlanetMotionState MotionState { get; private set; } = PlanetMotionState.AirborneFalling;
        public EarthMotionReproRecorder MotionRecorder => _motionRecorder;

        public Vector3 SupportFeetPoint(Vector3 up) => FeetPoint(
            up.sqrMagnitude > 0.5f ? up.normalized : _localUp);

        public void ApplyMovingSupport(
            in SupportFrameSnapshot support,
            Vector3 supportTopPoint,
            float maximumSpeed,
            float maximumAcceleration)
        {
            if (targetBody == null || !support.IsValid) return;
            SupportFrameSnapshot contactSupport = support.WithContactPoint(ToFloat3(supportTopPoint));
            SupportFrameContinuity continuity = MovingSurfaceSolver.ClassifyContinuity(
                _movingSupport,
                contactSupport,
                Mathf.Max(0.75f, maximumSpeed * Time.fixedDeltaTime * 2.5f),
                70f * Mathf.Deg2Rad);
            if (continuity == SupportFrameContinuity.Discontinuous)
                _pendingMotionFaults |= MotionFaultKind.SupportDiscontinuity;
            else if (continuity == SupportFrameContinuity.NewGeneration &&
                     _movingSupport.IsValid && _movingSupport.SurfaceId == contactSupport.SurfaceId)
                _pendingMotionFaults |= MotionFaultKind.SupportGenerationMismatch;
            Vector3 up = ToVector3(contactSupport.Up);
            Vector3 feet = FeetPoint(up);
            float verticalError = Vector3.Dot(supportTopPoint - feet, up);
            if (contactSupport.Emerging)
                verticalError = Mathf.Max(0f, verticalError);
            float3 acceleration = MovingSurfaceSolver.CarryAcceleration(
                ToFloat3(targetBody.linearVelocity),
                contactSupport.ContactPointVelocity,
                contactSupport.Up,
                verticalError,
                maximumSpeed,
                maximumAcceleration,
                Time.fixedDeltaTime);
            Vector3 upAcceleration = ToVector3(acceleration);
            Vector3 supportVelocity = ToVector3(contactSupport.ContactPointVelocity);
            bool sameSupport = continuity == SupportFrameContinuity.Stable &&
                               _lastCarrySurfaceId == contactSupport.SurfaceId &&
                               _lastCarryGeneration == contactSupport.Generation;
            Vector3 tangentVelocityChange = ToVector3(
                MovingSurfaceSolver.TangentCarryVelocityChange(
                    ToFloat3(_lastCarrySurfaceVelocity),
                    contactSupport.ContactPointVelocity,
                    contactSupport.Up,
                    sameSupport,
                    maximumAcceleration,
                    Time.fixedDeltaTime));
            // A discontinuous pooled/repositioned support establishes a new frame but
            // never injects its teleport delta into the character.
            if (continuity == SupportFrameContinuity.Discontinuous)
                tangentVelocityChange = Vector3.zero;
            Vector3 carryAcceleration = Vector3.ClampMagnitude(
                upAcceleration + (tangentVelocityChange / Mathf.Max(0.0001f, Time.fixedDeltaTime)),
                Mathf.Max(0.1f, maximumAcceleration));
            if (_puppet != null)
                _puppet.ApplyUniformVelocityChange(carryAcceleration * Time.fixedDeltaTime);
            else
                targetBody.AddForce(carryAcceleration, ForceMode.Acceleration);
            _movingSupport = contactSupport;
            _movingSupportTicks = 3;
            _lastCarrySurfaceId = contactSupport.SurfaceId;
            _lastCarryGeneration = contactSupport.Generation;
            _lastCarrySurfaceVelocity = supportVelocity;
        }

        public void ApplyMovingSupport(
            in MovingSupportSnapshot support,
            Vector3 supportTopPoint,
            float maximumSpeed,
            float maximumAcceleration) =>
            ApplyMovingSupport(support.Frame, supportTopPoint, maximumSpeed, maximumAcceleration);

        public void ApplyMovingSupportAnchorCorrection(
            Vector3 desiredRiderCenter,
            float stiffness,
            float maximumAcceleration)
        {
            if (targetBody == null || _movingSupportTicks <= 0 || !_movingSupport.IsValid) return;
            Vector3 delta = ToVector3(MovingSurfaceSolver.AnchorCorrectionVelocityChange(
                ToFloat3(targetBody.worldCenterOfMass),
                ToFloat3(desiredRiderCenter),
                _movingSupport.Up,
                stiffness,
                maximumAcceleration,
                Time.fixedDeltaTime));
            if (delta.sqrMagnitude <= 0f) return;
            if (_puppet != null) _puppet.ApplyUniformVelocityChange(delta);
            else targetBody.AddForce(delta, ForceMode.VelocityChange);
        }

        public void Configure(
            GravityWorldBehaviour world,
            Rigidbody body,
            CapsuleCollider configuredCapsule,
            MonoBehaviour inputSource,
            Transform configuredCameraFrame)
        {
            gravityWorld = world;
            targetBody = body;
            capsule = configuredCapsule;
            inputSourceBehaviour = inputSource;
            cameraFrame = configuredCameraFrame;
            ResolveReferences();
        }

        public void ConfigureFeel(
            float configuredMaxGroundSpeed,
            float configuredGroundAcceleration,
            float configuredAirControl)
        {
            maxGroundSpeed = Mathf.Max(0.1f, configuredMaxGroundSpeed);
            groundAcceleration = Mathf.Max(0.1f, configuredGroundAcceleration);
            airControl = Mathf.Clamp01(configuredAirControl);
        }

        public void ConfigureFeel(PlanetMotorFeelProfile configuredProfile)
        {
            feelProfile = configuredProfile;
            ApplyFeelProfile();
        }

        public void ConfigureInputSource(MonoBehaviour configuredInputSource)
        {
            inputSourceBehaviour = configuredInputSource;
            _inputSource = configuredInputSource as IPlanetMotorInputSource;
        }

        public void SetCastStance(float brace01) => _castBrace01 = Mathf.Clamp01(brace01);

        public void ConfigureTankSteering(bool enabled, float turnRateDegreesPerSecond)
        {
            tankSteering = enabled;
            tankTurnRateDegrees = Mathf.Max(10f, turnRateDegreesPerSecond);
            if (enabled && !_hasAimForward) SetAimDirection(transform.forward);
        }

        public void ConfigureOrientationFeel(
            float spring,
            float damping,
            float maximumTorque)
        {
            orientationSpring = Mathf.Max(0f, spring);
            orientationDamping = Mathf.Max(0f, damping);
            maxOrientationTorque = Mathf.Max(0.1f, maximumTorque);
        }

        public void SetAimDirection(Vector3 worldDirection)
        {
            float3 solved = PlanetFacingSolver.SolveTangentForward(
                ToFloat3(_localUp),
                ToFloat3(worldDirection),
                ToFloat3(_hasAimForward ? _aimForward : transform.forward));
            _aimForward = ToVector3(solved);
            _hasAimForward = true;
        }

        public void BeginExternalLaunch(int ignoredGroundTicks)
        {
            _ignoreGroundTicks = Mathf.Max(_ignoreGroundTicks, Mathf.Max(1, ignoredGroundTicks));
            IsGrounded = false;
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyFeelProfile();
            targetBody.useGravity = false;
            targetBody.maxAngularVelocity = 20f;
        }

        private void ResolveReferences()
        {
            if (targetBody == null)
            {
                targetBody = GetComponent<Rigidbody>();
            }

            if (capsule == null)
            {
                capsule = GetComponent<CapsuleCollider>();
            }

            _inputSource = inputSourceBehaviour as IPlanetMotorInputSource;
            if (_puppet == null) _puppet = GetComponent<ActiveRagdollPuppet>();
            if (_motionRecorder == null) _motionRecorder = GetComponent<EarthMotionReproRecorder>();
            if (_motionRecorder == null) _motionRecorder = gameObject.AddComponent<EarthMotionReproRecorder>();
            uint profileHash = feelProfile != null
                ? math.hash(new float4(
                    feelProfile.MaximumGroundSpeed,
                    feelProfile.Acceleration,
                    feelProfile.JumpSpeed,
                    feelProfile.MaximumSlopeAngle))
                : 0u;
            _motionRecorder.Configure(0u, profileHash);
        }

        private void FixedUpdate()
        {
            if (gravityWorld == null || !gravityWorld.IsReady || targetBody == null)
            {
                return;
            }

            using (FixedTickMarker.Auto())
            {
                GravitySample gravity = SampleGravity();
                if (!gravity.IsFinite || math.lengthsq(gravity.Up) < 0.5f)
                {
                    return;
                }

                _localUp = ToVector3(math.normalizesafe(gravity.Up, new float3(0f, 1f, 0f)));
                _lastGravityAcceleration = ToVector3(gravity.Acceleration);
                if (_hasAimForward)
                {
                    _aimForward = ToVector3(PlanetFacingSolver.SolveTangentForward(
                        ToFloat3(_localUp),
                        ToFloat3(_aimForward),
                        ToFloat3(transform.forward)));
                }
                UpdateGrounding();

                LastCommand = _inputSource?.SampleCommand(_tick)
                    ?? new PlanetMotorCommand(_tick, float2.zero, false);
                ushort coyoteTicks = (ushort)Mathf.Clamp(Mathf.CeilToInt(
                    (feelProfile != null ? feelProfile.CoyoteSeconds : 0.12f) /
                    Mathf.Max(0.001f, Time.fixedDeltaTime)), 1, ushort.MaxValue);
                ushort bufferTicks = (ushort)Mathf.Clamp(Mathf.CeilToInt(
                    (feelProfile != null ? feelProfile.JumpBufferSeconds : 0.14f) /
                    Mathf.Max(0.001f, Time.fixedDeltaTime)), 1, ushort.MaxValue);
                _jumpWindow = _jumpWindow.Step(
                    HasStableSupport, LastCommand.JumpPressed, coyoteTicks, bufferTicks);
                _tick++;

                ApplyMovement(LastCommand);
                ApplyOrientation();
                RecordMotionFrame();

                if (_movingSupportTicks > 0)
                {
                    _movingSupportTicks--;
                    if (_movingSupportTicks == 0)
                    {
                        _lastCarrySurfaceId = 0u;
                        _lastCarryGeneration = 0u;
                        _lastCarrySurfaceVelocity = Vector3.zero;
                    }
                }

                if (_ignoreGroundTicks > 0)
                {
                    _ignoreGroundTicks--;
                }
            }
        }

        private GravitySample SampleGravity()
        {
            Vector3 center = targetBody.worldCenterOfMass;
            return gravityWorld.World.Sample(new float3(center.x, center.y, center.z), _tick);
        }

        private void UpdateGrounding()
        {
            IsGrounded = false;
            _groundContactCount = 0;
            _groundNormal = _localUp;
            _groundDistance = groundProbeDistance;

            if (_ignoreGroundTicks > 0)
            {
                return;
            }

            Vector3 scale = transform.lossyScale;
            float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) * 0.92f;
            float halfHeight = Mathf.Max(radius, capsule.height * 0.5f * Mathf.Abs(scale.y));
            float centerToBottom = Mathf.Max(0f, halfHeight - radius);
            Vector3 origin = transform.TransformPoint(capsule.center);
            float maxDistance = centerToBottom + groundProbeDistance;
            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
                origin,
                radius,
                -_localUp,
                _groundHits,
                maxDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);

            float bestDistance = float.PositiveInfinity;
            float minimumSlopeDot = Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad);

            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _groundHits[index];
                if (hit.collider == null || hit.collider == capsule || hit.rigidbody == targetBody ||
                    (_puppet != null && _puppet.OwnsCollider(hit.collider)))
                {
                    continue;
                }

                float slopeDot = Vector3.Dot(hit.normal, _localUp);
                if (slopeDot < minimumSlopeDot)
                {
                    continue;
                }

                _groundContactCount++;
                if (hit.distance >= bestDistance) continue;

                bestDistance = hit.distance;
                _groundNormal = hit.normal;
            }

            if (!float.IsFinite(bestDistance))
            {
                return;
            }

            _groundDistance = Mathf.Max(0f, bestDistance - centerToBottom);
            IsGrounded = _groundDistance <= groundProbeDistance;
        }

        private void ApplyMovement(PlanetMotorCommand command)
        {
            Vector3 referenceForward = cameraFrame != null ? cameraFrame.forward : transform.forward;
            GravityFrame.BuildTangentBasis(
                ToFloat3(_localUp),
                ToFloat3(referenceForward),
                out float3 forwardFloat,
                out float3 rightFloat);

            Vector3 forward = ToVector3(forwardFloat);
            Vector3 right = ToVector3(rightFloat);
            Vector2 move = new Vector2(command.Move.x, command.Move.y);
            Vector3 desiredDirection;
            if (tankSteering)
            {
                float3 turned = PlanetTankSteeringSolver.Turn(
                    ToFloat3(_localUp),
                    ToFloat3(_hasAimForward ? _aimForward : forward),
                    move.x,
                    tankTurnRateDegrees,
                    Time.fixedDeltaTime);
                _aimForward = ToVector3(turned);
                _hasAimForward = true;
                desiredDirection = _aimForward * move.y;
            }
            else
            {
                desiredDirection = (forward * move.y) + (right * move.x);
            }

            bool stableSupport = HasStableSupport;
            if (stableSupport)
            {
                Vector3 supportNormal = IsGrounded
                    ? _groundNormal
                    : ToVector3(_movingSupport.Up);
                desiredDirection = Vector3.ProjectOnPlane(desiredDirection, supportNormal);
            }

            desiredDirection = Vector3.ClampMagnitude(desiredDirection, 1f);
            Vector3 velocity = targetBody.linearVelocity;
            Vector3 supportVelocity = _movingSupportTicks > 0
                ? ToVector3(_movingSupport.ContactPointVelocity)
                : Vector3.zero;
            Vector3 relativeVelocity = velocity - supportVelocity;
            Vector3 tangentVelocity = Vector3.ProjectOnPlane(relativeVelocity, _localUp);
            float castSpeed = feelProfile != null
                ? Mathf.Lerp(feelProfile.CastSpeedMultiplier, feelProfile.BraceSpeedMultiplier, _castBrace01)
                : Mathf.Lerp(0.46f, 0.2f, _castBrace01);
            float speedMultiplier = _castBrace01 > 0.001f ? castSpeed : 1f;
            Vector3 desiredVelocity = desiredDirection * maxGroundSpeed * speedMultiplier;
            bool accelerating = desiredVelocity.sqrMagnitude > tangentVelocity.sqrMagnitude + 0.01f;
            float accelerationLimit = (accelerating ? groundAcceleration : groundDeceleration) *
                                      (stableSupport ? 1f : airControl);
            if (IsGrounded && feelProfile != null)
                accelerationLimit *= feelProfile.TractionMultiplier;
            Vector3 acceleration = Vector3.ClampMagnitude(
                (desiredVelocity - tangentVelocity) / Time.fixedDeltaTime,
                accelerationLimit);
            targetBody.AddForce(acceleration, ForceMode.Acceleration);

            if (IsGrounded)
            {
                float normalSpeed = Vector3.Dot(relativeVelocity, _groundNormal);
                float inwardAdhesion = PlanetGroundAdhesionSolver.SolveInwardAcceleration(
                    _groundDistance,
                    groundProbeDistance,
                    normalSpeed,
                    adhesionSpring,
                    adhesionDamping);
                if (inwardAdhesion > 0f)
                    targetBody.AddForce(-_groundNormal * inwardAdhesion, ForceMode.Acceleration);
            }

            if (_jumpWindow.CanConsume)
            {
                Vector3 jumpVelocityChange = _localUp * jumpSpeed;
                if (_puppet != null)
                    _puppet.ApplyUniformVelocityChange(jumpVelocityChange);
                else
                    targetBody.AddForce(jumpVelocityChange, ForceMode.VelocityChange);
                IsGrounded = false;
                _jumpWindow = _jumpWindow.Consume();
                _ignoreGroundTicks = 4;
                _movingSupportTicks = 0;
                _lastCarrySurfaceId = 0u;
                _lastCarryGeneration = 0u;
                _lastCarrySurfaceVelocity = Vector3.zero;
            }

            Telemetry = new PlanetLocomotionTelemetry(
                _tick,
                IsGrounded,
                _localUp,
                tangentVelocity.magnitude,
                desiredVelocity.magnitude,
                _castBrace01,
                _movingSupportTicks > 0 ? _movingSupport.SurfaceId : 0u,
                _jumpWindow.CoyoteTicks,
                _jumpWindow.BufferTicks);
        }

        private void ApplyFeelProfile()
        {
            if (feelProfile == null) return;
            maxGroundSpeed = Mathf.Max(0.1f, feelProfile.MaximumGroundSpeed);
            groundAcceleration = Mathf.Max(0.1f, feelProfile.Acceleration);
            groundDeceleration = Mathf.Max(0.1f, feelProfile.Deceleration);
            airControl = Mathf.Clamp01(feelProfile.AirControl);
            jumpSpeed = Mathf.Max(0.1f, feelProfile.JumpSpeed);
            tankTurnRateDegrees = Mathf.Max(10f, feelProfile.TurnResponseDegrees);
            maxSlopeAngle = Mathf.Clamp(feelProfile.MaximumSlopeAngle, 1f, 89f);
        }

        private void RecordMotionFrame()
        {
            if (_motionRecorder == null || targetBody == null) return;
            CharacterPhysicalMode physicalMode = _puppet != null
                ? _puppet.CurrentState.Mode
                : CharacterPhysicalMode.AnimatedMotor;
            float verticalSpeed = Vector3.Dot(targetBody.linearVelocity, _localUp);
            bool jumpStarting = !IsGrounded && _ignoreGroundTicks >= 3 && verticalSpeed > 0f;
            bool surfRiding = _movingSupportTicks > 0 &&
                              (_movingSupport.SurfaceId & 0xFF000000u) == 0x5F000000u;
            MotionState = PlanetMotionIntegritySolver.ResolveState(
                IsGrounded,
                IsGrounded && _groundContactCount <= 1,
                _movingSupportTicks > 0,
                jumpStarting,
                verticalSpeed,
                _castBrace01,
                physicalMode,
                surfRiding,
                false);
            var frame = new PlanetMotionFrame(
                _tick,
                MotionState,
                ToFloat3(targetBody.position),
                ToMathQuaternion(targetBody.rotation),
                ToFloat3(targetBody.linearVelocity),
                ToFloat3(targetBody.angularVelocity),
                LastCommand.Move,
                LastCommand.JumpPressed,
                IsGrounded,
                _groundContactCount,
                _movingSupportTicks > 0 ? _movingSupport : default);
            MotionFaultKind faults = PlanetMotionIntegritySolver.Evaluate(frame, 80f, 45f) |
                                     _pendingMotionFaults;
            _pendingMotionFaults = MotionFaultKind.None;
            _motionRecorder.Record(frame, faults);
        }

        private Vector3 FeetPoint(Vector3 up)
        {
            Vector3 scale = transform.lossyScale;
            float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfHeight = Mathf.Max(radius, capsule.height * 0.5f * Mathf.Abs(scale.y));
            return transform.TransformPoint(capsule.center) - up * halfHeight;
        }

        private void ApplyOrientation()
        {
            Quaternion desiredRotation = _hasAimForward
                ? Quaternion.LookRotation(_aimForward, _localUp)
                : Quaternion.FromToRotation(transform.up, _localUp) * targetBody.rotation;
            CharacterPhysicalMode mode = _puppet != null
                ? _puppet.CurrentState.Mode
                : CharacterPhysicalMode.AnimatedMotor;
            if (mode == CharacterPhysicalMode.AnimatedMotor ||
                mode == CharacterPhysicalMode.PhysicalAssist ||
                mode == CharacterPhysicalMode.Stagger)
            {
                quaternion solved = PlanetOrientationSolver.Step(
                    ToMathQuaternion(targetBody.rotation),
                    ToMathQuaternion(desiredRotation),
                    Mathf.Sqrt(Mathf.Max(0.01f, orientationSpring)) * 1.8f,
                    maximumOrientationDegreesPerSecond,
                    Time.fixedDeltaTime);
                targetBody.angularVelocity = Vector3.Lerp(
                    targetBody.angularVelocity,
                    Vector3.zero,
                    1f - Mathf.Exp(-orientationDamping * Time.fixedDeltaTime));
                targetBody.MoveRotation(ToUnityQuaternion(solved));
                return;
            }
            Quaternion error = desiredRotation * Quaternion.Inverse(targetBody.rotation);
            error.ToAngleAxis(out float angleDegrees, out Vector3 axis);

            if (angleDegrees > 180f)
            {
                angleDegrees -= 360f;
            }

            if (!IsFinite(axis) || Mathf.Abs(angleDegrees) < 0.001f)
            {
                return;
            }

            Vector3 torque = (axis.normalized * (angleDegrees * Mathf.Deg2Rad * orientationSpring))
                - (targetBody.angularVelocity * orientationDamping);
            targetBody.AddTorque(Vector3.ClampMagnitude(torque, maxOrientationTorque), ForceMode.Acceleration);
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static quaternion ToMathQuaternion(Quaternion value) =>
            new quaternion(value.x, value.y, value.z, value.w);

        private static Quaternion ToUnityQuaternion(quaternion value) =>
            new Quaternion(value.value.x, value.value.y, value.value.z, value.value.w);

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }
    }

    public readonly struct PlanetLocomotionTelemetry
    {
        public PlanetLocomotionTelemetry(
            uint tick, bool grounded, Vector3 localUp, float speed, float desiredSpeed,
            float brace01, uint supportId, ushort coyoteTicks, ushort bufferTicks)
        {
            Tick = tick;
            Grounded = grounded;
            LocalUp = localUp;
            Speed = speed;
            DesiredSpeed = desiredSpeed;
            Brace01 = brace01;
            SupportId = supportId;
            CoyoteTicks = coyoteTicks;
            BufferTicks = bufferTicks;
        }

        public uint Tick { get; }
        public bool Grounded { get; }
        public Vector3 LocalUp { get; }
        public float Speed { get; }
        public float DesiredSpeed { get; }
        public float Brace01 { get; }
        public uint SupportId { get; }
        public ushort CoyoteTicks { get; }
        public ushort BufferTicks { get; }
    }
}
