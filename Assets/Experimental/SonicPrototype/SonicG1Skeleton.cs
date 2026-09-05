using System;
using UnityEngine;

namespace Elemental.Experimental.SonicPrototype
{
    /// <summary>
    /// Rotation/translation subset of NVIDIA's pinned G1 MuJoCo skeleton.
    /// The source is Tools/SonicPrototype/Official/g1_29dof_with_hand.xml.
    /// </summary>
    public static class SonicG1Skeleton
    {
        public const int PoseSize = 36;
        public const int JointCount = 29;
        public const int JointOffset = 7;

        public static readonly string[] JointNames =
        {
            "left_hip_pitch_joint", "left_hip_roll_joint", "left_hip_yaw_joint",
            "left_knee_joint", "left_ankle_pitch_joint", "left_ankle_roll_joint",
            "right_hip_pitch_joint", "right_hip_roll_joint", "right_hip_yaw_joint",
            "right_knee_joint", "right_ankle_pitch_joint", "right_ankle_roll_joint",
            "waist_yaw_joint", "waist_roll_joint", "waist_pitch_joint",
            "left_shoulder_pitch_joint", "left_shoulder_roll_joint", "left_shoulder_yaw_joint",
            "left_elbow_joint", "left_wrist_roll_joint", "left_wrist_pitch_joint", "left_wrist_yaw_joint",
            "right_shoulder_pitch_joint", "right_shoulder_roll_joint", "right_shoulder_yaw_joint",
            "right_elbow_joint", "right_wrist_roll_joint", "right_wrist_pitch_joint", "right_wrist_yaw_joint",
        };

        // Parent joint indices. -1 means the floating pelvis root.
        private static readonly int[] Parents =
        {
            -1, 0, 1, 2, 3, 4,
            -1, 6, 7, 8, 9, 10,
            -1, 12, 13,
            14, 15, 16, 17, 18, 19, 20,
            14, 22, 23, 24, 25, 26, 27,
        };

        private static readonly Vector3 X = new Vector3(1f, 0f, 0f);
        private static readonly Vector3 Y = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 Z = new Vector3(0f, 0f, 1f);

        private static readonly Vector3[] Axes =
        {
            Y, X, Z, Y, Y, X,
            Y, X, Z, Y, Y, X,
            Z, X, Y,
            Y, X, Z, Y, X, Y, Z,
            Y, X, Z, Y, X, Y, Z,
        };

        // MuJoCo body positions in parent-body coordinates, metres.
        private static readonly Vector3[] RestPositions =
        {
            V(0, .064452, -.1027), V(0, .052, -.030465), V(.025001, 0, -.12412),
            V(-.078273, .0021489, -.17734), V(0, -.000094445, -.30001), V(0, 0, -.017558),
            V(0, -.064452, -.1027), V(0, -.052, -.030465), V(.025001, 0, -.12412),
            V(-.078273, -.0021489, -.17734), V(0, .000094445, -.30001), V(0, 0, -.017558),
            V(0, 0, 0), V(-.0039635, 0, .035), V(0, 0, .019),
            V(.0039563, .10022, .23778), V(0, .038, -.013831), V(0, .00624, -.1032),
            V(.015783, 0, -.080518), V(.1, .00188791, -.01), V(.038, 0, 0), V(.046, 0, 0),
            V(.0039563, -.10021, .23778), V(0, -.038, -.013831), V(0, -.00624, -.1032),
            V(.015783, 0, -.080518), V(.1, -.00188791, -.01), V(.038, 0, 0), V(.046, 0, 0),
        };

        // MuJoCo quaternion order in XML is w,x,y,z; Unity constructor is x,y,z,w.
        private static readonly Quaternion[] RestRotations =
        {
            Q(1, 0, 0, 0), Q(.996179, 0, -.0873386, 0), Q(1, 0, 0, 0),
            Q(.996179, 0, .0873386, 0), Q(1, 0, 0, 0), Q(1, 0, 0, 0),
            Q(1, 0, 0, 0), Q(.996179, 0, -.0873386, 0), Q(1, 0, 0, 0),
            Q(.996179, 0, .0873386, 0), Q(1, 0, 0, 0), Q(1, 0, 0, 0),
            Q(1, 0, 0, 0), Q(1, 0, 0, 0), Q(1, 0, 0, 0),
            Q(.990264, .139201, .0000138722, -.0000986868), Q(.990268, -.139172, 0, 0), Q(1, 0, 0, 0),
            Q(1, 0, 0, 0), Q(1, 0, 0, 0), Q(1, 0, 0, 0), Q(1, 0, 0, 0),
            Q(.990264, -.139201, .0000138722, .0000986868), Q(.990268, .139172, 0, 0), Q(1, 0, 0, 0),
            Q(1, 0, 0, 0), Q(1, 0, 0, 0), Q(1, 0, 0, 0), Q(1, 0, 0, 0),
        };

