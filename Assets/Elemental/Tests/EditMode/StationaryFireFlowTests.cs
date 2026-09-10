using Elemental.Simulation.Fire;
using NUnit.Framework;
using Unity.Mathematics;
namespace Elemental.Tests.EditMode {
 public sealed class StationaryFireFlowTests {
  sealed class Empty:IFireFlowCollision {public bool Sweep(float3 p,float r,float3 d,out FireFlowHit h){h=default;return false;}}
  [TestCase(15),TestCase(60),TestCase(120)] public void TowerGasHasFiniteLocalCoolingAndBoundedWork(int fps) {
   var s=new FireFlowParticleSolver(true);var w=new Empty();bool smoke=false;float height=0;
   for(int f=0;f<fps*10;f++){s.Step(1f/fps,true,1,0,new float3(0,1,0),new float3(0,1,0),w);
    Assert.That(s.Count,Is.LessThanOrEqualTo(32));Assert.That(s.QueryCount,Is.LessThanOrEqualTo(224));
    for(int i=0;i<s.Count;i++){var p=s.Particles[i];Assert.That(math.all(math.isfinite(p.Position)),Is.True);Assert.That(p.Distance,Is.LessThanOrEqualTo(3.001f));height=math.max(height,p.Position.y);smoke|=p.Temperature<.35f&&p.Soot>.25f;}}
   Assert.That(height,Is.InRange(.7f,3f));Assert.That(smoke,Is.True);Assert.That(s.BudgetStops,Is.Zero);
   int count=s.Count;float3 first=s.Particles[0].Position;s.Step(0,true,1,0,new float3(0,1,0),new float3(0,1,0),w);
   Assert.That(s.Count,Is.EqualTo(count));Assert.That(math.distance(first,s.Particles[0].Position),Is.Zero);
   for(int i=0;i<180;i++)s.Step(1f/90,false,0,0,new float3(0,1,0),new float3(0,1,0),w);
   Assert.That(s.Count,Is.Zero);
  }
  [Test] public void HandPresetRetainsOriginalAllocationAndEightMetreTravelBudget(){var s=new FireFlowParticleSolver();Assert.That(s.ParticleLimit,Is.EqualTo(192));Assert.That(s.PathLimit,Is.EqualTo(8));Assert.That(s.QueryLimit,Is.EqualTo(768));}
 }
}
