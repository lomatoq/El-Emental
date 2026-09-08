using Elemental.Simulation.Characters;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthShortTransitionPolicyTests
    {
        [Test]
        public void StartWalkRequiresSettledIdleAndForwardTravelThenExpiresWithoutReplay()
        {
            var state = new EarthShortTransitionState();
            var input = new EarthShortTransitionInput { Grounded = true };
            EarthShortTransitionPolicy.Step(ref state, in input, .25f);
            input.ForwardSpeed = input.TangentSpeed = .8f;
            Assert.That(EarthShortTransitionPolicy.Step(ref state, in input, .016f).Kind,
                Is.EqualTo(EarthShortTransition.StartWalk));
            Assert.That(EarthShortTransitionPolicy.Step(ref state, in input, .33f).Kind,
                Is.EqualTo(EarthShortTransition.None));
            Assert.That(EarthShortTransitionPolicy.Step(ref state, in input, .016f).Kind,
                Is.EqualTo(EarthShortTransition.None));
        }

        [TestCase(-.8f)] [TestCase(0f)] [TestCase(3.1f)]
        public void BackpedalStrafeAndFastTravelDoNotPlayForwardStart(float forward)
        {
            var state = new EarthShortTransitionState { Initialized = true, IdleSeconds = 1f };
            var input = new EarthShortTransitionInput { Grounded = true, ForwardSpeed = forward,
                TangentSpeed = forward > 2f ? forward : .8f };
            Assert.That(EarthShortTransitionPolicy.Step(ref state, in input, .016f).Kind,
                Is.EqualTo(EarthShortTransition.None));
        }

        [TestCase(false)] [TestCase(true)]
        public void CrouchExitOnlyOwnsGroundedCancellationNeverTheLaunch(bool grounded)
        {
            var state = new EarthShortTransitionState { Initialized = true, WasCrouched = true };
            var input = new EarthShortTransitionInput { Grounded = grounded, VerticalSpeed = grounded ? 0f : 4f };
            Assert.That(EarthShortTransitionPolicy.Step(ref state, in input, .016f).Kind,
                Is.EqualTo(grounded ? EarthShortTransition.CrouchExit : EarthShortTransition.None));
        }

        [Test]
        public void BackwardStepOffUsesShortDropAndImmediatelyReturnsAtPhysicalSupport()
        {
            var state = new EarthShortTransitionState { Initialized = true, WasGrounded = true, HasSeenSupport = true };
            var input = new EarthShortTransitionInput { TangentSpeed = 1f, ForwardSpeed = -1f,
                VerticalSpeed = -.5f, HasLandingCandidate = true, FloorDistance = .45f };
            Assert.That(EarthShortTransitionPolicy.Step(ref state, in input, .016f).Kind,
                Is.EqualTo(EarthShortTransition.StepDown));
            input.Grounded = true;
            Assert.That(EarthShortTransitionPolicy.Step(ref state, in input, .016f).Kind,
                Is.EqualTo(EarthShortTransition.None));
        }

        [TestCase(0f, false)] [TestCase(2f, true)]
        public void UnknownOrTallDropRetainsExistingAirborneOwner(float distance, bool candidate)
        {
            var state = new EarthShortTransitionState { Initialized = true, WasGrounded = true, HasSeenSupport = true };
            var input = new EarthShortTransitionInput { TangentSpeed = 1f, VerticalSpeed = -1f,
                HasLandingCandidate = candidate, FloorDistance = distance };
            Assert.That(EarthShortTransitionPolicy.Step(ref state, in input, .016f).Kind,
                Is.EqualTo(EarthShortTransition.None));
        }

        [TestCase(EarthShortTransition.StartWalk)]
        [TestCase(EarthShortTransition.CrouchExit)]
        [TestCase(EarthShortTransition.StepDown)]
        public void GameplayPriorityCancelsEveryBridgeImmediately(EarthShortTransition kind)
        {
            var state = new EarthShortTransitionState { Initialized = true, Active = kind };
            var input = new EarthShortTransitionInput { ProtectedOwner = true };
            Assert.That(EarthShortTransitionPolicy.Step(ref state, in input, .016f).Kind,
                Is.EqualTo(EarthShortTransition.None));
        }
    }
}
