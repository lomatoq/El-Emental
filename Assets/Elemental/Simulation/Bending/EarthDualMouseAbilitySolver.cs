using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public readonly struct EarthCrestPath
    {
        public EarthCrestPath(float3 start, float3 end)
        {
            Start = start;
            End = end;
        }

        public float3 Start { get; }
        public float3 End { get; }
        public float3 Direction => math.normalizesafe(End - Start);
        public float Length => math.distance(Start, End);
    }

    public enum EarthStompStonePhase : byte
    {
        Rising = 0,
        Hovering = 1,
        Launch = 2
    }

    public readonly struct EarthStompStoneSequenceSample
    {
        public EarthStompStoneSequenceSample(EarthStompStonePhase phase, float rise01)
        {
            Phase = phase;
            Rise01 = math.saturate(rise01);
        }

        public EarthStompStonePhase Phase { get; }
        public float Rise01 { get; }
    }

    public static class EarthStompStoneSequenceSolver
    {
        public const float RiseSeconds = 0.28f;
        public const float HoverSeconds = 0.25f;
        public const float RecoverySeconds = 0.28f;

        public static EarthStompStoneSequenceSample Evaluate(float elapsedSeconds)
        {
            float elapsed = math.max(0f, elapsedSeconds);
            float rise01 = math.saturate(elapsed / RiseSeconds);
            if (elapsed < RiseSeconds)
                return new EarthStompStoneSequenceSample(EarthStompStonePhase.Rising, rise01);
            if (elapsed < RiseSeconds + HoverSeconds)
                return new EarthStompStoneSequenceSample(EarthStompStonePhase.Hovering, 1f);
            return new EarthStompStoneSequenceSample(EarthStompStonePhase.Launch, 1f);
        }
    }

    public readonly struct EarthPillarCrestLayoutSample
    {
        public EarthPillarCrestLayoutSample(
            float forwardOffset,
            float heightScale,
            float startDelay,
            float width,
            float depth)
        {
            ForwardOffset = forwardOffset;
            HeightScale = heightScale;
            StartDelay = startDelay;
            Width = width;
            Depth = depth;
        }

        public float ForwardOffset { get; }
        public float HeightScale { get; }
        public float StartDelay { get; }
        public float Width { get; }
        public float Depth { get; }
    }

    public static class EarthPillarCrestLayoutSolver
    {
        public const float SpacingMeters = 0.58f;
        public const float WidthMeters = 0.64f;
        public const float DepthMeters = 0.68f;
        public const float DelayPerPillarSeconds = 0.045f;

        public static EarthPillarCrestLayoutSample Sample(int index, int count)
        {
            int safeCount = math.max(1, count);
            int safeIndex = math.clamp(index, 0, safeCount - 1);
            float middle = (safeCount - 1) * 0.5f;
            float edge01 = middle > 0f ? math.abs(safeIndex - middle) / middle : 0f;
            return new EarthPillarCrestLayoutSample(
                safeIndex * SpacingMeters,
                math.lerp(1f, 0.65f, edge01),
                safeIndex * DelayPerPillarSeconds,
                WidthMeters,
                DepthMeters);
        }
    }
}
