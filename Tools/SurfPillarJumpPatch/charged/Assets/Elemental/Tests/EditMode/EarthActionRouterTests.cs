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

        [TestCase(true)]
        [TestCase(false)]
        public void BufferedSingleButtonTapCommitsWithoutLeavingAStaleOwner(bool primary)
        {
            var router = new EarthActionRouter();
            EarthActionRoute route = router.Step(new EarthActionRouterFrame(
                1f,
                primaryPressed: primary,
                primaryHeld: false,
                primaryReleased: primary,
                forcePressed: !primary,
                forceHeld: false,
                forceReleased: !primary));

            Assert.That(route.Owner, Is.EqualTo(primary
                ? EarthActionOwner.Primary
                : EarthActionOwner.VectorField));
            Assert.That(route.Phase, Is.EqualTo(EarthActionRoutePhase.Commit));
            Assert.That(router.Owner, Is.EqualTo(EarthActionOwner.None),
                "A tap replayed after the dual-button window must not steal the next wall or push.");
        }

        [Test]
        public void SameUpdateJumpTap_CommitsWhenReleaseEdgeHasAlreadyExpired()
        {
            var router = new EarthActionRouter();
            EarthActionRoute begin = router.Step(new EarthActionRouterFrame(
                4f,
                grounded: true,
                stableSupport: true,
                descending: true,
                jumpPressed: true,
                jumpHeld: false,
                jumpReleased: true));
            EarthActionRoute commit = router.Step(new EarthActionRouterFrame(
                4.016f,
                grounded: true,
                stableSupport: true,
                descending: true,
                jumpHeld: false));

            Assert.That(begin.Owner, Is.EqualTo(EarthActionOwner.Pillar));
            Assert.That(begin.Intent, Is.EqualTo(EarthActionIntentKind.PillarCharge));
            Assert.That(begin.Phase, Is.EqualTo(EarthActionRoutePhase.Begin));
            Assert.That(commit.Owner, Is.EqualTo(EarthActionOwner.Pillar));
            Assert.That(commit.Phase, Is.EqualTo(EarthActionRoutePhase.Commit));
            Assert.That(router.Owner, Is.EqualTo(EarthActionOwner.None),
                "A sub-frame Space tap must not leave a phantom pillar charge running after the key is up.");
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

        [Test]
        public void SpaceDuringSurfChargesUntilReleaseAndCommitsExactlyOnePillarJump()
        {
            var router = new EarthActionRouter();
            EarthActionRoute begin = router.Step(new EarthActionRouterFrame(
                1f,
                grounded: true,
                stableSupport: true,
                moveForward: 1f,
                modifierHeld: true));
            EarthActionRoute pressed = router.Step(new EarthActionRouterFrame(
                1.4f,
                grounded: false,
                stableSupport: true,
                moveForward: 1f,
                modifierHeld: true,
                jumpPressed: true,
                jumpHeld: true));
            EarthActionRoute held = router.Step(new EarthActionRouterFrame(
                2.125f,
                grounded: false,
                stableSupport: true,
                moveForward: 1f,
                modifierHeld: true,
                jumpHeld: true));
            EarthActionRoute released = router.Step(new EarthActionRouterFrame(
                2.85f,
                grounded: false,
                stableSupport: true,
                moveForward: 1f,
                modifierHeld: true,
                jumpReleased: true));
            EarthActionRoute staleRelease = router.Step(new EarthActionRouterFrame(
                2.866f,
                grounded: false,
                stableSupport: false,
                moveForward: 0f,
                modifierHeld: false,
                jumpReleased: true));

            Assert.That(begin.Owner, Is.EqualTo(EarthActionOwner.Surf));
            Assert.That(pressed.Owner, Is.EqualTo(EarthActionOwner.Surf));
            Assert.That(pressed.Phase, Is.EqualTo(EarthActionRoutePhase.Begin));
            Assert.That(pressed.Intent, Is.EqualTo(EarthActionIntentKind.PillarJump));
            Assert.That(pressed.Consumes(EarthInputConsumption.Jump), Is.True);
            Assert.That(held.Phase, Is.EqualTo(EarthActionRoutePhase.Continue));
            Assert.That(held.Intent, Is.EqualTo(EarthActionIntentKind.PillarJump));
            Assert.That(held.Charge01, Is.InRange(0.45f, 0.55f));
            Assert.That(released.Phase, Is.EqualTo(EarthActionRoutePhase.Commit));
            Assert.That(released.Intent, Is.EqualTo(EarthActionIntentKind.PillarJump));
            Assert.That(released.Charge01, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(router.Owner, Is.EqualTo(EarthActionOwner.None));
            Assert.That(staleRelease.HasOwner, Is.False,
                "The release edge belongs to the already committed surf trick and must not start another action.");
        }

        [Test]
        public void SurfPillarChargeCancelsWithoutLaunchWhenSurfSupportIsLost()
        {
            var router = new EarthActionRouter();
            router.Step(new EarthActionRouterFrame(
                1f,
                grounded: true,
                stableSupport: true,
                moveForward: 1f,
                modifierHeld: true));
            EarthActionRoute pressed = router.Step(new EarthActionRouterFrame(
                1.1f,
                stableSupport: true,
                moveForward: 1f,
                modifierHeld: true,
                jumpPressed: true,
                jumpHeld: true));
            EarthActionRoute lost = router.Step(new EarthActionRouterFrame(
                1.6f,
                stableSupport: false,
                moveForward: 1f,
                modifierHeld: true,
                jumpHeld: true));

            Assert.That(pressed.Intent, Is.EqualTo(EarthActionIntentKind.PillarJump));
            Assert.That(lost.Phase, Is.EqualTo(EarthActionRoutePhase.Commit));
            Assert.That(lost.Intent, Is.EqualTo(EarthActionIntentKind.Surf),
                "Losing the moving support must end surf without replaying a charged pillar later.");
            Assert.That(router.Owner, Is.EqualTo(EarthActionOwner.None));
        }
    }
}
