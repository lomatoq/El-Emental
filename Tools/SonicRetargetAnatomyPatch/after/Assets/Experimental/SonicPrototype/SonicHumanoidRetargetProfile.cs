using System;
using System.Collections.Generic;
using UnityEngine;

namespace Elemental.Experimental.SonicPrototype
{
    /// <summary>
    /// Pure rotation-space contract used by the experimental G1 retargeter.
    /// Source joint deltas live in the source parent's frame, so they are
    /// conjugated into the target parent's calibrated frame and pre-multiplied
    /// onto the target reference local rotation.
    /// </summary>
    public static class SonicHumanoidRetargetMath
    {
        public static Quaternion ParentFrameDelta(
            Quaternion sourceLocalCurrent,
            Quaternion sourceLocalRest) => Normalize(
                Normalize(sourceLocalCurrent) * Quaternion.Inverse(Normalize(sourceLocalRest)));

        public static Quaternion DeltaBasis(
            Quaternion targetParentReferenceModel,
            Quaternion sourceParentRestUnity) => Normalize(
                Quaternion.Inverse(Normalize(targetParentReferenceModel)) *
                Normalize(sourceParentRestUnity));

        public static Quaternion TargetLocal(
            Quaternion mappedSourceParentFrameDelta,
            Quaternion sourceToTargetParentBasis,
            Quaternion targetReferenceLocal)
        {
            Quaternion basis = Normalize(sourceToTargetParentBasis);
            Quaternion targetDelta = basis *
                                     Normalize(mappedSourceParentFrameDelta) *
                                     Quaternion.Inverse(basis);
            return Normalize(targetDelta * Normalize(targetReferenceLocal));
        }

        private static Quaternion Normalize(Quaternion value)
        {
            float magnitude = Mathf.Sqrt(
                value.x * value.x + value.y * value.y +
                value.z * value.z + value.w * value.w);
            return magnitude > .000001f
                ? new Quaternion(
                    value.x / magnitude,
                    value.y / magnitude,
                    value.z / magnitude,
                    value.w / magnitude)
                : Quaternion.identity;
        }
    }

    [CreateAssetMenu(menuName = "Elemental/Experimental/SONIC Humanoid Retarget Profile")]
    public sealed class SonicHumanoidRetargetProfile : ScriptableObject
    {
        [SerializeField] private string sourceSkeletonRevision =
            "NVlabs/GR00T-WholeBodyControl@daf389964fa4a4545218e8405f24eb55f4912453";
        [SerializeField] private string avatarName;
        [SerializeField] private SonicHumanoidBinding[] bindings = Array.Empty<SonicHumanoidBinding>();

        public string SourceSkeletonRevision => sourceSkeletonRevision;
        public string AvatarName => avatarName;
        public IReadOnlyList<SonicHumanoidBinding> Bindings => bindings;

        public bool Validate(Animator animator, out string reason)
        {
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                reason = "A valid Humanoid Animator is required.";
                return false;
            }
            if (!string.IsNullOrEmpty(avatarName) && !string.Equals(avatarName, animator.avatar.name, StringComparison.Ordinal))
            {
                reason = $"Profile was captured for avatar '{avatarName}', but the Animator uses '{animator.avatar.name}'.";
                return false;
            }
            if (bindings == null || bindings.Length == 0)
            {
                reason = "The profile has no bindings.";
                return false;
            }
            for (int index = 0; index < bindings.Length; index++)
            {
                SonicHumanoidBinding binding = bindings[index];
                if (binding.SourceJointIndex < -1 || binding.SourceJointIndex >= SonicG1Skeleton.JointCount)
                {
                    reason = $"Binding {binding.TargetBone} has invalid G1 index {binding.SourceJointIndex}.";
                    return false;
                }
                if (binding.SourceParentJointIndex < -1 || binding.SourceParentJointIndex >= SonicG1Skeleton.JointCount)
                {
                    reason = $"Binding {binding.TargetBone} has invalid G1 parent index {binding.SourceParentJointIndex}.";
                    return false;
                }
                if (animator.GetBoneTransform(binding.TargetBone) == null)
                {
                    reason = $"Animator is missing {binding.TargetBone}.";
                    return false;
                }
            }
            reason = string.Empty;
            return true;
        }

