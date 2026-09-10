using Unity.Mathematics;
namespace Elemental.Simulation.Rendering
{
    // One seeded cosmetic clock, independent of exposure and gameplay time owners.
    public struct DistantStormClock
    {
        private uint random;
        private float remaining,age,duration,gap,secondary;
        private bool started,rumbleSent;
        public uint Sequence { get; private set; }
        public float NextInterval { get; private set; }
        public bool RumbleDue { get; private set; }
        private float Next(){random=random*1664525u+1013904223u;return (random&0xffffffu)/16777216f;}
        public float Step(float delta,bool enabled,uint seed=38217)
        {
            RumbleDue=false;
            if(!started){random=seed;remaining=NextInterval=18+27*Next();age=10;started=true;}
            if(!enabled || !math.isfinite(delta) || delta<=0)return 0;
            remaining-=delta;age+=delta;
            if(remaining<=0)
            {
                Sequence++;age=0;duration=.25f+.30f*Next();gap=.18f+.12f*Next();secondary=Next()<.2f?.3f:0;
                remaining=NextInterval=18+27*Next();rumbleSent=false;
            }
            if(!rumbleSent && Sequence>0 && age>=2.5f){RumbleDue=true;rumbleSent=true;}
            return Pulse(age,duration)+secondary*Pulse(age-duration-gap,duration*.8f);
        }
        private static float Pulse(float age,float duration)
        {if(age<=0 || age>=duration)return 0;float wave=math.sin(math.PI*age/duration);return wave*wave;}
    }
}
