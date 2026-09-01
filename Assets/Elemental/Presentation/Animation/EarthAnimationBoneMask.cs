using System;
using Elemental.Simulation.Characters;
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

        public static EarthTransitionBodyMask BodyMaskFor(HumanBodyBones bone) =>
            bone switch
            {
                HumanBodyBones.Hips => EarthTransitionBodyMask.Pelvis,
                HumanBodyBones.Spine => EarthTransitionBodyMask.Spine,
                HumanBodyBones.Chest => EarthTransitionBodyMask.Spine,
                HumanBodyBones.UpperChest => EarthTransitionBodyMask.Spine,
                HumanBodyBones.Neck => EarthTransitionBodyMask.Head,
                HumanBodyBones.Head => EarthTransitionBodyMask.Head,
                HumanBodyBones.LeftShoulder => EarthTransitionBodyMask.LeftArm,
                HumanBodyBones.LeftUpperArm => EarthTransitionBodyMask.LeftArm,
                HumanBodyBones.LeftLowerArm => EarthTransitionBodyMask.LeftArm,
                HumanBodyBones.LeftHand => EarthTransitionBodyMask.LeftArm,
                HumanBodyBones.RightShoulder => EarthTransitionBodyMask.RightArm,
                HumanBodyBones.RightUpperArm => EarthTransitionBodyMask.RightArm,
                HumanBodyBones.RightLowerArm => EarthTransitionBodyMask.RightArm,
                HumanBodyBones.RightHand => EarthTransitionBodyMask.RightArm,
                HumanBodyBones.LeftUpperLeg => EarthTransitionBodyMask.LeftLeg,
                HumanBodyBones.LeftLowerLeg => EarthTransitionBodyMask.LeftLeg,
                HumanBodyBones.LeftFoot => EarthTransitionBodyMask.LeftLeg,
                HumanBodyBones.LeftToes => EarthTransitionBodyMask.LeftLeg,
                HumanBodyBones.RightUpperLeg => EarthTransitionBodyMask.RightLeg,
                HumanBodyBones.RightLowerLeg => EarthTransitionBodyMask.RightLeg,
                HumanBodyBones.RightFoot => EarthTransitionBodyMask.RightLeg,
                HumanBodyBones.RightToes => EarthTransitionBodyMask.RightLeg,
                _ => EarthTransitionBodyMask.None
            };

        public static bool ShouldApplyInertialization(
            EarthAnimationBoneOwnership boneOwnership,
            EarthAnimationBoneOwnership activeOwnership) =>
            (boneOwnership & activeOwnership) == 0;

        public static bool ShouldApplyInertialization(
            EarthTransitionBodyMask boneBodyMask,
            EarthTransitionBodyMask activeBodyMask,
            EarthAnimationBoneOwnership boneOwnership,
            EarthAnimationBoneOwnership activeOwnership) =>
            (boneBodyMask & activeBodyMask) != 0 &&
            ShouldApplyInertialization(boneOwnership, activeOwnership);
    }
}
