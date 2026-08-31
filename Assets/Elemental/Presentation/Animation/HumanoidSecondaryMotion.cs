using System;
using Elemental.Runtime.Characters;
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
        private const float MaximumSubstepSeconds = 1f / 120f;

        public static SecondaryBoneSpringState Step(
            SecondaryBoneSpringState state,
            Vector2 targetDegrees,
            float frequencyHz,
            float dampingRatio,
            float maximumAngleDegrees,
            float deltaTime)
        {
            if (!float.IsFinite(state.AngleDegrees.x) ||
                !float.IsFinite(state.AngleDegrees.y) ||
                !float.IsFinite(state.AngularVelocity.x) ||
                !float.IsFinite(state.AngularVelocity.y))
                state = default;
            if (!float.IsFinite(targetDegrees.x) || !float.IsFinite(targetDegrees.y))
                targetDegrees = Vector2.zero;
            if (!float.IsFinite(frequencyHz)) frequencyHz = 0f;
            if (!float.IsFinite(dampingRatio)) dampingRatio = 1f;
            if (!float.IsFinite(maximumAngleDegrees)) maximumAngleDegrees = 0f;
            if (!float.IsFinite(deltaTime)) deltaTime = 0f;
            float dt = Mathf.Clamp(deltaTime, 0f, 0.05f);
            float maximum = Mathf.Max(0f, maximumAngleDegrees);
            targetDegrees = Vector2.ClampMagnitude(targetDegrees, maximum);
            if (dt <= 0f || frequencyHz <= 0f)
            {
                state.AngleDegrees = Vector2.ClampMagnitude(state.AngleDegrees, maximum);
                return state;
            }

            // The old one-step integration became underdamped after render hitches
            // and could kick a belt or helmet tail far outside its authored arc.
            // Small deterministic substeps keep the same spring feel stable from
            // 30 Hz through high-refresh play without frame-rate-specific tuning.
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / MaximumSubstepSeconds), 1, 8);
            float step = dt / steps;
            float omega = 2f * Mathf.PI * frequencyHz;
            float damping = 2f * Mathf.Max(0f, dampingRatio) * omega;
            for (int index = 0; index < steps; index++)
            {
                Vector2 acceleration =
                    (targetDegrees - state.AngleDegrees) * (omega * omega) -
                    state.AngularVelocity * damping;
                state.AngularVelocity += acceleration * step;
                state.AngleDegrees += state.AngularVelocity * step;

                float magnitude = state.AngleDegrees.magnitude;
                if (magnitude <= maximum || magnitude <= 0.0001f) continue;
                Vector2 normal = state.AngleDegrees / magnitude;
                state.AngleDegrees = normal * maximum;
                float outwardVelocity = Vector2.Dot(state.AngularVelocity, normal);
                if (outwardVelocity > 0f)
                    state.AngularVelocity -= normal * outwardVelocity;
            }
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

            public void CaptureBindPose(Transform[] bones)
            {
                int count = bones != null ? bones.Length : 0;
                BindRotations = new Quaternion[count];
                Springs = new SecondaryBoneSpringState[count];
                for (int index = 0; index < count; index++)
                    BindRotations[index] = bones[index] != null
                        ? bones[index].localRotation
                        : Quaternion.identity;
            }

            public void ResetDynamics(Transform[] bones)
            {
                int count = bones != null ? bones.Length : 0;
                if (BindRotations.Length != count)
                {
                    CaptureBindPose(bones);
                    return;
                }
                Springs = new SecondaryBoneSpringState[count];
                for (int index = 0; index < count; index++)
                    if (bones[index] != null)
                        bones[index].localRotation = BindRotations[index];
            }

            public void ClearDynamics()
            {
                Springs = new SecondaryBoneSpringState[BindRotations.Length];
            }
        }

        [SerializeField] private Animator animator;
        [SerializeField] private Rigidbody motionSourceBody;
        [SerializeField] private HumanoidRagdollRig ragdollRig;
        [SerializeField] private Transform helmetAnchor;
        [SerializeField] private Transform hairLock;
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
        private Vector3 _hairBindLocalPosition;
        private Quaternion _hairBindLocalRotation = Quaternion.identity;
        private Vector3 _hairBindLocalScale = Vector3.one;
        private bool _hasHairBind;
        private bool _ragdollWasActive;
        private bool _configurationDiagnosticIssued;

        public int TailBoneCount => tailBones != null ? tailBones.Length : 0;
        public int BeltBoneCount =>
            (leftBeltBones != null ? leftBeltBones.Length : 0) +
            (rightBeltBones != null ? rightBeltBones.Length : 0);
        public bool IsConfigured =>
            HasEveryBone(tailBones, 3) &&
            HasEveryBone(leftBeltBones, 2) &&
            HasEveryBone(rightBeltBones, 2);
        public bool HasHelmetHairLock => helmetAnchor != null && hairLock != null &&
                                         hairLock.parent == helmetAnchor;
        public bool HasValidSecondaryHierarchy => IsConfigured &&
                                                  HasExpectedChain(tailBones, helmetAnchor) &&
                                                  HasExpectedChain(leftBeltBones, null) &&
                                                  HasExpectedChain(rightBeltBones, null);
        public string ConfigurationDiagnostic
        {
            get
            {
                if (animator == null || !animator.isHuman)
                    return "Secondary motion requires a valid Humanoid Animator.";
                if (helmetAnchor == null)
                    return "Secondary_HelmetAnchor is missing.";
                if (hairLock == null || hairLock.parent != helmetAnchor)
                    return "Secondary_HairLock must be rigidly parented to Secondary_HelmetAnchor.";
                if (!HasEveryBone(tailBones, 3) || !HasExpectedChain(tailBones, helmetAnchor))
                    return "Secondary_Tail_01..03 hierarchy is incomplete or mis-parented.";
                if (!HasEveryBone(leftBeltBones, 2) || !HasExpectedChain(leftBeltBones, null))
                    return "Secondary_Belt_L_01..02 hierarchy is incomplete or mis-parented.";
                if (!HasEveryBone(rightBeltBones, 2) || !HasExpectedChain(rightBeltBones, null))
                    return "Secondary_Belt_R_01..02 hierarchy is incomplete or mis-parented.";
                return string.Empty;
            }
        }

        public void ConfigureFromHierarchy(Animator configuredAnimator)
        {
            animator = configuredAnimator;
            Transform searchRoot = animator != null ? animator.transform : transform;
            if (ragdollRig == null) ragdollRig = GetComponentInParent<HumanoidRagdollRig>(true);
            helmetAnchor = FindBone(searchRoot, "Secondary_HelmetAnchor");
            hairLock = FindBone(searchRoot, "Secondary_HairLock");
            tailBones = FindBones(searchRoot,
                "Secondary_Tail_01", "Secondary_Tail_02", "Secondary_Tail_03");
            leftBeltBones = FindBones(searchRoot,
                "Secondary_Belt_L_01", "Secondary_Belt_L_02");
            rightBeltBones = FindBones(searchRoot,
                "Secondary_Belt_R_01", "Secondary_Belt_R_02");
            CaptureBindPose();
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (motionSourceBody == null) motionSourceBody = GetComponentInParent<Rigidbody>();
            if (ragdollRig == null) ragdollRig = GetComponentInParent<HumanoidRagdollRig>(true);
            if (!IsConfigured) ConfigureFromHierarchy(animator);
            else
            {
                if (helmetAnchor == null)
                    helmetAnchor = FindBone(animator != null ? animator.transform : transform,
                        "Secondary_HelmetAnchor");
                if (hairLock == null)
                    hairLock = FindBone(animator != null ? animator.transform : transform,
                        "Secondary_HairLock");
                CaptureBindPose();
            }
            ReportConfigurationFailureOnce();
        }

        private void OnEnable()
        {
            ResetDynamics();
        }

        private void OnDisable() => ResetDynamics();

        private void LateUpdate()
        {
            if (!IsConfigured) return;
            bool ragdollActive = ragdollRig != null && ragdollRig.IsRagdollActive;
            if (ragdollActive != _ragdollWasActive)
            {
                if (ragdollActive) ClearDynamicsWithoutPoseWrite();
                else ResetDynamics();
                _ragdollWasActive = ragdollActive;
            }
            if (ragdollActive) return;
            float dt = Time.deltaTime;
            if (dt <= 0.0001f) return;

            Vector3 position = transform.position;
            Vector3 sampledVelocity = motionSourceBody != null
                ? motionSourceBody.linearVelocity
                : Vector3.zero;
            if (!_hasKinematicSample)
            {
                _previousPosition = position;
                _previousVelocity = motionSourceBody != null
                    ? sampledVelocity
                    : Vector3.zero;
                _hasKinematicSample = true;
                EnforceHairLock();
                return;
            }

            Vector3 displacement = position - _previousPosition;
            if (displacement.sqrMagnitude > 9f)
            {
                ResetDynamics();
                _previousPosition = position;
                _previousVelocity = motionSourceBody != null
                    ? sampledVelocity
                    : Vector3.zero;
                EnforceHairLock();
                return;
            }

            Vector3 velocity = motionSourceBody != null
                ? sampledVelocity
                : displacement / dt;
            Vector3 worldAcceleration = (velocity - _previousVelocity) / dt;
            worldAcceleration = Vector3.ClampMagnitude(
                worldAcceleration,
                maximumSampledAcceleration);
            Vector3 localAcceleration = transform.InverseTransformDirection(worldAcceleration);
            float filter = 1f - Mathf.Exp(
                -dt / Mathf.Max(0.001f, accelerationFilterSeconds));
            _filteredLocalAcceleration = Vector3.Lerp(
                _filteredLocalAcceleration,
                localAcceleration,
                filter);

            // Acceleration is converted only into presentation rotation. It never feeds
            // movement, collision, hit response or the Animator state machine.
            Vector2 inertialTarget = new Vector2(
                -_filteredLocalAcceleration.z - _filteredLocalAcceleration.y * 0.16f,
                _filteredLocalAcceleration.x) * accelerationResponse;
            ApplyChain(tailBones, _tail, inertialTarget * 0.78f, maximumTailAngle, dt);
            ApplyChain(leftBeltBones, _leftBelt, inertialTarget, maximumBeltAngle, dt);
            ApplyChain(rightBeltBones, _rightBelt, inertialTarget, maximumBeltAngle, dt);
            EnforceHairLock();

            _previousPosition = position;
            _previousVelocity = velocity;
        }

        private void CaptureBindPose()
        {
            _tail.CaptureBindPose(tailBones);
            _leftBelt.CaptureBindPose(leftBeltBones);
            _rightBelt.CaptureBindPose(rightBeltBones);
            if (hairLock != null)
            {
                _hairBindLocalPosition = hairLock.localPosition;
                _hairBindLocalRotation = hairLock.localRotation;
                _hairBindLocalScale = hairLock.localScale;
                _hasHairBind = true;
            }
            else
            {
                _hasHairBind = false;
            }
            ResetDynamics();
        }

        private void ResetDynamics()
        {
            _tail.ResetDynamics(tailBones);
            _leftBelt.ResetDynamics(leftBeltBones);
            _rightBelt.ResetDynamics(rightBeltBones);
            _filteredLocalAcceleration = Vector3.zero;
            _hasKinematicSample = false;
            EnforceHairLock();
        }

        public void ResetAfterDiscontinuity() => ResetDynamics();

        private void ClearDynamicsWithoutPoseWrite()
        {
            _tail.ClearDynamics();
            _leftBelt.ClearDynamics();
            _rightBelt.ClearDynamics();
            _filteredLocalAcceleration = Vector3.zero;
            _hasKinematicSample = false;
        }

        private void ReportConfigurationFailureOnce()
        {
            if (_configurationDiagnosticIssued) return;
            string diagnostic = ConfigurationDiagnostic;
            if (string.IsNullOrEmpty(diagnostic)) return;
            _configurationDiagnosticIssued = true;
            Debug.LogError($"[Elemental] {diagnostic}", this);
        }

        private void EnforceHairLock()
        {
            if (!_hasHairBind || hairLock == null) return;
            hairLock.localPosition = _hairBindLocalPosition;
            hairLock.localRotation = _hairBindLocalRotation;
            hairLock.localScale = _hairBindLocalScale;
        }

        private void ApplyChain(
            Transform[] bones,
            ChainState chain,
            Vector2 target,
            float maximumAngle,
            float deltaTime)
        {
            if (bones == null || bones.Length == 0 || chain.Springs.Length != bones.Length)
                return;
            float weightSum = 0f;
            for (int index = 0; index < bones.Length; index++)
                weightSum += Mathf.Lerp(0.42f, 1f, (index + 1f) / bones.Length);
            for (int index = 0; index < bones.Length; index++)
            {
                Transform bone = bones[index];
                if (bone == null) continue;
                // The authored limit is for the whole chain, not every bone.
                // Normalized weights prevent a three-bone tail from accumulating
                // three times the requested bend and clipping through the helmet.
                float tipWeight = Mathf.Lerp(0.42f, 1f, (index + 1f) / bones.Length) /
                                  Mathf.Max(0.001f, weightSum);
                chain.Springs[index] = SecondaryBoneSpringSolver.Step(
                    chain.Springs[index],
                    target * tipWeight,
                    responseFrequencyHz,
                    dampingRatio,
                    maximumAngle * tipWeight,
                    deltaTime);
                Vector2 angle = chain.Springs[index].AngleDegrees;
                bone.localRotation = chain.BindRotations[index] *
                                     Quaternion.Euler(angle.x, 0f, angle.y);
            }
        }

        private static bool HasEveryBone(Transform[] bones, int expectedCount)
        {
            if (bones == null || bones.Length != expectedCount) return false;
            for (int index = 0; index < bones.Length; index++)
                if (bones[index] == null) return false;
            return true;
        }

        private static bool HasExpectedChain(Transform[] bones, Transform expectedRootParent)
        {
            if (bones == null || bones.Length == 0) return false;
            if (expectedRootParent != null && bones[0].parent != expectedRootParent) return false;
            for (int index = 1; index < bones.Length; index++)
                if (bones[index].parent != bones[index - 1]) return false;
            return true;
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

        private static Transform FindBone(Transform root, string name)
        {
            if (root == null) return null;
            Transform[] candidates = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < candidates.Length; index++)
                if (candidates[index].name == name) return candidates[index];
            return null;
        }
    }
}
