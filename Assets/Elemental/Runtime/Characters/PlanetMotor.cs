using Elemental.Simulation.Characters;
using Elemental.Simulation.Gravity;
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

        private readonly RaycastHit[] _groundHits = new RaycastHit[GroundHitCapacity];
        private IPlanetMotorInputSource _inputSource;
        private uint _tick;
        private int _ignoreGroundTicks;
        private Vector3 _localUp = Vector3.up;
        private Vector3 _groundNormal = Vector3.up;
        private float _groundDistance;
        private Vector3 _aimForward;
        private bool _hasAimForward;
        private ActiveRagdollPuppet _puppet;
        private MovingSupportSnapshot _movingSupport;
        private int _movingSupportTicks;
        private PlanetJumpWindowState _jumpWindow;
        private float _castBrace01;

        public bool IsGrounded { get; private set; }
        public Vector3 LocalUp => _localUp;
        public Vector3 FacingForward => _hasAimForward ? _aimForward : transform.forward;
        public PlanetMotorCommand LastCommand { get; private set; }
        public uint MovingSurfaceId => _movingSupportTicks > 0 ? _movingSupport.SurfaceId : 0u;
        public PlanetLocomotionTelemetry Telemetry { get; private set; }

        public void ApplyMovingSupport(
            in MovingSupportSnapshot support,
            Vector3 supportTopPoint,
            float maximumSpeed,
            float maximumAcceleration)
        {
            if (targetBody == null || !support.IsValid) return;
            Vector3 up = ToVector3(support.Up);
            Vector3 feet = FeetPoint(up);
            float verticalError = Vector3.Dot(supportTopPoint - feet, up);
            float3 acceleration = MovingSurfaceSolver.CarryAcceleration(
                ToFloat3(targetBody.linearVelocity),
                support.PointVelocity,
                support.Up,
                verticalError,
                maximumSpeed,
                maximumAcceleration,
                Time.fixedDeltaTime);
            targetBody.AddForce(ToVector3(acceleration), ForceMode.Acceleration);
            _movingSupport = support;
            _movingSupportTicks = 3;
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

        public void SetCastStance(float brace01) => _castBrace01 = Mathf.Clamp01(brace01);

        public void ConfigureTankSteering(bool enabled, float turnRateDegreesPerSecond)
        {
            tankSteering = enabled;
            tankTurnRateDegrees = Mathf.Max(10f, turnRateDegreesPerSecond);
            if (enabled && !_hasAimForward) SetAimDirection(transform.forward);
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
                    IsGrounded, LastCommand.JumpPressed, coyoteTicks, bufferTicks);
                _tick++;

                ApplyMovement(LastCommand);
                ApplyOrientation();

                if (_movingSupportTicks > 0) _movingSupportTicks--;

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
                if (slopeDot < minimumSlopeDot || hit.distance >= bestDistance)
                {
                    continue;
                }

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

            if (IsGrounded)
            {
                desiredDirection = Vector3.ProjectOnPlane(desiredDirection, _groundNormal);
            }

            desiredDirection = Vector3.ClampMagnitude(desiredDirection, 1f);
            Vector3 velocity = targetBody.linearVelocity;
            Vector3 supportVelocity = _movingSupportTicks > 0
                ? ToVector3(_movingSupport.PointVelocity)
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
                                      (IsGrounded ? 1f : airControl);
            if (IsGrounded && feelProfile != null)
                accelerationLimit *= feelProfile.TractionMultiplier;
            Vector3 acceleration = Vector3.ClampMagnitude(
                (desiredVelocity - tangentVelocity) / Time.fixedDeltaTime,
                accelerationLimit);
            targetBody.AddForce(acceleration, ForceMode.Acceleration);

            if (IsGrounded)
            {
                float compression = Mathf.Clamp01(1f - (_groundDistance / groundProbeDistance));
                float normalSpeed = Vector3.Dot(relativeVelocity, _groundNormal);
                float adhesion = (compression * adhesionSpring) - (normalSpeed * adhesionDamping);
                targetBody.AddForce(
                    _groundNormal * Mathf.Clamp(adhesion, -adhesionSpring, adhesionSpring),
                    ForceMode.Acceleration);
                if (feelProfile != null && normalSpeed > 0f)
                    targetBody.AddForce(-_groundNormal * Mathf.Min(
                        normalSpeed / Mathf.Max(0.001f, Time.fixedDeltaTime),
                        feelProfile.GroundSnapSpeed), ForceMode.Acceleration);
            }

            if (_jumpWindow.CanConsume)
            {
                Vector3 inherited = Vector3.ProjectOnPlane(supportVelocity - velocity, _localUp);
                targetBody.AddForce(inherited, ForceMode.VelocityChange);
                targetBody.AddForce(_localUp * jumpSpeed, ForceMode.VelocityChange);
                IsGrounded = false;
                _jumpWindow = _jumpWindow.Consume();
                _ignoreGroundTicks = 4;
                _movingSupportTicks = 0;
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