        public void CaptureFromAnimator(Animator animator)
        {
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                throw new ArgumentException("A valid Humanoid Animator is required.", nameof(animator));

            avatarName = animator.avatar.name;
            SonicBindingTemplate[] templates = SonicBindingTemplate.Defaults;
            bindings = new SonicHumanoidBinding[templates.Length];
            for (int index = 0; index < templates.Length; index++)
            {
                SonicBindingTemplate template = templates[index];
                Transform bone = animator.GetBoneTransform(template.TargetBone);
                if (bone == null)
                    throw new InvalidOperationException($"Selected Humanoid has no {template.TargetBone} bone.");
                bindings[index] = new SonicHumanoidBinding(
                    template.TargetBone,
                    template.SourceJointIndex,
                    template.SourceParentJointIndex,
                    bone.localRotation,
                    Quaternion.identity,
                    template.LocomotionWeight,
                    template.BoxingWeight);
            }
        }

        public void CaptureFromAvatarDefinition(Animator animator)
        {
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new ArgumentException("A valid Humanoid Animator is required.", nameof(animator));

            SkeletonBone[] skeleton = animator.avatar.humanDescription.skeleton;
            if (skeleton == null || skeleton.Length == 0)
                throw new InvalidOperationException("The Humanoid Avatar does not expose its imported T-pose skeleton.");

            avatarName = animator.avatar.name;
            SonicBindingTemplate[] templates = SonicBindingTemplate.Defaults;
            bindings = new SonicHumanoidBinding[templates.Length];
            for (int index = 0; index < templates.Length; index++)
            {
                SonicBindingTemplate template = templates[index];
                Transform bone = animator.GetBoneTransform(template.TargetBone);
                if (bone == null)
                    throw new InvalidOperationException($"Selected Humanoid has no {template.TargetBone} bone.");

                bool found = false;
                Quaternion importedRestLocal = Quaternion.identity;
                for (int skeletonIndex = 0; skeletonIndex < skeleton.Length; skeletonIndex++)
                {
                    if (!string.Equals(skeleton[skeletonIndex].name, bone.name, StringComparison.Ordinal))
                        continue;
                    if (found)
                        throw new InvalidOperationException(
                            $"Avatar skeleton contains more than one bone named '{bone.name}'.");
                    found = true;
                    importedRestLocal = skeleton[skeletonIndex].rotation;
                }
                if (!found)
                    throw new InvalidOperationException(
                        $"Avatar T-pose skeleton has no entry for {template.TargetBone} ('{bone.name}').");

                Quaternion targetParentRestWorld = ResolveParentRestWorld(animator, bone, skeleton);
                Quaternion sourceParentRestWorld = template.SourceParentJointIndex < 0
                    ? Quaternion.identity
                    : SonicG1Skeleton.GetRestWorldRotation(template.SourceParentJointIndex);
                Quaternion sourceParentRestInUnity =
                    SonicG1Skeleton.MapRotationToUnity(sourceParentRestWorld);

                bindings[index] = new SonicHumanoidBinding(
                    template.TargetBone,
                    template.SourceJointIndex,
                    template.SourceParentJointIndex,
                    importedRestLocal,
                    SonicHumanoidRetargetMath.DeltaBasis(
                        targetParentRestWorld,
                        sourceParentRestInUnity),
                    template.LocomotionWeight,
                    template.BoxingWeight);
            }
        }

