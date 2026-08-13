using System;
using System.Collections.Generic;
using Elemental.Simulation.Fields;
using Elemental.Simulation.Magic;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class FieldWorldTests
    {
        [Test]
        public void GustCorridor_AffectsInsideAndNotOutside()
        {
            FieldRegion gust = Region(AirFieldKind.GustCorridor, float3.zero, new float3(1f, 0f, 0f));

            Assert.That(gust.TrySample(new float3(4f, 0.25f, 0f), out FieldContribution inside), Is.True);
            Assert.That(inside.Velocity.x, Is.GreaterThan(0f));
            Assert.That(gust.TrySample(new float3(4f, 4f, 0f), out _), Is.False);
        }

        [Test]
        public void VortexVelocity_IsTangentialAndBounded()
        {
            FieldRegion vortex = Region(AirFieldKind.Vortex, float3.zero, new float3(0f, 1f, 0f));
            Assert.That(vortex.TrySample(new float3(2f, 0f, 0f), out FieldContribution sample), Is.True);

            Assert.That(math.abs(sample.Velocity.z), Is.GreaterThan(math.abs(sample.Velocity.x)));
            Assert.That(math.length(sample.Velocity), Is.LessThanOrEqualTo(20f));
        }

        [Test]
        public void OverlapSchedulerAndQueries_ReportBoundedDebt()
        {
            var world = new FieldWorld(64, 16);
            for (uint index = 1; index <= 64; index++)
            {
                Assert.That(world.Register(Region(
                    AirFieldKind.Vortex,
                    new float3(index * 0.01f, 0f, 0f),
                    new float3(0f, 1f, 0f),
                    index)), Is.True);
            }

            int processed = world.Tick(1f / 20f, 8);
            FieldSample sample = world.Sample(float3.zero);

            Assert.That(processed, Is.EqualTo(8));
            Assert.That(world.DeferredRegionUpdateCount, Is.EqualTo(56));
            Assert.That(sample.RegionChecks, Is.EqualTo(16));
            Assert.That(world.LastQueryDebt, Is.EqualTo(48));
        }

        [Test]
        public void AirAbilitiesCompileToExpectedFieldKinds()
        {
            var path = new List<float3> { float3.zero, new float3(8f, 0f, 0f) };
            AbilityId[] ids =
            {
                AirAbilityIds.GustCorridor,
                AirAbilityIds.Vortex,
                AirAbilityIds.LiftColumn,
                AirAbilityIds.AirBrake
            };
            AirFieldKind[] expected =
            {
                AirFieldKind.GustCorridor,
                AirFieldKind.Vortex,
                AirFieldKind.LiftColumn,
                AirFieldKind.AirBrake
            };

            for (int index = 0; index < ids.Length; index++)
            {
                var command = new MagicCommand(
                    (uint)index, 1u, ElementId.Air, ids[index], float3.zero,
                    new float3(0f, 1f, 0f), path, 1f, 0u, 10u);
                Assert.That(AirAbilityBuilder.TryBuild(in command, new FieldRegionId((uint)index + 1u), out FieldRegion region), Is.True);
                Assert.That(region.Kind, Is.EqualTo(expected[index]));
            }
        }

        [Test]
        public void CurveProjectile_IsRepeatableAndAccelerationIsCapped()
        {
            var world = new FieldWorld(8, 8);
            world.Register(Region(AirFieldKind.Vortex, float3.zero, new float3(0f, 1f, 0f)));
            var profile = new AerodynamicResponseProfile(0.6f, 0.8f, 0.15f, 25f);

            float3 first = Simulate(world, profile);
            float3 second = Simulate(world, profile);

            Assert.That(math.distance(first, second), Is.LessThan(0.0001f));
            Assert.That(math.abs(first.z), Is.GreaterThan(0.25f));
        }

        [Test]
        public void FieldQuerySteadyState_AllocatesZeroManagedBytes()
        {
            var world = new FieldWorld(32, 16);
            for (uint index = 1; index <= 16; index++)
            {
                world.Register(Region(AirFieldKind.GustCorridor, float3.zero, new float3(1f, 0f, 0f), index));
            }
            for (int index = 0; index < 32; index++)
            {
                world.Sample(new float3(index * 0.01f, 0f, 0f));
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 1000; index++)
            {
                world.Sample(new float3(index * 0.001f, 0f, 0f));
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.That(after - before, Is.EqualTo(0L));
        }

        private static float3 Simulate(FieldWorld world, AerodynamicResponseProfile profile)
        {
            float3 position = new float3(2f, 0f, -3f);
            float3 velocity = new float3(0f, 0f, 7f);
            const float dt = 1f / 60f;
            for (int tick = 0; tick < 120; tick++)
            {
                FieldSample sample = world.Sample(position);
                float3 acceleration = AerodynamicMath.ComputeAcceleration(
                    in sample, velocity, 2f, in profile, new float3(0f, 1f, 0f));
                Assert.That(math.length(acceleration), Is.LessThanOrEqualTo(25.001f));
                velocity += acceleration * dt;
                position += velocity * dt;
            }

            return position;
        }

        private static FieldRegion Region(
            AirFieldKind kind,
            float3 center,
            float3 axis,
            uint id = 1u)
        {
            return new FieldRegion(
                new FieldRegionId(id), 1u, kind, center, axis,
                3f, 10f, 20f, 1f, 5f, 128);
        }
    }
}
