using NUnit.Framework;
using Elemental.Simulation.Rendering;
namespace Elemental.Tests.EditMode
{
 public sealed class ValleyDissolveContinuityTests
 {
  [Test] public void DistantColumnsDoNotInheritPlanetUndersideHeightBand()
  {for(int height=-100;height<=100;height++){double opacity=ValleyAtmosphereMath.ComposeSurfaceOpacity(.3,.4,height,1000,55,1);Assert.That(opacity,Is.EqualTo(.58).Within(1e-9));}}
  [Test] public void HeightFogCanBecomeOpaqueAndPlanetBottomRemainsSealed()
  {Assert.That(ValleyAtmosphereMath.ComposeSurfaceOpacity(1,.78,-150,1000,55,1),Is.EqualTo(1));Assert.That(ValleyAtmosphereMath.ComposeSurfaceOpacity(0,0,-55,55,55,1),Is.EqualTo(1));Assert.That(ValleyAtmosphereMath.ComposeSurfaceOpacity(1,.78,55,55,55,0),Is.EqualTo(0));}
  [Test] public void RevisedWorldMetreFalloffChangesSmoothlyAcrossEntireColumn()
  {
   double previous=1;
   for(int h=-200;h<=500;h++)
   {
    double veil=ValleyAtmosphereMath.Opacity(125,h+70,1000,75,.012);
    double result=ValleyAtmosphereMath.ComposeSurfaceOpacity(veil,.6,h,1000,55,1);
    if(h>-200){Assert.That(result,Is.LessThanOrEqualTo(previous+1e-9));Assert.That(previous-result,Is.LessThan(.012));}
    previous=result;
   }
   Assert.That(ValleyAtmosphereMath.SkyOpacity(125,-.01,75,.012),Is.EqualTo(1));
  }
 }
}
