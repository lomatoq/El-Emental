using Elemental.Simulation.Structures;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthSurfIntegritySolverTests
    {
        [Test]
        public void SemanticGraphHasThreeProtectedAndNineDetachableStones()
        {
            Assert.That(EarthSurfCellGraph.CellCount, Is.EqualTo(12));
            Assert.That(EarthSurfCellGraph.CountBits(EarthSurfCellGraph.SupportCoreMask), Is.EqualTo(3));
            Assert.That(EarthSurfCellGraph.CountBits(EarthSurfCellGraph.DetachableMask), Is.EqualTo(9));
            Assert.That(EarthSurfCellGraph.SupportCoreMask & EarthSurfCellGraph.DetachableMask, Is.Zero);

            for (int index = 0; index < EarthSurfCellGraph.CellCount; index++)
            {
                EarthSurfCellDefinition cell = EarthSurfCellGraph.GetDefinition(index);
                Assert.That(cell.Index, Is.EqualTo(index));
                Assert.That(cell.Size01.x, Is.GreaterThan(0f));
                Assert.That(cell.Size01.y, Is.GreaterThan(0f));
                Assert.That(cell.NeighbourMask, Is.Not.Zero, $"Cell {index} must participate in the fixed graph.");
            }
        }

        [Test]
        public void CoplanarSupportTransferDoesNotWearBoard()
        {
            EarthSurfIntegrityState state = EarthSurfIntegrityState.Initial;
            var damageEvent = new EarthSurfDamageEvent(
                EarthSurfDamageKind.SupportTransfer,
                0.45f,
                4f,
                0f,
                11u);
            EarthSurfIntegrityDecision decision = EarthSurfIntegritySolver.Resolve(in state, in damageEvent);

            Assert.That(decision.Damage, Is.Zero);
            Assert.That(decision.Integrity, Is.EqualTo(100f));
            Assert.That(decision.DetachedCellMask, Is.Zero);
            Assert.That(decision.State.EventSequence, Is.EqualTo(0u));
        }

        [Test]
        public void HardTransferReleasesOnlyBoundedVisibleOuterCells()
        {
            EarthSurfIntegrityState state = EarthSurfIntegrityState.Initial;
            var damageEvent = new EarthSurfDamageEvent(
                EarthSurfDamageKind.SupportTransfer,
                4.2f,
                31f,
                -0.7f,
                19u);
            EarthSurfIntegrityDecision decision = EarthSurfIntegritySolver.Resolve(in state, in damageEvent);

            Assert.That(decision.Damage, Is.InRange(8f, 10f));
            Assert.That(decision.DetachedOuterCells, Is.InRange(1, 3));
            Assert.That(decision.DetachedCellMask & EarthSurfCellGraph.SupportCoreMask, Is.Zero);
            Assert.That(decision.State.AttachedMask & decision.DetachedCellMask, Is.Zero);
            Assert.That(decision.Collapse, Is.False);
        }

        [Test]
        public void OccupiedFootCoreAndBridgeNeverDetachAcrossDamageSequence()
        {
            EarthSurfIntegrityState state = EarthSurfIntegrityState.Initial;
            for (int eventIndex = 0; eventIndex < 8; eventIndex++)
            {
                var damageEvent = new EarthSurfDamageEvent(
                    eventIndex % 2 == 0 ? EarthSurfDamageKind.Bump : EarthSurfDamageKind.SideScrape,
                    7f,
                    35f,
                    eventIndex % 2 == 0 ? -1f : 1f,
                    (uint)(31 + eventIndex));
                EarthSurfIntegrityDecision decision = EarthSurfIntegritySolver.Resolve(in state, in damageEvent);
                Assert.That(decision.DetachedCellMask & EarthSurfCellGraph.SupportCoreMask, Is.Zero);
                Assert.That(decision.State.AttachedMask & EarthSurfCellGraph.SupportCoreMask,
                    Is.EqualTo(EarthSurfCellGraph.SupportCoreMask));
                state = decision.State;
            }
        }

        [Test]
        public void NoseCrashPrefersNoseCellsAndSevereCrashEndsSurf()
        {
            EarthSurfIntegrityState state = EarthSurfIntegrityState.Initial;
            var damageEvent = new EarthSurfDamageEvent(
                EarthSurfDamageKind.NoseCrash,
                12f,
                0f,
                0.6f,
                73u);
            EarthSurfIntegrityDecision decision = EarthSurfIntegritySolver.Resolve(in state, in damageEvent);

            ushort noseMask = (1 << 3) | (1 << 4) | (1 << 5);
            Assert.That(decision.DetachedCellMask & noseMask, Is.Not.Zero);
            Assert.That(decision.DetachedOuterCells, Is.EqualTo(3));
            Assert.That(decision.Collapse, Is.True,
                "A high-speed wall/nose crash must terminate the finite surf session.");
        }

        [Test]
        public void LowerWallBandClampsHighContactAndRejectsSlowScrape()
        {
            EarthSurfWallBandDecision hard = EarthSurfWallBandSolver.Resolve(0.84f, 11f);
            EarthSurfWallBandDecision slow = EarthSurfWallBandSolver.Resolve(0.12f, 3f);

            Assert.That(hard.Accepted, Is.True);
            Assert.That(hard.ImpactHeight01, Is.EqualTo(EarthSurfWallBandSolver.MaximumLowerBand01));
            Assert.That(hard.DamageRadius01, Is.LessThanOrEqualTo(0.22f));
            Assert.That(slow.Accepted, Is.False);
        }
    }
}
