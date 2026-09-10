using Elemental.Simulation.Fire;
using NUnit.Framework;
namespace Elemental.Tests.EditMode
{
 public sealed class FireStoneExposureTests
 {
  [Test] public void SmallMediumLargeRequireProgressivelyMoreExposure()
  {
   Assert.That(FireStoneExposure.RequiredSeconds(.2f,.35f,1.2f),Is.EqualTo(.08f));
   Assert.That(FireStoneExposure.RequiredSeconds(.8f,.35f,1.2f),Is.EqualTo(.2f));
   Assert.That(FireStoneExposure.RequiredSeconds(2f,.35f,1.2f),Is.EqualTo(.4f));
  }
  [Test] public void DuplicateColliderCannotMultiplyExposureAndGenerationsDoNotInheritHeat()
  {
   var state=new FireStoneExposure();
   for(int i=0;i<100;i++)Assert.That(state.Step(1,1,0,.02f,1,.2f,.35f,1.2f),Is.False);
   for(int i=1;i<3;i++)Assert.That(state.Step(1,1,i*.02f,.02f,1,.2f,.35f,1.2f),Is.False);
   Assert.That(state.Step(1,2,.3f,.02f,1,.2f,.35f,1.2f),Is.False);
   bool fired=false;for(int i=3;i<8;i++)fired|=state.Step(1,1,i*.02f,.02f,1,.2f,.35f,1.2f);
   Assert.That(fired,Is.True);
  }
  [Test] public void RemovalExpiresDoseAndInvalidInputsCannotDamage()
  {
   var state=new FireStoneExposure();
   for(int i=0;i<10;i++)state.Step(1,1,i*.02f,.02f,1,.2f,.35f,1.2f);
   Assert.That(state.Step(1,1,2,.02f,1,.2f,.35f,1.2f),Is.False);
   Assert.That(state.Step(1,1,3,100,float.NaN,.2f,.35f,1.2f),Is.False);
   Assert.That(FireStoneExposure.PushImpulse(600,.02f,1)/600,Is.EqualTo(.72f).Within(.0001f));
   Assert.That(FireStoneExposure.PushImpulse(10000,.02f,1),Is.LessThanOrEqualTo(480.001f));
  }
 }
}
