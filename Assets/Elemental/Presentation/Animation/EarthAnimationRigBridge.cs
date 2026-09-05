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
        // Retained so existing scene data migrates without losing its references.
        // The package constraint is held at zero once the stable constraints exist.
        [SerializeField] private TwoBoneIKConstraint leftArm = null;
        [SerializeField] private TwoBoneIKConstraint rightArm = null;
        [SerializeField] private EarthStableTwoBoneIkConstraint stableLeftArm;
        [SerializeField] private EarthStableTwoBoneIkConstraint stableRightArm;

        public bool IsBuilt => rig != null && stableLeftArm != null && stableRightArm != null;
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
            float clamped = Mathf.Clamp01(weight);
            if (rig != null) rig.weight = clamped;
            DisableLegacyConstraint(leftArm);
            DisableLegacyConstraint(rightArm);
            if (clamped <= 0.0001f) return;
            if (stableLeftArm != null) stableLeftArm.weight = 1f;
            if (stableRightArm != null) stableRightArm.weight = 1f;
        }

        public void ResetMagicIk()
        {
            if (rig != null) rig.weight = 0f;
            DisableLegacyConstraint(leftArm);
            DisableLegacyConstraint(rightArm);
            if (stableLeftArm != null) stableLeftArm.weight = 0f;
            if (stableRightArm != null) stableRightArm.weight = 0f;
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            BuildIfNeeded();
        }

        public void PrepareForEvaluation()
        {
            if (rig == null || rig.weight <= 0.001f) return;
            UpdateArmHint(stableLeftArm, -1f);
            UpdateArmHint(stableRightArm, 1f);
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

            stableLeftArm = EnsureArm(
                stableLeftArm, leftArm, "Left Arm Bending IK",
                HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
                HumanBodyBones.LeftHand, leftHandTarget, -1f);
            stableRightArm = EnsureArm(
                stableRightArm, rightArm, "Right Arm Bending IK",
                HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
                HumanBodyBones.RightHand, rightHandTarget, 1f);

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

        private EarthStableTwoBoneIkConstraint EnsureArm(
            EarthStableTwoBoneIkConstraint existing,
            TwoBoneIKConstraint legacy,
            string objectName,
            HumanBodyBones rootBone,
            HumanBodyBones midBone,
            HumanBodyBones tipBone,
            Transform target,
            float side)
        {
            Transform child = rig.transform.Find(objectName);
            GameObject armObject = child != null ? child.gameObject : new GameObject(objectName);
            if (child == null) armObject.transform.SetParent(rig.transform, false);
            if (legacy == null) legacy = armObject.GetComponent<TwoBoneIKConstraint>();
            DisableLegacyConstraint(legacy);
            if (existing == null) existing = armObject.GetComponent<EarthStableTwoBoneIkConstraint>();
            if (existing == null) existing = armObject.AddComponent<EarthStableTwoBoneIkConstraint>();

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
            if (rootBoneTransform == null || midBoneTransform == null || tipBoneTransform == null)
                return existing;

            EarthStableTwoBoneIkData data = existing.data;
            data.Root = rootBoneTransform;
            data.Mid = midBoneTransform;
            data.Tip = tipBoneTransform;
            data.Target = target;
            data.Hint = hint;
            data.TargetRotationWeight = .45f;
            data.MaximumReachFraction = .92f;
            data.MaintainTargetRotationOffset = true;
            existing.data = data;
            existing.weight = 1f;
            UpdateArmHint(existing, side);
            return existing;
        }

        private void UpdateArmHint(EarthStableTwoBoneIkConstraint constraint, float side)
        {
            if (constraint == null) return;
            EarthStableTwoBoneIkData data = constraint.data;
            if (data.Root == null || data.Mid == null || data.Target == null || data.Hint == null) return;

            Transform body = animator != null ? animator.transform : transform;
            Vector3 aim = data.Target.position - data.Root.position;
            if (aim.sqrMagnitude < .0001f) aim = body.forward;
            aim.Normalize();
            Vector3 outward = Vector3.ProjectOnPlane(body.right * side, aim);
            if (outward.sqrMagnitude < .0001f)
                outward = Vector3.ProjectOnPlane(Vector3.Cross(body.up, aim) * side, aim);
            if (outward.sqrMagnitude < .0001f)
                outward = Vector3.ProjectOnPlane(body.up, aim);
            outward.Normalize();

            float upperLength = Vector3.Distance(data.Root.position, data.Mid.position);
            float poleDistance = Mathf.Max(.22f, upperLength * .9f);
            // The previous solved elbow is deliberately not an input, so a flipped
            // solve cannot feed the next frame's pole.
            data.Hint.position = data.Root.position + aim * (upperLength * .55f) +
                                 outward * poleDistance - body.up * (upperLength * .10f);
        }

        private static void DisableLegacyConstraint(TwoBoneIKConstraint legacy)
        {
            if (legacy == null) return;
            legacy.weight = 0f;
            if (Application.isPlaying) legacy.enabled = false;
        }
    }
}
