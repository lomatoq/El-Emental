using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthFootSupportAuthorityIntegrationTests
    {
        [TestCase(30, 6f)]
        [TestCase(60, 6f)]
        [TestCase(120, 6f)]
        [TestCase(30, -6f)]
        [TestCase(60, -6f)]
        [TestCase(120, -6f)]
        public void StopDoesNotPullFeetTowardAWorldSpaceFilterBacklog(int fps, float speed)
        {
            EarthFootContactState left = default;
            EarthFootContactState right = default;
            EarthFootContactPairDecision decision = default;
            float position = 0f;
            for (int frame = 0; frame < fps; frame++)
            {
                position = speed * frame / fps;
                var l = TravellingFoot(true, position, 1f / fps, true, true);
                var r = TravellingFoot(false, position, 1f / fps, true, true);
                decision = EarthFootContactSolver.ResolvePair(ref left, ref right, in l, in r);
            }
            var stoppedLeft = TravellingFoot(true, position, 1f / fps, false, true);
            var stoppedRight = TravellingFoot(false, position, 1f / fps, false, true);
            decision = EarthFootContactSolver.ResolvePair(
                ref left, ref right, in stoppedLeft, in stoppedRight);
            Assert.That(math.abs(decision.Left.TargetLocal.z - position), Is.LessThan(0.025f));
            Assert.That(math.abs(decision.Right.TargetLocal.z - position), Is.LessThan(0.025f));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void StopOnChangedTerrainReseedsContactFollowingFromTheCurrentSurface(int fps)
        {
            EarthFootContactState left = default;
            EarthFootContactState right = default;
            float dt = 1f / fps;

            // Keep both feet in authored swing while the sampled support moves
            // through a pronounced vertical profile. The free-foot filter is
            // deliberately allowed to carry its prior surface correction.
            for (int frame = 0; frame < fps / 2; frame++)
            {
                float progress = frame / (float)math.max(1, fps / 2 - 1);
                float height = math.lerp(0.11f, -0.13f, progress);
                var movingLeft = TerrainFoot(
                    true, frame * 0.04f, height, 0.28f, dt, true);
                var movingRight = TerrainFoot(
                    false, frame * 0.04f, height, 0.28f, dt, true);
                EarthFootContactSolver.ResolvePair(
                    ref left, ref right, in movingLeft, in movingRight);
            }

            const float stoppedHeight = 0.09f;
            var stoppedLeft = TerrainFoot(
                true, 0.8f, stoppedHeight, -0.06f, dt, false);
            var stoppedRight = TerrainFoot(
                false, 0.8f, stoppedHeight, -0.06f, dt, false);
            EarthFootContactPairDecision stopped = EarthFootContactSolver.ResolvePair(
                ref left, ref right, in stoppedLeft, in stoppedRight);

            Assert.That(stopped.Left.Reason, Is.EqualTo(EarthFootContactReason.Stance));
            Assert.That(stopped.Right.Reason, Is.EqualTo(EarthFootContactReason.Stance));
            Assert.That(stopped.Left.TargetWeight, Is.EqualTo(1f));
            Assert.That(stopped.Right.TargetWeight, Is.EqualTo(1f));
            Assert.That(math.distance(stopped.Left.TargetLocal, stoppedLeft.ContactTargetLocal),
                Is.LessThan(0.0001f),
                "First stationary contact frame must not use a terrain target from the preceding swing.");
            Assert.That(math.distance(stopped.Right.TargetLocal, stoppedRight.ContactTargetLocal),
                Is.LessThan(0.0001f));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void StationaryContactFollowingDoesNotFeedSolvedFootMotionBackIntoTarget(int fps)
        {
            EarthFootContactState left = default;
            EarthFootContactState right = default;
            float dt = 1f / fps;
            var initialLeft = TerrainFoot(true, 0f, 0f, 0.03f, dt, false);
            var initialRight = TerrainFoot(false, 0f, 0f, 0.03f, dt, false);
            EarthFootContactSolver.ResolvePair(
                ref left, ref right, in initialLeft, in initialRight);

            for (int frame = 1; frame <= 4; frame++)
            {
                // Reproduce an Animator goal that contains some of the prior
                // frame's downward solve while the ray still resolves the same
                // static support point.
                float solvedHeight = 0.03f - frame * 0.08f;
                var feedbackLeft = TerrainFoot(
                    true, 0f, 0f, solvedHeight, dt, false);
                var feedbackRight = TerrainFoot(
                    false, 0f, 0f, solvedHeight, dt, false);
                EarthFootContactPairDecision decision = EarthFootContactSolver.ResolvePair(
                    ref left, ref right, in feedbackLeft, in feedbackRight);

                Assert.That(math.distance(
                        decision.Left.TargetLocal,
                        feedbackLeft.ContactTargetLocal),
                    Is.LessThan(0.0001f));
                Assert.That(math.distance(
                        decision.Right.TargetLocal,
                        feedbackRight.ContactTargetLocal),
                    Is.LessThan(0.0001f));
            }
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void StationaryContactFollowingUsesTheCurrentFullSurfaceTarget(int fps)
        {
            EarthFootContactState left = default;
            EarthFootContactState right = default;
            float dt = 1f / fps;
            var initialLeft = TerrainFoot(true, 0f, 0f, 0.03f, dt, false);
            var initialRight = TerrainFoot(false, 0f, 0f, 0.03f, dt, false);
            EarthFootContactPairDecision initial = EarthFootContactSolver.ResolvePair(
                ref left, ref right, in initialLeft, in initialRight);

            var steppedLeft = TerrainFoot(true, 0.5f, 0.13f, 0.16f, dt, false);
            var steppedRight = TerrainFoot(false, 0.5f, 0.13f, 0.16f, dt, false);
            EarthFootContactPairDecision stepped = EarthFootContactSolver.ResolvePair(
                ref left, ref right, in steppedLeft, in steppedRight);

            Assert.That(math.distance(
                    stepped.Left.TargetLocal,
                    steppedLeft.ContactTargetLocal),
                Is.LessThan(0.0001f),
                "An unlocked stationary follower must stay on the current curved surface sample.");
            Assert.That(math.distance(
                    stepped.Right.TargetLocal,
                    steppedRight.ContactTargetLocal),
                Is.LessThan(0.0001f));
            Assert.That(math.distance(stepped.Left.TargetLocal,initial.Left.TargetLocal),
                Is.GreaterThan(0.5f),
                "The regression must exercise a meaningful tangent and height change.");
        }

        [Test]
        public void LeavingPlatformDiscardsFilteredAnchorImmediately()
        {
            EarthFootContactState left = default;
            EarthFootContactState right = default;
            var l = TravellingFoot(true, 0f, 1f / 60f, false, true);
            var r = TravellingFoot(false, 0f, 1f / 60f, false, true);
            EarthFootContactSolver.ResolvePair(ref left, ref right, in l, in r);
            l = TravellingFoot(true, 2f, 1f / 60f, false, false);
            r = TravellingFoot(false, 2f, 1f / 60f, false, false);
            var released = EarthFootContactSolver.ResolvePair(ref left, ref right, in l, in r);
            Assert.That(released.Left.TargetWeight, Is.Zero);
            Assert.That(math.distance(released.Left.TargetLocal, l.FallbackTargetLocal), Is.LessThan(0.001f));
            Assert.That(released.Left.Locked, Is.False);
        }

        private static EarthFootContactInput TravellingFoot(
            bool left, float position, float dt, bool moving, bool supported)
        {
            float3 contact = new float3(left ? -0.15f : 0.15f, 0f, position);
            return new EarthFootContactInput(left, supported, moving, false, false,
                supported, 0.03f, 0f, 0f, 0.5f, contact, new float3(0f, 1f, 0f),
                contact + new float3(0f, 0.03f, 0f), new float3(0f, 1f, 0f),
                10u, 1u, dt, 0f);
        }

        private static EarthFootContactInput TerrainFoot(
            bool left,
            float position,
            float terrainHeight,
            float animatedHeight,
            float dt,
            bool moving)
        {
            float x = left ? -0.15f : 0.15f;
            float3 contact = new float3(x, terrainHeight, position);
            float3 animated = new float3(x, animatedHeight, position);
            return new EarthFootContactInput(
                left, true, moving, false, false, true,
                animatedHeight - terrainHeight, 0f, 0f, 0.5f,
                contact, new float3(0f, 1f, 0f), animated,
                new float3(0f, 1f, 0f), 10u, 1u, dt, 0f);
        }

        [Test]
        public void FootStateMachinePlantsMaintainsReleasesAndResetsAirborne()
        {
            EarthFootContactState leftState = default;
            EarthFootContactState rightState = default;
            EarthFootContactInput left = Contact(true, 71u, 3u, true);
            EarthFootContactInput right = Contact(false, 71u, 3u, false);

            EarthFootContactPairDecision planting = EarthFootContactSolver.ResolvePair(
                ref leftState,
                ref rightState,
                in left,
                in right);
            Assert.That(planting.Left.PlantState, Is.EqualTo(EarthFootPlantState.Planting));

            EarthFootContactPairDecision planted = EarthFootContactSolver.ResolvePair(
                ref leftState,
                ref rightState,
                in left,
                in right);
            Assert.That(planted.Left.PlantState, Is.EqualTo(EarthFootPlantState.Planted));

            EarthFootContactInput generationSwap = Contact(true, 71u, 4u, true);
            EarthFootContactPairDecision releasing = EarthFootContactSolver.ResolvePair(
                ref leftState,
                ref rightState,
                in generationSwap,
                in right);
            Assert.That(releasing.Left.PlantState, Is.EqualTo(EarthFootPlantState.Releasing));
            Assert.That(releasing.Left.Reason, Is.EqualTo(EarthFootContactReason.SupportSwap));

            EarthFootContactInput airborne = Contact(
                true,
                0u,
                0u,
                false,
                supported: false);
            EarthFootContactPairDecision reset = EarthFootContactSolver.ResolvePair(
                ref leftState,
                ref rightState,
                in airborne,
                in right);
            Assert.That(reset.Left.PlantState, Is.EqualTo(EarthFootPlantState.AirborneReset));
            Assert.That(reset.Left.Locked, Is.False);
        }

        [Test]
        public void ReleasedFragmentCannotRetainPreviousFootAuthority()
        {
            CharacterSupportCandidate priorCandidate = Candidate(
                44u,
                6u,
                CharacterSupportKind.ArenaWalkableProxy,
                0.02f,
                true);
            CharacterSupportSelection previous = new CharacterSupportSelection(
                true,
                priorCandidate,
                false);
            CharacterSupportCandidate[] candidates =
            {
                Candidate(44u, 7u, CharacterSupportKind.ReleasedFracture, 0.005f, false),
                Candidate(1u, 1u, CharacterSupportKind.PlanetGround, 0.08f, true)
            };

            CharacterSupportSelection selected = CharacterSupportAuthority.Select(
                candidates,
                candidates.Length,
                in previous,
                0.55f,
                0.035f);

            Assert.That(selected.HasSupport, Is.True);
            Assert.That(selected.Candidate.Kind, Is.EqualTo(CharacterSupportKind.PlanetGround));
            Assert.That(selected.RetainedPrevious, Is.False);
        }

        [Test]
        public void CandidateMatchIncludesGenerationAndGeometricTieBreakFields()
        {
            CharacterSupportCandidate selected = Candidate(
                9u,
                2u,
                CharacterSupportKind.MovingAbilitySurface,
                0.03f,
                true);
            CharacterSupportCandidate stale = Candidate(
                9u,
                1u,
                CharacterSupportKind.MovingAbilitySurface,
                0.03f,
                true);

            Assert.That(CharacterSupportAuthority.Matches(in selected, in selected), Is.True);
            Assert.That(CharacterSupportAuthority.Matches(in stale, in selected), Is.False);
        }

        [Test]
        public void AuthoredContactCurveSuppressesSwingAndSelectsThePlantedFoot()
        {
            EarthFootContactState leftState = default;
            EarthFootContactState rightState = default;
            EarthFootContactInput left = Contact(
                true, 20u, 1u, true, authoredContact: 0.08f);
            EarthFootContactInput right = Contact(
                false, 20u, 1u, true, authoredContact: 0.92f);

            EarthFootContactPairDecision decision = EarthFootContactSolver.ResolvePair(
                ref leftState,
                ref rightState,
                in left,
                in right);

            Assert.That(decision.Left.Locked, Is.False);
            Assert.That(decision.Left.Reason, Is.EqualTo(EarthFootContactReason.Swing));
            Assert.That(decision.Right.Locked, Is.True);
            Assert.That(decision.Right.Reason, Is.EqualTo(EarthFootContactReason.Capture));
        }

        private static EarthFootContactInput Contact(
            bool left,
            uint supportId,
            uint generation,
            bool hasContact,
            bool supported = true,
            float authoredContact = float.NaN) =>
            new EarthFootContactInput(
                left,
                supported,
                true,
                false,
                false,
                hasContact,
                hasContact ? 0.025f : float.PositiveInfinity,
                -0.25f,
                left ? 1f : 0f,
                0.25f,
                new float3(left ? -0.1f : 0.1f, 0f, 0f),
                new float3(0f, 1f, 0f),
                new float3(left ? -0.1f : 0.1f, 0.03f, 0f),
                new float3(0f, 1f, 0f),
                supportId,
                generation,
                1f / 60f,
                authoredContact);

        private static CharacterSupportCandidate Candidate(
            uint id,
            uint generation,
            CharacterSupportKind kind,
            float distance,
            bool walkable) =>
            new CharacterSupportCandidate(
                id,
                generation,
                kind,
                distance,
                1f,
                true,
                walkable);
    }
}
