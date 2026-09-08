using Elemental.Simulation.Structures;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthDebrisCollisionImpulseTests
    {
        [Test]
        public void StrongClosingContactKeepsIncomingMomentumForPhysicalSplit()
        {
            float impulse=EarthRockBreakPolicy.ContactImpulse(new float3(0,20,0),new float3(0,1,0),40,10);
            Assert.That(impulse,Is.EqualTo(800));
            Assert.That(EarthRockBreakPolicy.Resolve(.8f,40,impulse,false).PhysicalPieces,Is.EqualTo(4));
        }
        [Test]
        public void SeparationAndTangentialMotionDoNotInventClosingMomentum()
        {
            Assert.That(EarthRockBreakPolicy.ContactImpulse(new float3(0,-20,0),new float3(0,1,0),40,0),Is.Zero);
            Assert.That(EarthRockBreakPolicy.ContactImpulse(new float3(20,0,0),new float3(0,1,0),40,0),Is.Zero);
            Assert.That(EarthRockBreakPolicy.ContactImpulse(new float3(0,2,0),new float3(0,1,0),40,150),Is.EqualTo(150));
        }
    }
}
