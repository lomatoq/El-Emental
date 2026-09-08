using Elemental.Simulation.Bending;
using Elemental.Simulation.Structures;
using NUnit.Framework;
namespace Elemental.Tests.EditMode
{
    public sealed class EarthLandingSlamTests
    {
        private static EarthLandingSlamSettings Settings => EarthLandingSlamSettings.Default;
        [Test] public void HeldHighFallCommitsOnceAndRequiresNewFlight()
        {
            var state = new EarthLandingSlamState(); var settings = Settings;
            Assert.That(state.Step(false, true, true, 50, 0, settings), Is.False);
            Assert.That(state.Step(true, false, false, 55, -1, settings), Is.False);
            Assert.That(state.IsArmed, Is.True);
            Assert.That(state.Step(true, false, false, 52, -10, settings), Is.False);
            Assert.That(state.Step(true, true, true, 50, 0, settings), Is.True);
            for (int i = 0; i < 20; i++) Assert.That(state.Step(true, true, true, 50, 0, settings), Is.False);
            Assert.That(state.LastSpeed, Is.Zero);
        }
        [TestCase(false, true, 5f, 10f)]
        [TestCase(true, false, 5f, 10f)]
        [TestCase(true, true, .2f, 10f)]
        [TestCase(true, true, 5f, 2f)]
        public void ReleaseMissingContactSmallDropAndSlowDescentDoNotSlam(bool held, bool actualContact, float drop, float speed)
        {
            var state = new EarthLandingSlamState(); var settings = Settings;
            state.Step(false, true, true, 50, 0, settings);
            state.Step(true, false, false, 50 + drop, -speed, settings);
            Assert.That(state.Step(held, true, actualContact, 50, 0, settings), Is.False);
        }
        [Test] public void FallingIntoOwnCraterCannotRetriggerUntilChordIsReleased()
        {
            var state = new EarthLandingSlamState(); var settings = Settings;
            state.Step(false, true, true, 50, 0, settings);
            state.Step(true, false, false, 55, -10, settings);
            Assert.That(state.Step(true, true, true, 50, 0, settings), Is.True);
            state.Step(true, false, false, 50, -10, settings);
            Assert.That(state.Step(true, true, true, 47, 0, settings), Is.False);
            state.Step(false, true, true, 47, 0, settings);
            state.Step(true, false, false, 52, -10, settings);
            Assert.That(state.Step(true, true, true, 47, 0, settings), Is.True);
        }

        [Test] public void EarlySupportProbeWaitsForActualContactWithoutReplayingAnOldFall()
        {
            var state = new EarthLandingSlamState(); var settings = Settings;
            state.Step(false, true, true, 50, 0, settings);
            state.Step(true, false, false, 55, -10, settings);
            Assert.That(state.Step(true, true, false, 50, -10, settings), Is.False);
            Assert.That(state.Step(true, true, true, 50, 0, settings), Is.True);
            state.Step(true, false, false, 55, -10, settings);
            for (int i = 0; i < 6; i++) state.Step(true, true, false, 50, 0, settings);
            Assert.That(state.Step(true, true, true, 50, 0, settings), Is.False);
        }

        [Test] public void SpawnAirborneAndCancelledFlightCannotCreateImpactEnergy()
        {
            var state = new EarthLandingSlamState(); var settings = Settings;
            state.Step(true, false, false, 60, -20, settings);
            Assert.That(state.Step(true, true, true, 50, 0, settings), Is.False);
            state.Step(true, false, false, 60, -20, settings);
            state.Cancel();
            Assert.That(state.Step(true, true, true, 50, 0, settings), Is.False);
        }
        [Test] public void LandingSlamOpensOnlyBoundedFloorCellsWhileOrdinaryImpactCannot()
        {
            var ordinary = EarthArenaFractureGate.Resolve(false, EarthArenaFractureTrigger.OrdinaryImpact, 5000, 36);
            // The authored caster is 12kg: even a valid 7.5m/s magical slam is only 90 Ns.
            var slam = EarthArenaFractureGate.Resolve(false, EarthArenaFractureTrigger.LandingSlam, 90, 36);
            var meteor = EarthArenaFractureGate.Resolve(false, EarthArenaFractureTrigger.MeteorImpact, 5000, 36);
            Assert.That(ordinary.Accepted, Is.False);
            Assert.That(slam.Accepted, Is.True); Assert.That(slam.ReleaseCount, Is.EqualTo(4));
            Assert.That(meteor.ReleaseCount, Is.EqualTo(36));
            Assert.That(EarthArenaFractureGate.Resolve(false, EarthArenaFractureTrigger.LandingSlam, float.NaN, 36).Accepted, Is.False);
        }
    }
}
