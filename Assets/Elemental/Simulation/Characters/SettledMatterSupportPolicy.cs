using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    /// <summary>Sleeping admits a physical support; small contact wakes may retain it.
    /// Slow velocity alone cannot admit a stone at the apex of a throw.</summary>
    public static class SettledMatterSupportPolicy
    {
        public static bool CanSupport(bool sleeping, bool previouslySettled,
            bool kinematic, float linearSpeedSquared, float angularSpeedSquared)
        {
            if (kinematic || !math.isfinite(linearSpeedSquared) ||
                !math.isfinite(angularSpeedSquared)) return false;
            return sleeping || previouslySettled && linearSpeedSquared <= 0.15f * 0.15f &&
                angularSpeedSquared <= 0.35f * 0.35f;
        }
    }
}
