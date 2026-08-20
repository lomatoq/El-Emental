using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Elemental.Presentation.Animation
{
    /// <summary>
    /// Optional Animation Rigging layer for the Humanoid presentation. Gameplay
    /// remains root-motion free; this layer only aligns limbs to authored bending
    /// targets and can be removed without changing simulation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EarthAnimationRigBridge : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform leftHandTarget;
        [SerializeField] private Transform rightHandTarget;
        [SerializeField] private Rig rig;
        [SerializeField] private TwoBoneIKConstraint leftArm;
        [SerializeField] private TwoBoneIKConstraint rightArm;

        public bool IsBuilt => rig != null && leftArm != null && rightArm != null;
        public float Weight => rig != null ? rig.weight : 0f;

        public void Configure(
            Animator configuredAnimator,
            Transform configuredLeftTarget,
            Transform configuredRightTarget)
        {
            animator = configuredAnimator;
            leftHandTarget = configuredLeftTarget;
            rightHandTarget = configuredRightTarget;
            BuildIfNeeded();
        }

        public void SetMagicWeight(float weight)
        {
            if (rig != null) rig.weight = Mathf.Clamp01(weight);
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            BuildIfNeeded();
        }

        private void LateUpdate()
        {
            if (rig == null || rig.weight <= 0.001f) return;
            UpdateArmHint(leftArm, -1f);
            UpdateArmHint(rightArm, 1f);
        }

        private void BuildIfNeeded()
        {
            if (animator == null || !animator.isHuman ||
                leftHandTarget == null || rightHandTarget == null) return;

            RigBuilder builder = animator.GetComponent<RigBuilder>();
            if (builder == null) builder = animator.gameObject.AddComponent<RigBuilder>();
            if (rig == null)
            {
                Transform existing = animator.transform.Find("Earth Procedural Rig");
                GameObject root = existing != null
                    ? existing.gameObject
                    : new GameObject("Earth Procedural Rig");
                if (existing == null) root.transform.SetParent(animator.transform, false);
                rig = root.GetComponent<Rig>();
                if (rig == null) rig = root.AddComponent<Rig>();
            }

            leftArm = EnsureArm(
                leftArm,
                "Left Arm Bending IK",
                HumanBodyBones.LeftUpperArm,
                HumanBodyBones.LeftLowerArm,
                HumanBodyBones.LeftHand,
                leftHandTarget,
                -1f);
            rightArm = EnsureArm(
                rightArm,
                "Right Arm Bending IK",
                HumanBodyBones.RightUpperArm,
                HumanBodyBones.RightLowerArm,
                HumanBodyBones.RightHand,
                rightHandTarget,
                1f);

            bool found = false;
            for (int index = 0; index < builder.layers.Count; index++)
            {
                if (builder.layers[index].rig != rig) continue;
                found = true;
                break;
            }
            if (!found) builder.layers.Add(new RigLayer(rig, true));
            rig.weight = 0f;
            if (Application.isPlaying) builder.Build();
        }

        private TwoBoneIKConstraint EnsureArm(
            TwoBoneIKConstraint existing,
            string objectName,
            HumanBodyBones rootBone,
            HumanBodyBones midBone,
            HumanBodyBones tipBone,
            Transform target,
            float side)
        {
            if (existing == null)
            {
                Transform child = rig.transform.Find(objectName);
                GameObject go = child != null ? child.gameObject : new GameObject(objectName);
                if (child == null) go.transform.SetParent(rig.transform, false);
                existing = go.GetComponent<TwoBoneIKConstraint>();
                if (existing == null) existing = go.AddComponent<TwoBoneIKConstraint>();
            }
            Transform hint = existing.transform.Find("Hint");
            if (hint == null)
            {
                var hintObject = new GameObject("Hint");
                hint = hintObject.transform;
                hint.SetParent(existing.transform, false);
            }
            Transform rootBoneTransform = animator.GetBoneTransform(rootBone);
            Transform midBoneTransform = animator.GetBoneTransform(midBone);
            Transform tipBoneTransform = animator.GetBoneTransform(tipBone);
            if (rootBoneTransform == null || midBoneTransform == null || tipBoneTransform == null) return existing;
            hint.position = midBoneTransform.position + animator.transform.right * side * 0.28f -
                            animator.transform.forward * 0.12f;

            TwoBoneIKConstraintData data = existing.data;
            data.root = rootBoneTransform;
            data.mid = midBoneTransform;
            data.tip = tipBoneTransform;
            data.target = target;
            data.hint = hint;
            data.targetPositionWeight = 1f;
            data.targetRotationWeight = 0.72f;
            data.hintWeight = 0.68f;
            data.maintainTargetPositionOffset = false;
            data.maintainTargetRotationOffset = true;
            existing.data = data;
            existing.weight = 1f;
            return existing;
        }

        private void UpdateArmHint(TwoBoneIKConstraint constraint, float side)
        {
            if (constraint == null) return;
            TwoBoneIKConstraintData data = constraint.data;
            if (data.root == null || data.mid == null || data.target == null || data.hint == null) return;
            Vector3 up = animator != null ? animator.transform.up : transform.up;
            Vector3 aim = Vector3.ProjectOnPlane(data.target.position - data.root.position, up).normalized;
            if (aim.sqrMagnitude < 0.1f) aim = animator != null ? animator.transform.forward : transform.forward;
            Vector3 outward = Vector3.Cross(up, aim).normalized * side;
            Vector3 desired = data.mid.position + outward * 0.24f - aim * 0.09f + up * 0.035f;
            data.hint.position = Vector3.Lerp(data.hint.position, desired, 1f - Mathf.Exp(-18f * Time.deltaTime));
        }
    }
}
