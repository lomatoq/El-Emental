namespace Elemental.Simulation.Bending
{
    /// <summary>
    /// Player-authored interaction must never finalize an emerging structure.
    /// External impacts keep their independent damage path.
    /// </summary>
    public static class EarthEmergingStructureInteractionPolicy
    {
        public static bool AllowsPluck(float emergence01, bool fractured) =>
            fractured || emergence01 >= 0.999f;
    }
}
