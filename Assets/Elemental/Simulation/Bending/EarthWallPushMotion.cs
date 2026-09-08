using System;
namespace Elemental.Simulation.Bending
{
    public struct EarthWallPushMotion
    {
        public const double HoldDelaySeconds=.15, FullChargeSeconds=1;
        private double _age;
        private float _mass;
        public bool Active { get; private set; }
        public float Charge01=>Active?(float)Math.Clamp((_age-HoldDelaySeconds)/(FullChargeSeconds-HoldDelaySeconds),0,1):0;
        public float Begin(float mass)
        {
            if(Active)return 0;
            _mass=SafeMass(mass);_age=0;Active=true;return 0;
        }
        public float Step(float deltaTime)
        {
            if(Active&&float.IsFinite(deltaTime)&&deltaTime>0)_age=Math.Min(FullChargeSeconds,_age+deltaTime);
            return 0;
        }
        public float Release()
        {
            if(!Active)return 0;
            float impulse=Math.Min(24000,_mass*18)*(1+1.5f*Charge01);
            Cancel();return impulse;
        }
        public static int DustCount(float charge,bool launch)=>
            (launch?32:18)+(int)Math.Round(Math.Clamp(charge,0,1)*(launch?48:24));
        public static int ChipCount(float charge,bool launch)=>
            (launch?6:3)+(int)Math.Round(Math.Clamp(charge,0,1)*(launch?14:7));
        public void Cancel(){Active=false;_age=0;}
        public static float Drag(float mass)=>.30f*(.65f+SafeMass(mass)/1200f);
        private static float SafeMass(float mass)=>float.IsFinite(mass)?Math.Max(1,mass):1800;
    }
}
