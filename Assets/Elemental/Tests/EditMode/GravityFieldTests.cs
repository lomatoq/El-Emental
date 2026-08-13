using System;
using Elemental.Simulation.Gravity;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class GravityFieldTests
    {
        private static PointPlanetGravity CreateField(
            uint id = 1u,
            float3 center = default,
            float radius = 10f,
            float surfaceAcceleration = 12f,
            float falloffDistance = 40f)
        {
            return new PointPlanetGravity(
                new GravityFieldId(id),
                center,
                radius,
                surfaceAcceleration,
                1f,
                falloffDistance,
                2f,
                50f);
        }

        [Test]
        public void Sample_OnSurfacePointsTowardCenterWithConfiguredMagnitude()
        {
            PointPlanetGravity field = CreateField();

            GravitySample sample = field.Sample(new float3(10f, 0f, 0f), 0u);

            Assert.That(sample.Acceleration.x, Is.EqualTo(-12f).Within(0.0001f));
            Assert.That(sample.Acceleration.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(sample.Up.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(sample.Source, Is.EqualTo(new GravityFieldId(1u)));
        }

        [TestCase(0f, 0f, 0f)]
        [TestCase(0.00001f, 0f, 0f)]
        [TestCase(2f, -3f, 1f)]
        [TestCase(100000f, 100000f, -100000f)]
        public void Sample_IsFiniteAtCenterInsideAndFarAway(float x, float y, float z)
        {
            GravitySample sample = CreateField().Sample(new float3(x, y, z), 10u);

            Assert.That(sample.IsFinite, Is.True);
        }

        [Test]
        public void Sample_UsesGameplayFalloffOutsideSurface()
        {
            PointPlanetGravity field = CreateField();

            float surface = math.length(field.Sample(new float3(10f, 0f, 0f), 0u).Acceleration);
            float halfway = math.length(field.Sample(new float3(30f, 0f, 0f), 0u).Acceleration);
            float outside = math.length(field.Sample(new float3(60f, 0f, 0f), 0u).Acceleration);

            Assert.That(halfway, Is.GreaterThan(0f).And.LessThan(surface));
            Assert.That(outside, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void GravityWorld_SelectsStrongestField()
        {
            GravityWorld world = new GravityWorld();
            world.Register(CreateField(1u, new float3(-20f, 0f, 0f), 10f, 5f));
            world.Register(CreateField(2u, new float3(20f, 0f, 0f), 10f, 15f));

            GravitySample sample = world.Sample(float3.zero, 0u);

            Assert.That(sample.Source, Is.EqualTo(new GravityFieldId(2u)));
            Assert.That(sample.Acceleration.x, Is.GreaterThan(0f));
        }

        [Test]
        public void GravityWorld_InstancesDoNotShareRegistration()
        {
            GravityWorld first = new GravityWorld();
            GravityWorld second = new GravityWorld();
            first.Register(CreateField());

            Assert.That(first.Count, Is.EqualTo(1));
            Assert.That(second.Count, Is.EqualTo(0));
            Assert.That(second.Sample(new float3(10f, 0f, 0f), 0u).Source.IsValid, Is.False);
        }

        [Test]
        public void SampleLoop_DoesNotAllocateAfterWarmup()
        {
            GravityWorld world = new GravityWorld();
            world.Register(CreateField());
            world.Sample(new float3(14f, 2f, -1f), 0u);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (uint tick = 0u; tick < 10000u; tick++)
            {
                world.Sample(new float3(14f, 2f, -1f), tick);
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.EqualTo(0L));
        }
    }
}
