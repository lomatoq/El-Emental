using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthFootSupportAuthorityIntegrationTests
    {
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

        private static EarthFootContactInput Contact(
            bool left,
            uint supportId,
            uint generation,
            bool hasContact,
            bool supported = true) =>
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
                1f / 60f);

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
