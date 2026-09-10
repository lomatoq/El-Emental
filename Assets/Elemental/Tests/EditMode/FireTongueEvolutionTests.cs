using Elemental.Simulation.Fire;
using NUnit.Framework;
using Unity.Mathematics;
namespace Elemental.Tests.EditMode
{
 public sealed class FireTongueEvolutionTests
 {
  [Test]public void IdentityVariesShapeTemperatureAndSmokeWithoutFrameRandomness()
  {
   float minAspect=100,maxAspect=0,minHeat=1,maxHeat=0,minSmoke=1,maxSmoke=0;
   for(uint id=1;id<49;id++)
   {
    var a=FireTongueEvolution.Evaluate(id,id*.731f,.12f,.3f,.7f);var b=FireTongueEvolution.Evaluate(id,id*.731f,.12f,.3f,.7f);
    Assert.That(a.Roll,Is.EqualTo(b.Roll));Assert.That(a.Bend,Is.EqualTo(b.Bend));
    minAspect=math.min(minAspect,a.AspectScale);maxAspect=math.max(maxAspect,a.AspectScale);
    minHeat=math.min(minHeat,a.TemperatureScale);maxHeat=math.max(maxHeat,a.TemperatureScale);
    minSmoke=math.min(minSmoke,a.SmokeScale);maxSmoke=math.max(maxSmoke,a.SmokeScale);
   }
   Assert.That(maxAspect-minAspect,Is.GreaterThan(.25f));Assert.That(maxHeat-minHeat,Is.GreaterThan(.1f));Assert.That(maxSmoke-minSmoke,Is.GreaterThan(.2f));
  }
  [Test]public void LivingTongueChangesAspectBendAndRadiusThenContinuesThroughCooling()
  {
   var born=FireTongueEvolution.Evaluate(13,1.7f,.01f,.35f,1);var flame=FireTongueEvolution.Evaluate(13,1.7f,.25f,.35f,.65f);
   var smoke=FireTongueEvolution.Evaluate(13,1.7f,.6f,.35f,.2f);
   Assert.That(math.abs(born.AspectScale-flame.AspectScale),Is.GreaterThan(.12f));
   Assert.That(math.distance(born.Bend,flame.Bend),Is.GreaterThan(.07f));
   Assert.That(math.abs(flame.Roll-smoke.Roll),Is.GreaterThan(.05f));
   Assert.That(smoke.RadiusScale,Is.LessThan(born.RadiusScale));
  }
  [Test]public void FrameToFrameShapeMotionIsBoundedAtBirthAndCoolingBoundary()
  {
   for(uint id=1;id<25;id++)for(int step=0;step<70;step++)
   {
    float age=step/120f;var a=FireTongueEvolution.Evaluate(id,id*.7f,age,.23f,1);var b=FireTongueEvolution.Evaluate(id,id*.7f,age+1f/120,.23f,.9f);
    Assert.That(math.abs(a.Roll-b.Roll),Is.LessThan(.02f));Assert.That(math.abs(a.AspectScale-b.AspectScale),Is.LessThan(.035f));
    Assert.That(math.abs(a.RadiusScale-b.RadiusScale),Is.LessThan(.035f));
   }
  }
  [Test]public void SphereTongueSupportCannotIntrudeIntoAvatarInterior()
  {
   Assert.That(FireTongueEvolution.OutsideActor(new float3(0,1.3f,0),.2f,1.5f,0,2.3f),Is.False);
   Assert.That(FireTongueEvolution.OutsideActor(new float3(0,2.3f,0),.2f,1.5f,0,2.3f),Is.True);
   Assert.That(FireTongueEvolution.OutsideActor(new float3(0,1.8f,0),.3f,2,0,2.3f),Is.False);
  }
 }
}
