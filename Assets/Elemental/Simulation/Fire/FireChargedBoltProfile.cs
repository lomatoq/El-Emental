using System;
using Unity.Mathematics;
namespace Elemental.Simulation.Fire
{
    // Shared held/released dimensions. Gameplay owns power and collision; VFX reads this profile.
    public readonly struct FireChargedBoltProfile
    {
        public readonly float Mass01,VisualRadius,CollisionRadius,Power,Speed,DetailSize,TailHotSeconds,CoolingSeconds;
        public readonly int FlameTongues;
        private FireChargedBoltProfile(float mass)
        {
            Mass01=mass;Power=math.lerp(.9f,3.6f,mass);CollisionRadius=.2f*Power;
            VisualRadius=math.lerp(.18f,.95f,mass);Speed=math.lerp(24,35,mass);
            DetailSize=math.lerp(.50f,1.15f,mass);TailHotSeconds=math.lerp(.26f,.36f,mass);
            CoolingSeconds=math.lerp(.32f,.48f,mass);FlameTongues=(int)math.round(math.lerp(6,16,mass));
        }
        public static FireChargedBoltProfile Evaluate(float charge01)
        {
            if(!math.isfinite(charge01))throw new ArgumentException("Charge must be finite.");
            float t=math.saturate(charge01);return new FireChargedBoltProfile(t*t*(3-2*t));
        }
        public static FireChargedBoltProfile FromPower(float power)
        {
            if(!math.isfinite(power))throw new ArgumentException("Power must be finite.");
            return new FireChargedBoltProfile(math.saturate((power-.9f)/2.7f));
        }
        // One in four parcels survives into cooling. Keep headroom for sparks and fractional births.
        public float DetailRateForCapacity(int capacity)
        {
            if(capacity<8)throw new ArgumentOutOfRangeException(nameof(capacity));
            return math.min(math.lerp(128,208,Mass01),capacity*.82f/(TailHotSeconds+CoolingSeconds*.25f));
        }
    }
}
