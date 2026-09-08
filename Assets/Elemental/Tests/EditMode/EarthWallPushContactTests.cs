using Elemental.Simulation.Bending;
using NUnit.Framework;
namespace Elemental.Tests.EditMode
{
    public sealed class EarthWallPushContactTests
    {
        [Test] public void HeavyChipResponseIsLocalAndRejectsFloorOrLightStone()
        {
            Assert.That(EarthWallPushContactPolicy.ShouldChipHeavyObstacle(1000,0,true,10,0,-1),Is.True);
            Assert.That(EarthWallPushContactPolicy.ShouldChipHeavyObstacle(1000,0,true,10,1,0),Is.False);
            Assert.That(EarthWallPushContactPolicy.ShouldChipHeavyObstacle(1000,100,false,10,0,-1),Is.False);
            Assert.That(EarthWallPushContactPolicy.ChipDisplacement(10),Is.EqualTo(.025f).Within(.00001f));
        }
        [TestCase(1000f,50f,10f,2f,500f,true)]
        [TestCase(1000f,500f,10f,0f,500f,false)]
        [TestCase(1000f,50f,10f,-20f,500f,false)]
        [TestCase(1000f,50f,10f,20f,2000f,false)]
        [TestCase(1000f,50f,0f,10f,500f,false)]
        public void OnlyWallDrivenFiniteLightStoneContactsSkipSelfDamage(float wall,float stone,float speed,float incoming,float impulse,bool expected)
        {Assert.That(EarthWallPushContactPolicy.IsOutgoingLooseStone(wall,stone,speed,incoming,impulse),Is.EqualTo(expected));}
    }
}