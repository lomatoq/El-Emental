namespace Elemental.Simulation.Matter
{
    /// <summary>A new match restores authored live sources, not spent pooled shells or cosmetic dust.</summary>
    public static class EarthArenaBaselinePolicy
    {
        public static bool RestoresRepresentation(bool authoredSource, EarthMatterPhase phase, EarthRepresentationTier representation) =>
            authoredSource && phase != EarthMatterPhase.Consumed && representation != EarthRepresentationTier.DormantRecord;
    }
}
