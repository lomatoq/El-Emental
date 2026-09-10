using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Combat
{
    /// <summary>Authoritative reservation for the existing KO deadline, not a second life timer.</summary>
    public readonly struct EarthRespawnCue
    {
        public EarthRespawnCue(EarthDuelFighterId fighter, uint lifeGeneration, uint revision,
            float3 rootPosition, quaternion rotation, float3 feetPosition, float3 up,
            double startsAt, double activeAt)
        {
            if (lifeGeneration == 0 || revision == 0 || !math.all(math.isfinite(rootPosition)) ||
                !math.all(math.isfinite(feetPosition)) || !math.all(math.isfinite(up)) ||
                !math.all(math.isfinite(rotation.value)) || math.lengthsq(rotation.value) < .5f || math.lengthsq(up) < .5f ||
                !double.IsFinite(startsAt) || !double.IsFinite(activeAt) || activeAt <= startsAt)
                throw new ArgumentException("Respawn cue requires a finite validated pose, generation and future deadline.");
            Fighter = fighter; LifeGeneration = lifeGeneration; Revision = revision;
            RootPosition = rootPosition; Rotation = rotation; FeetPosition = feetPosition; Up = math.normalize(up);
            StartsAt = startsAt; ActiveAt = activeAt;
        }
        public EarthDuelFighterId Fighter { get; }
        public uint LifeGeneration { get; }
        public uint Revision { get; }
        public float3 RootPosition { get; }
        public quaternion Rotation { get; }
        public float3 FeetPosition { get; }
        public float3 Up { get; }
        public double StartsAt { get; }
        public double ActiveAt { get; }
        public bool IsValid => LifeGeneration != 0 && Revision != 0 && ActiveAt > StartsAt;
    }

    public readonly struct GoldRespawnFrame
    {
        public GoldRespawnFrame(bool visible, bool showProxy, float ringRadius, float ringAlpha,
            float scale, float lift, float emission)
        { Visible = visible; ShowProxy = showProxy; RingRadius = ringRadius; RingAlpha = ringAlpha;
          Scale = scale; Lift = lift; Emission = emission; }
        public bool Visible { get; }
        public bool ShowProxy { get; }
        public float RingRadius { get; }
        public float RingAlpha { get; }
        public float Scale { get; }
        public float Lift { get; }
        public float Emission { get; }
    }

    public static class GoldRespawnTimeline
    {
        public const float TerminalSeconds = .70f;
        public static GoldRespawnFrame Sample(in EarthRespawnCue cue, double time, bool reducedMotion)
        {
            if (!cue.IsValid || !double.IsFinite(time) || time < cue.StartsAt || time >= cue.ActiveAt)
                return new GoldRespawnFrame(false, false, 0, 0, 1, 0, 0);
            // A late replacement pose compresses the remaining visual interval, never extends KO.
            float age = (float)((time - cue.StartsAt) / (cue.ActiveAt - cue.StartsAt)) * TerminalSeconds;
            float converge = Smooth((age - .08f) / .34f);
            float ringAlpha = Smooth(age / .10f) * (1 - Smooth((age - .50f) / .20f));
            // The reservation pose is already canonical. Reveal it at full size
            // and its final position, including the very first visible frame.
            float scale = 1;
            float lift = 0;
            float emission = Smooth((age - .38f) / .10f) * (1 - Smooth((age - .54f) / .14f));
            return new GoldRespawnFrame(true, age >= .40f, math.lerp(1, .06f, converge),
                ringAlpha * (reducedMotion ? .55f : 1), scale, lift, emission * (reducedMotion ? .6f : 1));
        }
        private static float Smooth(float value) { float t = math.saturate(value); return t * t * (3 - 2 * t); }
    }
}
