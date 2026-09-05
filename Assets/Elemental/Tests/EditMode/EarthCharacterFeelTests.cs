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
        public void FullContactBoundsNewSupportDropPerRenderedPoseAndRecoversSmoothly()
        {
            float boundedDrop = EarthPelvisCompensation.SelectAppliedOffset(
                0f, -0.18f, -0.08f, true);
            float partialCapture = EarthPelvisCompensation.SelectAppliedOffset(
                0f, -0.18f, -0.08f, false);
            float smallReachCorrection = EarthPelvisCompensation.SelectAppliedOffset(
                0f, -0.018f, -0.004f, true);
            float recovery = EarthPelvisCompensation.SelectAppliedOffset(
                -0.018f, 0f, -0.012f, true);

            Assert.That(boundedDrop,
                Is.EqualTo(-EarthPelvisCompensation.MaximumDownwardFrameStep).Within(0.000001f));
            Assert.That(partialCapture,
                Is.EqualTo(-EarthPelvisCompensation.MaximumDownwardFrameStep).Within(0.000001f));
            Assert.That(smallReachCorrection, Is.EqualTo(-0.018f).Within(0.000001f));
            Assert.That(recovery, Is.EqualTo(-0.012f).Within(0.000001f));
        }

        [Test]
        public void PelvisCompensationCanCancelBaseRiseWithoutAWorldSpaceDrop()
        {
            const float previousOffset = -0.10f;
            const float previousBaseHeight = 1.00f;
            const float nextBaseHeight = 1.11f;
            float applied = EarthPelvisCompensation.SelectAppliedOffset(
                previousOffset,
                -0.18f,
                -0.12f,
                true,
                nextBaseHeight - previousBaseHeight);

            Assert.That(applied, Is.EqualTo(-0.18f).Within(0.0001f));
            Assert.That(nextBaseHeight + applied,
                Is.GreaterThanOrEqualTo(previousBaseHeight + previousOffset -
                                        EarthPelvisCompensation.MaximumDownwardFrameStep));
            Assert.That(EarthPelvisCompensation.SelectAppliedOffset(
                    previousOffset,
                    -0.18f,
                    -0.12f,
                    true,
                    0f),
                Is.EqualTo(-0.15f).Within(0.0001f),
                "A stationary walk-stop handoff must retain the five-centimetre safety bound.");
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
        public void LocomotionInputImmediatelyReleasesCastingFootBrace()
        {
            bool stationary = EarthFootPlantMotionGate.ShouldLock(
                true, false, true, 0.177f, 0f, float2.zero);
            bool moving = EarthFootPlantMotionGate.ShouldLock(
                true, false, true, 0.9f, 0f, new float2(0f, 1f));
            float movingWeight = EarthFootPlantMotionGate.TargetContactWeight(
                true, false, moving, 0f, new float2(0f, 1f));

            Assert.That(stationary, Is.True,
                "A stationary MMB brace should still feel rooted.");
            Assert.That(moving, Is.False,
                "Movement input must win before velocity rises so old foot constraints cannot trail behind.");
            Assert.That(movingWeight, Is.InRange(0.35f, 0.8f),
                "Locomotion must keep surface-following IK without preserving the old casting lock.");
        }

        [Test]
        public void SupportRelativeCoastKeepsGaitContactActiveAfterInputRelease()
        {
            Assert.That(EarthFootPlantMotionGate.IsLocomoting(float2.zero, 1.2f), Is.True);
            Assert.That(EarthFootPlantMotionGate.IsLocomoting(float2.zero, 0.05f), Is.False);
            Assert.That(EarthFootPlantMotionGate.IsLocomoting(
                new float2(0f, 1f), 0f), Is.True);
        }

        [Test]
        public void SurfKeepsItsIntentionalFootLockWhileMotorInputIsHeld()
        {
            bool locked = EarthFootPlantMotionGate.ShouldLock(
                true, true, true, 1f, 12f, new float2(0f, 1f));
            float weight = EarthFootPlantMotionGate.TargetContactWeight(
                true, true, locked, 12f, new float2(0f, 1f));

            Assert.That(locked, Is.True);
            Assert.That(weight, Is.EqualTo(1f));
        }

        [Test]
        public void OrdinaryIdleLeavesLegsToTheAuthoredHumanoidClip()
        {
            bool locked = EarthFootPlantMotionGate.ShouldLock(
                true, false, false, 0f, 0f, float2.zero);
            float weight = EarthFootPlantMotionGate.TargetContactWeight(
                true, false, locked, 0f, float2.zero);

            Assert.That(locked, Is.False);
            Assert.That(weight, Is.InRange(0.7f, 0.9f),
                "An idle character should settle both soles onto the radial support without a world-space lock.");
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
        public void GaitRateTracksMeasuredTangentSpeedInsideAuthoredClamp()
        {
            Assert.That(EarthAnimationParameterFilter.ResolveGaitRateTarget(0f), Is.EqualTo(1f));
            Assert.That(EarthAnimationParameterFilter.ResolveGaitRateTarget(2f), Is.EqualTo(0.92f).Within(0.001f));
            Assert.That(EarthAnimationParameterFilter.ResolveGaitRateTarget(6f), Is.EqualTo(1.10f).Within(0.001f));
            Assert.That(EarthAnimationParameterFilter.ResolveGaitRateTarget(50f), Is.EqualTo(1.10f));
        }

        [Test]
        public void EachFootCanEnterAndLeaveStanceIndependently()
        {
            EarthFootStanceState state = default;
            EarthFootStanceDecision captured = EarthFootStanceGate.Step(
                in state, true, true, false, false, true, 0.04f, false);
            EarthFootStanceState capturedState = captured.State;
            EarthFootStanceDecision maintained = EarthFootStanceGate.Step(
                in capturedState, true, true, false, true, true, 0.12f, false);
            EarthFootStanceState maintainedState = maintained.State;
            EarthFootStanceDecision released = EarthFootStanceGate.Step(
                in maintainedState, true, true, false, true, true, 0.18f, false);

            Assert.That(captured.Captured, Is.True);
            Assert.That(maintained.Maintained, Is.True);
            Assert.That(released.Locked, Is.False);
        }

        [Test]
        public void LocomotionStanceCannotDoubleLockOrRecaptureAnUnrearmedSwingFoot()
        {
            EarthFootStanceState left = default;
            EarthFootStanceDecision leftContact = EarthFootStanceGate.Step(
                in left, true, true, false, false, true, 0.03f, false);
            EarthFootStanceState right = default;
            EarthFootStanceDecision rightBlocked = EarthFootStanceGate.Step(
                in right, true, true, false, false, true, 0.02f, leftContact.Locked);

            EarthFootStanceState leftLocked = leftContact.State;
            EarthFootStanceDecision leftReleased = EarthFootStanceGate.Step(
                in leftLocked, true, true, false, true, true, 0.16f, false);
            EarthFootStanceState leftSwing = leftReleased.State;
            EarthFootStanceDecision leftTooSoon = EarthFootStanceGate.Step(
                in leftSwing, true, true, false, true, true, 0.04f, false);

            Assert.That(leftContact.Locked, Is.True);
            Assert.That(rightBlocked.Locked, Is.False,
                "Only one locomotion foot may own a support-relative anchor.");
            Assert.That(leftReleased.Locked, Is.False);
            Assert.That(leftTooSoon.Locked, Is.False,
                "A released foot must complete a swing/re-arm cycle before another capture.");
            Assert.That(EarthFootStanceGate.ContactWeight(true, false, 0.15f), Is.LessThan(0.1f),
                "The airborne swing foot must not inherit the stance foot's high IK weight.");
        }

        [Test]
        public void AppliedFootIkWeightCannotPopAcrossAContactTransition()
        {
            float released = EarthFootIkWeightBlend.Step(1f, 0f, 1f / 30f, 0.055f);
            float captured = EarthFootIkWeightBlend.Step(0f, 1f, 1f / 30f, 0.04f);

            Assert.That(1f - released, Is.LessThanOrEqualTo(0.2801f));
            Assert.That(captured, Is.LessThanOrEqualTo(0.2801f));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void ContactWeightReleasesSwingWithinTwoNormalizedFrames(int frameRate)
        {
            float weight = 1f;
            float elapsedFrames = 0f;
            float deltaTime = 1f / frameRate;
            while (elapsedFrames < 2f)
            {
                weight = EarthFootIkWeightBlend.StepContact(
                    weight,
                    0f,
                    deltaTime);
                elapsedFrames += deltaTime * 60f;
            }
            Assert.That(weight, Is.LessThanOrEqualTo(0.15f));
        }

        [Test]
        public void PairWiseFootResolverNeverDoubleLocksOrdinaryLocomotion()
        {
            EarthFootContactState leftState = default;
            EarthFootContactState rightState = default;
            EarthFootContactInput left = CreateContactInput(true, true, 0.03f, -0.4f, 0f, 0.75f);
            EarthFootContactInput right = CreateContactInput(false, true, 0.03f, -0.4f, 0f, 0.75f);

            EarthFootContactPairDecision pair = EarthFootContactSolver.ResolvePair(
                ref leftState, ref rightState, in left, in right);

            Assert.That(pair.BothLocked, Is.False);
            Assert.That(pair.Left.Locked, Is.False,
                "The gait phase, not left-first evaluation order, should resolve an exact tie.");
            Assert.That(pair.Right.Locked, Is.True);
            Assert.That(pair.Left.ReleaseCooldownSeconds, Is.Zero,
                "Losing first-capture arbitration is not a release and must not starve that leg.");
        }

        [Test]
        public void TurnInPlaceCapturesExactlyOnePivotFootAtNeutralClearance()
        {
            EarthFootContactState leftState = default;
            EarthFootContactState rightState = default;
            EarthFootContactInput left = CreateContactInput(
                true, true, 0.12f, -0.01f, -0.12f, 0f,
                pivotingInPlace: true);
            EarthFootContactInput right = CreateContactInput(
                false, true, 0.10f, -0.01f, -0.10f, 0f,
                pivotingInPlace: true);

            EarthFootContactPairDecision pair = EarthFootContactSolver.ResolvePair(
                ref leftState, ref rightState, in left, in right);

            Assert.That(pair.BothLocked, Is.False);
            Assert.That(pair.Left.Locked || pair.Right.Locked, Is.True,
                "A turn-in-place needs one stable pivot anchor even from the neutral idle clearance.");
        }

        [Test]
        public void PivotCapturePreservesRenderedTangentialFootPosition()
        {
            float3 rendered = new float3(0.18f, 0.09f, -0.14f);
            float3 contact = new float3(0.42f, 0.01f, 0.22f);
            float3 normal = new float3(0f, 1f, 0f);

            float3 anchor = EarthFootContactSolver.CaptureRenderedPivotAnchor(
                rendered,
                contact,
                normal);

            Assert.That(anchor.x, Is.EqualTo(rendered.x).Within(0.0001f));
            Assert.That(anchor.z, Is.EqualTo(rendered.z).Within(0.0001f));
            Assert.That(anchor.y, Is.EqualTo(contact.y).Within(0.0001f));
        }

        [Test]
        public void ActiveLandingBaseLayerOwnsContactUntilAnimatorLeavesIt()
        {
            EarthAuthoredActionId action =
                EarthAuthoredActionResolver.ResolveBaseLayerContactOwnership(
                    EarthAuthoredActionId.Locomotion,
                    EarthAuthoredActionId.MovingLandingRoll);

            Assert.That(action, Is.EqualTo(EarthAuthoredActionId.MovingLandingRoll));
            Assert.That(
                EarthAuthoredActionResolver.ResolveBaseLayerContactOwnership(
                    EarthAuthoredActionId.Jump,
                    EarthAuthoredActionId.MovingLandingRoll),
                Is.EqualTo(EarthAuthoredActionId.Jump),
                "A new flight action must remain able to interrupt an outgoing landing.");
        }

        [Test]
        public void DeniedOrReleasedFootCannotRecaptureInsideHysteresisWindow()
        {
            EarthFootContactState leftState = default;
            EarthFootContactState rightState = default;
            EarthFootContactInput firstLeft = CreateContactInput(true, true, 0.03f, -0.4f, 1f, 0.25f);
            EarthFootContactInput firstRight = CreateContactInput(false, true, 0.18f, 0.4f, 0f, 0.25f);
            EarthFootContactSolver.ResolvePair(
                ref leftState, ref rightState, in firstLeft, in firstRight);

            EarthFootContactInput leftRelease = CreateContactInput(
                true, true, 0.18f, 0.4f, 1f, 0.30f, 0.02f);
            EarthFootContactInput rightSwing = CreateContactInput(
                false, true, 0.18f, 0.4f, 0f, 0.30f, 0.02f);
            EarthFootContactSolver.ResolvePair(
                ref leftState, ref rightState, in leftRelease, in rightSwing);

            EarthFootContactInput leftTooSoon = CreateContactInput(
                true, true, 0.03f, -0.4f, 1f, 0.10f, 0.02f);
            EarthFootContactPairDecision second = EarthFootContactSolver.ResolvePair(
                ref leftState, ref rightState, in leftTooSoon, in rightSwing);

            Assert.That(second.Left.Locked, Is.False);
            Assert.That(second.Left.ReleaseCooldownSeconds, Is.GreaterThan(0.09f));
        }

        [Test]
        public void StationaryContactFollowingUsesCurrentSupportLocalTarget()
        {
            EarthFootContactState leftState = default;
            EarthFootContactState rightState = default;
            EarthFootContactInput firstLeft = CreateContactInput(
                true, false, 0.01f, 0f, 0f, 0f, 1f / 60f, float3.zero);
            EarthFootContactInput firstRight = CreateContactInput(
                false, false, 0.01f, 0f, 0f, 0f, 1f / 60f, new float3(0.2f, 0f, 0f));
            EarthFootContactSolver.ResolvePair(
                ref leftState, ref rightState, in firstLeft, in firstRight);

            EarthFootContactInput movedLeft = CreateContactInput(
                true, false, 0.01f, 0f, 0f, 0f, 1f / 60f, new float3(0.5f, 0f, 0f));
            EarthFootContactInput movedRight = CreateContactInput(
                false, false, 0.01f, 0f, 0f, 0f, 1f / 60f, new float3(0.2f, 0f, 0f));
            EarthFootContactPairDecision moved = EarthFootContactSolver.ResolvePair(
                ref leftState, ref rightState, in movedLeft, in movedRight);

            Assert.That(math.distance(moved.Left.TargetLocal, movedLeft.ContactTargetLocal),
                Is.LessThan(0.0001f));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void AnkleInertializationBoundsAuthoredIkSeamAtEveryFrameRate(int fps)
        {
            quaternion current = quaternion.identity;
            quaternion target = quaternion.AxisAngle(
                new float3(1f, 0f, 0f),
                math.radians(120f));
            float deltaTime = 1f / fps;
            float maximumNormalizedStep = 0f;
            for (int frame = 0; frame < fps; frame++)
            {
                quaternion next = EarthAnkleRotationInertializer.Step(
                    current,
                    target,
                    deltaTime);
                float actualStep = QuaternionAngleDegrees(current, next);
                maximumNormalizedStep = math.max(
                    maximumNormalizedStep,
                    actualStep * fps / 60f);
                current = next;
            }

            Assert.That(maximumNormalizedStep,
                Is.LessThanOrEqualTo(
                    EarthAnkleRotationInertializer.MaximumDegreesAt60Hz + 0.001f));
            Assert.That(QuaternionAngleDegrees(current, target), Is.LessThan(0.01f));
        }

        private static float QuaternionAngleDegrees(quaternion from, quaternion to)
        {
            quaternion delta = math.mul(math.inverse(from), to);
            return math.degrees(2f * math.acos(
                math.clamp(math.abs(delta.value.w), 0f, 1f)));
        }

        private static EarthFootContactInput CreateContactInput(
            bool left,
            bool locomoting,
            float clearance,
            float verticalVelocity,
            float priority,
            float gaitPhase,
            float deltaTime = 1f / 60f,
            float3 target = default,
            bool pivotingInPlace = false)
        {
            return new EarthFootContactInput(
                left,
                true,
                locomoting,
                pivotingInPlace,
                false,
                true,
                clearance,
                verticalVelocity,
                priority,
                gaitPhase,
                target,
                new float3(0f, 1f, 0f),
                target,
                new float3(0f, 1f, 0f),
                77u,
                4u,
                deltaTime);
        }

        [Test]
        public void MotionAuditCountsLockChatterAndTemporalDiscontinuities()
        {
            EarthAnimationMotionAuditState state = default;
            var first = new EarthAnimationMotionSample(
                0.02f,
                new float3(-0.1f, 0.04f, 0f),
                new float3(0.1f, 0.04f, 0f),
                new float3(0f, -1f, 0.1f),
                new float3(0f, -1f, 0.1f),
                0.7f,
                0.7f,
                0.7f,
                -0.03f,
                false,
                false,
                true,
                7u,
                2u);
            EarthAnimationMotionAudit.Step(ref state, in first);
            var second = new EarthAnimationMotionSample(
                0.02f,
                new float3(-0.1f, 0.04f, 0.02f),
                new float3(0.1f, 0.04f, 0.02f),
                new float3(0f, -1f, 0.1f),
                new float3(0f, -1f, 0.1f),
                0.72f,
                0.72f,
                0.2f,
                -0.032f,
                true,
                false,
                true,
                7u,
                2u);
            EarthAnimationMotionAudit.Step(ref state, in second);
            var discontinuous = new EarthAnimationMotionSample(
                0.02f,
                new float3(-0.1f, 0.04f, 0.14f),
                new float3(0.1f, 0.04f, 0.02f),
                new float3(0.7f, -0.3f, 0f),
                new float3(0f, -1f, 0.1f),
                0.2f,
                0.2f,
                0.8f,
                -0.082f,
                false,
                true,
                true,
                7u,
                2u);
            EarthAnimationMotionAuditSummary summary =
                EarthAnimationMotionAudit.Step(ref state, in discontinuous);

            Assert.That(summary.SampleCount, Is.EqualTo(3));
            Assert.That(summary.LeftLockTransitions, Is.EqualTo(2));
            Assert.That(summary.RightLockTransitions, Is.EqualTo(1));
            Assert.That(summary.DiscontinuityFrames, Is.EqualTo(1));
            Assert.That(summary.MaximumFootStep, Is.GreaterThan(0.1f));
            Assert.That(summary.MaximumPelvisStep, Is.GreaterThan(0.04f));
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
