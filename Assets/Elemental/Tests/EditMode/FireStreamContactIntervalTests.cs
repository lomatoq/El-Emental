using Elemental.Simulation.Fire;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class FireStreamContactIntervalTests
    {
        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void CrossingIntegratesOnlyActualContactAtEverySamplingRate(int rate)
        {
            float contact = 0;
            for (int i = 0; i < rate; i++)
            {
                float x = -2 + 4f * i / rate;
                contact += FireStreamContactInterval.ContactSeconds(float3.zero, new float3(0,0,1),
                    8, 8, new float3(x,-1,4), new float3(x,1,4),
                    new float3(4f/rate,0,0), .5f, 1f/rate);
            }
            Assert.That(contact, Is.EqualTo(.25f).Within(.00001f));
            Assert.That(contact * 16, Is.EqualTo(4f).Within(.0002f));
        }

        [Test]
        public void FastCrossingBetweenTwoMissesIsNotAFullTickHit()
        {
            float contact = FireStreamContactInterval.ContactSeconds(float3.zero, new float3(0,0,1),
                8, 8, new float3(-2,-1,4), new float3(-2,1,4), new float3(4,0,0), .5f, .1f);
            Assert.That(contact, Is.EqualTo(.025f).Within(.000001f));
        }

        [Test]
        public void GrowingFrontAndRoundedEndpointHaveTheirOwnEntryTime()
        {
            float contact = FireStreamContactInterval.ContactSeconds(float3.zero, new float3(0,0,1),
                0, 8, new float3(0,0,4), new float3(0,0,4), float3.zero, .5f, .1f);
            Assert.That(contact, Is.EqualTo(.05625f).Within(.000001f));
        }

        [Test]
        public void ObstacleClippedBeamDoesNotDamageTheFighterBehindIt()
        {
            Assert.That(FireStreamContactInterval.ContactSeconds(float3.zero, new float3(0,0,1),
                2, 2, new float3(0,-1,4), new float3(0,1,4), float3.zero, .5f, .1f), Is.Zero);
        }

        [Test]
        public void ParallelCapsulesAndStationaryContactRemainFinite()
        {
            Assert.That(FireStreamContactInterval.ContactSeconds(float3.zero, new float3(0,0,1),
                8, 8, new float3(.2f,0,3), new float3(.2f,0,5), float3.zero, .5f, .1f),
                Is.EqualTo(.1f).Within(.000001f));
            Assert.That(FireStreamContactInterval.ContactSeconds(float3.zero, float3.zero,
                8, 8, float3.zero, float3.zero, float3.zero, .5f, .1f), Is.Zero);
        }
    }
}
