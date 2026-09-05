using Unity.Mathematics;

namespace Elemental.Simulation.Animation
{
    public struct DeterministicMotionClock
    {
        public double TimeSeconds { get; private set; }
        public float SampleRate { get; private set; }

        public DeterministicMotionClock(float sampleRate)
        {
            TimeSeconds = 0d;
            SampleRate = math.max(1f, sampleRate);
        }

        public double ContinuousFrame => TimeSeconds * SampleRate;
        public int Frame => (int)math.floor(ContinuousFrame);
        public float Alpha => (float)(ContinuousFrame - Frame);

        public void Advance(float deltaTime)
        {
            TimeSeconds += math.max(0f, deltaTime);
        }

        public void Seek(double seconds)
        {
            TimeSeconds = math.max(0d, seconds);
        }
    }
}