        private static readonly Quaternion[] RestWorldRotations = new Quaternion[JointCount];
        private static readonly Vector3[] RestWorldPositions = new Vector3[JointCount];

        static SonicG1Skeleton()
        {
            EvaluateInternal(
                Vector3.zero,
                Quaternion.identity,
                null,
                RestWorldRotations,
                RestWorldPositions);
        }

        public static int GetParent(int jointIndex) => Parents[jointIndex];
        public static Quaternion GetRestWorldRotation(int jointIndex) => RestWorldRotations[jointIndex];
        public static Vector3 GetRestWorldPosition(int jointIndex) => RestWorldPositions[jointIndex];

        public static bool TryEvaluate(
            float[] qpos,
            Quaternion[] worldRotations,
            Vector3[] worldPositions)
        {
            if (qpos == null || qpos.Length < PoseSize ||
                worldRotations == null || worldRotations.Length < JointCount ||
                worldPositions == null || worldPositions.Length < JointCount)
                return false;

            Vector3 rootPosition = new Vector3(qpos[0], qpos[1], qpos[2]);
            Quaternion rootRotation = Normalize(new Quaternion(qpos[4], qpos[5], qpos[6], qpos[3]));
            if (!IsFinite(rootPosition) || !IsFinite(rootRotation))
                return false;

            for (int index = JointOffset; index < PoseSize; index++)
            {
                if (!float.IsFinite(qpos[index]))
                    return false;
            }

            EvaluateInternal(rootPosition, rootRotation, qpos, worldRotations, worldPositions);
            return true;
        }

        public static Quaternion SourceRootRotation(float[] qpos)
        {
            return qpos != null && qpos.Length >= PoseSize
                ? Normalize(new Quaternion(qpos[4], qpos[5], qpos[6], qpos[3]))
                : Quaternion.identity;
        }

        /// <summary>
        /// Maps a proper rotation through the source X-forward/Y-left/Z-up to
        /// Unity X-right/Y-up/Z-forward reflection. Quaternion vectors are axial,
        /// so the reflected vector also receives the determinant sign.
        /// </summary>
        public static Quaternion MapRotationToUnity(Quaternion source)
        {
            return Normalize(new Quaternion(source.y, -source.z, -source.x, source.w));
        }

        public static Vector3 MapPositionToUnity(Vector3 source) =>
            new Vector3(-source.y, source.z, source.x);

        private static void EvaluateInternal(
            Vector3 rootPosition,
            Quaternion rootRotation,
            float[] qpos,
            Quaternion[] worldRotations,
            Vector3[] worldPositions)
        {
            for (int index = 0; index < JointCount; index++)
            {
                int parent = Parents[index];
                Quaternion parentRotation = parent >= 0 ? worldRotations[parent] : rootRotation;
                Vector3 parentPosition = parent >= 0 ? worldPositions[parent] : rootPosition;
                float angleRadians = qpos != null ? qpos[JointOffset + index] : 0f;
                Quaternion hinge = Quaternion.AngleAxis(angleRadians * Mathf.Rad2Deg, Axes[index]);
                worldPositions[index] = parentPosition + parentRotation * RestPositions[index];
                worldRotations[index] = Normalize(parentRotation * RestRotations[index] * hinge);
            }
        }

        private static Vector3 V(double x, double y, double z) =>
            new Vector3((float)x, (float)y, (float)z);

        private static Quaternion Q(double w, double x, double y, double z) =>
            Normalize(new Quaternion((float)x, (float)y, (float)z, (float)w));

        private static Quaternion Normalize(Quaternion value)
        {
            float magnitude = Mathf.Sqrt(
                value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w);
            return magnitude > 0.000001f
                ? new Quaternion(value.x / magnitude, value.y / magnitude, value.z / magnitude, value.w / magnitude)
                : Quaternion.identity;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) &&
            float.IsFinite(value.z) && float.IsFinite(value.w);
    }
}
