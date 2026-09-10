using System;
using Unity.Mathematics;
namespace Elemental.Simulation.Fire
{
    public readonly struct FireBoltBodyShape
    {
        public readonly float Radius,Width,Nose,Rear,Bend,Phase;
        private FireBoltBodyShape(float radius,float width,float nose,float rear,float bend,float phase)
        {Radius=radius;Width=width;Nose=nose;Rear=rear;Bend=bend;Phase=phase;}
        public static FireBoltBodyShape Evaluate(float power,float age,uint id)
        {
            if(!math.isfinite(power)||power<=0||!math.isfinite(age))throw new ArgumentException("Finite positive bolt power and finite age required.");
            float phase=math.frac(id*.61803398875f)*6.2831853f,t=math.max(0,age);
            return new FireBoltBodyShape(.2f*power,.67f+.07f*math.sin(t*4.1f+phase),.87f+.05f*math.sin(t*5.3f+phase*.7f),.92f+.04f*math.cos(t*3.2f+phase),.05f*math.sin(t*4.6f+phase*1.1f),phase);
        }
        public float CrossSection(float z)
        {
            if(z>=Nose||z<=-Rear)return 0;
            return Width*(z>=0?math.sqrt(math.saturate(1-(z/Nose)*(z/Nose))):math.pow(math.saturate(1+z/Rear),.55f));
        }
        // The noisy flame can only remove density within this authoritative sphere envelope.
        public bool ContainsNormalized(float3 point)
        {
            if(!math.all(math.isfinite(point))||math.lengthsq(point)>1)return false;
            float width=CrossSection(point.z);float2 transverse=point.xy-new float2(Bend*math.saturate(1-point.z*point.z),0);
            return width>0&&math.lengthsq(transverse)<=width*width;
        }
    }
}
