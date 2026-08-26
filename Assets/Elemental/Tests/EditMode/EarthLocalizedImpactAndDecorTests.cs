using Elemental.Simulation.Combat;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthLocalizedImpactAndDecorTests
    {
        [Test]
        public void ThreeNearbyStoneHitsEscalateLocalToFullRagdoll()
        {
            EarthLocalizedHitClusterState state = default;
            EarthLocalizedHitClusterResult first = EarthLocalizedHitClusterSolver.Step(
                in state, float3.zero, 1f);
            EarthLocalizedHitClusterState firstState = first.State;
            EarthLocalizedHitClusterResult second = EarthLocalizedHitClusterSolver.Step(
                in firstState, new float3(0.2f, 0f, 0f), 1.2f);
            EarthLocalizedHitClusterState secondState = second.State;
            EarthLocalizedHitClusterResult third = EarthLocalizedHitClusterSolver.Step(
                in secondState, new float3(0.3f, 0f, 0f), 1.4f);
            Assert.That(first.FullRagdoll, Is.False);
            Assert.That(second.FullRagdoll, Is.False);
            Assert.That(third.FullRagdoll, Is.True);
        }

        [Test]
        public void DistantOrLateStoneRestartsCluster()
        {
            var state = new EarthLocalizedHitClusterState(float3.zero, 1f, 2);
            EarthLocalizedHitClusterResult distant = EarthLocalizedHitClusterSolver.Step(
                in state, new float3(2f, 0f, 0f), 1.2f);
            EarthLocalizedHitClusterResult late = EarthLocalizedHitClusterSolver.Step(
                in state, new float3(0.1f, 0f, 0f), 2f);
            Assert.That(distant.State.HitCount, Is.EqualTo(1));
            Assert.That(late.State.HitCount, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedSourceOrWeakHitsCannotEscalateToFullRagdoll()
        {
            EarthLocalizedHitClusterState state = default;
            EarthLocalizedHitClusterResult first = EarthLocalizedHitClusterSolver.Step(
                in state, float3.zero, 1f, 10u, 1f);
            EarthLocalizedHitClusterState firstState = first.State;
            EarthLocalizedHitClusterResult duplicate = EarthLocalizedHitClusterSolver.Step(
                in firstState, new float3(0.1f, 0f, 0f), 1.1f, 10u, 8f);
            EarthLocalizedHitClusterState duplicateState = duplicate.State;
            EarthLocalizedHitClusterResult second = EarthLocalizedHitClusterSolver.Step(
                in duplicateState, new float3(0.2f, 0f, 0f), 1.2f, 11u, 1f);
            EarthLocalizedHitClusterState secondState = second.State;
            EarthLocalizedHitClusterResult third = EarthLocalizedHitClusterSolver.Step(
                in secondState, new float3(0.3f, 0f, 0f), 1.3f, 12u, 1f);
            Assert.That(duplicate.State.HitCount, Is.EqualTo(1));
            Assert.That(third.State.HitCount, Is.EqualTo(3));
            Assert.That(third.FullRagdoll, Is.False);
        }

        [Test]
        public void DecorRockDetachesBeforeItShatters()
        {
            EarthDecorRockDamageResult detach = EarthDecorRockDamageSolver.Resolve(
                900f, 120f, true, 90f, 1250f);
            EarthDecorRockDamageResult shatter = EarthDecorRockDamageSolver.Resolve(
                900f, 1300f, true, 90f, 1250f);
            Assert.That(detach.Detach, Is.True);
            Assert.That(detach.Shatter, Is.False);
            Assert.That(shatter.Shatter, Is.True);
        }
    }
}
