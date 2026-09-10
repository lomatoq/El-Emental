using System;
using Elemental.Simulation.Fire;
using NUnit.Framework;
using Unity.Mathematics;
namespace Elemental.Tests.EditMode
{
 public sealed class FireBoltBodyShapeTests
 {
  [Test] public void QuickAndFullBodiesRemainInsideTheirActualHitSphereThroughoutFlight()
  {
   foreach(float power in new[]{.55f,.9f,3.6f})for(int frame=0;frame<120;frame++)
   {
    var s=FireBoltBodyShape.Evaluate(power,frame/60f,31);
    Assert.That(s.Radius,Is.EqualTo(power*.2f).Within(.00001f));
    for(int axis=0;axis<3;axis++){float3 p=default;p[axis]=1.001f;Assert.That(s.ContainsNormalized(p),Is.False);p[axis]=-1.001f;Assert.That(s.ContainsNormalized(p),Is.False);}
    Assert.That(s.CrossSection(s.Nose),Is.Zero);Assert.That(s.CrossSection(-s.Rear),Is.Zero);
    Assert.That(s.ContainsNormalized(float3.zero),Is.True);
   }
  }
  [Test] public void RoundedHotFrontAndTaperedRearEvolveContinuouslyWithoutFrameRateDependency()
  {
   var first=FireBoltBodyShape.Evaluate(3.6f,0,9);float change=0;
   for(int frame=1;frame<=60;frame++)
   {
    var s=FireBoltBodyShape.Evaluate(3.6f,frame/60f,9);var previous=FireBoltBodyShape.Evaluate(3.6f,(frame-1)/60f,9);
    Assert.That(s.CrossSection(s.Nose*.6f),Is.GreaterThan(s.CrossSection(-s.Rear*.6f)));
    Assert.That(math.abs(s.Width-previous.Width)+math.abs(s.Bend-previous.Bend),Is.LessThan(.015f));
    change=math.max(change,math.abs(s.Width-first.Width)+math.abs(s.Bend-first.Bend));
    if(frame%2==0)Assert.That(s.Width,Is.EqualTo(FireBoltBodyShape.Evaluate(3.6f,(frame/2)/30f,9).Width));
   }
   Assert.That(change,Is.GreaterThan(.1f));
  }
  [Test] public void InvalidBodyInputsAreRejectedAndColdNegativeAgeCannotReverseAnimation()
  {
   Assert.Throws<ArgumentException>(()=>FireBoltBodyShape.Evaluate(float.NaN,1,1));
   Assert.Throws<ArgumentException>(()=>FireBoltBodyShape.Evaluate(1,float.PositiveInfinity,1));
   Assert.Throws<ArgumentException>(()=>FireBoltBodyShape.Evaluate(0,1,1));
   Assert.That(FireBoltBodyShape.Evaluate(1,-1,1).Width,Is.EqualTo(FireBoltBodyShape.Evaluate(1,0,1).Width));
  }
 }
}
