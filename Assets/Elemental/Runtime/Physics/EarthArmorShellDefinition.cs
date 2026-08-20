using System;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    public enum EarthArmorShellRegion : byte
    {
        Head = 0,
        Torso = 1,
        Pelvis = 2,
        Arm = 3,
        Leg = 4
    }

    [Serializable]
    public struct EarthArmorShellSegment
    {
        public EarthArmorShellSegment(
            EarthArmorShellRegion region,
            HumanBodyBones bone,
            Vector3 characterDirection,
            Vector3 scale)
        {
            Region = region;
            Bone = bone;
            CharacterDirection = characterDirection.normalized;
            Scale = scale;
        }
        public EarthArmorShellRegion Region;
        public HumanBodyBones Bone;
        public Vector3 CharacterDirection;
        public Vector3 Scale;
    }

    [CreateAssetMenu(menuName = "Elemental/Magic/Earth Armor Shell Definition", fileName = "EarthArmorShellDefinition")]
    public sealed class EarthArmorShellDefinition : ScriptableObject
    {
        public const int RequiredSegmentCount = 96;
        [SerializeField] private EarthArmorShellSegment[] segments = Array.Empty<EarthArmorShellSegment>();
        public EarthArmorShellSegment[] Segments => segments;
        public bool IsValid => segments != null && segments.Length == RequiredSegmentCount;

        [ContextMenu("Bake Default Humanoid Shell")]
        public void BakeDefaultHumanoidShell()
        {
            segments = CreateDefaultSegments();
        }

        public static EarthArmorShellSegment[] CreateDefaultSegments()
        {
            var result = new EarthArmorShellSegment[RequiredSegmentCount];
            int output = 0;

            // Head: two chipped caps plus two staggered eight-tile rings. Eighteen
            // independent stones close the silhouette without giant intersecting
            // slabs or an uncovered face.
            Add(result, ref output, EarthArmorShellRegion.Head, HumanBodyBones.Head,
                Vector3.up, 0.32f, 0.075f, 0.30f);
            Add(result, ref output, EarthArmorShellRegion.Head, HumanBodyBones.Head,
                new Vector3(0f, -0.88f, 0.48f), 0.28f, 0.065f, 0.25f);
            AddRing(result, ref output, EarthArmorShellRegion.Head, HumanBodyBones.Head,
                8, 0f, 0.34f, 0.205f, 0.065f, 0.22f);
            AddRing(result, ref output, EarthArmorShellRegion.Head, HumanBodyBones.Head,
                8, 22.5f, -0.28f, 0.195f, 0.060f, 0.21f);

            // Torso: three staggered six-tile anatomical rings.
            AddRing(result, ref output, EarthArmorShellRegion.Torso, HumanBodyBones.UpperChest,
                6, 30f, 0.08f, 0.285f, 0.070f, 0.30f);
            AddRing(result, ref output, EarthArmorShellRegion.Torso, HumanBodyBones.Chest,
                6, 0f, 0f, 0.295f, 0.072f, 0.31f);
            AddRing(result, ref output, EarthArmorShellRegion.Torso, HumanBodyBones.Spine,
                6, 30f, -0.06f, 0.275f, 0.068f, 0.29f);

            // Pelvis: an eight-sided belt meets edge-to-edge around the hips.
            AddRing(result, ref output, EarthArmorShellRegion.Pelvis, HumanBodyBones.Hips,
                8, 22.5f, 0f, 0.235f, 0.066f, 0.255f);

            // Arms: five stones around every long segment plus two hand caps.
            AddLimbRing(result, ref output, HumanBodyBones.LeftUpperArm, 0f, 0.135f, 0.052f, 0.245f);
            AddLimbRing(result, ref output, HumanBodyBones.LeftLowerArm, 36f, 0.122f, 0.048f, 0.225f);
            AddLimbRing(result, ref output, HumanBodyBones.RightUpperArm, 0f, 0.135f, 0.052f, 0.245f);
            AddLimbRing(result, ref output, HumanBodyBones.RightLowerArm, 36f, 0.122f, 0.048f, 0.225f);
            AddRing(result, ref output, EarthArmorShellRegion.Arm, HumanBodyBones.LeftHand,
                2, 90f, 0.10f, 0.14f, 0.048f, 0.16f);
            AddRing(result, ref output, EarthArmorShellRegion.Arm, HumanBodyBones.RightHand,
                2, 90f, 0.10f, 0.14f, 0.048f, 0.16f);

            // Legs: five stones per long segment and four angled foot plates.
            AddLegRing(result, ref output, HumanBodyBones.LeftUpperLeg, 0f, 0.165f, 0.058f, 0.31f);
            AddLegRing(result, ref output, HumanBodyBones.LeftLowerLeg, 36f, 0.145f, 0.054f, 0.285f);
            AddRing(result, ref output, EarthArmorShellRegion.Leg, HumanBodyBones.LeftFoot,
                4, 45f, 0.28f, 0.18f, 0.055f, 0.26f);
            AddLegRing(result, ref output, HumanBodyBones.RightUpperLeg, 0f, 0.165f, 0.058f, 0.31f);
            AddLegRing(result, ref output, HumanBodyBones.RightLowerLeg, 36f, 0.145f, 0.054f, 0.285f);
            AddRing(result, ref output, EarthArmorShellRegion.Leg, HumanBodyBones.RightFoot,
                4, 45f, 0.28f, 0.18f, 0.055f, 0.26f);

            return result;
        }

        private static void AddLimbRing(
            EarthArmorShellSegment[] output,
            ref int index,
            HumanBodyBones bone,
            float angleOffsetDegrees,
            float width,
            float thickness,
            float length) =>
            AddRing(output, ref index, EarthArmorShellRegion.Arm, bone,
                5, angleOffsetDegrees, 0f, width, thickness, length);

        private static void AddLegRing(
            EarthArmorShellSegment[] output,
            ref int index,
            HumanBodyBones bone,
            float angleOffsetDegrees,
            float width,
            float thickness,
            float length) =>
            AddRing(output, ref index, EarthArmorShellRegion.Leg, bone,
                5, angleOffsetDegrees, 0f, width, thickness, length);

        private static void AddRing(
            EarthArmorShellSegment[] output,
            ref int index,
            EarthArmorShellRegion region,
            HumanBodyBones bone,
            int count,
            float angleOffsetDegrees,
            float verticalBias,
            float width,
            float thickness,
            float length)
        {
            for (int ringIndex = 0; ringIndex < count; ringIndex++)
            {
                float angle = (angleOffsetDegrees + ringIndex * 360f / count) * Mathf.Deg2Rad;
                float widthVariation = Mathf.Lerp(0.88f, 1.12f,
                    Hash01(index * 37 + ringIndex * 19 + 11));
                float lengthVariation = Mathf.Lerp(0.90f, 1.10f,
                    Hash01(index * 53 + ringIndex * 23 + 17));
                Add(output, ref index, region, bone,
                    new Vector3(Mathf.Sin(angle), verticalBias, Mathf.Cos(angle)),
                    width * widthVariation,
                    thickness * Mathf.Lerp(0.93f, 1.08f,
                        Hash01(index * 71 + ringIndex * 29 + 23)),
                    length * lengthVariation);
            }
        }

        private static float Hash01(int value)
        {
            uint hash = (uint)value;
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFFu) / 16777215f;
        }

        private static void Add(
            EarthArmorShellSegment[] output,
            ref int index,
            EarthArmorShellRegion region,
            HumanBodyBones bone,
            Vector3 direction,
            float width,
            float thickness,
            float length)
        {
            if (index >= output.Length) return;
            output[index++] = S(region, bone, direction, width, thickness, length);
        }

        private static EarthArmorShellSegment S(
            EarthArmorShellRegion region,
            HumanBodyBones bone,
            Vector3 direction,
            float x,
            float y,
            float z) => new EarthArmorShellSegment(region, bone, direction, new Vector3(x, y, z));
    }
}
