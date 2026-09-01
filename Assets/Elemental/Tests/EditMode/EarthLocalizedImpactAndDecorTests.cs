using Elemental.Simulation.Combat;
using Elemental.Simulation.Structures;
using Elemental.Runtime.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

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

        [Test]
        public void LargeOrFastRockBreaksIntoMoreFasterSecondaryPieces()
        {
            EarthSecondaryFractureSample ordinary = EarthSecondaryFractureSolver.Evaluate(
                9, 16, 3.8f, 1.65f, 0.35f, 0.9f, 4f, 18f);
            EarthSecondaryFractureSample hero = EarthSecondaryFractureSolver.Evaluate(
                9, 16, 3.8f, 1.65f, 1.4f, 0.9f, 42f, 18f);

            Assert.That(ordinary.PieceCount, Is.EqualTo(9));
            Assert.That(hero.PieceCount, Is.EqualTo(16));
            Assert.That(hero.SpreadSpeed, Is.GreaterThan(ordinary.SpreadSpeed));
        }

        [Test]
        public void LocalizedHitProfileStaysInsideReadableShortReactionBudget()
        {
            CharacterImpactResponseProfile profile =
                ScriptableObject.CreateInstance<CharacterImpactResponseProfile>();
            try
            {
                Assert.That(profile.LocalizedHitReaction, Is.True);
                Assert.That(profile.LocalizedHitDuration, Is.InRange(0.12f, 0.22f));
                Assert.That(profile.LocalizedParentWeight, Is.InRange(0.45f, 0.60f));
                Assert.That(profile.LocalizedTorsoWeight, Is.InRange(0.20f, 0.32f));
                Assert.That(profile.LocalizedArmChestMaxAngle, Is.InRange(7f, 14f));
                Assert.That(profile.LocalizedHeadMaxAngle, Is.InRange(4f, 8f));
                Assert.That(profile.LocalizedHipsLegWeight, Is.LessThanOrEqualTo(0.35f));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }
}
