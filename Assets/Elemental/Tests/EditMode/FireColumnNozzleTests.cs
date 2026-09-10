using Elemental.Simulation.Fire;
using NUnit.Framework;
using Unity.Mathematics;
namespace Elemental.Tests.EditMode
{
 public sealed class FireColumnNozzleTests
 {
  private sealed class RecordingOpenWorld:IFireFlowCollision
  {
   public float Minimum=float.PositiveInfinity,Maximum;public int Queries;
   public bool Sweep(float3 p,float radius,float3 d,out FireFlowHit hit){Minimum=math.min(Minimum,radius);Maximum=math.max(Maximum,radius);Queries++;hit=default;return false;}
  }
  [Test]public void ColumnBirthAreaHasWidthInBothTangentAxesAndUsesRealSweptRadius()
  {
   var solver=new FireFlowParticleSolver(false,64);var world=new RecordingOpenWorld();
   solver.Injection=new FireFlowInjection(0,4.2f,104,.44f,3,.85f,developmentScale:1.2f,staggerBirths:true,coolingTail:.4f,smokeStride:4,birthDiscRadius:.20f,nozzleRadius:.18f);
   float minX=100,maxX=-100,minZ=100,maxZ=-100;int young=0;
   for(int frame=0;frame<100;frame++)
   {
    solver.Step(1f/60,true,1,new float3(0,.25f,0),new float3(0,1,0),new float3(0,1,0),world);
    for(int i=0;i<solver.Count;i++){var p=solver.Particles[i];if(p.Spark||p.Age>.06f)continue;young++;minX=math.min(minX,p.Position.x);maxX=math.max(maxX,p.Position.x);minZ=math.min(minZ,p.Position.z);maxZ=math.max(maxZ,p.Position.z);Assert.That(p.NozzleRadius,Is.EqualTo(.18f));}
   }
   Assert.That(young,Is.GreaterThan(50));Assert.That(maxX-minX,Is.GreaterThan(.27f));Assert.That(maxZ-minZ,Is.GreaterThan(.27f));
   Assert.That(world.Queries,Is.GreaterThan(100));Assert.That(world.Maximum,Is.GreaterThan(.18f));Assert.That(solver.RejectedBirths,Is.Zero);Assert.That(solver.BudgetStops,Is.Zero);
  }
  [Test]public void OtherSkillsKeepTheirOriginalNozzleAndNoAreaInjection()
  {
   var injection=new FireFlowInjection(0,10,100,.3f,3,1);
   Assert.That(injection.NozzleRadius,Is.EqualTo(.09f));Assert.That(injection.BirthDiscRadius,Is.Zero);
  }
 }
}
