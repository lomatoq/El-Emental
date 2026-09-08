using NUnit.Framework;
using UnityEngine;
using Elemental.Presentation.Rendering;
namespace Elemental.Tests.EditMode
{
 public sealed class ProceduralCloudBanksTests
 {
  [Test] public void BanksRemainOutsidePlayableCapAndCoverBothViewingCorridors()
  {
   int front=0,rear=0;
   Assert.That(ProceduralCloudBanks.BankCount,Is.EqualTo(10));
   for(int i=0;i<ProceduralCloudBanks.BankCount;i++)
   {
    Vector3 centre=ProceduralCloudBanks.Centre(i);
    // Largest bank half extent is below 560m; centres remain >1km away.
    Assert.That(centre.magnitude,Is.GreaterThan(1100));
    Assert.That(centre.y,Is.InRange(250,680));
    if(centre.z>0)front++;else rear++;
    for(int j=0;j<i;j++)Assert.That(Vector3.Distance(centre,ProceduralCloudBanks.Centre(j)),Is.GreaterThan(i<6?600:250));
   }
   Assert.That(front,Is.EqualTo(5));Assert.That(rear,Is.EqualTo(5));
  }
  [Test] public void UnsupportedBankIndicesFailExplicitly()
  {
   Assert.Throws<System.ArgumentOutOfRangeException>(()=>ProceduralCloudBanks.Centre(-1));
   Assert.Throws<System.ArgumentOutOfRangeException>(()=>ProceduralCloudBanks.Centre(ProceduralCloudBanks.BankCount));
  }
 }
}
