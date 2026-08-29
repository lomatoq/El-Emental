using System;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    [Serializable]
    public struct SecondaryBoneSpringState
    {
        public Vector2 AngleDegrees;
        public Vector2 AngularVelocity;
    }

    public static class SecondaryBoneSpringSolver
    {
        public static SecondaryBoneSpringState Step(
            SecondaryBoneSpringState state,
            Vector2 targetDegrees,
            float frequencyHz,
            float dampingRatio,
            float maximumAngleDegrees,
            float deltaTime)
        {
            float dt = Mathf.Clamp(deltaTime, 0f, 0.05f);
            float maximum = Mathf.Max(0f, maximumAngleDegrees);
            targetDegrees = Vector2.ClampMagnitude(targetDegrees, maximum);
            if (dt <= 0f || frequencyHz <= 0f)
            {
                state.AngleDegrees = Vector2.ClampMagnitude(state.AngleDegrees, maximum);
                return state;
            }

            float omega = 2f * Mathf.PI * frequencyHz;
            Vector2 acceleration =
                (targetDegrees - state.AngleDegrees) * (omega * omega) -
                state.AngularVelocity * (2f * Mathf.Max(0f, dampingRatio) * omega);
            state.AngularVelocity += acceleration * dt;
            state.AngleDegrees += state.AngularVelocity * dt;

            float magnitude = state.AngleDegrees.magnitude;
            if (magnitude <= maximum || magnitude <= 0.0001f) return state;
            Vector2 normal = state.AngleDegrees / magnitude;
            state.AngleDegrees = normal * maximum;
            float outwardVelocity = Vector2.Dot(state.AngularVelocity, normal);
            if (outwardVelocity > 0f) state.AngularVelocity -= normal * outwardVelocity;
            return state;
        }
    }

    [DefaultExecutionOrder(250)]
    public sealed class HumanoidSecondaryMotion : MonoBehaviour
    {
        [Serializable]
        private sealed class ChainState
        {
            public Quaternion[] BindRotations = Array.Empty<Quaternion>();
            public SecondaryBoneSpringState[] Springs = Array.Empty<SecondaryBoneSpringState>();

            public void Reset(Transform[] bones)
            {
                int count = bones != null ? bones.Length : 0;
                BindRotations = new Quaternion[count];
                Springs = new SecondaryBoneSpringState[count];
                for (int index = 0; index < count; index++)
                    BindRotations[index] = bones[index] != null ? bones[index].localRotation : Quaternion.identity;
            }
        }

        [SerializeField] private Animator animator;
        [SerializeField] private Transform[] tailBones = Array.Empty<Transform>();
        [SerializeField] private Transform[] leftBeltBones = Array.Empty<Transform>();
        [SerializeField] private Transform[] rightBeltBones = Array.Empty<Transform>();
        [SerializeField, Range(1f, 12f)] private float responseFrequencyHz = 5.6f;
        [SerializeField, Range(0.2f, 1.5f)] private float dampingRatio = 0.72f;
        [SerializeField, Range(0.1f, 2f)] private float accelerationResponse = 0.72f;
        [SerializeField, Range(4f, 30f)] private float maximumTailAngle = 18f;
        [SerializeField, Range(4f, 35f)] private float maximumBeltAngle = 22f;
        [SerializeField, Range(8f, 40f)] private float maximumSampledAcceleration = 26f;
        [SerializeField, Range(0.01f, 0.2f)] private float accelerationFilterSeconds = 0.055f;

        private readonly ChainState _tail = new ChainState();
        private readonly ChainState _leftBelt = new ChainState();
        private readonly ChainState _rightBelt = new ChainState();
        private Vector3 _previousPosition;
        private Vector3 _previousVelocity;
        private Vector3 _filteredLocalAcceleration;
        private bool _hasKinematicSample;

        public int TailBoneCount => tailBones != null ? tailBones.Length : 0;
        public int BeltBoneCount =>
            (leftBeltBones != null ? leftBeltBones.Length : 0) +
            (rightBeltBones != null ? rightBeltBones.Length : 0);
        public bool IsConfigured => TailBoneCount == 3 && BeltBoneCount == 4;

        public void ConfigureFromHierarchy(Animator configuredAnimator)
        {
            animator = configuredAnimator;
            Transform searchRoot = animator != null ? animator.transform : transform;
            tailBones = FindBones(searchRoot,
                "Secondary_Tail_01", "Secondary_Tail_02", "Secondary_Tail_03");
            leftBeltBones = FindBones(searchRoot,
                "Secondary_Belt_L_01", "Secondary_Belt_L_02");
            rightBeltBones = FindBones(searchRoot,
                "Secondary_Belt_R_01", "Secondary_Belt_R_02");
            ResetState();
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (!IsConfigured) ConfigureFromHierarchy(animator);
            else ResetState();
        }

        private void OnEnable()
        {
            _hasKinematicSample = false;
            _filteredLocalAcceleration = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (!IsConfigured) return;
            float dt = Time.deltaTime;
            if (dt <= 0.0001f) return;

            Vector3 position = transform.position;
            if (!_hasKinematicSample)
            {
                _previousPosition = position;
                _previousVelocity = Vector3.zero;
                _hasKinematicSample = true;
                return;
            }

            Vector3 displacement = position - _previousPosition;
            if (displacement.sqrMagnitude > 9f)
            {
                ResetState();
                _previousPosition = position;
                _previousVelocity = Vector3.zero;
                return;
            }

            Vector3 velocity = displacement / dt;
            Vector3 worldAcceleration = (velocity - _previousVelocity) / dt;
            worldAcceleration = Vector3.ClampMagnitude(worldAcceleration, maximumSampledAcceleration);
            Vector3 localAcceleration = transform.InverseTransformDirection(worldAcceleration);
            float filter = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, accelerationFilterSeconds));
            _filteredLocalAcceleration = Vector3.Lerp(_filteredLocalAcceleration, localAcceleration, filter);

            // Acceleration is converted only into presentation rotation. It never feeds
            // movement, collision, hit response or the Animator state machine.
            Vector2 inertialTarget = new Vector2(
                -_filteredLocalAcceleration.z - _filteredLocalAcceleration.y * 0.16f,
                _filteredLocalAcceleration.x) * accelerationResponse;
            ApplyChain(tailBones, _tail, inertialTarget * 0.78f, maximumTailAngle, dt);
            ApplyChain(leftBeltBones, _leftBelt, inertialTarget, maximumBeltAngle, dt);
            ApplyChain(rightBeltBones, _rightBelt, inertialTarget, maximumBeltAngle, dt);

            _previousPosition = position;
            _previousVelocity = velocity;
        }

        private void ResetState()
        {
            _tail.Reset(tailBones);
            _leftBelt.Reset(leftBeltBones);
            _rightBelt.Reset(rightBeltBones);
            _filteredLocalAcceleration = Vector3.zero;
            _hasKinematicSample = false;
        }

        private void ApplyChain(
            Transform[] bones,
            ChainState chain,
            Vector2 target,
            float maximumAngle,
            float deltaTime)
        {
            if (bones == null || chain.Springs.Length != bones.Length) return;
            for (int index = 0; index < bones.Length; index++)
            {
                Transform bone = bones[index];
                if (bone == null) continue;
                float tipWeight = Mathf.Lerp(0.58f, 1f, (index + 1f) / bones.Length);
                chain.Springs[index] = SecondaryBoneSpringSolver.Step(
                    chain.Springs[index],
                    target * tipWeight,
                    responseFrequencyHz,
                    dampingRatio,
                    maximumAngle,
                    deltaTime);
                Vector2 angle = chain.Springs[index].AngleDegrees;
                bone.localRotation = chain.BindRotations[index] * Quaternion.Euler(angle.x, 0f, angle.y);
            }
        }

        private static Transform[] FindBones(Transform root, params string[] names)
        {
            Transform[] result = new Transform[names.Length];
            if (root == null) return result;
            Transform[] candidates = root.GetComponentsInChildren<Transform>(true);
            for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
                for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
                    if (candidates[candidateIndex].name == names[nameIndex])
                    {
                        result[nameIndex] = candidates[candidateIndex];
                        break;
                    }
            return result;
        }
    }
}
