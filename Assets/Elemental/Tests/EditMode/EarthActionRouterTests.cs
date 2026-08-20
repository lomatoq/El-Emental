using Elemental.Simulation.Bending;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthActionRouterTests
    {
        [Test]
        public void ShiftSpaceWaitsForChordThenOwnsOnlyWave()
        {
            var router = new EarthActionRouter();
            EarthActionRoute pending = router.Step(new EarthActionRouterFrame(
                1f, grounded: true, stableSupport: true,
                modifierHeld: true, jumpPressed: true, jumpHeld: true));
            EarthActionRoute wave = router.Step(new EarthActionRouterFrame(
                1.16f, grounded: true, stableSupport: true,
                modifierHeld: true, jumpHeld: true));

            Assert.That(pending.Owner, Is.EqualTo(EarthActionOwner.ShiftSpaceChord));
            Assert.That(pending.Consumes(EarthInputConsumption.Primary), Is.True);
            Assert.That(router.ChordState.IsPending, Is.False,
                "After the window expires the same router owns Wave, not the pending chord.");
            Assert.That(wave.Owner, Is.EqualTo(EarthActionOwner.Wave));
            Assert.That(wave.Phase, Is.EqualTo(EarthActionRoutePhase.Begin));
            Assert.That(wave.Intent, Is.EqualTo(EarthActionIntentKind.WaveCharge));
        }

        [Test]
        public void LmbInsideChordWindowSwitchesToResonanceAndNeverStartsWave()
        {
            var router = new EarthActionRouter();
            router.Step(new EarthActionRouterFrame(
                2f, grounded: true, stableSupport: true,
                modifierHeld: true, jumpPressed: true, jumpHeld: true));
            EarthInputChordState chord = router.ChordState;
            EarthActionRoute resonance = router.Step(new EarthActionRouterFrame(
                2.09f, grounded: true, stableSupport: true,
                modifierHeld: true, jumpHeld: true,
                primaryPressed: true, primaryHeld: true));
            EarthActionRoute release = router.Step(new EarthActionRouterFrame(
                2.8f, grounded: true, stableSupport: true,
                modifierHeld: true, jumpReleased: true, primaryHeld: true));

            Assert.That(chord.IsPending, Is.True);
            Assert.That(chord.StartedAt, Is.EqualTo(2f));
            Assert.That(chord.Window01(2.075f), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(resonance.Owner, Is.EqualTo(EarthActionOwner.Resonance));
            Assert.That(resonance.Intent, Is.EqualTo(EarthActionIntentKind.ResonanceCharge));
            Assert.That(release.Owner, Is.EqualTo(EarthActionOwner.Resonance));
            Assert.That(release.Phase, Is.EqualTo(EarthActionRoutePhase.Commit));
            Assert.That(router.Owner, Is.EqualTo(EarthActionOwner.None));
        }

        [Test]
        public void ArmorBeatsGravityAndSurfRequiresNoMouseButton()
        {
            var armorRouter = new EarthActionRouter();
            EarthActionRoute armor = armorRouter.Step(new EarthActionRouterFrame(
                0f, grounded: true, stableSupport: true, moveForward: 1f,
                modifierHeld: true, fieldPressed: true, fieldHeld: true));
            var surfRouter = new EarthActionRouter();
            EarthActionRoute blockedSurf = surfRouter.Step(new EarthActionRouterFrame(
                0f, grounded: true, stableSupport: true, moveForward: 1f,
                modifierHeld: true, primaryHeld: true));

            Assert.That(armor.Owner, Is.EqualTo(EarthActionOwner.Armor));
            Assert.That(armor.Consumes(EarthInputConsumption.Field), Is.True);
            Assert.That(blockedSurf.Owner, Is.EqualTo(EarthActionOwner.None));
        }

        [Test]
        public void ActiveArmorRetainsPrimaryAndForceForAimedPlateFire()
        {
            var router = new EarthActionRouter();
            router.Step(new EarthActionRouterFrame(
                0f, grounded: true, stableSupport: true,
                modifierHeld: true, fieldPressed: true, fieldHeld: true));

            EarthActionRoute singleShot = router.Step(new EarthActionRouterFrame(
                0.1f, grounded: true, stableSupport: true,
                modifierHeld: true, fieldHeld: true,
                primaryPressed: true, primaryHeld: true));
            EarthActionRoute volley = router.Step(new EarthActionRouterFrame(
                0.2f, grounded: true, stableSupport: true,
                modifierHeld: true, fieldHeld: true,
                forcePressed: true, forceHeld: true));

            Assert.That(singleShot.Owner, Is.EqualTo(EarthActionOwner.Armor));
            Assert.That(singleShot.Consumes(EarthInputConsumption.Primary), Is.True);
            Assert.That(volley.Owner, Is.EqualTo(EarthActionOwner.Armor));
            Assert.That(volley.Consumes(EarthInputConsumption.Force), Is.True);
            Assert.That(router.Owner, Is.EqualTo(EarthActionOwner.Armor),
                "LMB/RMB fire must not hand the expanded armor session to drawing or vector push.");
        }

        [Test]
        public void ActiveSessionCannotBeStolenAndCancelAlwaysWins()
        {
            var router = new EarthActionRouter();
            router.Step(new EarthActionRouterFrame(0f, forcePressed: true, forceHeld: true));
            EarthActionRoute stillPush = router.Step(new EarthActionRouterFrame(
                0.2f, modifierHeld: true, fieldPressed: true, fieldHeld: true,
                forceHeld: true, jumpPressed: true));
            EarthActionRoute canceled = router.Step(new EarthActionRouterFrame(
                0.21f, cancelPressed: true, forceHeld: true));

            Assert.That(stillPush.Owner, Is.EqualTo(EarthActionOwner.VectorField));
            Assert.That(canceled.Phase, Is.EqualTo(EarthActionRoutePhase.Cancel));
            Assert.That(router.Owner, Is.EqualTo(EarthActionOwner.None));
        }

        [Test]
        public void ShiftForwardStartsSurfFromStableMovingOrNearbySupport()
        {
            var router = new EarthActionRouter();
            EarthActionRoute route = router.Step(new EarthActionRouterFrame(
                0f,
                grounded: false,
                stableSupport: true,
                moveForward: 1f,
                modifierHeld: true));

            Assert.That(route.Owner, Is.EqualTo(EarthActionOwner.Surf));
            Assert.That(route.Phase, Is.EqualTo(EarthActionRoutePhase.Begin));
            Assert.That(route.Consumes(EarthInputConsumption.Move), Is.True);
        }
    }
}
