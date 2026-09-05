using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    /// <summary>A travelling pulse sampled at fixed column positions:
    /// height(distance, time) = profile(time - distance / speed).
    /// Phase durations shape the pulse; they never gate neighbouring rows.</summary>
    public readonly struct EarthWaveTravelSchedule
    {
        public readonly float FirstDistance, SecondsPerMeter, TravelSeconds, Duration;
        public float EffectiveSpeed => 1f / SecondsPerMeter;
        public EarthWaveTravelSchedule(float first, float last, float speed,
            in EarthWaveAnimationTiming timing)
        {
            FirstDistance = first;
            float span = math.max(0f, last - first);
            SecondsPerMeter = 1f / math.max(.1f, speed);
            TravelSeconds = span * SecondsPerMeter;
            Duration = TravelSeconds + timing.TotalDuration;
        }
        public float Delay(float distance) => math.max(0f, distance - FirstDistance) * SecondsPerMeter;
    }

    public readonly struct EarthWaveAnimationTiming
    {
        public readonly float Anticipation, Rise, Settle, Hold, Retreat;
        public EarthWaveAnimationTiming(float anticipation, float rise, float settle, float hold, float retreat)
        {
            Anticipation = math.clamp(anticipation, .01f, 2f);
            Rise = math.clamp(rise, .05f, 5f);
            Settle = math.clamp(settle, .01f, 3f);
            Hold = math.clamp(hold, 0f, 5f);
            Retreat = math.clamp(retreat, .05f, 5f);
        }
        public float Duration => Rise + Settle + Hold + Retreat;
        public float TotalDuration => Anticipation + Duration;
        public int Locate(float time, out float progress)
        {
            if (time < 0f) { progress = math.saturate((time + Anticipation) / Anticipation); return 0; }
            if (time < Rise) { progress = time / Rise; return 1; }
            time -= Rise;
            if (time < Settle) { progress = time / Settle; return 2; }
            time -= Settle;
            if (time < Hold) { progress = time / math.max(.001f, Hold); return 3; }
            time -= Hold;
            progress = math.saturate(time / Retreat);
            return 4;
        }
    }
}
