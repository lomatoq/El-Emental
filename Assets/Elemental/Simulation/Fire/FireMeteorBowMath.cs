using Unity.Mathematics;
namespace Elemental.Simulation.Fire
{
    // Body-space support comes from the current pose, never the locomotion root.
    public static class FireMeteorBowMath
    {
        public const float MaximumDeformation=.07f,ShellThickness=.105f,WakeLength=.48f;
        public const int RaySteps=24;
        public static float Radius(float3 shoulderLeft,float3 shoulderRight)
            =>math.clamp(math.distance(shoulderLeft,shoulderRight)*.58f+.025f,.22f,.65f);
        public static float3 ClosestBodyPoint(float3 p,float3 hips,float3 head)
        {
            float3 axis=head-hips;float t=math.saturate(math.dot(p-hips,axis)/math.max(.0001f,math.lengthsq(axis)));
            return hips+axis*t;
        }
        public static float3 Minimum(float3 hips,float3 head,float3 forward,float radius)
            =>math.min(math.min(hips,head)-radius,hips-forward*WakeLength-radius*1.25f)-MaximumDeformation-ShellThickness;
        public static float3 Maximum(float3 hips,float3 head,float3 forward,float radius)
            =>math.max(math.max(hips,head)+radius,hips-forward*WakeLength+radius*1.25f)+MaximumDeformation+ShellThickness;
        public static float Deformation(float3 p,float clock)
            =>MaximumDeformation*(.65f*math.sin(p.x*8+p.z*5+clock*4.8f)*math.sin(p.y*7-p.z*3+clock*2.1f)+.35f*math.sin(p.x*17+p.y*11+p.z*6+clock*7.1f));
    }
}
