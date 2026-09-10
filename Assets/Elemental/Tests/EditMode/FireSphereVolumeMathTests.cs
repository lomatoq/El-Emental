using Elemental.Simulation.Fire;
using NUnit.Framework;
using Unity.Mathematics;
namespace Elemental.Tests.EditMode
{
 public sealed class FireSphereVolumeMathTests
 {
  [Test]public void OutsideRayAndOpaqueWallClipExactWorldInterval()
  {
   Assert.That(FireSphereVolumeMath.RayInterval(new float3(0,0,-5),new float3(0,0,2),0,2.3f,float.PositiveInfinity,out var full),Is.True);
   Assert.That(full.x,Is.EqualTo(5-2.3f*1.08f).Within(.00001f));Assert.That(full.y,Is.EqualTo(5+2.3f*1.08f).Within(.00001f));
   Assert.That(FireSphereVolumeMath.RayInterval(new float3(0,0,-5),new float3(0,0,1),0,2.3f,2,out _),Is.False);
   Assert.That(FireSphereVolumeMath.RayInterval(new float3(0,0,-5),new float3(0,0,1),0,2.3f,4,out var clipped),Is.True);Assert.That(clipped.y,Is.EqualTo(4));
  }
  [Test]public void InsideCameraStartsAtZeroAndMissesStayEmpty()
  {
   Assert.That(FireSphereVolumeMath.RayInterval(0,new float3(1,0,0),0,2.3f,float.PositiveInfinity,out var inside),Is.True);Assert.That(inside.x,Is.Zero);Assert.That(inside.y,Is.EqualTo(2.3f*1.08f).Within(.00001f));
   Assert.That(FireSphereVolumeMath.RayInterval(new float3(0,0,5),new float3(0,0,1),0,2.3f,float.PositiveInfinity,out _),Is.False);
   Assert.That(FireSphereVolumeMath.RayInterval(new float3(8,0,-5),new float3(0,0,1),0,2.3f,float.PositiveInfinity,out _),Is.False);
   Assert.That(FireSphereVolumeMath.RayInterval(0,0,0,2.3f,10,out _),Is.False);
  }
 }
}
