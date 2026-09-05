using Elemental.Simulation.Characters;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthAirborneMantleAdmissionTests
    {
        [TestCase(-1.2f)]
        [TestCase(0f)]
        [TestCase(4.5f)]
        public void ReachableAirborneApproachCanCatchLedge(float relativeUpSpeed)
        {
            Assert.That(EarthMantleMotion.CanCatchAirborne(
                .8f,false,relativeUpSpeed,.82f,.15f,1.35f,.98f,.70f,true),Is.True);
        }

        [TestCase(EarthMantleMotion.MinimumAirborneCatchRelativeUpSpeed-.01f)]
        [TestCase(EarthMantleMotion.MaximumAirborneCatchRelativeUpSpeed+.01f)]
        public void UnsafeRelativeVerticalSpeedCannotCatch(float relativeUpSpeed)
        {
            Assert.That(EarthMantleMotion.CanCatchAirborne(
                .8f,false,relativeUpSpeed,.82f,.15f,1.35f,.98f,.70f,true),Is.False);
        }

        [Test]
        public void CatchStillRequiresIntentWalkableTopAndReachableHeight()
        {
            Assert.That(EarthMantleMotion.CanCatchAirborne(
                .59f,false,0f,.82f,.15f,1.35f,.98f,.70f,true),Is.False);
            Assert.That(EarthMantleMotion.CanCatchAirborne(
                .8f,false,0f,.82f,.15f,1.35f,.98f,.70f,false),Is.False);
            Assert.That(EarthMantleMotion.CanCatchAirborne(
                .8f,false,0f,1.36f,.15f,1.35f,.98f,.70f,true),Is.False);
            Assert.That(EarthMantleMotion.CanCatchAirborne(
                .8f,true,0f,.82f,.15f,1.35f,.98f,.70f,true),Is.False);
        }

        [Test]
        public void ExistingGroundedAdmissionStillRejectsUnsupportedStarts()
        {
            Assert.That(EarthMantleMotion.CanStart(
                .8f,false,false,.82f,.35f,1.35f,.98f,.70f,true),Is.False);
        }
    }
}
