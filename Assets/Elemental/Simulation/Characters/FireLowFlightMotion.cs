using Unity.Mathematics;
namespace Elemental.Simulation.Characters
{
    // Local-up hover and speed contract. Geometry samples enter explicitly; no rendering authority.
    public static class FireLowFlightMotion
    {
        public const float Height=.55f, MaximumSupportDistance=1.8f, MaximumSpeed=15f;
        public const float Acceleration=32f, HoverAcceleration=36f, MaximumVerticalSpeed=3f;
        public static bool WantsFlight(bool shift,float forward,bool primary,bool secondary,bool middle,bool jump)
            =>shift&&math.isfinite(forward)&&forward>=.18f&&!primary&&!secondary&&!middle&&!jump;
        public static float3 VelocityChange(float3 velocity,float3 up,float3 direction,float supportDistance,
            float gravityUp,float delta,float forwardClearance,float upwardClearance)
        {
            if(!math.all(math.isfinite(velocity))||!math.all(math.isfinite(up))||
               !math.all(math.isfinite(direction))||!math.isfinite(supportDistance)||
               !math.isfinite(gravityUp)||!math.isfinite(delta)||delta<=0||supportDistance>MaximumSupportDistance)
                return float3.zero;
            up=math.normalizesafe(up,new float3(0,1,0));direction-=up*math.dot(direction,up);
            float amount=math.saturate(math.length(direction));direction=math.normalizesafe(direction);
            float vertical=math.dot(velocity,up);float3 tangent=velocity-up*vertical;
            float3 desired=direction*(MaximumSpeed*amount),change=desired-tangent;
            change=math.normalizesafe(change)*math.min(math.length(change),Acceleration*delta);
            float3 next=tangent+change;
            if(math.isfinite(forwardClearance))
            {
                float speed=math.length(next),allowed=math.max(0,forwardClearance)/delta;
                if(speed>allowed)next*=allowed/math.max(.0001f,speed);
            }
            float targetVertical=math.clamp((Height-supportDistance)*7f,-MaximumVerticalSpeed,MaximumVerticalSpeed);
            float nextVertical=vertical+math.clamp(targetVertical-vertical,-HoverAcceleration*delta,HoverAcceleration*delta);
            if(math.isfinite(upwardClearance))nextVertical=math.min(nextVertical,math.max(0,upwardClearance)/delta);
            return next-tangent+up*(nextVertical-vertical-gravityUp*delta);
        }
    }
}
