using Elemental.Runtime.Characters;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Combat;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    /// <summary>
    /// Final upper-body adaptation pass. Animator/magic authored pose evaluates
    /// first, organic motion evaluates next, this bounded inertial pass evaluates
    /// last. Feet, knees and hips are never written here.
    /// </summary>
    [DefaultExecutionOrder(900)]
    [DisallowMultipleComponent]
    public sealed class HumanoidProceduralBodyResponse : MonoBehaviour
    {
        private static readonly ProfilerMarker BodyMarker =
            new ProfilerMarker("Elemental.Character.ProceduralBody");

        [SerializeField] private Animator animator;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private Rigidbody rootBody;
        [SerializeField] private HumanoidRagdollRig ragdoll;
        [SerializeField] private HumanoidCharacterPresentation presentation;
        [SerializeField] private EarthCharacterImpactTarget impactTarget;
        [SerializeField, Range(0.25f, 1f)] private float impactTransferWeight = 0.88f;
        [SerializeField, Range(60f, 200f)] private float impactAngularVelocityCap = 170f;

        private Transform _chest;
        private Transform _head;
        private Vector3 _previousTangentVelocity;
        private bool _hasVelocity;
        private bool _subscribed;
        private EarthInertialBodyState _state;
        private float3 _pendingImpactKick;
        private float _impactChestTransfer = 1f;
        private float _impactHeadTransfer = 0.30f;

        public float3 CurrentAnglesDegrees { get; private set; }
        public float3 CurrentImpactAnglesDegrees { get; private set; }
        public int AcceptedProceduralImpactCount { get; private set; }

        public void Configure(
            Animator configuredAnimator,
            PlanetMotor configuredMotor,
            Rigidbody configuredRootBody,
            HumanoidRagdollRig configuredRagdoll,
            HumanoidCharacterPresentation configuredPresentation)
        {
            Unsubscribe();
            animator = configuredAnimator;
            motor = configuredMotor;
            rootBody = configuredRootBody;
            ragdoll = configuredRagdoll;
            presentation = configuredPresentation;
            impactTarget = GetComponentInParent<EarthCharacterImpactTarget>();
            CacheBones();
            if (isActiveAndEnabled) Subscribe();
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (motor == null) motor = GetComponentInParent<PlanetMotor>();
            if (rootBody == null) rootBody = GetComponentInParent<Rigidbody>();
            if (ragdoll == null) ragdoll = GetComponent<HumanoidRagdollRig>();
            if (presentation == null) presentation = GetComponent<HumanoidCharacterPresentation>();
            if (impactTarget == null) impactTarget = GetComponentInParent<EarthCharacterImpactTarget>();
            CacheBones();
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            Unsubscribe();
            _state = default;
            _pendingImpactKick = float3.zero;
            _hasVelocity = false;
            CurrentAnglesDegrees = float3.zero;
            CurrentImpactAnglesDegrees = float3.zero;
            AcceptedProceduralImpactCount = 0;
        }

        private void Subscribe()
        {
            if (_subscribed || impactTarget == null) return;
            impactTarget.WorldResponseRequested += OnWorldResponse;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (impactTarget != null)
                impactTarget.WorldResponseRequested -= OnWorldResponse;
            _subscribed = false;
        }

        private void OnWorldResponse(EarthWorldResponseEvent response)
        {
            if (impactTarget == null || response.TargetStableId != impactTarget.StableFighterId)
                return;
            if (EarthImpactPresentationOwnership.Resolve(response.Response) !=
                EarthImpactPresentationOwner.ProceduralAngularSpring)
                return;
            presentation?.NotifyImpactResponse(response.Response);
            Vector3 worldDirection = new Vector3(
                response.Direction.x,
                response.Direction.y,
                response.Direction.z);
            Vector3 local = transform.InverseTransformDirection(worldDirection);
            bool headHit = _head != null && _chest != null &&
                           Vector3.SqrMagnitude(ToVector3(response.Point) - _head.position) <
                           Vector3.SqrMagnitude(ToVector3(response.Point) - _chest.position);
            _impactChestTransfer = headHit ? 0.62f : 1f;
            _impactHeadTransfer = headHit ? 0.78f : 0.30f;
            _pendingImpactKick += EarthInertialBodyMotionSolver.ResolveDirectionalAngularVelocity(
                new float3(local.x, local.y, local.z),
                math.lerp(0.8f, 4.6f, response.Intensity01),
                impactTransferWeight,
                impactAngularVelocityCap);
            _pendingImpactKick = math.clamp(
                _pendingImpactKick,
                new float3(-impactAngularVelocityCap),
                new float3(impactAngularVelocityCap));
            AcceptedProceduralImpactCount++;
        }

        private void LateUpdate()
        {
            if (animator == null || motor == null || rootBody == null ||
                _chest == null || _head == null) return;
            // The Animator/EAMM base pose is evaluated on scaled GameTime. Do not
            // integrate state or multiply another additive body offset into frozen
            // bones while a capture or gameplay pause holds that clock at zero.
            if (Time.deltaTime <= 0f) return;
            using (BodyMarker.Auto())
            {
                float dt = Mathf.Max(0.0001f, Time.deltaTime);
                Vector3 up = motor.LocalUp.sqrMagnitude > 0.5f
                    ? motor.LocalUp.normalized
                    : transform.up;
                Vector3 supportVelocity = Vector3.zero;
                if (motor.CurrentSupportFrame.IsValid)
                {
                    float3 source = motor.CurrentSupportFrame.ContactPointVelocity;
                    supportVelocity = new Vector3(source.x, source.y, source.z);
                }
                Vector3 tangentVelocity = Vector3.ProjectOnPlane(
                    rootBody.linearVelocity - supportVelocity,
                    up);
                bool protectedMantle = presentation != null &&
                    presentation.CurrentAuthoredAction == EarthAuthoredActionId.Mantle;
                if (protectedMantle)
                {
                    // Do not accumulate hidden acceleration/spring state during
                    // motor-owned traversal and release it as a chest snap on the
                    // first grounded frame after the mantle.
                    _state = default;
                    _pendingImpactKick = float3.zero;
                    _previousTangentVelocity = tangentVelocity;
                    _hasVelocity = true;
                    CurrentAnglesDegrees = float3.zero;
                    CurrentImpactAnglesDegrees = float3.zero;
                    return;
                }
                Vector3 acceleration = Vector3.zero;
                if (_hasVelocity)
                    acceleration = Vector3.ClampMagnitude(
                        (tangentVelocity - _previousTangentVelocity) / dt,
                        26f);
                _previousTangentVelocity = tangentVelocity;
                _hasVelocity = true;
                Vector3 localAcceleration = transform.InverseTransformDirection(acceleration);
                Vector3 localGroundNormal = transform.InverseTransformDirection(
                    motor.HasStableSupport ? motor.GroundNormal : up);
                float slopePitch = Mathf.Atan2(localGroundNormal.z, Mathf.Max(0.01f, localGroundNormal.y)) *
                                   Mathf.Rad2Deg * 0.35f;
                float slopeRoll = -Mathf.Atan2(localGroundNormal.x, Mathf.Max(0.01f, localGroundNormal.y)) *
                                  Mathf.Rad2Deg * 0.35f;
                bool isRagdoll = ragdoll != null && ragdoll.IsRagdollActive;
                EarthInertialBodySample sample = EarthInertialBodyMotionSolver.Step(
                    in _state,
                    new float3(localAcceleration.x, localAcceleration.y, localAcceleration.z),
                    presentation != null ? presentation.MeasuredYawRateDegrees : 0f,
                    motor.LastCommand.Move.x,
                    new float2(slopePitch, slopeRoll),
                    _pendingImpactKick,
                    motor.HasStableSupport,
                    isRagdoll,
                    dt);
                _state = sample.State;
                _pendingImpactKick = float3.zero;
                CurrentAnglesDegrees = sample.AnglesDegrees;
                CurrentImpactAnglesDegrees = sample.ImpactAnglesDegrees;
                // Humanoid mantle hand IK is solved during animation evaluation.
                // A later chest/head rotation moves both wrists away from the
                // physical ledge and breaks the single-owner contact pose.
                if (isRagdoll) return;
                float3 chestAngles = sample.LocomotionAnglesDegrees +
                                     sample.ImpactAnglesDegrees * _impactChestTransfer;
                float3 headAngles = new float3(
                    -sample.LocomotionAnglesDegrees.x * 0.20f,
                    -sample.LocomotionAnglesDegrees.y * 0.28f,
                    -sample.LocomotionAnglesDegrees.z * 0.32f) +
                    sample.ImpactAnglesDegrees * _impactHeadTransfer;
                _chest.localRotation *= Quaternion.Euler(ToVector3(chestAngles));
                _head.localRotation *= Quaternion.Euler(ToVector3(headAngles));
            }
        }

        private void CacheBones()
        {
            if (animator == null || !animator.isHuman) return;
            _chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ??
                     animator.GetBoneTransform(HumanBodyBones.Chest);
            _head = animator.GetBoneTransform(HumanBodyBones.Head);
        }

        private static Vector3 ToVector3(float3 value) =>
            new Vector3(value.x, value.y, value.z);
    }
}
