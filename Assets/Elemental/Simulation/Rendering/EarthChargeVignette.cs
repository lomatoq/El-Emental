using Unity.Mathematics;

namespace Elemental.Simulation.Rendering
{
    /// <summary>Bounded dark tension envelope. Reduced Motion retains darkness, removes breathing.</summary>
    public static class EarthChargeVignette
    {
        public static float Solve(float baseline, float charge, float maximum, float pulseDepth,
            float pulseHz, float seconds, bool reducedMotion)
        {
            baseline = math.isfinite(baseline) ? math.clamp(baseline, 0f, .6f) : .1f;
            charge = math.isfinite(charge) ? math.saturate(charge) : 0f;
            maximum = math.isfinite(maximum) ? math.clamp(maximum, baseline, .6f) : baseline;
            pulseDepth = math.isfinite(pulseDepth) ? math.clamp(pulseDepth, 0f, .2f) : 0f;
            pulseHz = math.isfinite(pulseHz) ? math.clamp(pulseHz, .1f, 2f) : 1f;
            seconds = math.isfinite(seconds) ? seconds : 0f;
            float breath = reducedMotion ? 1f : 1f - pulseDepth * (.5f + .5f * math.sin(seconds * pulseHz * 6.2831853f));
            return math.lerp(baseline, maximum, charge * (2f - charge) * breath);
        }
    }
}
