using System;

namespace Elemental.Simulation.Structures
{
    public enum EarthArenaFractureTrigger : byte
    {
        OrdinaryImpact = 0,
        MagicPluck = 1,
        MeteorImpact = 2
    }

    public readonly struct EarthArenaFractureDecision
    {
        public EarthArenaFractureDecision(bool accepted, int releaseCount)
        {
            Accepted = accepted;
            ReleaseCount = Math.Max(0, releaseCount);
        }

        public bool Accepted { get; }
        public int ReleaseCount { get; }
    }

    /// <summary>
    /// Pure bounded activation policy for authored arena fracture. Ordinary combat
    /// releases a small local budget; only the explicit meteor trigger may swap an
    /// entire meteor-only proxy at once.
    /// </summary>
    public static class EarthArenaFractureGate
    {
        public const float MinimumOrdinaryImpulse = 95f;

        public static EarthArenaFractureDecision Resolve(
            bool ordinaryDamageEnabled,
            EarthArenaFractureTrigger trigger,
            float impulse,
            int remainingPieceCount)
        {
            int remaining = Math.Max(0, remainingPieceCount);
            if (remaining == 0) return default;
            if (trigger == EarthArenaFractureTrigger.MeteorImpact)
                return new EarthArenaFractureDecision(true, remaining);
            if (!ordinaryDamageEnabled) return default;
            if (trigger == EarthArenaFractureTrigger.MagicPluck)
                return new EarthArenaFractureDecision(true, 1);
            if (!float.IsFinite(impulse) || impulse < MinimumOrdinaryImpulse) return default;

            int releaseCount = impulse >= 1600f ? 3 : impulse >= 650f ? 2 : 1;
            return new EarthArenaFractureDecision(true, Math.Min(releaseCount, remaining));
        }
    }
}
