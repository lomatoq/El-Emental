using System.Collections.Generic;
using Unity.Mathematics;

namespace Elemental.Input.Gestures
{
    public sealed class PointerPathSampler
    {
        private readonly List<float2> _points = new List<float2>(64);
        private readonly float _minimumDistanceSquared;

        public PointerPathSampler(float minimumDistancePixels = 4f)
        {
            _minimumDistanceSquared = minimumDistancePixels * minimumDistancePixels;
        }

        public IReadOnlyList<float2> Points => _points;
        public bool IsActive { get; private set; }
        public float StartTime { get; private set; }

        public void Begin(float2 position, float time)
        {
            _points.Clear();
            _points.Add(position);
            StartTime = time;
            IsActive = true;
        }

        public void Sample(float2 position)
        {
            if (!IsActive)
            {
                return;
            }

            if (math.distancesq(_points[_points.Count - 1], position) >= _minimumDistanceSquared)
            {
                _points.Add(position);
            }
        }

        public void End(float2 position)
        {
            Sample(position);
            IsActive = false;
        }

        public void Cancel()
        {
            _points.Clear();
            IsActive = false;
        }
    }
}
