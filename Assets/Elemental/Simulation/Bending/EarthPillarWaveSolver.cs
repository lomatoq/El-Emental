using System.Collections.Generic;
using Elemental.Simulation.Structures;
using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public enum EarthWaveSemanticFamily : byte
    {
        BroadRipple = 0,
        SerpentRidge = 1,
        ForwardFan = 2,
        ForkedWave = 3,
        CrownEruption = 4,
        RollingTerraces = 5
    }

    public static class EarthWaveFamilySelector
    {
        public static EarthWaveSemanticFamily Select(
            float sector01,
            float power01,
            EarthWaveSemanticFamily previous,
            int sequence)
        {
            int preferred;
            if (sector01 > 0.86f && power01 > 0.64f) preferred = (int)EarthWaveSemanticFamily.CrownEruption;
            else if (sector01 < 0.24f && power01 > 0.58f) preferred = (int)EarthWaveSemanticFamily.SerpentRidge;
            else preferred = (sequence * 5 + (int)math.round(power01 * 7f) + (int)math.round(sector01 * 11f)) % 6;
            if (preferred == (int)previous) preferred = (preferred + 1 + (sequence & 1)) % 6;
            return (EarthWaveSemanticFamily)preferred;
        }
    }

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
            float cellOverlapRatio = 0.07f)
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
            CellOverlapRatio = math.clamp(cellOverlapRatio, 0.02f, 0.16f);
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
        public float CellOverlapRatio { get; }
        public float VisualGapRatio => CellOverlapRatio;

        public static EarthPillarWaveTuning Default => new EarthPillarWaveTuning(
            6, 8, 2.0f, 11.2f, 0.64f, 1.28f, 0.28f, 3.4f, 5.4f, 0.04f, 0.07f);
    }

    public readonly struct EarthPillarWaveSample
    {
        public EarthPillarWaveSample(
            int row,
            float angleDegrees,
            float arcDistance,
            float width,
            float depth,
            float height,
            float startDelay,
            float holdDuration,
            float crest01,
            int shapeSides = 6,
            float shapeAreaScale = 1f,
            float spiralPhase01 = 0f,
            EarthWaveSemanticFamily family = EarthWaveSemanticFamily.BroadRipple)
        {
            Row = row;
            AngleDegrees = angleDegrees;
            ArcDistance = arcDistance;
            Width = width;
            Depth = depth;
            Height = height;
            StartDelay = startDelay;
            HoldDuration = holdDuration;
            Crest01 = crest01;
            ShapeSides = math.clamp(shapeSides, 3, 8);
            ShapeAreaScale = math.clamp(shapeAreaScale, 0.45f, 1.7f);
            SpiralPhase01 = math.frac(spiralPhase01);
            Family = family;
        }

        public int Row { get; }
        public float AngleDegrees { get; }
        public float ArcDistance { get; }
        public float Width { get; }
        public float Depth { get; }
        public float Height { get; }
        public float StartDelay { get; }
        public float HoldDuration { get; }
        public float Crest01 { get; }
        /// <summary>Deterministic polygon family used by the runtime wave-cell mesh.</summary>
        public int ShapeSides { get; }
        /// <summary>Visual/collider footprint variation without opening a gap in the shared web layout.</summary>
        public float ShapeAreaScale { get; }
        /// <summary>Normalized offset along the broken capture spiral.</summary>
        public float SpiralPhase01 { get; }
        public EarthWaveSemanticFamily Family { get; }
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

    public readonly struct EarthWebWaveCell
    {
        public EarthWebWaveCell(
            int stableIndex,
            in EarthPillarWaveSample sample,
            float2[] footprint = null,
            float area = 0f)
        {
            StableIndex = stableIndex;
            Sample = sample;
            Footprint = footprint ?? System.Array.Empty<float2>();
            Area = math.max(0f, area);
        }

        public int StableIndex { get; }
        public EarthPillarWaveSample Sample { get; }
        /// <summary>Cell-local polygon in metres. Neighbours originate from shared Voronoi bisectors.</summary>
        public float2[] Footprint { get; }
        public float Area { get; }
    }

    public readonly struct EarthWebWaveTopology
    {
        public EarthWebWaveTopology(
            int seedIndex,
            int radialThreadCount,
            EarthWebWaveCell[] cells,
            EarthWaveSemanticFamily family = EarthWaveSemanticFamily.BroadRipple)
        {
            SeedIndex = math.clamp(seedIndex, 0, 5);
            RadialThreadCount = math.clamp(radialThreadCount, 12, 18);
            Cells = cells ?? System.Array.Empty<EarthWebWaveCell>();
            Family = family;
        }

        public int SeedIndex { get; }
        public int RadialThreadCount { get; }
        public EarthWebWaveCell[] Cells { get; }
        public EarthWaveSemanticFamily Family { get; }
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
            in EarthPillarWaveTuning tuning,
            int topologySeedIndex = 0,
            EarthWaveSemanticFamily family = EarthWaveSemanticFamily.BroadRipple)
        {
            int topologySeed = math.clamp(topologySeedIndex, 0, 5);
            float sector = math.saturate(sectorCharge01);
            float power = math.saturate(powerCharge01);
            int rows = (int)math.round(math.lerp(tuning.MinimumRows, tuning.MaximumRows, power));
            rows = math.clamp(rows, 5, 9);
            float sectorDegrees = math.lerp(45f, 360f, sector);
            switch (family)
            {
                case EarthWaveSemanticFamily.SerpentRidge:
                    sectorDegrees = math.min(sectorDegrees, 82f);
                    break;
                case EarthWaveSemanticFamily.ForwardFan:
                    sectorDegrees = math.clamp(sectorDegrees, 72f, 128f);
                    break;
                case EarthWaveSemanticFamily.ForkedWave:
                    sectorDegrees = math.clamp(sectorDegrees, 96f, 154f);
                    break;
                case EarthWaveSemanticFamily.CrownEruption:
                    sectorDegrees = 360f;
                    break;
                case EarthWaveSemanticFamily.RollingTerraces:
                    sectorDegrees = math.clamp(sectorDegrees, 120f, 230f);
                    break;
            }
            float sectorRadians = math.radians(sectorDegrees);
            float chargedDistance = math.lerp(
                math.max(tuning.MinimumDistance + 2f, tuning.MaximumDistance * 0.56f),
                tuning.MaximumDistance,
                power);
            // A full radial cast spends the same fixed pool over a wider angle, so its
            // range contracts slightly instead of turning the outer row into giant slabs.
            float maximumDistance = chargedDistance * math.lerp(1f, 0.84f, sector);
            if (family == EarthWaveSemanticFamily.CrownEruption) maximumDistance *= 0.62f;
            else if (family == EarthWaveSemanticFamily.SerpentRidge) maximumDistance *= 1.08f;

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
                float targetSpacing = width / math.max(0.2f, 1f - 0.10f);
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
                float crestCenter = family == EarthWaveSemanticFamily.CrownEruption ? 0.40f : 0.58f;
                float crestWidth = family == EarthWaveSemanticFamily.RollingTerraces ? 0.23f : 0.145f;
                float crestOffset = (row01 - crestCenter) / crestWidth;
                float crest01 = math.exp(-0.5f * crestOffset * crestOffset);
                if (family == EarthWaveSemanticFamily.RollingTerraces)
                    crest01 = math.saturate(0.42f + ((row & 1) == 0 ? 0.48f : 0.18f) + crest01 * 0.24f);
                float baseHeight = math.lerp(tuning.MinimumHeight, tuning.CrestHeight, crest01) *
                                   math.lerp(0.72f, 1f, power);
                int count = counts[row];
                float arcLength = sectorRadians * distances[row];
                float spacing = sectorDegrees >= 359.5f
                    ? arcLength / count
                    : arcLength / math.max(1, count - 1);
                // The fixed 96-cell budget changes the actual spacing. Size each
                // block from that final Voronoi cell rather than from an unrelated
                // art width, otherwise reduced counts produce the huge empty lanes
                // seen in the old wave.
                float width = math.max(desiredWidths[row], spacing * (1f + tuning.CellOverlapRatio));
                float previousDistance = row > 0 ? distances[row - 1] : distances[row] - (distances[1] - distances[0]);
                float nextDistance = row < rows - 1
                    ? distances[row + 1]
                    : distances[row] + (distances[row] - distances[row - 1]);
                float radialCellDepth = ((distances[row] - previousDistance) * 0.5f) +
                                        ((nextDistance - distances[row]) * 0.5f);
                float depth = radialCellDepth * (1f + tuning.CellOverlapRatio);
                float rowDelay = (distances[row] - firstDistance) / tuning.WaveSpeed;
                float hold = tuning.BaseHoldSeconds * math.lerp(0.75f, 1.5f, crest01);
                for (int column = 0; column < count; column++)
                {
                    float column01 = count > 1 ? column / (float)(count - 1) : 0.5f;
                    float angle = sectorDegrees >= 359.5f
                        ? (360f * column / count) - 180f
                        : math.lerp(-sectorDegrees * 0.5f, sectorDegrees * 0.5f, column01);
                    float variation = Hash01((uint)(row + 1 + topologySeed * 131), column);
                    float shapeVariation = Hash01((uint)(row + 73 + topologySeed * 197), column + 17);
                    float angularSpacing = sectorDegrees >= 359.5f
                        ? 360f / count
                        : sectorDegrees / math.max(1, count - 1);
                    // Follow a broken capture spiral rather than staggering polar bricks.
                    // The slowly rotating phase keeps radial spokes readable, while the
                    // bounded branch offset makes occasional three/five-sided junctions.
                    float spiralPhase = math.frac(
                        (row01 * 0.72f) + (shapeVariation * 0.11f));
                    angle += angularSpacing * spiralPhase;
                    if (row > 1 && ((column + row * 3) % 7) == 0)
                        angle += angularSpacing * math.lerp(-0.28f, 0.28f, variation);
                    angle += (shapeVariation - 0.5f) * angularSpacing * 0.16f;
                    float familyHeight = 1f;
                    if (family == EarthWaveSemanticFamily.SerpentRidge)
                    {
                        angle += math.sin(row * 1.37f + topologySeed * 0.71f) * math.lerp(8f, 24f, row01);
                        familyHeight = math.lerp(0.74f, 1.18f, crest01);
                    }
                    else if (family == EarthWaveSemanticFamily.ForwardFan)
                    {
                        float centre = 1f - math.saturate(math.abs(angle) / math.max(1f, sectorDegrees * 0.5f));
                        familyHeight = math.lerp(0.58f, 1.34f, centre * centre);
                    }
                    else if (family == EarthWaveSemanticFamily.ForkedWave && row > 1)
                    {
                        float branch = angle < 0f ? -1f : 1f;
                        if (math.abs(angle) < angularSpacing * 1.25f) branch = ((column + row) & 1) == 0 ? -1f : 1f;
                        angle += branch * math.lerp(3f, 18f, row01);
                        familyHeight = math.lerp(0.82f, 1.16f, row01);
                    }
                    else if (family == EarthWaveSemanticFamily.CrownEruption)
                        familyHeight = math.lerp(1.34f, 0.68f, row01);
                    else if (family == EarthWaveSemanticFamily.RollingTerraces)
                        familyHeight = (row & 1) == 0 ? 1.12f : 0.72f;
                    float radialVariation = (variation - 0.5f) * depth * 0.26f;
                    float sampleDistance = math.max(tuning.MinimumDistance * 0.8f,
                        distances[row] + radialVariation);
                    float timeVariation = (radialVariation / tuning.WaveSpeed) +
                                          ((variation - 0.5f) * 0.075f) +
                                          ((column & 1) * 0.014f);
                    float heightVariation = math.lerp(0.78f, 1.17f, variation);
                    float widthVariation = math.lerp(1.03f, 1.11f, shapeVariation);
                    float depthVariation = math.lerp(0.72f, 1.38f, variation);
                    int shapeSides = 3 + (int)math.floor(
                        Hash01((uint)(row + 311 + topologySeed * 223), column + 97) * 6f);
                    float areaHash = Hash01((uint)(row + 541 + topologySeed * 269), column + 211);
                    float areaVariation = areaHash < 0.25f
                        ? math.lerp(0.45f, 0.58f, areaHash / 0.25f)
                        : areaHash > 0.78f
                            ? math.lerp(1.55f, 1.70f, (areaHash - 0.78f) / 0.22f)
                            : math.lerp(0.78f, 1.18f, (areaHash - 0.25f) / 0.53f);
                    result.Add(new EarthPillarWaveSample(
                        row,
                        angle,
                        sampleDistance,
                        width * widthVariation,
                        depth * depthVariation,
                        baseHeight * heightVariation * familyHeight,
                        math.max(0f, rowDelay + timeVariation),
                        hold,
                        crest01,
                        shapeSides,
                        areaVariation,
                        spiralPhase,
                        family));
                }
            }
            return result.ToArray();
        }

        public static EarthWebWaveTopology BuildTopology(
            float sectorCharge01,
            float powerCharge01,
            in EarthPillarWaveTuning tuning,
            int topologySeedIndex,
            EarthWaveSemanticFamily family = EarthWaveSemanticFamily.BroadRipple)
        {
            int seed = math.clamp(topologySeedIndex, 0, 5);
            // Tessellate a full seeded radial/spiral web first and crop by sector
            // afterwards. Ghost sites inside the first row keep a real central hole;
            // without them the nearest cells would stretch back under the caster.
            EarthPillarWaveSample[] samples = Build(1f, powerCharge01, in tuning, seed, family);
            const int innerGhostCount = 9;
            var sites = new float2[samples.Length + innerGhostCount];
            for (int index = 0; index < samples.Length; index++)
            {
                EarthPillarWaveSample sample = samples[index];
                float radians = math.radians(sample.AngleDegrees);
                float2 site = new float2(math.sin(radians), math.cos(radians)) * sample.ArcDistance;
                // Close pairs make the small intermediate chips seen in natural
                // fracture, while the resulting gap creates neighbouring large slabs.
                if (sample.ShapeAreaScale < 0.66f && index > 0 &&
                    samples[index - 1].Row == sample.Row)
                    site = math.lerp(site, sites[index - 1], 0.58f);
                sites[index] = site;
            }
            sites[samples.Length] = float2.zero;
            float ghostRadius = tuning.MinimumDistance * 0.47f;
            for (int ghost = 1; ghost < innerGhostCount; ghost++)
            {
                float angle = (ghost - 1) * math.PI * 2f / (innerGhostCount - 1) + seed * 0.071f;
                sites[samples.Length + ghost] = new float2(math.sin(angle), math.cos(angle)) * ghostRadius;
            }
            const int boundarySides = 32;
            var boundary = new float2[boundarySides];
            float outerRadius = tuning.MaximumDistance * 0.92f + tuning.MaximumWidth * 0.72f;
            for (int index = 0; index < boundarySides; index++)
            {
                float angle = index * math.PI * 2f / boundarySides;
                boundary[index] = new float2(math.sin(angle), math.cos(angle)) * outerRadius;
            }
            EarthStructureFracturePlan plan = VoronoiFractureSolver.BuildClippedFromSites(
                (uint)(0x57EBA11u + seed * 977u), boundary, sites);
            float sectorDegrees = math.lerp(45f, 360f, math.saturate(sectorCharge01));
            float halfSector = sectorDegrees * 0.5f + 0.01f;
            var cells = new List<EarthWebWaveCell>(samples.Length);
            for (int index = 0; index < samples.Length; index++)
            {
                EarthPillarWaveSample source = samples[index];
                if (sectorDegrees < 359.5f && math.abs(source.AngleDegrees) > halfSector) continue;
                VoronoiFractureCell cell = plan.Cells[index];
                float2[] footprint = SimplifyFootprint(cell.Vertices, cell.Centroid, 8);
                float centroidDistance = math.length(cell.Centroid);
                float centroidAngle = math.degrees(math.atan2(cell.Centroid.x, cell.Centroid.y));
                float centroidRadians = math.radians(centroidAngle);
                float sine = math.sin(centroidRadians);
                float cosine = math.cos(centroidRadians);
                for (int vertex = 0; vertex < footprint.Length; vertex++)
                {
                    float2 point = footprint[vertex];
                    footprint[vertex] = new float2(
                        point.x * cosine - point.y * sine,
                        point.x * sine + point.y * cosine);
                }
                var sample = new EarthPillarWaveSample(
                    source.Row,
                    centroidAngle,
                    centroidDistance,
                    source.Width,
                    source.Depth,
                    source.Height,
                    source.StartDelay,
                    source.HoldDuration,
                    source.Crest01,
                    footprint.Length,
                    source.ShapeAreaScale,
                    source.SpiralPhase01,
                    family);
                cells.Add(new EarthWebWaveCell(index, in sample, footprint, cell.Area));
            }
            int radialThreads = 12 + ((seed * 5 + 3) % 7);
            return new EarthWebWaveTopology(seed, radialThreads, cells.ToArray(), family);
        }

        private static float2[] SimplifyFootprint(float2[] vertices, float2 centroid, int maximumVertices)
        {
            if (vertices == null || vertices.Length < 3) return System.Array.Empty<float2>();
            int count = math.min(maximumVertices, vertices.Length);
            var local = new float2[count];
            if (vertices.Length <= maximumVertices)
            {
                for (int index = 0; index < count; index++) local[index] = vertices[index] - centroid;
                return local;
            }
            for (int index = 0; index < count; index++)
            {
                int source = (int)math.floor(index * vertices.Length / (float)count);
                local[index] = vertices[source] - centroid;
            }
            return local;
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

        public static float ResolveCellBaseOffset(
            float sampleHeight,
            float slabThickness,
            in EarthPillarWaveMotionSample motion)
        {
            float burialDepth = math.max(math.max(0.24f, slabThickness) * 1.12f, 0.42f);
            return math.max(0.1f, sampleHeight) * motion.Height01 - burialDepth * motion.Sink01;
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
