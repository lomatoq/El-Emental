using NUnit.Framework;
using Elemental.Simulation.Characters;
namespace Elemental.Tests.EditMode {
 public sealed class FireLiftMotionTests {
  [TestCase(30),TestCase(60),TestCase(120)] public void ChargeRisesSmoothlyAndReleaseFalls(int fps){
   float dt=1f/fps,v=0,y=0,early=0;
   for(int i=0;i<fps*3;i++){float charge=Unity.Mathematics.math.saturate(i*dt/2.5f);float before=v;
    v+=FireLiftMotion.VelocityChange(v,-9.81f,charge,dt,float.PositiveInfinity)-9.81f*dt;y+=v*dt;
    Assert.That(v,Is.InRange(0,8.001f));Assert.That(System.Math.Abs(v-before),Is.LessThanOrEqualTo(18f*dt+.0001f));if(i==fps/2)early=v;}
   Assert.That(v,Is.GreaterThan(early+3));Assert.That(y,Is.GreaterThan(8));
   for(int i=0;i<fps*2;i++)v-=9.81f*dt;Assert.That(v,Is.LessThan(0));
  }
  [Test] public void CeilingConstraintCancelsPredictedUpwardTravelWithoutReposition(){float dt=.02f,v=8;float after=v+FireLiftMotion.VelocityChange(v,-9.81f,1,dt,.01f)-9.81f*dt;Assert.That(after*dt,Is.LessThanOrEqualTo(.010001f));}
  [TestCase(-4f),TestCase(0f),TestCase(8f)] public void LiftPresentationUsesFallNotApexWithoutChangingOrdinaryMotion(float velocity){
   var state=new EarthAnimationRescueState();var tuning=EarthAnimationRescueTuning.Default;var candidate=default(EarthLandingCandidateSnapshot);
   var sample=EarthAnimationStateResolver.Step(ref state,in tuning,in candidate,false,false,false,FireLiftMotion.AnimationVerticalSpeed(velocity,true),0,1f/60);
   Assert.That(sample.Phase,Is.EqualTo(EarthAnimationPhase.Falling));Assert.That(FireLiftMotion.AnimationVerticalSpeed(velocity,false),Is.EqualTo(velocity));
  }
  [Test] public void InvalidOrPausedClockProducesNoMotorImpulse(){Assert.That(FireLiftMotion.VelocityChange(0,-9.81f,1,0,1),Is.Zero);Assert.That(FireLiftMotion.VelocityChange(float.NaN,-9.81f,1,.02f,1),Is.Zero);}
 }
}
