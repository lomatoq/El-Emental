using Unity.Mathematics;
namespace Elemental.Simulation.Rendering
{
 public readonly struct ValleyTimePalette
 {
  public readonly float3 FogTop,FogBottom,CloudTop,CloudBottom;
  public readonly float SunsetWeight;
  private ValleyTimePalette(float3 top,float3 bottom,float3 cloudTop,float3 cloudBottom,float sunset)
  {FogTop=top;FogBottom=bottom;CloudTop=cloudTop;CloudBottom=cloudBottom;SunsetWeight=sunset;}
  public static ValleyTimePalette Evaluate(float night,float solarAltitude,float3 dayTop,float3 dayBottom)
  {
   night=math.saturate(night);solarAltitude=math.clamp(solarAltitude,-1,1);
   // Both dawn and dusk follow true solar elevation, independently of clock duration.
   float sunset=math.smoothstep(-.22f,-.04f,solarAltitude)*(1-math.smoothstep(.08f,.35f,solarAltitude));
   float3 top=math.lerp(dayTop,new float3(.13f,.15f,.19f),night);
   float3 bottom=math.lerp(dayBottom,new float3(.10f,.12f,.16f),night);
   top=math.lerp(top,new float3(1f,.53f,.30f),sunset*.94f);
   bottom=math.lerp(bottom,new float3(.57f,.33f,.43f),sunset*.90f);
   float3 cloudTop=math.lerp(new float3(1),new float3(.30f,.33f,.38f),night);
   float3 cloudBottom=math.lerp(new float3(1),new float3(.23f,.26f,.31f),night);
   cloudTop=math.lerp(cloudTop,new float3(1,.52f,.26f),sunset*.98f);
   cloudBottom=math.lerp(cloudBottom,new float3(.66f,.32f,.40f),sunset*.94f);
   return new ValleyTimePalette(top,bottom,cloudTop,cloudBottom,sunset);
  }
 }
}
