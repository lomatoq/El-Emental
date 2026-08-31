using System;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    [Flags]
    public enum EarthAnimationBoneOwnership : byte
    {
        None = 0,
        LeftFootPlant = 1 << 0,
        RightFootPlant = 1 << 1,
        LeftHandContact = 1 << 2,
        RightHandContact = 1 << 3,
        FullRagdoll = 1 << 4
    }

    public static class EarthAnimationBoneMask
    {
        private static readonly HumanBodyBones[] TrackedBones =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.UpperChest,
            HumanBodyBones.Neck,
            HumanBodyBones.Head,
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightShoulder,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.LeftToes,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot,
            HumanBodyBones.RightToes
        };

        public static int TrackedBoneCount => TrackedBones.Length;

        public static HumanBodyBones BoneAt(int index) => TrackedBones[index];

        public static EarthAnimationBoneOwnership OwnershipFor(HumanBodyBones bone)
        {
            EarthAnimationBoneOwnership ownership = EarthAnimationBoneOwnership.FullRagdoll;
            switch (bone)
            {
                case HumanBodyBones.LeftUpperLeg:
                case HumanBodyBones.LeftLowerLeg:
                case HumanBodyBones.LeftFoot:
                case HumanBodyBones.LeftToes:
                    ownership |= EarthAnimationBoneOwnership.LeftFootPlant;
                    break;
                case HumanBodyBones.RightUpperLeg:
                case HumanBodyBones.RightLowerLeg:
                case HumanBodyBones.RightFoot:
                case HumanBodyBones.RightToes:
                    ownership |= EarthAnimationBoneOwnership.RightFootPlant;
                    break;
                case HumanBodyBones.LeftShoulder:
                case HumanBodyBones.LeftUpperArm:
                case HumanBodyBones.LeftLowerArm:
                case HumanBodyBones.LeftHand:
                    ownership |= EarthAnimationBoneOwnership.LeftHandContact;
                    break;
                case HumanBodyBones.RightShoulder:
                case HumanBodyBones.RightUpperArm:
                case HumanBodyBones.RightLowerArm:
                case HumanBodyBones.RightHand:
                    ownership |= EarthAnimationBoneOwnership.RightHandContact;
                    break;
            }
            return ownership;
        }

        public static bool ShouldApplyInertialization(
            EarthAnimationBoneOwnership boneOwnership,
            EarthAnimationBoneOwnership activeOwnership) =>
            (boneOwnership & activeOwnership) == 0;
    }
}
