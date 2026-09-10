using System;
using Unity.Mathematics;
namespace Elemental.Simulation.Fire
{
    public readonly struct FireBoltTrailSpan
    {
        public readonly float3 A,B;public readonly float RadiusA,RadiusB,AgeA,AgeB;
        public FireBoltTrailSpan(float3 a,float3 b,float ra,float rb,float aa,float ab){A=a;B=b;RadiusA=ra;RadiusB=rb;AgeA=aa;AgeB=ab;}
    }
    public static class FireBoltTrailMath
    {
        public const int MaximumSpans=6;
        public static float Radius(float coreRadius,float age)=>math.max(.035f,(.055f+coreRadius*.62f)*math.pow(1-math.saturate(age),.72f));
        // Read transported gas, then validate the complete connecting volume independently.
        // No uncollided position is extrapolated past an obstacle or missing gas sample.
        public static int Build(bool emitting,float3 head,float coreRadius,FireFlowParticle[] particles,int count,IFireFlowCollision world,FireBoltTrailSpan[] output,out int queries)
        {
            if(world==null||output==null||output.Length<MaximumSpans||particles==null||count<0||count>particles.Length||!math.all(math.isfinite(head))||!math.isfinite(coreRadius)||coreRadius<=0)throw new ArgumentException("Explicit finite trail source and preallocated span buffer required.");
            queries=0;int written=0;float3 previous=head;float previousAge=0;bool havePrevious=emitting;
            for(int band=0;band<MaximumSpans;band++)
            {
                float target=.08f+band*.16f,best=float.MaxValue;int selected=-1;
                for(int i=0;i<count;i++)
                {
                    var p=particles[i];if(p.Spark||p.HotLifetime<=0||p.Temperature<.42f||!math.all(math.isfinite(p.Position))||!math.isfinite(p.Age)||!math.isfinite(p.HotLifetime)||!math.isfinite(p.Temperature))continue;
                    float age=p.Age/p.HotLifetime;if(age> .94f||(havePrevious&&age<=previousAge+.015f))continue;
                    float score=math.abs(age-target);if(score<best){best=score;selected=i;}
                }
                if(selected<0)break;
                var gas=particles[selected];float nextAge=gas.Age/gas.HotLifetime;float3 next=gas.Position;
                if(!havePrevious){previous=next;previousAge=nextAge;havePrevious=true;continue;}
                float3 delta=next-previous;float length=math.length(delta);
                if(length<.025f)continue;if(length>3)break;
                float ra=Radius(coreRadius,previousAge),rb=Radius(coreRadius,nextAge),radius=math.max(ra,rb);queries++;
                bool hit=world.Sweep(previous,radius,delta,out var contact);
                if(hit)
                {
                    if(contact.Blocked||!math.isfinite(contact.Fraction)||contact.Fraction<=.005f)break;
                    float fraction=math.max(0,math.saturate(contact.Fraction)-.005f);
                    next=previous+delta*fraction;rb=math.lerp(ra,rb,fraction);nextAge=math.lerp(previousAge,nextAge,fraction);
                    if(math.distance(previous,next)<.025f)break;
                }
                output[written++]=new FireBoltTrailSpan(previous,next,ra,rb,previousAge,nextAge);
                if(hit)break;previous=next;previousAge=nextAge;
            }
            return written;
        }
    }
}
