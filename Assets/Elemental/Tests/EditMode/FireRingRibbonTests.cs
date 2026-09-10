using Elemental.Simulation.Fire;
using NUnit.Framework;
using Unity.Mathematics;
namespace Elemental.Tests.EditMode
{
 public sealed class FireRingRibbonTests
 {
  private sealed class Wall:IFireFlowCollision
  {
   public bool blocked;public float radius;
   public bool Sweep(float3 p,float r,float3 d,out FireFlowHit hit){radius=r;hit=default;if(blocked){hit.Blocked=true;return true;}if(p.x+d.x+r<2)return false;hit.Fraction=(2-r-p.x)/d.x;return true;}
  }
  [Test]public void RibbonUsesFullVisibleRadiusAndStopsAtFirstSolid()
  {
   var world=new Wall();Assert.That(FireRingRibbonMath.TrySpan(0,new float3(3,0,0),world,out var end),Is.True);
   Assert.That(world.radius,Is.EqualTo(FireRingRibbonMath.Radius));Assert.That(end.x+world.radius,Is.LessThan(2));
   world.blocked=true;Assert.That(FireRingRibbonMath.TrySpan(0,new float3(1,0,0),world,out _),Is.False);
   Assert.That(FireRingRibbonMath.TrySpan(0,new float3(8,0,0),world,out _),Is.False);
  }
  [Test]public void FineSphereAndLongBoltRemainWithinExistingGasSeats()
  {
   var presets=new[]{new FireFlowInjection(new float3(0,0,3),1.2f,104,.30f,8,.68f,coolingTail:.42f,smokeStride:4,trackEmitterPath:true),new FireFlowInjection(new float3(3,0,0),0,64,.20f,8,.64f,coolingTail:.35f,smokeStride:4)};
   for(int mode=0;mode<2;mode++)
   {
    var flow=new FireFlowParticleSolver(false,mode==0?48:32);flow.Injection=presets[mode];var world=new Empty();bool smoke=false;
    for(int frame=0;frame<180;frame++){flow.Step(1f/60,true,1,0,new float3(0,0,-1),new float3(0,1,0),world);for(int i=0;i<flow.Count;i++)smoke|=flow.Particles[i].Age>flow.Particles[i].HotLifetime;}
    Assert.That(flow.RejectedBirths,Is.Zero);Assert.That(flow.BudgetStops,Is.Zero);Assert.That(smoke,Is.True);
   }
  }
  private sealed class Empty:IFireFlowCollision {public bool Sweep(float3 p,float r,float3 d,out FireFlowHit hit){hit=default;return false;}}
 }
}
