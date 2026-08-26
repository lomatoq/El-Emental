using Unity.Mathematics;

namespace Elemental.Simulation.Structures
{
    public readonly struct EarthDecorRockDamageResult
    {
        public EarthDecorRockDamageResult(float integrity, bool detach, bool shatter)
        {
            Integrity = math.max(0f, integrity);
            Detach = detach;
            Shatter = shatter;
        }

        public float Integrity { get; }
        public bool Detach { get; }
        public bool Shatter { get; }
    }

    public static class EarthDecorRockDamageSolver
    {
        public static EarthDecorRockDamageResult Resolve(
            float integrity,
            float impulse,
            bool anchored,
            float detachImpulse,
            float shatterImpulse)
        {
            float safeImpulse = math.max(0f, impulse);
            float remaining = math.max(0f, integrity - safeImpulse);
            bool shatter = safeImpulse >= math.max(1f, shatterImpulse) || remaining <= 0f;
            bool detach = anchored && !shatter && safeImpulse >= math.max(1f, detachImpulse);
            return new EarthDecorRockDamageResult(remaining, detach, shatter);
        }
    }
}
