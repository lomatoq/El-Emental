using Unity.Mathematics;
namespace Elemental.Simulation.Fire
{
    // World-space interval contract shared by the visual proxy and native depth cases.
    public static class FireSphereVolumeMath
    {
        public const float Radius=2.3f,Support=1.08f,InnerClearRadius=.68f;
        public const int RaySteps=24;
        public static bool RayInterval(float3 origin,float3 direction,float3 center,float radius,float opaqueDistance,out float2 interval)
        {
            interval=0;if(!math.all(math.isfinite(origin))||!math.all(math.isfinite(direction))||!math.all(math.isfinite(center))||!math.isfinite(radius)||radius<=0||math.lengthsq(direction)<.000001f||math.isnan(opaqueDistance))return false;
            direction=math.normalize(direction);float3 offset=origin-center;float b=math.dot(offset,direction),c=math.lengthsq(offset)-radius*radius*Support*Support;
            float discriminant=b*b-c;if(discriminant<=0)return false;
            float root=math.sqrt(discriminant);interval=new float2(math.max(0,-b-root),math.min(-b+root,opaqueDistance));
            return interval.y>interval.x;
        }
    }
}
