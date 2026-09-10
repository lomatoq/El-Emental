using System;
namespace Elemental.Simulation.Structures
{
    // Fixed storage. Alternating sources cannot bypass a contact episode cooldown;
    // saturation refuses additional admission rather than evicting a live source.
    public sealed class EarthImpactContactWindow
    {
        private readonly uint[] sources=new uint[32];
        private readonly float[] until=new float[32];
        public bool TryAdmit(uint source,float now,float seconds=.35f)
        {
            if(!float.IsFinite(now)||!float.IsFinite(seconds)||seconds<=0)return false;
            int free=-1;
            for(int i=0;i<sources.Length;i++)
            {
                if(until[i]>now){if(sources[i]==source)return false;}
                else if(free<0)free=i;
            }
            if(free<0)return false;
            sources[free]=source;until[free]=now+seconds;return true;
        }
        public void Clear(){Array.Clear(sources,0,sources.Length);Array.Clear(until,0,until.Length);}
    }
}
