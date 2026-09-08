using Elemental.Simulation.Combat;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthRepairCrushTests
    {
        [TestCase(.05f)]
        [TestCase(2f)]
        [TestCase(40f)]
        public void RepairCannotCompleteInOnePhysicsStepAndTravelsContinuously(float distance)
        {
            float duration = EarthRepairFlight.Duration(distance);
            Assert.That(duration, Is.GreaterThanOrEqualTo(.28f));
            Assert.That(EarthRepairFlight.Phase(0f, duration), Is.Zero);
            Assert.That(EarthRepairFlight.Phase(.02f, duration), Is.InRange(.000001f, .02f));
            Assert.That(EarthRepairFlight.Phase(duration * .5f, duration), Is.EqualTo(.5f).Within(.0001f));
            Assert.That(EarthRepairFlight.Phase(duration, duration), Is.EqualTo(1f));
        }

        [TestCase(600f, 12f, 6f, 1f, true)]
        [TestCase(600f, 42f, 6f, 1f, true)]
        [TestCase(12f, 12f, 10f, 1f, false)]
        [TestCase(40f, 42f, 10f, 1f, false)]
        [TestCase(600f, 42f, 1f, 1f, false)]
        [TestCase(600f, 42f, 6f, -1f, false)]
        public void CrushingRequiresMassDownwardSpeedAndContactAboveBody(float mass, float targetMass,
            float speed, float height, bool expected)
        {
            Assert.That(EarthCharacterImpactSolver.IsHeavyCrush(mass, targetMass, speed,
                new float3(0f, -1f, 0f), math.up(), height), Is.EqualTo(expected));
            Assert.That(EarthCharacterImpactSolver.IsHeavyCrush(mass, targetMass, speed,
                new float3(1f, 0f, 0f), math.up(), height), Is.False, "Ordinary sideways stones retain existing outcome tuning.");
        }

        [Test]
        public void ReceiverNormalOrientsPreContactVelocityDownwardWithoutUsingRebound()
        {
            float3 down = new(0f, -1f, 0f);
            Assert.That(EarthCharacterImpactSolver.OrientIncomingContactVelocity(math.up() * 6f, down), Is.EqualTo(down * 6f));
            Assert.That(EarthCharacterImpactSolver.OrientIncomingContactVelocity(down * 6f, down), Is.EqualTo(down * 6f));
        }
    }
}
