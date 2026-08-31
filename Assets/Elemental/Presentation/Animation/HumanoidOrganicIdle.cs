using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Characters;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    [DefaultExecutionOrder(850)]
    [DisallowMultipleComponent]
    public sealed class HumanoidOrganicIdle : MonoBehaviour
    {
        private static readonly int CastHash = Animator.StringToHash("Cast");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        [SerializeField] private Animator animator;
        [SerializeField] private HumanoidCharacterPresentation presentation;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private HumanoidRagdollRig ragdoll;
        [SerializeField] private EarthSurfController surf;
        [SerializeField, Range(0.2f, 3f)] private float blendInSeconds = 0.65f;
        [SerializeField, Range(0.05f, 0.5f)] private float blendOutSeconds = 0.14f;

        private Transform _chest;
        private Transform _head;
        private Transform _leftShoulder;
        private Transform _rightShoulder;
        private float _weight;
        private float _phase01;
        private float _surfWeight;
        private float _surfSteer;
        private float _surfSteerVelocity;

        public float Weight => _weight;

        public void Configure(
            Animator configuredAnimator,
            HumanoidCharacterPresentation configuredPresentation,
            PlanetMotor configuredMotor,
            HumanoidRagdollRig configuredRagdoll,
            float configuredBlendInSeconds = 0.65f,
            float configuredBlendOutSeconds = 0.14f)
        {
            animator = configuredAnimator;
            presentation = configuredPresentation;
            motor = configuredMotor;
            ragdoll = configuredRagdoll;
            if (surf == null) surf = GetComponentInParent<EarthSurfController>();
            blendInSeconds = Mathf.Clamp(configuredBlendInSeconds, 0.2f, 3f);
            blendOutSeconds = Mathf.Clamp(configuredBlendOutSeconds, 0.05f, 0.5f);
            CacheBones();
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (presentation == null) presentation = GetComponent<HumanoidCharacterPresentation>();
            if (motor == null) motor = GetComponentInParent<PlanetMotor>();
            if (ragdoll == null) ragdoll = GetComponent<HumanoidRagdollRig>();
            if (surf == null) surf = GetComponentInParent<EarthSurfController>();
            _phase01 = Mathf.Repeat(GetEntityId().GetHashCode() * 0.000173f, 1f);
            CacheBones();
        }

        private void LateUpdate()
        {
            bool animatorReady = animator != null && animator.isActiveAndEnabled && animator.isHuman;
            bool grounded = animatorReady && animator.GetBool(GroundedHash);
            bool casting = animatorReady && animator.GetBool(CastHash);
            float speed = presentation != null
                ? Mathf.Abs(presentation.FilteredSpeed)
                : motor != null ? Mathf.Abs(motor.LastCommand.Move.y) : 0f;
            bool idle = animatorReady && grounded && !casting && speed < 0.08f &&
                        (ragdoll == null || !ragdoll.IsRagdollActive);
            bool surfing = animatorReady && surf != null && surf.IsActive &&
                           (ragdoll == null || !ragdoll.IsRagdollActive);
            float response = idle ? blendInSeconds : blendOutSeconds;
            _weight = Mathf.MoveTowards(
                _weight,
                idle ? 1f : 0f,
                Time.deltaTime / Mathf.Max(0.01f, response));
            _surfWeight = Mathf.MoveTowards(
                _surfWeight,
                surfing ? 1f : 0f,
                Time.deltaTime / (surfing ? 0.12f : 0.16f));
            float targetSteer = surfing && motor != null ? motor.LastCommand.Move.x : 0f;
            _surfSteer = Mathf.SmoothDamp(
                _surfSteer,
                targetSteer,
                ref _surfSteerVelocity,
                0.09f,
                12f,
                Mathf.Max(0.0001f, Time.deltaTime));
            if ((_weight <= 0.0001f && _surfWeight <= 0.0001f) ||
                _chest == null || _head == null) return;

            if (_weight > 0.0001f)
            {
                EarthOrganicIdlePose pose = EarthOrganicIdleSolver.Evaluate(Time.time, _phase01, _weight);
                // Hip/pelvis ownership belongs to EarthFootContactController.
                // Organic idle remains an additive chest/head/shoulder pass.
                _chest.localRotation *= Quaternion.Euler(
                    pose.Breath * 1.15f,
                    pose.CounterMotion * 0.72f,
                    -pose.WeightShift * 0.48f);
                _head.localRotation *= Quaternion.Euler(
                    -pose.Breath * 0.42f,
                    -pose.CounterMotion * 0.36f,
                    pose.WeightShift * 0.26f);
                if (_leftShoulder != null)
                    _leftShoulder.localRotation *= Quaternion.Euler(0f, 0f, pose.Breath * 0.38f);
                if (_rightShoulder != null)
                    _rightShoulder.localRotation *= Quaternion.Euler(0f, 0f, -pose.Breath * 0.38f);
            }
            if (_surfWeight <= 0.0001f) return;
            EarthOrganicSurfPose surfPose = EarthOrganicIdleSolver.EvaluateSurf(
                Time.time,
                surf != null ? Mathf.InverseLerp(2f, 13f, surf.Speed) : 0f,
                _surfSteer,
                surf != null ? surf.BankDegrees : 0f,
                _surfWeight);
            _chest.localRotation *= Quaternion.Euler(surfPose.Pitch, surfPose.Yaw, surfPose.Roll);
            _head.localRotation *= Quaternion.Euler(
                -surfPose.Pitch * 0.24f,
                -surfPose.Yaw * 0.22f,
                surfPose.HeadCounterRoll);
            if (_leftShoulder != null)
                _leftShoulder.localRotation *= Quaternion.Euler(0f, 0f, -surfPose.Roll * 0.18f);
            if (_rightShoulder != null)
                _rightShoulder.localRotation *= Quaternion.Euler(0f, 0f, surfPose.Roll * 0.18f);
        }

        private void CacheBones()
        {
            if (animator == null || !animator.isHuman) return;
            _chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ??
                     animator.GetBoneTransform(HumanBodyBones.Chest);
            _head = animator.GetBoneTransform(HumanBodyBones.Head);
            _leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            _rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
        }
    }
}
