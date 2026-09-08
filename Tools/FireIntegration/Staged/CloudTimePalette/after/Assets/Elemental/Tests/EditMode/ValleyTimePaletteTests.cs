using NUnit.Framework;
using Unity.Mathematics;
using Elemental.Simulation.Rendering;
namespace Elemental.Tests.EditMode
{
 public sealed class ValleyTimePaletteTests
 {
  private static readonly float3 DayTop=new float3(.78f,.88f,.98f),DayBottom=new float3(.659f,.78f,.875f);
  [Test] public void NoonPreservesUserDayRampAndCloudMaterialColors()
  {var p=ValleyTimePalette.Evaluate(0,1,DayTop,DayBottom);Assert.That(math.distance(p.FogTop,DayTop),Is.LessThan(1e-6));Assert.That(math.distance(p.FogBottom,DayBottom),Is.LessThan(1e-6));Assert.That(math.distance(p.CloudTop,new float3(1)),Is.LessThan(1e-6));}
  [Test] public void SunsetIsSolarDrivenAndMidnightIsCoolGrey()
  {var dusk=ValleyTimePalette.Evaluate(.4f,0,DayTop,DayBottom);var night=ValleyTimePalette.Evaluate(1,-1,DayTop,DayBottom);Assert.That(dusk.SunsetWeight,Is.EqualTo(1).Within(1e-6));Assert.That(dusk.CloudTop.x,Is.GreaterThan(dusk.CloudTop.z));Assert.That(night.SunsetWeight,Is.EqualTo(0));Assert.That(night.CloudTop.z-night.CloudTop.x,Is.LessThan(.1f));Assert.That(math.cmin(night.FogBottom),Is.GreaterThan(.09f));}
  [Test] public void CurveIsFiniteBoundedAndContinuousAcrossHorizon()
  {var previous=ValleyTimePalette.Evaluate(1,-1,DayTop,DayBottom);for(int i=1;i<=2000;i++){float altitude=-1+i*.001f;var p=ValleyTimePalette.Evaluate(math.saturate(.5f-altitude*2),altitude,DayTop,DayBottom);Assert.That(math.all(math.isfinite(p.CloudTop)),Is.True);Assert.That(math.cmin(p.FogTop),Is.GreaterThanOrEqualTo(0));Assert.That(math.cmax(p.CloudTop),Is.LessThanOrEqualTo(1));Assert.That(math.distance(p.FogTop,previous.FogTop),Is.LessThan(.02f));previous=p;}}
 }
}
