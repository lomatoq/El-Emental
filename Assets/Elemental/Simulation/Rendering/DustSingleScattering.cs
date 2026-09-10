using System;
namespace Elemental.Simulation.Rendering
{
    /// <summary>Diffuse billboard response in the project's authored light units.</summary>
    public static class DustSingleScattering
    {
        public static float BoundedPeak(float peak)
            => peak <= .75f ? Math.Max(0, peak) : .75f + .25f * (1 - (float)Math.Exp(-(peak - .75f) / .25f));
        public static float HuePreservingGain(float peak)
            => peak > .75f ? BoundedPeak(peak) / peak : 1;
    }
}
