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
        public const int RequiredSegmentCount = 64;
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

            // Head: two chipped caps plus two staggered five-tile rings. The face is
            // covered by stone as deliberately as the back of the skull.
            Add(result, ref output, EarthArmorShellRegion.Head, HumanBodyBones.Head,
                Vector3.up, 0.60f, 0.12f, 0.60f);
            Add(result, ref output, EarthArmorShellRegion.Head, HumanBodyBones.Head,
                new Vector3(0f, -0.92f, 0.38f), 0.54f, 0.11f, 0.48f);
            AddRing(result, ref output, EarthArmorShellRegion.Head, HumanBodyBones.Head,
                5, 0f, 0.38f, 0.56f, 0.11f, 0.39f);
            AddRing(result, ref output, EarthArmorShellRegion.Head, HumanBodyBones.Head,
                5, 36f, -0.30f, 0.55f, 0.105f, 0.38f);

            // Torso: three staggered anatomical rings, not five oversized slabs.
            AddRing(result, ref output, EarthArmorShellRegion.Torso, HumanBodyBones.UpperChest,
                4, 45f, 0.08f, 0.62f, 0.12f, 0.46f);
            AddRing(result, ref output, EarthArmorShellRegion.Torso, HumanBodyBones.Chest,
                4, 0f, 0f, 0.62f, 0.12f, 0.46f);
            AddRing(result, ref output, EarthArmorShellRegion.Torso, HumanBodyBones.Spine,
                4, 45f, -0.06f, 0.60f, 0.115f, 0.44f);

            // Pelvis: a six-sided belt meets edge-to-edge around the hips.
            AddRing(result, ref output, EarthArmorShellRegion.Pelvis, HumanBodyBones.Hips,
                6, 0f, 0f, 0.38f, 0.11f, 0.42f);

            // Arms: four tiles around every upper and lower limb segment.
            AddLimbRing(result, ref output, HumanBodyBones.LeftUpperArm, 0f, 0.32f, 0.10f, 0.43f);
            AddLimbRing(result, ref output, HumanBodyBones.LeftLowerArm, 45f, 0.30f, 0.095f, 0.39f);
            AddLimbRing(result, ref output, HumanBodyBones.RightUpperArm, 0f, 0.32f, 0.10f, 0.43f);
            AddLimbRing(result, ref output, HumanBodyBones.RightLowerArm, 45f, 0.30f, 0.095f, 0.39f);

            // Legs: four tiles per long segment plus a broad angled foot cap.
            AddLegRing(result, ref output, HumanBodyBones.LeftUpperLeg, 0f, 0.38f, 0.11f, 0.48f);
            AddLegRing(result, ref output, HumanBodyBones.LeftLowerLeg, 45f, 0.34f, 0.105f, 0.45f);
            Add(result, ref output, EarthArmorShellRegion.Leg, HumanBodyBones.LeftFoot,
                new Vector3(0f, 0.72f, 0.69f), 0.46f, 0.10f, 0.58f);
            AddLegRing(result, ref output, HumanBodyBones.RightUpperLeg, 0f, 0.38f, 0.11f, 0.48f);
            AddLegRing(result, ref output, HumanBodyBones.RightLowerLeg, 45f, 0.34f, 0.105f, 0.45f);
            Add(result, ref output, EarthArmorShellRegion.Leg, HumanBodyBones.RightFoot,
                new Vector3(0f, 0.72f, 0.69f), 0.46f, 0.10f, 0.58f);

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
                4, angleOffsetDegrees, 0f, width, thickness, length);

        private static void AddLegRing(
            EarthArmorShellSegment[] output,
            ref int index,
            HumanBodyBones bone,
            float angleOffsetDegrees,
            float width,
            float thickness,
            float length) =>
            AddRing(output, ref index, EarthArmorShellRegion.Leg, bone,
                4, angleOffsetDegrees, 0f, width, thickness, length);

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
                Add(output, ref index, region, bone,
                    new Vector3(Mathf.Sin(angle), verticalBias, Mathf.Cos(angle)),
                    width, thickness, length);
            }
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
