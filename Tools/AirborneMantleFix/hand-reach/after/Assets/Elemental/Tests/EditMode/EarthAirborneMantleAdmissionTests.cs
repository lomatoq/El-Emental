using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;

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

        [Test]
        public void AirborneProbeReachesBeforeCapsuleContactWithoutChangingGroundedRange()
        {
            float grounded=EarthMantleMotion.ResolveWallProbeDistance(.6f,.6f,false);
            float airborne=EarthMantleMotion.ResolveWallProbeDistance(.6f,.6f,true);

            Assert.That(grounded,Is.EqualTo(1.2f).Within(.0001f));
            Assert.That(airborne-grounded,
                Is.EqualTo(EarthMantleMotion.AirborneCatchReachBonus).Within(.0001f));
            Assert.That(airborne,Is.GreaterThan(1.35f),
                "The moving-platform proof lip must be detected before wall contact.");
        }

        [Test]
        public void AirborneReachApproachesWallButKeepsCapsuleClear()
        {
            const float wallDistance=1.275f;
            const float capsuleRadius=.35f;
            float travel=EarthMantleMotion.ResolveAirborneApproachTravel(
                wallDistance,capsuleRadius);

            Assert.That(travel,Is.GreaterThan(.8f));
            Assert.That(wallDistance-travel,
                Is.EqualTo(capsuleRadius+EarthMantleMotion.AirborneApproachWallClearance)
                    .Within(.0001f));
        }

        [Test]
        public void AirborneReachMovesIntoHandRangeBeforeVerticalLiftWithoutFrameRateDependence()
        {
            float3 start=float3.zero;
            float3 approach=new(0f,0f,.845f);
            float3 end=new(0f,.75f,1.25f);
            float3 up=new(0f,1f,0f);

            float3 halfway=EarthMantleMotion.Evaluate(
                start,approach,end,up,EarthMantleMotion.ApproachEnd*.5f);
            float3 reached=EarthMantleMotion.Evaluate(
                start,approach,end,up,EarthMantleMotion.ApproachEnd);
            float3 raising=EarthMantleMotion.Evaluate(start,approach,end,up,.30f);

            Assert.That(halfway.z,Is.InRange(.35f,.50f));
            Assert.That(halfway.y,Is.EqualTo(0f).Within(.0001f));
            Assert.That(math.distance(reached,approach),Is.LessThan(.0001f));
            Assert.That(raising.y,Is.GreaterThan(0f));
            Assert.That(raising.z,Is.GreaterThanOrEqualTo(approach.z));

            float3 atThirtyA=EarthMantleMotion.Evaluate(start,approach,end,up,.30f);
            float3 atThirtyB=EarthMantleMotion.Evaluate(start,approach,end,up,30f/100f);
            Assert.That(math.distance(atThirtyA,atThirtyB),Is.LessThan(.000001f),
                "The path must be a function of elapsed progress, not rendered frame count.");
        }
    }
}
