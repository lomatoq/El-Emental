using System;

namespace Elemental.Simulation.Structures
{
    public enum EarthArenaFractureTrigger : byte
    {
        OrdinaryImpact = 0,
        MagicPluck = 1,
        MeteorImpact = 2,
        LandingSlam = 3
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

        public static float NormalContactImpulse(float closingSpeed,float effectiveMass,float solverImpulse)
        {
            if(!float.IsFinite(closingSpeed)||!float.IsFinite(effectiveMass)||!float.IsFinite(solverImpulse)||
                closingSpeed<.75f||effectiveMass<=0f||solverImpulse<0f)return 0f;
            float momentum=closingSpeed*effectiveMass;
            return float.IsFinite(momentum)?Math.Min(2f*momentum,Math.Max(momentum,solverImpulse)):0f;
        }

        // Damage is a persistent history, while momentum belongs only to this hit.
        // This budget is shared across direct releases; unsupported pieces get no kick.
        public static float ReleaseImpulsePerPiece(float currentImpulse, int requestedCount)
            => float.IsFinite(currentImpulse) && currentImpulse > 0f && requestedCount > 0
                ? currentImpulse / requestedCount : 0f;

        public static bool IsMeaningfulDamage(float impulse, float threshold, float minimumFraction)
            => float.IsFinite(impulse) && float.IsFinite(threshold) && float.IsFinite(minimumFraction) &&
                impulse >= Math.Max(1f, Math.Max(1f, threshold) * Math.Clamp(minimumFraction, 0f, 1f));


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
            if (trigger == EarthArenaFractureTrigger.LandingSlam)
                return float.IsFinite(impulse) && impulse > 0f
                    ? new EarthArenaFractureDecision(true, Math.Min(4, remaining)) : default;
            if (!ordinaryDamageEnabled) return default;
            if (trigger == EarthArenaFractureTrigger.MagicPluck)
                return new EarthArenaFractureDecision(true, 1);
            if (!float.IsFinite(impulse) || impulse < MinimumOrdinaryImpulse) return default;

            int releaseCount = impulse >= 1600f ? 3 : impulse >= 650f ? 2 : 1;
            return new EarthArenaFractureDecision(true, Math.Min(releaseCount, remaining));
        }
    }
}
