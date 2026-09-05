using Elemental.Simulation.Gravity;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthBodyRestStateTests
    {
        [Test]
        public void SustainedSupportedRestSleepsButAirborneOrMovingBodiesDoNot()
        {
            var state = new EarthBodyRestState();
            for (int i = 0; i < 200; i++)
                Assert.That(state.Step(false, float3.zero, float3.zero, .02f), Is.False);
            for (int i = 0; i < 20; i++)
                Assert.That(state.Step(true, float3.zero, float3.zero, .02f), Is.False);
            Assert.That(state.Step(true, new float3(.1f, 0, 0), float3.zero, .02f), Is.False);
            for (int i = 0; i < 20; i++)
                Assert.That(state.Step(true, float3.zero, float3.zero, .02f), Is.False);
            Assert.That(state.Step(true, float3.zero, new float3(0, .2f, 0), .02f), Is.False);
            bool slept = false;
            for (int i = 0; i < 32; i++)
                slept |= state.Step(true, float3.zero, float3.zero, .02f);
            Assert.That(slept, Is.True);
        }
    }
}
