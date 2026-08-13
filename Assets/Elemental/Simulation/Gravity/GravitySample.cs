using Unity.Mathematics;

namespace Elemental.Simulation.Gravity
{
    public readonly struct GravitySample
    {
        public GravitySample(float3 acceleration, float3 up, float potentialHint, GravityFieldId source)
        {
            Acceleration = acceleration;
            Up = up;
            PotentialHint = potentialHint;
            Source = source;
        }

        public float3 Acceleration { get; }
        public float3 Up { get; }
        public float PotentialHint { get; }
        public GravityFieldId Source { get; }

        public bool IsFinite =>
            math.all(math.isfinite(Acceleration)) &&
            math.all(math.isfinite(Up)) &&
            math.isfinite(PotentialHint);

        public static GravitySample None =>
            new GravitySample(float3.zero, new float3(0f, 1f, 0f), 0f, default);
    }
}
