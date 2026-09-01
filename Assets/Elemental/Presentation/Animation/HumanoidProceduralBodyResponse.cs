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
        [SerializeField] private ActiveRagdollPuppet poweredPuppet;
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
            poweredPuppet = GetComponentInParent<ActiveRagdollPuppet>();
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
            if (poweredPuppet == null) poweredPuppet = GetComponentInParent<ActiveRagdollPuppet>();
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
            if (_subscribed) return;
            if (impactTarget != null)
                impactTarget.WorldResponseRequested += OnWorldResponse;
            if (poweredPuppet != null)
                poweredPuppet.PhysicalActionRequested += OnPhysicalActionRequested;
            _subscribed = impactTarget != null || poweredPuppet != null;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (impactTarget != null)
                impactTarget.WorldResponseRequested -= OnWorldResponse;
            if (poweredPuppet != null)
                poweredPuppet.PhysicalActionRequested -= OnPhysicalActionRequested;
            _subscribed = false;
        }

        private void OnWorldResponse(EarthWorldResponseEvent response)
        {
            if (impactTarget == null || response.TargetStableId != impactTarget.StableFighterId)
                return;
            if (poweredPuppet != null)
            {
                EarthPoweredImpactDecision physicalDecision =
                    poweredPuppet.ReceiveAcceptedWorldResponse(in response);
                if (physicalDecision.Duplicate)
                    return;
                if (physicalDecision.Accepted &&
                    physicalDecision.Owner == EarthPoweredImpactOwner.PoweredPhysicalAssist)
                    return;
            }
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

        private void OnPhysicalActionRequested(EarthPhysicalActionRequest request)
        {
            presentation?.TryHandlePhysicalAction(in request);
        }

        private void LateUpdate()
        {
            if (poweredPuppet != null && presentation != null)
            {
                EarthFootContactController feet = presentation.FootContactController;
                poweredPuppet.SetPoweredFootContactState(
                    feet != null && feet.LeftPlantState == EarthFootPlantState.Planted,
                    feet != null && feet.RightPlantState == EarthFootPlantState.Planted);
            }
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
                CurrentImpactAnglesDegrees = sample.ImpactAnglesDegrees;
                if (isRagdoll) return;
                float castSuppression = presentation != null
                    ? math.lerp(1f, 0.45f, math.saturate(presentation.MagicPresentationWeight))
                    : 1f;
                float3 chestAngles = sample.LocomotionAnglesDegrees * castSuppression +
                                     sample.ImpactAnglesDegrees * _impactChestTransfer;
                float3 headAngles = new float3(
                    -sample.LocomotionAnglesDegrees.x * 0.20f,
                    -sample.LocomotionAnglesDegrees.y * 0.28f,
                    -sample.LocomotionAnglesDegrees.z * 0.32f) * castSuppression +
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
