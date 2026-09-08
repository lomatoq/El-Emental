using System;
using Elemental.Simulation.Combat;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthDuelMatchStateTests
    {
        [Test]
        public void LoadingAndPauseDoNotConsumeRoundTime()
        {
            var state = new EarthDuelMatchState();
            state.Step(20f);
            Assert.That(state.RemainingSeconds, Is.EqualTo(300f));
            state.IsReady = true;
            state.Step(0f);
            Assert.That(state.RemainingSeconds, Is.EqualTo(300f));
            state.Step(5f);
            Assert.That(state.RemainingSeconds, Is.EqualTo(295f));
        }

        [Test]
        public void PartialDamageDoesNotScoreAndRepeatedDeathScoresOnce()
        {
            var state = new EarthDuelMatchState { IsReady = true };
            Assert.That(state.Damage(EarthDuelFighterId.Player, 8f), Is.False);
            Assert.That(state.PlayerHealth, Is.EqualTo(92f));
            Assert.That(state.BotScore, Is.Zero);
            Assert.That(state.Damage(EarthDuelFighterId.Player, 100f), Is.True);
            Assert.That(state.PlayerHealth, Is.Zero);
            Assert.That(state.Damage(EarthDuelFighterId.Player, 100f), Is.False);
            Assert.That(state.BotScore, Is.EqualTo(1));
        }

        [Test]
        public void SimultaneousDeathsGiveBothSidesOnePointAndRespawnRestoresHealth()
        {
            var state = new EarthDuelMatchState { IsReady = true };
            state.Damage(EarthDuelFighterId.Player, 100f);
            state.Damage(EarthDuelFighterId.Bot, 100f);
            Assert.That(state.PlayerScore, Is.EqualTo(1));
            Assert.That(state.BotScore, Is.EqualTo(1));
            state.Respawn(EarthDuelFighterId.Player);
            Assert.That(state.PlayerHealth, Is.EqualTo(100f));
            Assert.That(state.BotHealth, Is.Zero);
            state.Damage(EarthDuelFighterId.Player, 100f);
            Assert.That(state.BotScore, Is.EqualTo(2));
        }

        [Test]
        public void RoundEndFreezesHealthAndScoreUntilRestart()
        {
            var state = new EarthDuelMatchState { IsReady = true };
            state.Damage(EarthDuelFighterId.Bot, 100f);
            state.Step(400f);
            Assert.That(state.IsOver, Is.True);
            Assert.That(state.RemainingSeconds, Is.Zero);
            Assert.That(state.CombatAllowed, Is.False);
            Assert.That(state.Damage(EarthDuelFighterId.Player, 100f), Is.False);
            state.Respawn(EarthDuelFighterId.Bot);
            Assert.That(state.BotHealth, Is.Zero);
            Assert.That(state.PlayerScore, Is.EqualTo(1));
            state.Restart();
            Assert.That(state.PlayerScore, Is.Zero);
            Assert.That(state.BotScore, Is.Zero);
            Assert.That(state.BotHealth, Is.EqualTo(100f));
            Assert.That(state.RemainingSeconds, Is.EqualTo(300f));
            Assert.That(state.CombatAllowed, Is.True);
        }

        [Test]
        public void ReadinessRejectsDamageAndHealthHasNoPassiveRegeneration()
        {
            var state = new EarthDuelMatchState();
            state.Damage(EarthDuelFighterId.Bot, 30f);
            Assert.That(state.BotHealth, Is.EqualTo(100f));
            state.IsReady = true;
            state.Damage(EarthDuelFighterId.Bot, 30f);
            state.Step(30f);
            Assert.That(state.BotHealth, Is.EqualTo(70f));
        }

        [Test]
        public void InvalidNumbersCannotPoisonHealthOrClock()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EarthDuelMatchState(float.NaN));
            var state = new EarthDuelMatchState { IsReady = true };
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Step(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Damage(EarthDuelFighterId.Player, float.PositiveInfinity));
            Assert.That(state.PlayerHealth, Is.EqualTo(100f));
        }

        [Test]
        public void DamageProfileKeepsSupportAndSlowPhysicsHarmless()
        {
            EarthDuelDamageSettings settings = EarthDuelDamageSettings.Default;
            Assert.That(settings.Resolve(EarthCharacterImpactSourceKind.Physics, 8f, 0.5f), Is.Zero);
            Assert.That(settings.Resolve(EarthCharacterImpactSourceKind.Physics, 100f, 100f), Is.EqualTo(25f));
            Assert.That(settings.Resolve(EarthCharacterImpactSourceKind.StonePunch, 8f, 50f), Is.EqualTo(24f));
            Assert.That(settings.Resolve(EarthCharacterImpactSourceKind.BotProjectile, 8f, 50f), Is.EqualTo(24f));
        }
    }
}
