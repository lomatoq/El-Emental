using System;

namespace Elemental.Presentation.UI
{
    public static class FrontendAudioEnvelope
    {
        public static double Overlap(double duration,double requested) =>
            Math.Max(.01,Math.Min(Math.Max(.01,requested),Math.Max(.01,duration*.25)));
        public static float Smooth(float from,float to,double elapsed,double duration)
        {
            double t=Math.Clamp(elapsed/Math.Max(.001,duration),0,1);
            return from+(to-from)*(float)(t*t*(3-2*t));
        }
        public static float LoopGain(double age,double duration,double overlap)
        {
            if(age<0 || age>=duration)return 0;
            double t=Math.Min(1,Math.Min(age/overlap,(duration-age)/overlap));
            return (float)Math.Sin(Math.PI*.5*Math.Max(0,t));
        }
        public static float OneShotGain(double age,double duration,double attack,double release)
        {
            if(age<0 || age>=duration)return 0;
            return Math.Min(Smooth(0,1,age,attack),Smooth(0,1,duration-age,release));
        }
    }
}
