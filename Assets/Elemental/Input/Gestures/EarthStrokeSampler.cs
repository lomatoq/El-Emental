using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Profiling;

namespace Elemental.Input.Gestures
{
    /// <summary>Collects only viewport-normalized samples; screen resolution never enters recognition.</summary>
    public sealed class EarthStrokeSampler
    {
        private static readonly ProfilerMarker SampleMarker =
            new ProfilerMarker("Elemental.Earth.Gesture.Sample");

        private readonly List<PointerStrokeSample> _samples;
        private readonly float _minimumDistanceSquared;
        private readonly int _maximumSamples;

        public EarthStrokeSampler(
            float minimumViewportDistance = 0.0025f,
            int maximumSamples = 192)
        {
            _minimumDistanceSquared = math.max(0.000001f,
                minimumViewportDistance * minimumViewportDistance);
            _maximumSamples = math.max(16, maximumSamples);
            _samples = new List<PointerStrokeSample>(_maximumSamples);
        }

        public IReadOnlyList<PointerStrokeSample> Samples => _samples;
        public bool IsActive { get; private set; }
        public float StartTime { get; private set; }

        public void Begin(float2 viewportPosition01, float time, float pressure01 = 1f)
        {
            using (SampleMarker.Auto())
            {
                _samples.Clear();
                _samples.Add(new PointerStrokeSample(viewportPosition01, time, pressure01));
                StartTime = time;
                IsActive = true;
            }
        }

        public void Sample(float2 viewportPosition01, float time, float pressure01 = 1f)
        {
            if (!IsActive || _samples.Count >= _maximumSamples) return;
            using (SampleMarker.Auto())
            {
                PointerStrokeSample next = new PointerStrokeSample(viewportPosition01, time, pressure01);
                PointerStrokeSample previous = _samples[_samples.Count - 1];
                if (math.distancesq(previous.ViewportPosition01, next.ViewportPosition01) <
                    _minimumDistanceSquared) return;
                _samples.Add(next);
            }
        }

        public void End(float2 viewportPosition01, float time, float pressure01 = 1f)
        {
            Sample(viewportPosition01, time, pressure01);
            IsActive = false;
        }

        public void Cancel()
        {
            _samples.Clear();
            IsActive = false;
        }
    }
}
