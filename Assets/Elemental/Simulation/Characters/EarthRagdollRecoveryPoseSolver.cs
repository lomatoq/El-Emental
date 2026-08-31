using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public enum EarthRagdollRecoverySide : byte
    {
        Unknown = 0,
        Front = 1,
        Back = 2
    }

    public struct EarthRagdollRecoveryGateState
    {
        public bool Consumed;
    }

    public readonly struct EarthRagdollRecoveryPose
    {
        public EarthRagdollRecoveryPose(
            float3 rootPosition,
            quaternion rootRotation,
            EarthRagdollRecoverySide side,
            float clearanceLiftMeters,
            bool clearanceSucceeded,
            bool usedFacingFallback)
        {
            RootPosition = rootPosition;
            RootRotation = rootRotation;
            Side = side;
            ClearanceLiftMeters = clearanceLiftMeters;
            ClearanceSucceeded = clearanceSucceeded;
            UsedFacingFallback = usedFacingFallback;
        }

        public float3 RootPosition { get; }
        public quaternion RootRotation { get; }
        public EarthRagdollRecoverySide Side { get; }
        public float ClearanceLiftMeters { get; }
        public bool ClearanceSucceeded { get; }
        public bool UsedFacingFallback { get; }
    }

    public static class EarthRagdollRecoveryPoseSolver
    {
        public const float FirstClearanceLiftMeters = 0.18f;
        public const float MaximumClearanceLiftMeters = 0.35f;

        public static bool TryConsumeRecoveryRequest(
            ref EarthRagdollRecoveryGateState state,
            bool ragdollActive)
        {
            if (!ragdollActive || state.Consumed) return false;
            state.Consumed = true;
            return true;
        }

        public static float SelectClearanceLift(
            bool basePoseClear,
            bool firstLiftClear,
            bool maximumLiftClear,
            out bool clearanceSucceeded)
        {
            if (basePoseClear)
            {
                clearanceSucceeded = true;
                return 0f;
            }
            if (firstLiftClear)
            {
                clearanceSucceeded = true;
                return FirstClearanceLiftMeters;
            }
            clearanceSucceeded = maximumLiftClear;
            return MaximumClearanceLiftMeters;
        }

        public static EarthRagdollRecoveryPose Resolve(
            float3 pelvisPosition,
            float3 chestPosition,
            float3 pelvisForward,
            float3 chestForward,
            float3 chestOutward,
            float3 localUp,
            float3 preferredForward,
            float3 pelvisOffsetLocal,
            float clearanceLiftMeters,
            bool clearanceSucceeded)
        {
            float3 up = math.normalizesafe(SelectFinite(localUp), new float3(0f, 1f, 0f));
            float3 preferred = ProjectDirection(preferredForward, up);
            bool usedFallback = math.lengthsq(preferred) < 0.0001f;
            if (usedFallback)
                preferred = OrthogonalForward(up);

            float3 actualFacing = ProjectDirection(pelvisForward + chestForward, up);
            if (math.lengthsq(actualFacing) < 0.0001f)
                actualFacing = ProjectDirection(chestPosition - pelvisPosition, up);
            if (math.lengthsq(actualFacing) < 0.0001f)
            {
                actualFacing = preferred;
                usedFallback = true;
            }
            actualFacing = math.normalizesafe(actualFacing, preferred);
            // Quaternion sign ambiguity and a collapsed chest can otherwise turn
            // a get-up into an instantaneous 180-degree root flip.
            if (math.dot(actualFacing, preferred) < 0f)
                actualFacing = -actualFacing;

            quaternion rotation = quaternion.LookRotationSafe(actualFacing, up);
            float lift = math.clamp(
                math.isfinite(clearanceLiftMeters) ? clearanceLiftMeters : 0f,
                0f,
                MaximumClearanceLiftMeters);
            float3 safePelvis = SelectFinite(pelvisPosition);
            float3 safeOffset = SelectFinite(pelvisOffsetLocal);
            float3 rootPosition = safePelvis - math.rotate(rotation, safeOffset) + up * lift;
            if (!math.all(math.isfinite(rootPosition)))
            {
                rootPosition = safePelvis + up * lift;
                usedFallback = true;
            }

            float outwardDot = math.dot(
                math.normalizesafe(SelectFinite(chestOutward), up),
                up);
            EarthRagdollRecoverySide side = outwardDot > 0.08f
                ? EarthRagdollRecoverySide.Back
                : outwardDot < -0.08f
                    ? EarthRagdollRecoverySide.Front
                    : EarthRagdollRecoverySide.Unknown;
            return new EarthRagdollRecoveryPose(
                rootPosition,
                rotation,
                side,
                lift,
                clearanceSucceeded,
                usedFallback);
        }

        private static float3 ProjectDirection(float3 direction, float3 up)
        {
            float3 finite = SelectFinite(direction);
            return finite - up * math.dot(finite, up);
        }

        private static float3 OrthogonalForward(float3 up)
        {
            float3 axis = math.abs(up.y) < 0.92f
                ? new float3(0f, 1f, 0f)
                : new float3(1f, 0f, 0f);
            return math.normalizesafe(math.cross(axis, up), new float3(0f, 0f, 1f));
        }

        private static float3 SelectFinite(float3 value) =>
            math.select(float3.zero, value, math.isfinite(value));
    }
}
