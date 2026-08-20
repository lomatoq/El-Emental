using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthCharacterFeelTests
    {
        [Test]
        public void CastTimingTraversesRootLoadStrikeSustainAndRecover()
        {
            var timing = new EarthCastTiming(12, 6, 10, 0.4f);
            Assert.That(EarthCastPhaseSolver.Evaluate(0, in timing, false), Is.EqualTo(EarthCastPhase.Acquire));
            Assert.That(EarthCastPhaseSolver.Evaluate(4, in timing, false), Is.EqualTo(EarthCastPhase.Root));
            Assert.That(EarthCastPhaseSolver.Evaluate(9, in timing, false), Is.EqualTo(EarthCastPhase.Load));
            Assert.That(EarthCastPhaseSolver.Evaluate(timing.ContactTick, in timing, false), Is.EqualTo(EarthCastPhase.Strike));
            Assert.That(EarthCastPhaseSolver.Evaluate(17, in timing, true), Is.EqualTo(EarthCastPhase.Sustain));
            Assert.That(EarthCastPhaseSolver.Evaluate(20, in timing, false), Is.EqualTo(EarthCastPhase.Recover));
            Assert.That(EarthCastPhaseSolver.Evaluate(29, in timing, false), Is.EqualTo(EarthCastPhase.Idle));
        }

        [Test]
        public void HeavyMassProducesMoreEffortBraceAndCompression()
        {
            EarthPoseIntent light = EarthPoseSolver.Solve(
                EarthTechniqueKind.Grip, EarthCastPhase.Sustain,
                new float3(0.2f, 0f, 1f), new float3(1f, 0f, 3f),
                8f, 2f, 0.3f, true);
            EarthPoseIntent heavy = EarthPoseSolver.Solve(
                EarthTechniqueKind.Grip, EarthCastPhase.Sustain,
                new float3(0.2f, 0f, 1f), new float3(1f, 0f, 3f),
                900f, 20f, 1f, true);

            Assert.That(heavy.Effort01, Is.GreaterThan(light.Effort01 + 0.25f));
            Assert.That(heavy.Brace01, Is.GreaterThan(light.Brace01));
            Assert.That(heavy.PelvisCompression01, Is.GreaterThan(light.PelvisCompression01));
            Assert.That(heavy.LocksFeet, Is.True);
        }

        [Test]
        public void AirbornePoseCannotClaimAPlantedBrace()
        {
            EarthPoseIntent airborne = EarthPoseSolver.Solve(
                EarthTechniqueKind.GroundWave, EarthCastPhase.Strike,
                new float3(0f, 0f, 1f), float3.zero,
                500f, 20f, 1f, false);
            Assert.That(airborne.Brace01, Is.Zero);
            Assert.That(airborne.Effort01, Is.LessThan(0.6f));
        }

        [Test]
        public void LockedFootIgnoresAnimationSlideUntilLockReleases()
        {
            EarthFootPlantResult first = EarthFootPlantSolver.Solve(
                new float3(0f, 1f, 0f), true, float3.zero, new float3(0f, 1f, 0f),
                new float3(0f, 1f, 0f), true, true, false, float3.zero, 0.04f);
            EarthFootPlantResult moved = EarthFootPlantSolver.Solve(
                new float3(0.5f, 1f, 0.4f), true, new float3(0.5f, 0f, 0.4f), new float3(0f, 1f, 0f),
                new float3(0f, 1f, 0f), true, true, first.Locked, first.Position, 0.04f);
            EarthFootPlantResult released = EarthFootPlantSolver.Solve(
                new float3(0.5f, 1f, 0.4f), true, new float3(0.5f, 0f, 0.4f), new float3(0f, 1f, 0f),
                new float3(0f, 1f, 0f), true, false, moved.Locked, moved.Position, 0.04f);

            Assert.That(moved.Position, Is.EqualTo(first.Position));
            Assert.That(released.Locked, Is.False);
            Assert.That(released.Weight01, Is.Zero,
                "Locomotion clips must own the feet whenever no cast brace requests a lock.");
            Assert.That(released.Position, Is.EqualTo(new float3(0.5f, 1f, 0.4f)));
        }

        [Test]
        public void GroundAdhesionAlwaysPullsInwardAndCannotCreateHoverThrust()
        {
            float atContact = PlanetGroundAdhesionSolver.SolveInwardAcceleration(
                0f, 0.35f, 0f, 90f, 12f);
            float separated = PlanetGroundAdhesionSolver.SolveInwardAcceleration(
                0.28f, 0.35f, 0f, 90f, 12f);
            float departing = PlanetGroundAdhesionSolver.SolveInwardAcceleration(
                0.28f, 0.35f, 4f, 90f, 12f);

            Assert.That(atContact, Is.Zero);
            Assert.That(separated, Is.GreaterThan(0f));
            Assert.That(departing, Is.GreaterThanOrEqualTo(separated));
            Assert.That(departing, Is.LessThanOrEqualTo(90f));
        }

        [Test]
        public void JumpBufferAndCoyoteWindowCanMeetOnLaterTick()
        {
            PlanetJumpWindowState state = default;
            state = state.Step(false, true, 6, 7);
            Assert.That(state.CanConsume, Is.False);
            state = state.Step(true, false, 6, 7);
            Assert.That(state.CanConsume, Is.True);
            Assert.That(state.Consume().CanConsume, Is.False);

            state = new PlanetJumpWindowState(3, 0);
            state = state.Step(false, true, 6, 7);
            Assert.That(state.CanConsume, Is.True);
        }

        [Test]
        public void PelvisCompensationIsBoundedAndUsesLowestSupport()
        {
            float offset = EarthPelvisCompensation.Solve(-0.08f, 0.03f, 0.5f, 0.22f);
            Assert.That(offset, Is.LessThan(-0.08f));
            Assert.That(offset, Is.GreaterThanOrEqualTo(-0.22f));
            Assert.That(EarthPelvisCompensation.Solve(-2f, -1f, 1f, 0.22f), Is.EqualTo(-0.22f));
        }

        [Test]
        public void MovingFootContactFollowsGroundWithoutFreezingStride()
        {
            EarthFootPlantResult contact = EarthFootPlantSolver.SolveContact(
                new float3(0.2f, 1.1f, 0.4f),
                true,
                new float3(0.2f, 0.7f, 0.4f),
                new float3(0f, 1f, 0f),
                new float3(0f, 1f, 0f),
                true,
                0.035f);
            Assert.That(contact.Locked, Is.False);
            Assert.That(contact.Weight01, Is.EqualTo(1f));
            Assert.That(contact.Position.y, Is.EqualTo(0.735f).Within(0.0001f));

            EarthFootPlantResult airborne = EarthFootPlantSolver.SolveContact(
                float3.zero,
                true,
                new float3(0f, -2f, 0f),
                new float3(0f, 1f, 0f),
                new float3(0f, 1f, 0f),
                false,
                0.035f);
            Assert.That(airborne.Weight01, Is.Zero);
        }

        [Test]
        public void KneeHintsStayInTheRadialCharacterFrameAndKeepTheirSide()
        {
            float3 hip = new float3(0f, 24f, 0f);
            float3 left = EarthStableKneeHintSolver.Solve(
                hip,
                new float3(0f, 0f, 1f),
                new float3(1f, 0f, 0f),
                new float3(0f, 1f, 0f),
                -1f,
                float3.zero);
            float3 right = EarthStableKneeHintSolver.Solve(
                hip,
                new float3(0f, 0f, 1f),
                new float3(1f, 0f, 0f),
                new float3(0f, 1f, 0f),
                1f,
                float3.zero);
            Assert.That(left.x, Is.LessThan(hip.x));
            Assert.That(right.x, Is.GreaterThan(hip.x));
            Assert.That(left.z, Is.GreaterThan(hip.z + 0.2f));
            Assert.That(right.z, Is.GreaterThan(hip.z + 0.2f));
        }

        [Test]
        public void FootLockRemainsLocalToAMovingSupportFrame()
        {
            var first = new SupportFrameSnapshot(
                77u, 4u,
                new float3(2f, 3f, 4f),
                quaternion.identity,
                float3.zero,
                float3.zero,
                float3.zero,
                new float3(0f, 1f, 0f),
                false);
            float3 local = EarthSupportFootLockSolver.CaptureLocal(
                new float3(2.4f, 3.2f, 4.1f), in first);
            var moved = new SupportFrameSnapshot(
                77u, 4u,
                new float3(5f, 3f, 4f),
                quaternion.RotateY(math.radians(90f)),
                float3.zero,
                float3.zero,
                float3.zero,
                new float3(0f, 1f, 0f),
                false);
            float3 world = EarthSupportFootLockSolver.ResolveWorld(local, in moved);
            Assert.That(math.distance(world, new float3(5.1f, 3.2f, 3.6f)), Is.LessThan(0.001f));
            Assert.That(EarthSupportFootLockSolver.SameSupport(77u, 4u, in moved), Is.True);
            Assert.That(EarthSupportFootLockSolver.SameSupport(77u, 3u, in moved), Is.False);
        }

        [Test]
        public void SurfAnchorCorrectionIsTangentBoundedAndRejectsTeleportChasing()
        {
            float3 correction = MovingSurfaceSolver.AnchorCorrectionVelocityChange(
                new float3(0f, 24f, 0f),
                new float3(0.2f, 24.8f, 0.1f),
                new float3(0f, 1f, 0f),
                38f,
                95f,
                0.02f);
            Assert.That(math.abs(correction.y), Is.LessThan(0.00001f));
            Assert.That(math.length(correction), Is.LessThanOrEqualTo(1.9f));
            float3 teleport = MovingSurfaceSolver.AnchorCorrectionVelocityChange(
                float3.zero,
                new float3(4f, 0f, 0f),
                new float3(0f, 1f, 0f),
                38f,
                95f,
                0.02f);
            Assert.That(teleport, Is.EqualTo(float3.zero));
        }
    }
}
