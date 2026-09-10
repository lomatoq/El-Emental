using Unity.Mathematics;
namespace Elemental.Simulation.Fire
{
 public static class FireRingRibbonMath
 {
  public const int MaximumSpans=24;public const float Radius=.42f;
  public static bool TrySpan(float3 from,float3 to,IFireFlowCollision collision,out float3 end)
  {
   end=from;float3 delta=to-from;float length=math.length(delta);
   if(!math.all(math.isfinite(from))||!math.all(math.isfinite(to))||length<.02f||length>4.5f)return false;
   if(!collision.Sweep(from,Radius,delta,out var hit)){end=to;return true;}
   if(hit.Blocked||!math.isfinite(hit.Fraction)||hit.Fraction<=0)return false;
   end=from+delta*math.max(0,math.saturate(hit.Fraction)-.005f);
   return math.distance(from,end)>.08f;
  }
 }
}
