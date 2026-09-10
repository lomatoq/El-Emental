using Elemental.Simulation.Fire;
using NUnit.Framework;
namespace Elemental.Tests.EditMode
{
 public sealed class FireLightEnvelopeTests
 {
  [Test] public void FlickerIsBoundedContinuousAndFadesWithHeat()
  {
   float min=2,max=0,previous=FireLightEnvelope.Sample(0,2);
   for(int i=1;i<1200;i++){float value=FireLightEnvelope.Sample(i/120f,2);Assert.That(value,Is.InRange(.8f,1.2f));Assert.That(System.Math.Abs(value-previous),Is.LessThan(.02f));min=System.Math.Min(min,value);max=System.Math.Max(max,value);previous=value;}
   Assert.That(max-min,Is.GreaterThan(.2f));Assert.That(FireLightEnvelope.Sample(2,2,0),Is.Zero);
   Assert.That(FireLightEnvelope.Sample(float.NaN,0),Is.Zero);
  }
 }
}
