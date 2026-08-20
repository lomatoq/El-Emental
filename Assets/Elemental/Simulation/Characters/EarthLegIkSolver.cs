using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public static class EarthStableKneeHintSolver
    {
        public static float3 Solve(
            float3 hip,
            float3 characterForward,
            float3 characterRight,
            float3 localUp,
            float side,
            float3 previousDirection,
            float forwardOffset = 0.42f,
            float sideOffset = 0.18f)
        {
            float3 up = math.normalizesafe(localUp, new float3(0f, 1f, 0f));
            float3 forward = characterForward - up * math.dot(characterForward, up);
            forward = math.normalizesafe(forward, OrthonormalTangent(up));
            float3 right = characterRight - up * math.dot(characterRight, up);
            right = math.normalizesafe(right, math.cross(up, forward));
            float3 desired = math.normalizesafe(
                forward * math.max(0.05f, forwardOffset) +
                right * (math.sign(side) * math.max(0.02f, sideOffset)) -
                up * 0.04f,
                forward);
            float3 previous = math.normalizesafe(previousDirection, desired);
            // Preserve the established bend side near a straight-leg singularity.
            // A sudden 180-degree hint flip is always worse than a short lag.
            if (math.dot(previous, desired) < -0.15f) desired = previous;
            return hip + desired * math.max(0.24f, forwardOffset + sideOffset);
        }

        private static float3 OrthonormalTangent(float3 normal)
        {
            float3 reference = math.abs(normal.y) < 0.92f
                ? new float3(0f, 1f, 0f)
                : new float3(1f, 0f, 0f);
            return math.normalizesafe(math.cross(reference, normal), new float3(1f, 0f, 0f));
        }
    }

    public static class EarthSupportFootLockSolver
    {
        public static float3 CaptureLocal(float3 worldPosition, in SupportFrameSnapshot support)
        {
            if (!support.IsValid) return worldPosition;
            return math.rotate(math.inverse(support.Rotation), worldPosition - support.Position);
        }

        public static float3 ResolveWorld(float3 localPosition, in SupportFrameSnapshot support)
        {
            if (!support.IsValid) return localPosition;
            return support.Position + math.rotate(support.Rotation, localPosition);
        }

        public static bool SameSupport(
            uint capturedId,
            uint capturedGeneration,
            in SupportFrameSnapshot support) =>
            support.IsValid && support.SurfaceId == capturedId &&
            support.Generation == capturedGeneration;
    }
}