        private static Quaternion ResolveParentRestWorld(
            Animator animator,
            Transform bone,
            SkeletonBone[] skeleton)
        {
            var chain = new List<Transform>();
            Transform cursor = bone.parent;
            while (cursor != null && cursor != animator.transform)
            {
                chain.Add(cursor);
                cursor = cursor.parent;
            }
            if (cursor != animator.transform)
                throw new InvalidOperationException(
                    $"Humanoid bone '{bone.name}' is not below Animator '{animator.name}'.");

            Quaternion result = Quaternion.identity;
            for (int chainIndex = chain.Count - 1; chainIndex >= 0; chainIndex--)
            {
                string name = chain[chainIndex].name;
                bool found = false;
                Quaternion local = Quaternion.identity;
                for (int skeletonIndex = 0; skeletonIndex < skeleton.Length; skeletonIndex++)
                {
                    if (!string.Equals(skeleton[skeletonIndex].name, name, StringComparison.Ordinal))
                        continue;
                    if (found)
                        throw new InvalidOperationException(
                            $"Avatar skeleton contains more than one parent bone named '{name}'.");
                    found = true;
                    local = skeleton[skeletonIndex].rotation;
                }
                if (!found)
                    throw new InvalidOperationException(
                        $"Avatar T-pose skeleton has no parent entry '{name}' required by '{bone.name}'.");
                result = result * local;
            }
            return result;
        }
    }

    [Serializable]
    public struct SonicHumanoidBinding
    {
        [SerializeField] private HumanBodyBones targetBone;
        [SerializeField] private int sourceJointIndex;
        [SerializeField] private int sourceParentJointIndex;
        [SerializeField] private Quaternion targetRestLocal;
        [Tooltip("Conjugates the mapped G1 world delta into this Humanoid bone's calibrated basis.")]
        [SerializeField] private Quaternion deltaBasis;
        [SerializeField, Range(0f, 1f)] private float locomotionWeight;
        [SerializeField, Range(0f, 1f)] private float boxingWeight;

        public HumanBodyBones TargetBone => targetBone;
        public int SourceJointIndex => sourceJointIndex;
        public int SourceParentJointIndex => sourceParentJointIndex;
        public Quaternion TargetRestLocal => targetRestLocal;
        public Quaternion DeltaBasis => deltaBasis;
        public float LocomotionWeight => locomotionWeight;
        public float BoxingWeight => boxingWeight;

        public SonicHumanoidBinding(
            HumanBodyBones targetBone,
            int sourceJointIndex,
            int sourceParentJointIndex,
            Quaternion targetRestLocal,
            Quaternion deltaBasis,
            float locomotionWeight,
            float boxingWeight)
        {
            this.targetBone = targetBone;
            this.sourceJointIndex = sourceJointIndex;
            this.sourceParentJointIndex = sourceParentJointIndex;
            this.targetRestLocal = targetRestLocal;
            this.deltaBasis = deltaBasis;
            this.locomotionWeight = locomotionWeight;
            this.boxingWeight = boxingWeight;
        }
    }

    internal readonly struct SonicBindingTemplate
    {
        public static readonly SonicBindingTemplate[] Defaults =
        {
            new SonicBindingTemplate(HumanBodyBones.Hips, -1, -1, .65f, .65f),
            new SonicBindingTemplate(HumanBodyBones.Chest, 14, -1, .80f, 1f),
            new SonicBindingTemplate(HumanBodyBones.LeftUpperLeg, 2, -1, 1f, 1f),
            new SonicBindingTemplate(HumanBodyBones.LeftLowerLeg, 3, 2, 1f, 1f),
            new SonicBindingTemplate(HumanBodyBones.LeftFoot, 5, 3, 1f, 1f),
            new SonicBindingTemplate(HumanBodyBones.RightUpperLeg, 8, -1, 1f, 1f),
            new SonicBindingTemplate(HumanBodyBones.RightLowerLeg, 9, 8, 1f, 1f),
            new SonicBindingTemplate(HumanBodyBones.RightFoot, 11, 9, 1f, 1f),
            new SonicBindingTemplate(HumanBodyBones.LeftUpperArm, 17, 14, .75f, 1f),
            new SonicBindingTemplate(HumanBodyBones.LeftLowerArm, 18, 17, .75f, 1f),
            new SonicBindingTemplate(HumanBodyBones.LeftHand, 21, 18, .55f, .85f),
            new SonicBindingTemplate(HumanBodyBones.RightUpperArm, 24, 14, .75f, 1f),
            new SonicBindingTemplate(HumanBodyBones.RightLowerArm, 25, 24, .75f, 1f),
            new SonicBindingTemplate(HumanBodyBones.RightHand, 28, 25, .55f, .85f),
        };

        public readonly HumanBodyBones TargetBone;
        public readonly int SourceJointIndex;
        public readonly int SourceParentJointIndex;
        public readonly float LocomotionWeight;
        public readonly float BoxingWeight;

        public SonicBindingTemplate(
            HumanBodyBones targetBone,
            int sourceJointIndex,
            int sourceParentJointIndex,
            float locomotionWeight,
            float boxingWeight)
        {
            TargetBone = targetBone;
            SourceJointIndex = sourceJointIndex;
            SourceParentJointIndex = sourceParentJointIndex;
            LocomotionWeight = locomotionWeight;
            BoxingWeight = boxingWeight;
        }
    }
}
