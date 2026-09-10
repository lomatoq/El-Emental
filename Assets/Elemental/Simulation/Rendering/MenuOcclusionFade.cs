using System;
namespace Elemental.Simulation.Rendering
{
    /// <summary>Render-clock transition; no topology or gameplay visibility authority.</summary>
    public static class MenuOcclusionFade
    {
        public const float FadeOutSeconds = .18f, FadeInSeconds = .24f;
        public static float Step(float current, float target, float deltaSeconds)
        {
            current = Math.Clamp(current, 0, 1); target = Math.Clamp(target, 0, 1);
            float step = Math.Max(0, deltaSeconds) / (target < current ? FadeOutSeconds : FadeInSeconds);
            return target < current ? Math.Max(target, current - step) : Math.Min(target, current + step);
        }
    }
}
