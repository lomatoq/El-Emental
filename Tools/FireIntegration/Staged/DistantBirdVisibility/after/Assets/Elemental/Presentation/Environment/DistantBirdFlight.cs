using System;
using Unity.Mathematics;

namespace Elemental.Presentation.DistantScenery
{
    public static class DistantBirdFlight
    {
        public struct Bird
        { public float3 Center,Formation;public float Phase,Period,Span,FlapPeriod,FlapPhase; }
        public struct Pose
        { public float3 Position,Forward;public float Bank,WingAngle; }
        public static Bird Create(int seed,int index)
        {
            int flock=index/4,member=index%4;var group=new RockRandom(unchecked(seed+flock*73856093));
            float side=flock%2==0?-1:1,depth=flock%4<2?1:-1;
            // One nearer flock in each camera hemisphere remains perceptible in the
            // actual 785-high window; the other flock preserves distant scale contrast.
            bool near=flock%2==0;
            var center=new float3(side*group.Next(100,185),group.Next(near?110:135,near?140:185),depth*group.Next(near?560:780,near?650:1050));
            float phase=group.Next(0,6.2831853f),period=group.Next(80,130);
            var individual=new RockRandom(unchecked(seed+index*19349663));
            return new Bird{Center=center,Formation=new float3((member-1.5f)*individual.Next(4,7),individual.Next(-3,3),member*individual.Next(3,6)),
                Phase=phase,Period=period,Span=individual.Next(near?2.25f:1.3f,near?2.65f:1.9f),FlapPeriod=individual.Next(.65f,1.05f),FlapPhase=individual.Next(0,6.2831853f)};
        }
        public static Pose Evaluate(Bird bird,double time,bool reducedMotion)
        {
            if(double.IsNaN(time)||double.IsInfinity(time))time=0;
            if(reducedMotion)time=0;
            float theta=(float)(time/(bird.Period*2)%1.0)*12.5663706f+bird.Phase;
            float3 tangent=new float3(-75*math.sin(theta),7*math.cos(theta*.5f),38*math.cos(theta));
            float3 forward=math.normalize(tangent),right=math.normalizesafe(math.cross(new float3(0,1,0),forward),new float3(1,0,0));
            float3 position=bird.Center+new float3(75*math.cos(theta),14*math.sin(theta*.5f),38*math.sin(theta));
            position+=right*bird.Formation.x+new float3(0,bird.Formation.y,0)-forward*bird.Formation.z;
            float glide=math.smoothstep(-.2f,.45f,math.sin((float)(time/12%1)*6.2831853f+bird.FlapPhase));
            float flap=math.sin((float)(time/bird.FlapPeriod%1)*6.2831853f+bird.FlapPhase);
            return new Pose{Position=position,Forward=forward,Bank=.22f*math.sin(theta+.6f),
                WingAngle=reducedMotion?.12f:math.lerp(.12f,.15f+flap*.72f,glide)};
        }
    }
}
