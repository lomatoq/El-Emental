using Elemental.Core.Math;
using Elemental.Core.Time;
using Elemental.Simulation.Time;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class SimulationClockTests
    {
        [Test]
        public void Advance_IncrementsExactlyOneTick()
        {
            SimulationClock clock = new SimulationClock(60, new SimulationTick(41u));

            SimulationTick result = clock.Advance();

            Assert.That(result.Value, Is.EqualTo(42u));
            Assert.That(clock.CurrentTick, Is.EqualTo(result));
            Assert.That(clock.StepSeconds, Is.EqualTo(1f / 60f).Within(0.000001f));
        }

        [Test]
        public void DeterministicRandom_RepeatsSequenceForSameSeed()
        {
            DeterministicRandom first = new DeterministicRandom(0xC0FFEEu);
            DeterministicRandom second = new DeterministicRandom(0xC0FFEEu);

            for (int index = 0; index < 128; index++)
            {
                Assert.That(first.NextUInt(), Is.EqualTo(second.NextUInt()));
            }
        }

        [Test]
        public void DeterministicRandom_ZeroSeedStillProducesSequence()
        {
            DeterministicRandom random = new DeterministicRandom(0u);

            Assert.That(random.NextUInt(), Is.Not.EqualTo(0u));
        }
    }
}
