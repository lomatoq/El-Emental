using Elemental.Runtime.Physics;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthCombatDummyTests
    {
        [Test]
        public void RepeatedImpactRunawayIsClampedAndSteeredBackWithoutTeleporting()
        {
            float3 position = new float3(-274f, -54f, -238f);
            float3 runaway = new float3(-607f, -161f, -583f);
            float3 stabilized = EarthCombatMotionSafetySolver.Stabilize(
                position, runaway, 22f, 72f, 32f, 0.02f);

            Assert.That(math.all(math.isfinite(stabilized)), Is.True);
            Assert.That(math.length(stabilized), Is.LessThanOrEqualTo(22.001f));
            Assert.That(math.dot(stabilized, math.normalize(position)),
                Is.LessThan(math.dot(runaway, math.normalize(position))));
        }

        [Test]
        public void SafetySolverLeavesOrdinaryInArenaMotionUntouched()
        {
            float3 velocity = new float3(4f, -1f, 7f);
            float3 stabilized = EarthCombatMotionSafetySolver.Stabilize(
                new float3(0f, 26f, 3f), velocity, 22f, 72f, 32f, 0.02f);
            Assert.That(stabilized, Is.EqualTo(velocity));
        }

        [Test]
        public void ResponseSeparatesBraceStaggerAndRagdoll()
        {
            EarthCombatResponse light = EarthCombatResponseSolver.Evaluate(80f, 200f, false);
            EarthCombatResponse stagger = EarthCombatResponseSolver.Evaluate(360f, 2400f, false);
            EarthCombatResponse braced = EarthCombatResponseSolver.Evaluate(360f, 2400f, true);
            EarthCombatResponse heavy = EarthCombatResponseSolver.Evaluate(1500f, 16000f, false);

            Assert.That(light.State, Is.EqualTo(EarthCombatDummyState.Grounded));
            Assert.That(stagger.State, Is.EqualTo(EarthCombatDummyState.Staggered));
            Assert.That(braced.RetainedImpulse01, Is.LessThan(stagger.RetainedImpulse01));
            Assert.That(heavy.State, Is.EqualTo(EarthCombatDummyState.FullRagdoll));
            Assert.That(heavy.HoldSeconds, Is.GreaterThan(stagger.HoldSeconds));
        }
    }
}
