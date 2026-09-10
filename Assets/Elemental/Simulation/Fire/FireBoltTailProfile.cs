using Unity.Mathematics;
namespace Elemental.Simulation.Fire
{
    // Traveling fire sheds momentum continuously. Held charge keeps its separate growth timing.
    public static class FireBoltTailProfile
    {
        public const float DetailStartAge=.08f;
        public static FireFlowInjection Injection(float power,float speed,float age,uint id,float3 direction,float3 up,bool drill,int capacity)
        {
            var profile=FireChargedBoltProfile.FromPower(power);
            direction=math.normalizesafe(direction,new float3(0,0,1));
            float3 side=math.normalizesafe(math.cross(up,direction),FireContactMath.Tangent(direction));
            float3 across=math.cross(direction,side);
            float phase=age*17+id*.73f;
            float3 roll=side*math.sin(phase)+across*math.cos(phase);
            float hot=math.lerp(.20f,.26f,profile.Mass01);
            float rate=math.min(160,capacity*.82f/(hot+profile.CoolingSeconds*.25f));
            // Sufficient inheritance joins the nose before the fine flame becomes visible.
            // Cooling retains this transported velocity; ordinary drag and sweeps still apply.
            return new FireFlowInjection(direction*(speed*.55f)+roll*(drill?1.8f:.9f),.4f,rate,hot,12,profile.DetailSize*.85f,
                developmentScale:3,tailAgeScale:1,staggerBirths:true,birthTangent:roll*math.lerp(.08f,.20f,profile.Mass01),
                coolingTail:profile.CoolingSeconds,uniformBirthSpread:true,elongation:.28f,smokeExpansion:1.8f,smokeStride:4,trackEmitterPath:true);
        }
    }
}
