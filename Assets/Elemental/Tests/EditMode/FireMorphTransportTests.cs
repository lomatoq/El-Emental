using Elemental.Simulation.Fire;
using NUnit.Framework;
using Unity.Mathematics;
namespace Elemental.Tests.EditMode
{
 public sealed class FireMorphTransportTests
 {
  private sealed class Empty:IFireFlowCollision{public bool Sweep(float3 p,float r,float3 d,out FireFlowHit hit){hit=default;return false;}}
  [Test]public void SphericalSurfaceGuideKeepsHotTonguesOnSphereThenReleasesCooling()
  {
   var flow=new FireFlowParticleSolver(false,32);flow.SetSimulationRate(45);flow.Injection=new FireFlowInjection(new float3(0,0,-5),0,64,.20f,12,.64f,coolingTail:.35f,smokeStride:4);
   flow.ConfigureSphericalGuide(true,0,new float3(0,1,0),2.3f,5,0);var world=new Empty();bool smoke=false;
   for(int frame=0;frame<150;frame++)
   {
    flow.Step(1f/60,true,1,new float3(2.3f,0,0),new float3(0,0,-1),new float3(0,1,0),world);
    for(int i=0;i<flow.Count;i++){var p=flow.Particles[i];if(p.Spark)continue;if(p.Age<p.HotLifetime)Assert.That(math.abs(math.length(p.Position)-2.3f),Is.LessThan(.3f));else smoke=true;}
   }
   Assert.That(smoke,Is.True);Assert.That(flow.RejectedBirths,Is.Zero);Assert.That(flow.BudgetStops,Is.Zero);
  }
  [Test]public void EveryChargedReleaseProfileKeepsItsBoundedTailCapacity()
  {
   foreach(float charge in new[]{0f,.25f,.5f,.75f,1f})
   {
    var profile=FireChargedBoltProfile.Evaluate(charge);var flow=new FireFlowParticleSolver(false,48);flow.Injection=new FireFlowInjection(new float3(0,0,5),1.2f,profile.DetailRateForCapacity(48),profile.TailHotSeconds,12,profile.DetailSize*.7f,coolingTail:profile.CoolingSeconds,smokeStride:4,trackEmitterPath:true);
    var world=new Empty();for(int frame=0;frame<180;frame++)flow.Step(1f/60,true,1,new float3(0,0,frame*.4f),new float3(0,0,-1),new float3(0,1,0),world);
    Assert.That(flow.RejectedBirths,Is.Zero);Assert.That(flow.BudgetStops,Is.Zero);
   }
  }
 }
}
