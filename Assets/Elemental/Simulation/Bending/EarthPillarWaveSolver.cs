using System.Collections.Generic;
using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public readonly struct EarthPillarWaveTuning
    {
        public EarthPillarWaveTuning(
            int minimumRows,
            int maximumRows,
            float minimumDistance,
            float maximumDistance,
            float minimumWidth,
            float maximumWidth,
            float minimumHeight,
            float crestHeight,
            float waveSpeed,
            float baseHoldSeconds,
            float visualGapRatio = 0.28f)
        {
            MinimumRows = math.clamp(minimumRows, 5, 9);
            MaximumRows = math.clamp(maximumRows, MinimumRows, 9);
            MinimumDistance = math.max(0.5f, minimumDistance);
            MaximumDistance = math.max(MinimumDistance + 0.5f, maximumDistance);
            MinimumWidth = math.max(0.2f, minimumWidth);
            MaximumWidth = math.max(MinimumWidth, maximumWidth);
            MinimumHeight = math.max(0.1f, minimumHeight);
            CrestHeight = math.max(MinimumHeight, crestHeight);
            WaveSpeed = math.max(0.1f, waveSpeed);
            BaseHoldSeconds = math.max(0.03f, baseHoldSeconds);
            VisualGapRatio = math.clamp(visualGapRatio, 0.1f, 0.55f);
        }

        public int MinimumRows { get; }
        public int MaximumRows { get; }
        public float MinimumDistance { get; }
        public float MaximumDistance { get; }
        public float MinimumWidth { get; }
        public float MaximumWidth { get; }
        public float MinimumHeight { get; }
        public float CrestHeight { get; }
        public float WaveSpeed { get; }
        public float BaseHoldSeconds { get; }
        public float VisualGapRatio { get; }

        public static EarthPillarWaveTuning Default => new EarthPillarWaveTuning(
            6, 8, 2.0f, 11.2f, 0.64f, 1.28f, 0.28f, 3.4f, 5.4f, 0.04f, 0.38f);
    }

    public readonly struct EarthPillarWaveSample
    {
        public EarthPillarWaveSample(
            int row,
            float angleDegrees,
            float arcDistance,
            float width,
            float height,
            float startDelay,
            float holdDuration,
            float crest01)
        {
            Row = row;
            AngleDegrees = angleDegrees;
            ArcDistance = arcDistance;
            Width = width;
            Height = height;
            StartDelay = startDelay;
            HoldDuration = holdDuration;
            Crest01 = crest01;
        }

        public int Row { get; }
        public float AngleDegrees { get; }
        public float ArcDistance { get; }
        public float Width { get; }
        public float Height { get; }
        public float StartDelay { get; }
        public float HoldDuration { get; }
        public float Crest01 { get; }
        public float Delay => StartDelay;
    }

    public readonly struct EarthPillarWaveMotionSample
    {
        public EarthPillarWaveMotionSample(float height01, float width01, float sink01, bool complete)
        {
            Height01 = height01;
            Width01 = width01;
            Sink01 = sink01;
            Complete = complete;
        }

        public float Height01 { get; }
        public float Width01 { get; }
        public float Sink01 { get; }
        public bool Complete { get; }
    }

    public static class EarthPillarWaveSolver
    {
        public const int MaximumColumns = 96;

        public static EarthPillarWaveSample[] Build(float sectorCharge01, float powerCharge01)
        {
            EarthPillarWaveTuning tuning = EarthPillarWaveTuning.Default;
            return Build(sectorCharge01, powerCharge01, in tuning);
        }

        public static EarthPillarWaveSample[] Build(
            float sectorCharge01,
            float powerCharge01,
            in EarthPillarWaveTuning tuning)
        {
            float sector = math.saturate(sectorCharge01);
            float power = math.saturate(powerCharge01);
            int rows = (int)math.round(math.lerp(tuning.MinimumRows, tuning.MaximumRows, power));
            rows = math.clamp(rows, 5, 9);
            float sectorDegrees = math.lerp(45f, 360f, sector);
            float sectorRadians = math.radians(sectorDegrees);
            float chargedDistance = math.lerp(
                math.max(tuning.MinimumDistance + 2f, tuning.MaximumDistance * 0.56f),
                tuning.MaximumDistance,
                power);
            // A full radial cast spends the same fixed pool over a wider angle, so its
            // range contracts slightly instead of turning the outer row into giant slabs.
            float maximumDistance = chargedDistance * math.lerp(1f, 0.84f, sector);

            var desiredCounts = new int[rows];
            var distances = new float[rows];
            var desiredWidths = new float[rows];
            int desiredTotal = 0;
            for (int row = 0; row < rows; row++)
            {
                float row01 = rows > 1 ? row / (float)(rows - 1) : 0f;
                float distance = math.lerp(tuning.MinimumDistance, maximumDistance, row01);
                float width = math.lerp(tuning.MinimumWidth, tuning.MaximumWidth, math.sqrt(row01));
                float arcLength = sectorRadians * distance;
                float targetSpacing = width / math.max(0.2f, 1f - tuning.VisualGapRatio);
                int count = sectorDegrees >= 359.5f
                    ? (int)math.ceil(arcLength / targetSpacing)
                    : (int)math.ceil(arcLength / targetSpacing) + 1;
                count = math.max(3, count);
                desiredCounts[row] = count;
                distances[row] = distance;
                desiredWidths[row] = width;
                desiredTotal += count;
            }

            int[] counts = FitCountsToPool(desiredCounts, desiredTotal);
            var result = new List<EarthPillarWaveSample>(MaximumColumns);
            float firstDistance = distances[0];
            for (int row = 0; row < rows; row++)
            {
                float row01 = rows > 1 ? row / (float)(rows - 1) : 0f;
                // Keep one readable travelling ridge. A broad Gaussian made several
                // neighbouring rows look like a static palisade when viewed across the
                // planet horizon; this narrower shoulder leaves only two or three rows
                // alive at once while retaining a smooth low -> crest -> low profile.
                float crestOffset = (row01 - 0.58f) / 0.145f;
                float crest01 = math.exp(-0.5f * crestOffset * crestOffset);
                float baseHeight = math.lerp(tuning.MinimumHeight, tuning.CrestHeight, crest01) *
                                   math.lerp(0.72f, 1f, power);
                int count = counts[row];
                float arcLength = sectorRadians * distances[row];
                float spacing = sectorDegrees >= 359.5f
                    ? arcLength / count
                    : arcLength / math.max(1, count - 1);
                float width = math.min(desiredWidths[row], spacing * (1f - tuning.VisualGapRatio));
                float rowDelay = (distances[row] - firstDistance) / tuning.WaveSpeed;
                float hold = tuning.BaseHoldSeconds * math.lerp(0.75f, 1.5f, crest01);
                for (int column = 0; column < count; column++)
                {
                    float column01 = count > 1 ? column / (float)(count - 1) : 0.5f;
                    float angle = sectorDegrees >= 359.5f
                        ? (360f * column / count) - 180f
                        : math.lerp(-sectorDegrees * 0.5f, sectorDegrees * 0.5f, column01);
                    float variation = Hash01((uint)(row + 1), column);
                    float shapeVariation = Hash01((uint)(row + 73), column + 17);
                    float angularSpacing = sectorDegrees >= 359.5f
                        ? 360f / count
                        : sectorDegrees / math.max(1, count - 1);
                    // Brick-like row staggering and a bounded radial drift prevent the
                    // wave from reading as several perfectly straight picket fences.
                    if ((row & 1) != 0) angle += angularSpacing * 0.46f;
                    angle += (shapeVariation - 0.5f) * angularSpacing * 0.22f;
                    float radialVariation = (variation - 0.5f) * width * 0.58f;
                    float sampleDistance = math.max(tuning.MinimumDistance * 0.8f,
                        distances[row] + radialVariation);
                    float timeVariation = (radialVariation / tuning.WaveSpeed) +
                                          ((variation - 0.5f) * 0.075f) +
                                          ((column & 1) * 0.014f);
                    float heightVariation = math.lerp(0.86f, 1.08f, variation);
                    float widthVariation = math.lerp(0.82f, 1.02f, shapeVariation);
                    result.Add(new EarthPillarWaveSample(
                        row,
                        angle,
                        sampleDistance,
                        width * widthVariation,
                        baseHeight * heightVariation,
                        math.max(0f, rowDelay + timeVariation),
                        hold,
                        crest01));
                }
            }
            return result.ToArray();
        }

        public static EarthPillarWaveMotionSample EvaluateMotion(
            float localTime,
            float riseSeconds,
            float holdSeconds,
            float retreatSeconds)
        {
            float rise = math.max(0.05f, riseSeconds);
            float hold = math.max(0f, holdSeconds);
            float retreat = math.max(0.05f, retreatSeconds);
            if (localTime < 0f) return new EarthPillarWaveMotionSample(0f, 0.7f, 0f, false);
            if (localTime <= rise)
            {
                float t = math.saturate(localTime / rise);
                float height;
                if (t < 0.82f)
                    height = math.lerp(0.025f, 1.055f, SmootherStep(t / 0.82f));
                else
                    height = math.lerp(1.055f, 1f, SmootherStep((t - 0.82f) / 0.18f));
                return new EarthPillarWaveMotionSample(
                    height,
                    math.lerp(0.70f, 1f, SmootherStep(t)),
                    0f,
                    false);
            }
            if (localTime <= rise + hold)
                return new EarthPillarWaveMotionSample(1f, 1f, 0f, false);
            float retreat01 = math.saturate((localTime - rise - hold) / retreat);
            float easedRetreat = SmootherStep(retreat01);
            return new EarthPillarWaveMotionSample(
                math.max(0f, 1f - easedRetreat),
                math.lerp(1f, 0.78f, easedRetreat),
                easedRetreat,
                retreat01 >= 1f);
        }

        private static int[] FitCountsToPool(int[] desiredCounts, int desiredTotal)
        {
            int rows = desiredCounts.Length;
            var counts = new int[rows];
            if (desiredTotal <= MaximumColumns)
            {
                for (int row = 0; row < rows; row++) counts[row] = desiredCounts[row];
                return counts;
            }

            float scale = MaximumColumns / (float)desiredTotal;
            int used = 0;
            for (int row = 0; row < rows; row++)
            {
                counts[row] = math.max(3, (int)math.floor(desiredCounts[row] * scale));
                used += counts[row];
            }
            while (used < MaximumColumns)
            {
                int bestRow = 0;
                float bestDeficit = float.NegativeInfinity;
                for (int row = 0; row < rows; row++)
                {
                    float deficit = desiredCounts[row] * scale - counts[row];
                    if (deficit <= bestDeficit) continue;
                    bestDeficit = deficit;
                    bestRow = row;
                }
                counts[bestRow]++;
                used++;
            }
            while (used > MaximumColumns)
            {
                for (int row = rows - 1; row >= 0 && used > MaximumColumns; row--)
                {
                    if (counts[row] <= 3) continue;
                    counts[row]--;
                    used--;
                }
            }
            return counts;
        }

        private static float SmootherStep(float value)
        {
            float t = math.saturate(value);
            return t * t * t * (t * ((t * 6f) - 15f) + 10f);
        }

        private static float Hash01(uint seed, int index)
        {
            uint value = seed ^ ((uint)(index + 1) * 0x9E3779B9u);
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }
    }
}
