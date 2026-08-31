using Elemental.Simulation.Characters;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class CharacterSupportAuthorityTests
    {
        [Test]
        public void ArenaProxyWinsCoincidentPlanetSeam()
        {
            CharacterSupportCandidate[] candidates =
            {
                Candidate(10u, CharacterSupportKind.PlanetGround, 0.012f),
                Candidate(20u, CharacterSupportKind.ArenaWalkableProxy, 0.018f)
            };

            CharacterSupportSelection selected = CharacterSupportAuthority.Select(
                candidates,
                candidates.Length,
                CharacterSupportSelection.None,
                0.55f,
                0.035f);

            Assert.That(selected.HasSupport, Is.True);
            Assert.That(selected.Candidate.SurfaceId, Is.EqualTo(20u));
        }

        [Test]
        public void PreviousLegitimateSupportIsRetainedWithinHysteresis()
        {
            CharacterSupportCandidate arena = Candidate(
                20u,
                CharacterSupportKind.ArenaWalkableProxy,
                0.025f);
            CharacterSupportSelection previous = new CharacterSupportSelection(
                true,
                arena,
                false);
            CharacterSupportCandidate[] candidates =
            {
                new CharacterSupportCandidate(
                    20u, 1u, CharacterSupportKind.ArenaWalkableProxy,
                    0.030f, 1f, true, true),
                new CharacterSupportCandidate(
                    21u, 1u, CharacterSupportKind.ArenaWalkableProxy,
                    0.012f, 1f, true, true)
            };

            CharacterSupportSelection selected = CharacterSupportAuthority.Select(
                candidates,
                candidates.Length,
                in previous,
                0.55f,
                0.025f);

            Assert.That(selected.Candidate.SurfaceId, Is.EqualTo(20u));
            Assert.That(selected.RetainedPrevious, Is.True);
        }

        [TestCase(CharacterSupportKind.ReleasedFracture)]
        [TestCase(CharacterSupportKind.DynamicDebris)]
        public void DynamicFragmentsCanNeverBecomeGround(CharacterSupportKind kind)
        {
            CharacterSupportCandidate[] candidates =
            {
                Candidate(99u, kind, 0.001f),
                Candidate(10u, CharacterSupportKind.PlanetGround, 0.08f)
            };

            CharacterSupportSelection selected = CharacterSupportAuthority.Select(
                candidates,
                candidates.Length,
                CharacterSupportSelection.None,
                0.55f,
                0.035f);

            Assert.That(selected.Candidate.SurfaceId, Is.EqualTo(10u));
        }

        [Test]
        public void TieBreakIsIndependentOfCandidateOrder()
        {
            CharacterSupportCandidate lowId = Candidate(
                7u,
                CharacterSupportKind.ArenaWalkableProxy,
                0.02f);
            CharacterSupportCandidate highId = Candidate(
                9u,
                CharacterSupportKind.ArenaWalkableProxy,
                0.02f);

            CharacterSupportSelection first = CharacterSupportAuthority.Select(
                new[] { highId, lowId }, 2, CharacterSupportSelection.None, 0.55f, 0.03f);
            CharacterSupportSelection second = CharacterSupportAuthority.Select(
                new[] { lowId, highId }, 2, CharacterSupportSelection.None, 0.55f, 0.03f);

            Assert.That(first.Candidate.SurfaceId, Is.EqualTo(7u));
            Assert.That(second.Candidate.SurfaceId, Is.EqualTo(7u));
        }

        private static CharacterSupportCandidate Candidate(
            uint id,
            CharacterSupportKind kind,
            float distance) =>
            new CharacterSupportCandidate(id, 1u, kind, distance, 1f, true, true);
    }
}
