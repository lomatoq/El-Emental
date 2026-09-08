using Elemental.Simulation.Combat;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthLifeResultTests
    {
        [TestCase(0, 1, EarthLifeResult.Won)]
        [TestCase(1, 0, EarthLifeResult.Lost)]
        [TestCase(1, 1, EarthLifeResult.Draw)]
        public void EachAcceptedLifeProducesOneResult(int local, int opponent, EarthLifeResult expected)
        {
            var state = new EarthLifeResultState();
            state.Step(local, opponent, .016f);
            Assert.That(state.Result, Is.EqualTo(EarthLifeResult.None));
            state.Step(local, opponent, .13f);
            Assert.That(state.Result, Is.EqualTo(expected)); Assert.That(state.Resolutions, Is.EqualTo(1));
            state.Step(local, opponent, 4);
            Assert.That(state.Result, Is.EqualTo(EarthLifeResult.None));
            state.Step(local, opponent, .2f); Assert.That(state.Resolutions, Is.EqualTo(1));
            state.Step(local * 2, opponent * 2, .016f); state.Step(local * 2, opponent * 2, .13f);
            Assert.That(state.Result, Is.EqualTo(expected)); Assert.That(state.Resolutions, Is.EqualTo(2));
        }
        [Test]
        public void OppositeDeathsInsideWindowCombineWithoutFlashingAWinner()
        {
            var state = new EarthLifeResultState();
            state.Step(0, 1, .016f); state.Step(0, 1, .05f);
            state.Step(1, 1, .016f);
            Assert.That(state.Result, Is.EqualTo(EarthLifeResult.None));
            state.Step(1, 1, .08f);
            Assert.That(state.Result, Is.EqualTo(EarthLifeResult.Draw)); Assert.That(state.Resolutions, Is.EqualTo(1));
        }
        [Test]
        public void RestartClearsOldResultsAndPauseDoesNotExpireThem()
        {
            var state = new EarthLifeResultState();
            state.Step(0, 1, .016f); state.Step(0, 1, .13f);
            float remaining = state.Remaining; state.Step(0, 1, 0);
            Assert.That(state.Remaining, Is.EqualTo(remaining));
            state.Step(0, 0, .016f); Assert.That(state.Result, Is.EqualTo(EarthLifeResult.None));
            state.Step(0, 0, 5); Assert.That(state.Resolutions, Is.Zero);
        }
    }
}
