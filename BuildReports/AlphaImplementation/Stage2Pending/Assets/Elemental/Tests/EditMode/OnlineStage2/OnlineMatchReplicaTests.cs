using Elemental.Simulation.Combat;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class OnlineMatchReplicaTests
    {
        [Test]
        public void ReplicaReplacementDoesNotReplayDamageOrIncrementScore()
        {
            var match = new EarthDuelMatchState();
            Assert.That(match.TryApplyReplica(0, 68, 2, 3, 120, true), Is.True);
            Assert.That(match.TryApplyReplica(0, 68, 2, 3, 120, true), Is.True);
            Assert.That(match.PlayerHealth, Is.Zero);
            Assert.That(match.BotHealth, Is.EqualTo(68));
            Assert.That(match.PlayerScore, Is.EqualTo(2));
            Assert.That(match.BotScore, Is.EqualTo(3));
        }
        [TestCase(float.NaN, 100, 100)]
        [TestCase(-1, 100, 100)]
        [TestCase(101, 100, 100)]
        [TestCase(100, 100, 301)]
        public void InvalidReplicaDoesNotPartiallyChangeMatch(float health, float otherHealth, float time)
        {
            var match = new EarthDuelMatchState();
            Assert.That(match.TryApplyReplica(health, otherHealth, 1, 1, time, true), Is.False);
            Assert.That(match.PlayerHealth, Is.EqualTo(100));
            Assert.That(match.PlayerScore, Is.Zero);
            Assert.That(match.RemainingSeconds, Is.EqualTo(300));
            Assert.That(match.IsReady, Is.False);
        }
    }
}
