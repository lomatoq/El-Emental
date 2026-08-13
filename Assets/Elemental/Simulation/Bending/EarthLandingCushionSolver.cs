using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public readonly struct EarthLandingPrediction
    {
        public EarthLandingPrediction(bool valid, float seconds, float3 surfacePoint, float impactSpeed)
        {
            Valid = valid;
            Seconds = seconds;
            SurfacePoint = surfacePoint;
            ImpactSpeed = impactSpeed;
        }

        public bool Valid { get; }
        public float Seconds { get; }
        public float3 SurfacePoint { get; }
        public float ImpactSpeed { get; }
    }

    public static class EarthLandingCushionSolver
    {
        public static EarthLandingPrediction Predict(
            float3 position,
            float3 velocity,
            float3 planetCenter,
            float surfaceRadius,
            float gravity,
            float maximumSeconds = 4f)
        {
            float3 up = math.normalizesafe(position - planetCenter, new float3(0f, 1f, 0f));
            float height = math.distance(position, planetCenter) - math.max(0.1f, surfaceRadius);
            float upSpeed = math.dot(velocity, up);
            float safeGravity = math.max(0.01f, gravity);
            float discriminant = (upSpeed * upSpeed) + (2f * safeGravity * math.max(0f, height));
            float seconds = (upSpeed + math.sqrt(discriminant)) / safeGravity;
            if (!math.isfinite(seconds) || seconds < 0f || seconds > math.max(0.1f, maximumSeconds))
                return default;
            float3 tangentVelocity = velocity - (up * upSpeed);
            float3 projected = position + (tangentVelocity * seconds) +
                               (up * ((upSpeed * seconds) - (0.5f * safeGravity * seconds * seconds)));
            float3 radial = math.normalizesafe(projected - planetCenter, up);
            float impactSpeed = math.max(0f, -(upSpeed - (safeGravity * seconds)));
            return new EarthLandingPrediction(
                true,
                seconds,
                planetCenter + (radial * math.max(0.1f, surfaceRadius)),
                impactSpeed);
        }

        public static float RequiredUpwardVelocityChange(float currentUpSpeed, float maximumLandingSpeed)
        {
            float target = -math.max(0f, maximumLandingSpeed);
            return currentUpSpeed < target ? target - currentUpSpeed : 0f;
        }
    }
}
