using Elemental.Simulation.Gravity;
using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public readonly struct PlanetCameraFramingResult
    {
        public PlanetCameraFramingResult(
            float3 position,
            float3 focus,
            float3 occlusionAnchor)
        {
            Position = position;
            Focus = focus;
            OcclusionAnchor = occlusionAnchor;
        }

        public float3 Position { get; }
        public float3 Focus { get; }
        public float3 OcclusionAnchor { get; }
    }

    public static class PlanetCameraFramingSolver
    {
        public static PlanetCameraFramingResult Solve(
            float3 targetPosition,
            float3 localUp,
            float3 heading,
            float3 targetVelocity,
            float distance,
            float cameraHeight,
            float focusHeight,
            float lookAheadDistance,
            float speedLookAheadDistance,
            float lookAheadReferenceSpeed,
            float shoulderOffset)
        {
            float3 up = math.normalizesafe(localUp, new float3(0f, 1f, 0f));
            GravityFrame.BuildTangentBasis(up, heading, out float3 forward, out float3 right);
            float forwardSpeed = math.max(0f, math.dot(targetVelocity, forward));
            float speed01 = math.saturate(
                forwardSpeed / math.max(0.1f, lookAheadReferenceSpeed));
            float lookAhead = math.max(0f, lookAheadDistance) +
                              (math.max(0f, speedLookAheadDistance) * speed01);
            float3 position = targetPosition +
                              (up * math.max(0f, cameraHeight)) -
                              (forward * math.max(0.1f, distance)) +
                              (right * shoulderOffset);
            float3 occlusionAnchor = targetPosition + (up * math.max(0f, focusHeight));
            float3 focus = occlusionAnchor + (forward * lookAhead);
            return new PlanetCameraFramingResult(position, focus, occlusionAnchor);
        }
    }
}
