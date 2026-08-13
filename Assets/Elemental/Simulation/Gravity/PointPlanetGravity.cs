using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Gravity
{
    public sealed class PointPlanetGravity : IGravityField
    {
        private const float DirectionEpsilonSquared = 0.000001f;

        public PointPlanetGravity(
            GravityFieldId id,
            float3 center,
            float radius,
            float surfaceAcceleration,
            float innerClampRadius,
            float falloffDistance,
            float falloffExponent = 2f,
            float maxAcceleration = 100f)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException("Gravity field ID must be non-zero.", nameof(id));
            }

            if (!math.all(math.isfinite(center)))
            {
                throw new ArgumentException("Center must be finite.", nameof(center));
            }

            ValidatePositive(radius, nameof(radius));
            ValidatePositive(surfaceAcceleration, nameof(surfaceAcceleration));
            ValidatePositive(innerClampRadius, nameof(innerClampRadius));
            ValidatePositive(falloffDistance, nameof(falloffDistance));
            ValidatePositive(falloffExponent, nameof(falloffExponent));
            ValidatePositive(maxAcceleration, nameof(maxAcceleration));

            Id = id;
            Center = center;
            Radius = radius;
            SurfaceAcceleration = surfaceAcceleration;
            InnerClampRadius = math.min(innerClampRadius, radius);
            FalloffDistance = falloffDistance;
            FalloffExponent = falloffExponent;
            MaxAcceleration = maxAcceleration;
        }

        public GravityFieldId Id { get; }
        public float3 Center { get; }
        public float Radius { get; }
        public float SurfaceAcceleration { get; }
        public float InnerClampRadius { get; }
        public float FalloffDistance { get; }
        public float FalloffExponent { get; }
        public float MaxAcceleration { get; }

        public GravitySample Sample(float3 worldPosition, uint tick)
        {
            float3 fromCenter = worldPosition - Center;
            float distanceSquared = math.lengthsq(fromCenter);

            if (!math.isfinite(distanceSquared) || distanceSquared <= DirectionEpsilonSquared)
            {
                return new GravitySample(float3.zero, new float3(0f, 1f, 0f), 0f, Id);
            }

            float distance = math.sqrt(distanceSquared);
            float safeDistance = math.max(distance, InnerClampRadius);
            float3 up = fromCenter / distance;
            float magnitude = EvaluateMagnitude(safeDistance);
            float3 acceleration = -up * magnitude;
            float potentialHint = magnitude * math.max(0f, distance - Radius);

            return new GravitySample(acceleration, up, potentialHint, Id);
        }

        private float EvaluateMagnitude(float distance)
        {
            if (distance <= Radius)
            {
                return math.min(SurfaceAcceleration, MaxAcceleration);
            }

            float normalizedAltitude = math.saturate((distance - Radius) / FalloffDistance);
            float attenuation = math.pow(1f - normalizedAltitude, FalloffExponent);
            return math.min(SurfaceAcceleration * attenuation, MaxAcceleration);
        }

        private static void ValidatePositive(float value, string parameterName)
        {
            if (!math.isfinite(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Value must be finite and positive.");
            }
        }
    }
}
