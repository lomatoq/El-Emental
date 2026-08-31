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

        private Transform _chest;
        private Transform _head;
        private Vector3 _previousTangentVelocity;
        private bool _hasVelocity;
        private bool _subscribed;
        private EarthInertialBodyState _state;
        private float3 _pendingImpactKick;

        public float3 CurrentAnglesDegrees { get; private set; }

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
            presentation?.NotifyImpactResponse(response.Response);
            Vector3 worldDirection = new Vector3(
                response.Direction.x,
                response.Direction.y,
                response.Direction.z);
            Vector3 local = transform.InverseTransformDirection(worldDirection);
            _pendingImpactKick += EarthInertialBodyMotionSolver.ResolveDirectionalKick(
                new float3(local.x, local.y, local.z),
                math.lerp(0.8f, 4.6f, response.Intensity01));
            _pendingImpactKick = math.clamp(
                _pendingImpactKick,
                new float3(-8f),
                new float3(8f));
        }

        private void LateUpdate()
        {
            if (animator == null || motor == null || rootBody == null ||
                _chest == null || _head == null) return;
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
                Vector3 acceleration = Vector3.zero;
                if (_hasVelocity)
                    acceleration = Vector3.ClampMagnitude(
                        (tangentVelocity - _previousTangentVelocity) / dt,
                        26f);
                _previousTangentVelocity = tangentVelocity;
                _hasVelocity = true;
                Vector3 localAcceleration = transform.InverseTransformDirection(acceleration);
                bool isRagdoll = ragdoll != null && ragdoll.IsRagdollActive;
                EarthInertialBodySample sample = EarthInertialBodyMotionSolver.Step(
                    in _state,
                    new float3(localAcceleration.x, localAcceleration.y, localAcceleration.z),
                    presentation != null ? presentation.MeasuredYawRateDegrees : 0f,
                    motor.LastCommand.Move.x,
                    _pendingImpactKick,
                    motor.HasStableSupport,
                    isRagdoll,
                    dt);
                _state = sample.State;
                _pendingImpactKick = float3.zero;
                CurrentAnglesDegrees = sample.AnglesDegrees;
                if (isRagdoll) return;
                Vector3 angles = new Vector3(
                    sample.AnglesDegrees.x,
                    sample.AnglesDegrees.y,
                    sample.AnglesDegrees.z);
                _chest.localRotation *= Quaternion.Euler(angles);
                _head.localRotation *= Quaternion.Euler(-angles.x * 0.20f, -angles.y * 0.28f, -angles.z * 0.32f);
            }
        }

        private void CacheBones()
        {
            if (animator == null || !animator.isHuman) return;
            _chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ??
                     animator.GetBoneTransform(HumanBodyBones.Chest);
            _head = animator.GetBoneTransform(HumanBodyBones.Head);
        }
    }
}
