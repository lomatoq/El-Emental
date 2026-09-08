namespace Elemental.Simulation.Time
{
    public static class FrontendWorldClockPolicy
    {
        // Menu pages are live. Only a match boundary owns this world-clock hold;
        // explicit local pause and network authority manage their own clocks.
        public static bool ShouldHold(bool networkRound, bool locallyPaused,
            bool transitioning, bool resettingArena, bool roundFinished) =>
            !networkRound && !locallyPaused && (transitioning || resettingArena || roundFinished);
    }
}
