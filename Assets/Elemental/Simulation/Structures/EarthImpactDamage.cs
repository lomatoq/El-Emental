using System;

namespace Elemental.Simulation.Structures
{
    /// <summary>Persistent impact fatigue. Contact jitter and non-finite input add no damage.</summary>
    public struct EarthImpactDamage
    {
        public float Impulse { get; private set; }
        public bool Add(float impulse)
        {
            if (!float.IsFinite(impulse) || impulse < 1f) return false;
            Impulse = Math.Min(10000000f, Impulse + impulse);
            return true;
        }
        public void Consume(float impulse) => Impulse = Math.Max(0f, Impulse - Math.Max(0f, impulse));
    }
}
