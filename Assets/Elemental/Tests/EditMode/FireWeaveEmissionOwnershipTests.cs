using Elemental.Simulation.Fire;
using NUnit.Framework;
namespace Elemental.Tests.EditMode
{
 public sealed class FireWeaveEmissionOwnershipTests
 {
  [Test]public void SemanticOrbitStopsHandInjectionBeforePresentationDampingFinishes()
  {
   Assert.That(FireWeaveEmissionOwnership.SourceOrbitBlend(FireWeaveForm.Flood,.48f),Is.EqualTo(.48f));
   Assert.That(FireWeaveEmissionOwnership.SourceOrbitBlend(FireWeaveForm.Orbit,.48f),Is.EqualTo(1));
   Assert.That(FireWeaveEmissionOwnership.SourceOrbitBlend(FireWeaveForm.Sphere,0),Is.EqualTo(1));
  }
  [Test]public void InvalidVisualCurlCannotCreateNonFiniteSourceGeometry()
  {
   Assert.That(FireWeaveEmissionOwnership.SourceOrbitBlend(FireWeaveForm.Flood,float.NaN),Is.Zero);
   Assert.That(FireWeaveEmissionOwnership.SourceOrbitBlend(FireWeaveForm.Flood,2),Is.EqualTo(1));
   Assert.That(FireWeaveEmissionOwnership.SourceOrbitBlend(FireWeaveForm.Flood,-2),Is.Zero);
  }
 }
}
