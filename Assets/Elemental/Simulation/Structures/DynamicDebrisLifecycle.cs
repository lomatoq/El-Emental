using Unity.Mathematics;

namespace Elemental.Simulation.Structures
{
    public readonly struct DynamicDebrisLifecycleSample
    {
        public DynamicDebrisLifecycleSample(float scale01, bool shrinking, bool complete)
        {
            Scale01 = scale01;
            Shrinking = shrinking;
            Complete = complete;
        }

        public float Scale01 { get; }
        public bool Shrinking { get; }
        public bool Complete { get; }
    }

    /// <summary>
    /// Pure presentation-lifetime contract for physical debris. It changes only
    /// visual scale: runtime adapters deliberately keep velocity, collision and
    /// local gravity alive until Complete becomes true.
    /// </summary>
    public static class DynamicDebrisLifecycle
    {
        public static DynamicDebrisLifecycleSample Evaluate(
            float elapsedSeconds,
            float freeMotionSeconds,
            float shrinkSeconds)
        {
            float elapsed = math.max(0f, elapsedSeconds);
            float shrinkStart = math.max(0f, freeMotionSeconds);
            float duration = math.max(0.01f, shrinkSeconds);
            if (elapsed < shrinkStart)
                return new DynamicDebrisLifecycleSample(1f, false, false);

            float t = math.saturate((elapsed - shrinkStart) / duration);
            float eased = t * t * (3f - (2f * t));
            return new DynamicDebrisLifecycleSample(
                math.max(0f, 1f - eased),
                true,
                t >= 1f);
        }
    }
}
