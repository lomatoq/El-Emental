using Unity.Mathematics;
namespace Elemental.Simulation.Characters
{
    public static class FireFlightPose
    {
        public const float MaximumLeanDegrees=18f;
        public static float2 StepLean(float2 current,float2 tangentVelocity,bool lifting,float dt)
        {
            if(!math.all(math.isfinite(current)))current=float2.zero;
            if(!math.all(math.isfinite(tangentVelocity)))tangentVelocity=float2.zero;
            float speed=math.length(tangentVelocity);
            float2 target=lifting&&speed>.0001f ? tangentVelocity/speed*(MaximumLeanDegrees*math.saturate(speed/4f)) : float2.zero;
            float t=1-math.exp(-math.max(0,math.isfinite(dt)?dt:0)/.14f);
            float2 next=math.lerp(current,target,t);
            return math.lengthsq(next)>MaximumLeanDegrees*MaximumLeanDegrees ? math.normalize(next)*MaximumLeanDegrees : next;
        }
    }
}
