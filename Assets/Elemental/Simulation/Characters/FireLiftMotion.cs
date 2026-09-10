using Unity.Mathematics;
namespace Elemental.Simulation.Characters {
 // Pure local-up servo. Contact constraint limits speed before forces reach PhysX.
 public static class FireLiftMotion {
  // Outside the existing resolver apex band abs(v)<.3, while retaining actual fast falls.
  public static float AnimationVerticalSpeed(float actual,bool liftAirbornePose)=>liftAirbornePose?math.min(-.5f,actual):actual;
  public static float TargetSpeed(float charge)=>math.lerp(2f,8f,math.saturate(charge));
  public static float VelocityChange(float speed,float gravityUp,float charge,float delta,float clearance) {
   if(!math.isfinite(speed)||!math.isfinite(gravityUp)||!math.isfinite(charge)||!math.isfinite(delta)||delta<=0)return 0;
   float target=TargetSpeed(charge),next=speed+math.clamp(target-speed,-18f*delta,18f*delta);
   if(math.isfinite(clearance))next=math.min(next,math.max(0,clearance)/delta);
   return next-speed-gravityUp*delta;
  }
 }
}
