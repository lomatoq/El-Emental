using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public readonly struct EarthPlatformPreparationSlice
    {
        public EarthPlatformPreparationSlice(int startIndex, int count, bool complete)
        {
            StartIndex = startIndex;
            Count = count;
            Complete = complete;
        }

        public int StartIndex { get; }
        public int Count { get; }
        public bool Complete { get; }
    }

    public static class EarthPlatformPreparationBudget
    {
        public static EarthPlatformPreparationSlice Next(
            int preparedCellCount,
            int totalCellCount,
            int maximumCellsPerFrame = 1)
        {
            if (preparedCellCount < 0 || totalCellCount < 0 ||
                preparedCellCount > totalCellCount)
                throw new ArgumentOutOfRangeException();
            if (maximumCellsPerFrame <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumCellsPerFrame));
            int remaining = totalCellCount - preparedCellCount;
            int count = math.min(remaining, maximumCellsPerFrame);
            return new EarthPlatformPreparationSlice(
                preparedCellCount,
                count,
                remaining <= maximumCellsPerFrame);
        }
    }
}
