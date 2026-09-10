using Elemental.Simulation.Fire;
using NUnit.Framework;
using Unity.Mathematics;
namespace Elemental.Tests.EditMode
{
 public sealed class FireMeteorBowMathTests
 {
  [Test]public void SupportFollowsPosedBodyAndLeavesNoRemoteForwardLobe()
  {
   float3 hips=new float3(5,1,7),head=new float3(5,1.2f,8),forward=new float3(0,0,1);
   float radius=FireMeteorBowMath.Radius(new float3(4.7f,1,7.7f),new float3(5.3f,1,7.7f));
   Assert.That(radius,Is.InRange(.35f,.39f));
   var hi=FireMeteorBowMath.Maximum(hips,head,forward,radius);
   Assert.That(hi.z-head.z,Is.LessThan(.6f));
   Assert.That(math.distance(FireMeteorBowMath.ClosestBodyPoint(new float3(5,1.1f,7.5f),hips,head),(hips+head)*.5f),Is.LessThan(.0001f));
   float3 shift=new float3(-30,20,-10);
   Assert.That(math.distance(FireMeteorBowMath.Maximum(hips+shift,head+shift,forward,radius),hi+shift),Is.LessThan(.0001f));
  }
  [Test]public void LiveSurfaceDeformationIsContinuousBoundedAndChangesWithTime()
  {
   float peakChange=0;var p=new float3(.32f,.27f,.41f);
   for(int i=0;i<600;i++)
   {
    float t=i/60f,a=FireMeteorBowMath.Deformation(p,t),b=FireMeteorBowMath.Deformation(p,t+1f/60);
    Assert.That(math.abs(a),Is.LessThanOrEqualTo(FireMeteorBowMath.MaximumDeformation));
    Assert.That(math.abs(a-b),Is.LessThan(.014f));peakChange=math.max(peakChange,math.abs(a));
   }
   Assert.That(peakChange,Is.GreaterThan(.04f));
  }
 }
}
