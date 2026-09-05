using Elemental.Simulation.Bending;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthGravityAcquisitionTests
    {
        [TestCase(1f, 9u, 4f, 1u, true)]
        [TestCase(4f, 1u, 1f, 9u, false)]
        [TestCase(1f, 2u, 1f, 3u, true)]
        [TestCase(1f, 3u, 1f, 2u, false)]
        public void AreaCapturePrefersNearestThenStableIdentity(float distance, uint id,
            float otherDistance, uint otherId, bool expected)
        {
            Assert.That(EarthGravityGripSolver.PreferCaptureCandidate(distance,id,otherDistance,otherId),
                Is.EqualTo(expected));
        }

        [Test]
        public void ArmorReleaseLeavesNextPlainMiddlePressOwnedByGravity()
        {
            var router=new EarthActionRouter();
            Assert.That(router.Step(new EarthActionRouterFrame(0f,modifierHeld:true,fieldPressed:true,fieldHeld:true)).Owner,
                Is.EqualTo(EarthActionOwner.Armor));
            var release=router.Step(new EarthActionRouterFrame(.5f,fieldReleased:true));
            Assert.That(release.Owner,Is.EqualTo(EarthActionOwner.Armor));
            Assert.That(router.Owner,Is.EqualTo(EarthActionOwner.None));
            Assert.That(router.Step(new EarthActionRouterFrame(.6f,fieldPressed:true,fieldHeld:true)).Owner,
                Is.EqualTo(EarthActionOwner.Gravity));
        }

        [TestCase(0,false,false)]
        [TestCase(0,true,true)]
        [TestCase(1,false,true)]
        [TestCase(1,true,true)]
        public void GripRequiresCapturedMatterOrAControllableStructure(int captured, bool structure, bool expected)
        {
            Assert.That(EarthGravityGripSolver.CanBeginSession(captured,structure),Is.EqualTo(expected));
        }
    }
}
