using Elemental.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
namespace Elemental.Tests.EditMode
{
 public sealed class PortraitFloorOcclusionTests
 {
  [Test]public void FlatSupportIsKeptWhileChestHeightWallRemainsAnOccluder()
  {
   var center=new Vector3(0,1.1f,0);
   Assert.That(CinematicMenuCamera.IsBelowPortraitFeet(new Bounds(new Vector3(0,-.15f,1),new Vector3(7,.3f,7)),center,Vector3.up,2),Is.True);
   Assert.That(CinematicMenuCamera.IsBelowPortraitFeet(new Bounds(new Vector3(0,1,1),new Vector3(1,2,.3f)),center,Vector3.up,2),Is.False);
  }
  [Test]public void FootProtectionUsesPlanetUpRatherThanWorldY()
  {
   var center=new Vector3(1.1f,0,0);
   Assert.That(CinematicMenuCamera.IsBelowPortraitFeet(new Bounds(new Vector3(-.15f,0,1),new Vector3(.3f,7,7)),center,Vector3.right,2),Is.True);
   Assert.That(CinematicMenuCamera.IsBelowPortraitFeet(new Bounds(new Vector3(1,0,1),new Vector3(2,1,.3f)),center,Vector3.right,2),Is.False);
  }
 }
}
