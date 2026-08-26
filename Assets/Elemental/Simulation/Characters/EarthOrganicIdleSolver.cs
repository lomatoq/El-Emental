using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public readonly struct EarthOrganicIdlePose
    {
        public EarthOrganicIdlePose(float breath, float weightShift, float counterMotion)
        {
            Breath = breath;
            WeightShift = weightShift;
            CounterMotion = counterMotion;
        }

        public float Breath { get; }
        public float WeightShift { get; }
        public float CounterMotion { get; }
    }

    public readonly struct EarthOrganicSurfPose
    {
        public EarthOrganicSurfPose(float pitch, float yaw, float roll, float headCounterRoll)
        {
            Pitch = pitch;
            Yaw = yaw;
            Roll = roll;
            HeadCounterRoll = headCounterRoll;
        }

        public float Pitch { get; }
        public float Yaw { get; }
        public float Roll { get; }
        public float HeadCounterRoll { get; }
    }

    public static class EarthOrganicIdleSolver
    {
        public static EarthOrganicIdlePose Evaluate(float seconds, float phase01, float weight01)
        {
            if (!float.IsFinite(seconds) || !float.IsFinite(phase01) || !float.IsFinite(weight01))
                throw new ArgumentOutOfRangeException();
            float weight = math.saturate(weight01);
            float phase = seconds + math.frac(phase01) * 3.17f;
            float breath = math.sin(phase * 2.15f) * weight;
            float shift = math.sin(phase * 0.83f + 1.2f) * weight;
            float counter = math.sin(phase * 1.31f + 2.4f) * weight;
            return new EarthOrganicIdlePose(breath, shift, counter);
        }

        public static EarthOrganicSurfPose EvaluateSurf(
            float seconds,
            float speed01,
            float steering,
            float bankDegrees,
            float weight01)
        {
            if (!float.IsFinite(seconds) || !float.IsFinite(speed01) || !float.IsFinite(steering) ||
                !float.IsFinite(bankDegrees) || !float.IsFinite(weight01))
                throw new ArgumentOutOfRangeException();
            float weight = math.saturate(weight01);
            float speed = math.saturate(speed01);
            float steer = math.clamp(steering, -1f, 1f);
            float bank = math.clamp(bankDegrees, -18f, 18f);
            float terrainPulse = math.sin(seconds * math.lerp(3.2f, 6.4f, speed)) * (0.45f + speed * 0.65f);
            return new EarthOrganicSurfPose(
                (-2.4f - speed * 4.2f + terrainPulse * 0.22f) * weight,
                steer * (2.2f + speed * 2.8f) * weight,
                (-bank * 0.36f - steer * 1.8f + terrainPulse * 0.28f) * weight,
                (bank * 0.22f + steer * 0.9f - terrainPulse * 0.12f) * weight);
        }
    }
}
