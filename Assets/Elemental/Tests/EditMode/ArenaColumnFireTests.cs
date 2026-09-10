using NUnit.Framework;
using Elemental.Presentation.Fire;
namespace Elemental.Tests.EditMode
{
 public sealed class ArenaColumnFireTests
 {
  [Test] public void NightLightingIsBoundedAndTracksActualSolarNightContinuously()
  {
   float previous=0;
   for(int i=0;i<=100;i++){float value=ArenaColumnFires.NightIntensity(i/100f,1);Assert.That(value,Is.GreaterThanOrEqualTo(previous));Assert.That(value,Is.InRange(.299f,14.001f));previous=value;}
   Assert.That(ArenaColumnFires.NightIntensity(1,100),Is.LessThanOrEqualTo(15.401f));
   Assert.That(ArenaColumnFires.NightIntensity(0,-100),Is.GreaterThan(0));
  }
  [Test] public void DecorativeBudgetRemainsSevenFiresAndFourLights()
  {Assert.That(ArenaColumnFires.MaximumFires,Is.EqualTo(7));Assert.That(ArenaColumnFires.MaximumLights,Is.EqualTo(7));}
 }
}
