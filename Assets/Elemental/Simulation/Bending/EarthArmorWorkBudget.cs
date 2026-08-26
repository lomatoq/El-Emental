namespace Elemental.Simulation.Bending
{
    public readonly struct EarthArmorWorkSlice
    {
        public readonly int Start;
        public readonly int Count;
        public readonly bool IsComplete;

        public EarthArmorWorkSlice(int start, int count, bool isComplete)
        {
            Start = start;
            Count = count;
            IsComplete = isComplete;
        }
    }

    /// <summary>
    /// Pure scheduling contract for armor preparation and release. Runtime work is
    /// deliberately capped per fixed tick so a complete 96-plate shell can never be
    /// activated or physicalized in one main-thread spike.
    /// </summary>
    public static class EarthArmorWorkBudget
    {
        public static EarthArmorWorkSlice Next(int completed, int total, int maximumPerSlice)
        {
            int safeTotal = total < 0 ? 0 : total;
            int start = completed < 0 ? 0 : completed > safeTotal ? safeTotal : completed;
            int budget = maximumPerSlice < 1 ? 1 : maximumPerSlice;
            int remaining = safeTotal - start;
            int count = remaining < budget ? remaining : budget;
            return new EarthArmorWorkSlice(start, count, start + count >= safeTotal);
        }
    }
}
