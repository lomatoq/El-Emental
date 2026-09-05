using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MotionMatching;
using UnityEngine;

namespace Elemental.Presentation.MotionMatching
{
    public enum EAMMRuntimeStatus : byte
    {
        Disabled = 0,
        MissingCalibration = 1,
        InvalidMapping = 2,
        PoseRejected = 3,
        Active = 4
    }

    [Serializable]
    public struct EarthRetargetBindBone
    {
        [SerializeField] private HumanBodyBones bone;
        [SerializeField] private string sourceJointName;
        [SerializeField] private Quaternion sourceRestLocalRotation;

        public EarthRetargetBindBone(
            HumanBodyBones bone,
            string sourceJointName,
            Quaternion sourceRestLocalRotation)
        {
            this.bone = bone;
            this.sourceJointName = sourceJointName;
            this.sourceRestLocalRotation = sourceRestLocalRotation;
        }

        public HumanBodyBones Bone => bone;
        public string SourceJointName => sourceJointName;
        public Quaternion SourceRestLocalRotation => sourceRestLocalRotation;
    }

    /// <summary>
    /// Immutable, editor-baked source rest pose used by the runtime local-space
    /// retargeter. Runtime calibration is deliberately forbidden: a missing or
    /// stale asset leaves authored animation in complete control.
    /// </summary>
    [CreateAssetMenu(menuName = "Elemental/Animation/EAMM Retarget Bind Pose")]
    public sealed class EarthRetargetBindPose : ScriptableObject
    {
        // v2 stores rest rotations relative to the collapsed JLPM skeleton,
        // including the synthetic hips parent, rather than raw FBX parents.
        public const int CurrentSchemaVersion = 2;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string sourceAvatarHash;
        [SerializeField] private string sourceSkeletonHash;
        [SerializeField] private Vector3 sourceForward = Vector3.forward;
        [SerializeField] private Vector3 sourceUp = Vector3.up;
        [SerializeField] private List<EarthRetargetBindBone> bones = new();

        public int SchemaVersion => schemaVersion;
        public string SourceAvatarHash => sourceAvatarHash;
        public string SourceSkeletonHash => sourceSkeletonHash;
        public Vector3 SourceForward => sourceForward;
        public Vector3 SourceUp => sourceUp;
        public IReadOnlyList<EarthRetargetBindBone> Bones => bones;

        public void Configure(
            string avatarHash,
            string skeletonHash,
            Vector3 forward,
            Vector3 up,
            IReadOnlyList<EarthRetargetBindBone> configuredBones)
        {
            schemaVersion = CurrentSchemaVersion;
            sourceAvatarHash = avatarHash ?? string.Empty;
            sourceSkeletonHash = skeletonHash ?? string.Empty;
            sourceForward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            sourceUp = up.sqrMagnitude > 0.0001f ? up.normalized : Vector3.up;
            bones.Clear();
            if (configuredBones == null) return;
            for (int index = 0; index < configuredBones.Count; index++)
                bones.Add(configuredBones[index]);
        }

        public bool TryGet(HumanBodyBones bone, out EarthRetargetBindBone entry)
        {
            for (int index = 0; index < bones.Count; index++)
            {
                if (bones[index].Bone != bone) continue;
                entry = bones[index];
                return true;
            }
            entry = default;
            return false;
        }

        public bool ValidateAgainst(MotionMatchingData data, out string reason)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                reason = "bind-schema-mismatch";
                return false;
            }
            if (data == null || bones == null || bones.Count == 0)
            {
                reason = "missing-bind-data";
                return false;
            }
            for (int index = 0; index < bones.Count; index++)
            {
                EarthRetargetBindBone entry = bones[index];
                if (!data.GetJointName(entry.Bone, out string mappedName) ||
                    !string.Equals(mappedName, entry.SourceJointName, StringComparison.Ordinal))
                {
                    reason = $"source-map-mismatch:{entry.Bone}";
                    return false;
                }
                if (!IsFiniteNormalized(entry.SourceRestLocalRotation))
                {
                    reason = $"invalid-source-rest:{entry.Bone}";
                    return false;
                }
            }
            reason = "valid";
            return true;
        }

        public static string ComputeAvatarHash(Avatar avatar)
        {
            if (avatar == null) return string.Empty;
            HumanDescription description = avatar.humanDescription;
            var builder = new StringBuilder(2048);
            builder.Append(avatar.name).Append('|');
            for (int index = 0; index < description.human.Length; index++)
                builder.Append(description.human[index].humanName).Append('=')
                    .Append(description.human[index].boneName).Append(';');
            return StableHash(builder.ToString());
        }

        public static string ComputeSkeletonHash(IReadOnlyList<EarthRetargetBindBone> entries)
        {
            var builder = new StringBuilder(4096);
            if (entries != null)
            {
                for (int index = 0; index < entries.Count; index++)
                {
                    EarthRetargetBindBone entry = entries[index];
                    Quaternion rotation = entry.SourceRestLocalRotation;
                    builder.Append((int)entry.Bone).Append(':').Append(entry.SourceJointName).Append(':')
                        .Append(rotation.x.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                        .Append(rotation.y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                        .Append(rotation.z.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                        .Append(rotation.w.ToString("R", CultureInfo.InvariantCulture)).Append(';');
                }
            }
            return StableHash(builder.ToString());
        }

        private static string StableHash(string value)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            for (int index = 0; index < value.Length; index++)
            {
                hash ^= value[index];
                hash *= prime;
            }
            return hash.ToString("X16", CultureInfo.InvariantCulture);
        }

        private static bool IsFiniteNormalized(Quaternion value)
        {
            if (!float.IsFinite(value.x) || !float.IsFinite(value.y) ||
                !float.IsFinite(value.z) || !float.IsFinite(value.w)) return false;
            float magnitude = Mathf.Sqrt(
                value.x * value.x + value.y * value.y +
                value.z * value.z + value.w * value.w);
            return magnitude is > 0.98f and < 1.02f;
        }
    }
}
