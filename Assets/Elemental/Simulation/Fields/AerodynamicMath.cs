using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Fields
{
    public readonly struct AerodynamicResponseProfile
    {
        public AerodynamicResponseProfile(
            float projectedArea,
            float dragCoefficient,
            float liftCoefficient,
            float maximumAcceleration)
        {
            if (!float.IsFinite(projectedArea) || projectedArea <= 0f ||
                !float.IsFinite(dragCoefficient) || dragCoefficient < 0f ||
                !float.IsFinite(liftCoefficient) || liftCoefficient < 0f ||
                !float.IsFinite(maximumAcceleration) || maximumAcceleration <= 0f)
            {
                throw new ArgumentOutOfRangeException();
            }

            ProjectedArea = projectedArea;
            DragCoefficient = dragCoefficient;
            LiftCoefficient = liftCoefficient;
            MaximumAcceleration = maximumAcceleration;
        }

        public float ProjectedArea { get; }
        public float DragCoefficient { get; }
        public float LiftCoefficient { get; }
        public float MaximumAcceleration { get; }
    }

    public static class AerodynamicMath
    {
        public static float3 ComputeAcceleration(
            in FieldSample sample,
            float3 bodyVelocity,
            float mass,
            in AerodynamicResponseProfile profile,
            float3 localUp)
        {
            if (!math.all(math.isfinite(bodyVelocity)) || !float.IsFinite(mass) || mass <= 0f)
            {
                return float3.zero;
            }

            float3 relative = sample.Velocity - bodyVelocity;
            float speed = math.length(relative);
            if (speed <= 0.0001f)
            {
                return float3.zero;
            }

            float dragMagnitude = 0.5f * profile.DragCoefficient * profile.ProjectedArea * speed * speed;
            float3 drag = math.normalizesafe(relative) * (dragMagnitude / mass) * sample.DragMultiplier;
            float3 up = math.normalizesafe(localUp, new float3(0f, 1f, 0f));
            float3 side = math.cross(relative, up);
            float3 liftDirection = math.normalizesafe(math.cross(side, relative));
            float3 lift = liftDirection * (dragMagnitude * profile.LiftCoefficient / math.max(mass, 0.01f));
            float3 acceleration = drag + lift;
            float magnitude = math.length(acceleration);
            if (magnitude > profile.MaximumAcceleration)
            {
                acceleration *= profile.MaximumAcceleration / magnitude;
            }

            return math.all(math.isfinite(acceleration)) ? acceleration : float3.zero;
        }
    }
}
