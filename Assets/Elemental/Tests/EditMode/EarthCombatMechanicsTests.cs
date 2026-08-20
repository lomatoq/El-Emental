using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthCombatMechanicsTests
    {
        [Test]
        public void RedirectPreservesBoundedEnergyAndUsesRequestedVector()
        {
            EarthProjectileRedirectResult result = EarthProjectileRedirectSolver.Solve(
                new float3(0f, 0f, 24f), new float3(1f, 0f, 0f), 1f, 32f);
            Assert.That(result.Valid, Is.True);
            Assert.That(result.Velocity.x, Is.GreaterThan(15f));
            Assert.That(math.length(result.Velocity), Is.LessThanOrEqualTo(24.01f));
            Assert.That(result.RetainedEnergy01, Is.InRange(0.5f, 1f));
        }

        [Test]
        public void SeismicCounterRequiresBraceAndCreatesFollowUpMatter()
        {
            Assert.That(EarthSeismicCounterSolver.Evaluate(false, 600f, 9000f).Triggered, Is.False);
            EarthSeismicCounterResult result = EarthSeismicCounterSolver.Evaluate(true, 600f, 9000f);
            Assert.That(result.Triggered, Is.True);
            Assert.That(result.Radius, Is.GreaterThan(3f));
            Assert.That(result.FollowUpMatter01, Is.GreaterThan(0.4f));
        }

        [Test]
        public void TrapReleasesOnTimeOrEscapeImpulse()
        {
            EarthTrapSample holding = EarthTrapSolver.Step(EarthTrapState.Captured, 0.4f, 2f, 0f, 300f);
            Assert.That(holding.Release, Is.False);
            EarthTrapSample escaped = EarthTrapSolver.Step(EarthTrapState.Captured, 0.4f, 2f, 320f, 300f);
            Assert.That(escaped.Release, Is.True);
            Assert.That(escaped.State, Is.EqualTo(EarthTrapState.Spent));
        }

        [Test]
        public void DeterministicCombatFuzz_KeepsRedirectVelocityFiniteAndBounded()
        {
            uint random = 0xC04BA711u;
            for (int index = 0; index < 100000; index++)
            {
                random = Next(random);
                float speed = 0.2f + ((random >> 10) & 255u) * 0.18f;
                float control = ((random >> 18) & 255u) / 255f;
                float3 incoming = math.normalizesafe(new float3(
                    (int)(random & 31u) - 15f,
                    (int)((random >> 5) & 31u) - 15f,
                    (int)((random >> 10) & 31u) - 15f), new float3(0f, 0f, 1f)) * speed;
                float3 direction = math.normalizesafe(new float3(
                    (int)((random >> 15) & 31u) - 15f,
                    (int)((random >> 20) & 31u) - 15f,
                    (int)((random >> 25) & 31u) - 15f), new float3(1f, 0f, 0f));
                EarthProjectileRedirectResult result = EarthProjectileRedirectSolver.Solve(
                    incoming, direction, control, 32f);
                Assert.That(math.all(math.isfinite(result.Velocity)), Is.True, $"seed={random:X8}");
                Assert.That(math.length(result.Velocity), Is.LessThanOrEqualTo(Mathf.Min(speed, 32f) + 0.001f));
                Assert.That(result.RetainedEnergy01, Is.InRange(0f, 1f));
            }
        }

        private static uint Next(uint value)
        {
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            return value;
        }
    }
}
