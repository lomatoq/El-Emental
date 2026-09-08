using Elemental.Simulation.Characters;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class SettledMatterSupportTests
    {
        [Test] public void ApexCannotBecomeSupportButSleepingRubbleCan()
        {
            Assert.That(SettledMatterSupportPolicy.CanSupport(false, false, false, 0f, 0f), Is.False);
            Assert.That(SettledMatterSupportPolicy.CanSupport(true, false, false, 0f, 0f), Is.True);
        }
        [Test] public void ContactWakeRetainsButThrowAndKinematicGrabRelease()
        {
            Assert.That(SettledMatterSupportPolicy.CanSupport(false, true, false, .01f, .04f), Is.True);
            Assert.That(SettledMatterSupportPolicy.CanSupport(false, true, false, 1f, 0f), Is.False);
            Assert.That(SettledMatterSupportPolicy.CanSupport(false, true, true, 0f, 0f), Is.False);
            Assert.That(SettledMatterSupportPolicy.CanSupport(false, true, false, 0f, 1f), Is.False);
        }
        [Test] public void ActualNearerRockBeatsBuriedArenaRegardlessOfCandidateOrder()
        {
            var rock = new CharacterSupportCandidate(2, 3, CharacterSupportKind.SettledMatter, .1f, 1, true, true);
            var floor = new CharacterSupportCandidate(1, 1, CharacterSupportKind.ArenaWalkableProxy, .5f, 1, true, true);
            foreach (var candidates in new[] { new[] { floor, rock }, new[] { rock, floor } })
                Assert.That(CharacterSupportAuthority.Select(candidates, 2, default, .55f, .035f).Candidate.SurfaceId, Is.EqualTo(2));
        }
        [Test] public void ActualNearerPlatformBeatsBuriedArena()
        {
            var platform = new CharacterSupportCandidate(2, 3, CharacterSupportKind.MovingAbilitySurface, .1f, 1, true, true);
            var floor = new CharacterSupportCandidate(1, 1, CharacterSupportKind.ArenaWalkableProxy, .5f, 1, true, true);
            Assert.That(CharacterSupportAuthority.Select(new[] { floor, platform }, 2, default, .55f, .035f).Candidate.SurfaceId, Is.EqualTo(2));
        }
    }
}
