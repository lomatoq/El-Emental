using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public readonly struct EarthMagicReachSample
    {
        public EarthMagicReachSample(float3 localAim, float reachMeters, float handSpreadMeters)
        {
            LocalAim = localAim;
            ReachMeters = reachMeters;
            HandSpreadMeters = handSpreadMeters;
        }

        public float3 LocalAim { get; }
        public float ReachMeters { get; }
        public float HandSpreadMeters { get; }
    }

    /// <summary>
    /// Keeps magic hand targets inside a believable shoulder envelope. Distant
    /// world targets choose direction only and can never hyperextend the arms or
    /// fold the torso through an unreachable IK request.
    /// </summary>
    public static class EarthMagicReachSolver
    {
        public static EarthMagicReachSample Resolve(
            float3 localDirection,
            EarthCastPhase phase,
            float effort01)
        {
            float3 direction = math.normalizesafe(
                math.select(new float3(0f, 0f, 1f), localDirection, math.isfinite(localDirection)),
                new float3(0f, 0f, 1f));
            direction.y = math.clamp(direction.y, -0.30f, 0.62f);
            direction = math.normalizesafe(direction, new float3(0f, 0f, 1f));
            float effort = math.saturate(math.isfinite(effort01) ? effort01 : 0.5f);
            float phaseReach = phase switch
            {
                EarthCastPhase.Acquire => 0.78f,
                EarthCastPhase.Root => 0.86f,
                EarthCastPhase.Load => 0.92f,
                EarthCastPhase.Strike => 1f,
                EarthCastPhase.Sustain => 0.94f,
                EarthCastPhase.Recover => 0.76f,
                _ => 0.70f
            };
            float reach = math.lerp(0.42f, 0.64f, effort) * phaseReach;
            float spread = math.lerp(0.13f, 0.20f, effort);
            return new EarthMagicReachSample(direction, reach, spread);
        }
    }
}
