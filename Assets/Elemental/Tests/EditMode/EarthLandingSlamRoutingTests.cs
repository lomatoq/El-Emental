using Elemental.Simulation.Bending;
using NUnit.Framework;
namespace Elemental.Tests.EditMode
{
    public sealed class EarthLandingSlamRoutingTests
    {
        [Test] public void AirborneHeldChordNeedsNoSecondSpacePress()
        {
            var router=new EarthActionRouter();
            var route=router.Step(new EarthActionRouterFrame(1,modifierHeld:true,jumpHeld:true,descending:true));
            Assert.That(route.Owner,Is.EqualTo(EarthActionOwner.LandingSlam));
            Assert.That(route.Phase,Is.EqualTo(EarthActionRoutePhase.Begin));
            Assert.That(route.Consumes(EarthInputConsumption.Jump),Is.True);
            Assert.That(route.Intent,Is.EqualTo(EarthActionIntentKind.GroundSlam));
        }
        [Test] public void AddingShiftUpgradesSpaceCushionAndLandingDoesNotCastAnotherWave()
        {
            var router=new EarthActionRouter();
            Assert.That(router.Step(new EarthActionRouterFrame(1,descending:true,jumpPressed:true,jumpHeld:true)).Owner,Is.EqualTo(EarthActionOwner.LandingCushion));
            Assert.That(router.Step(new EarthActionRouterFrame(1.1f,descending:true,modifierHeld:true,jumpHeld:true)).Owner,Is.EqualTo(EarthActionOwner.LandingSlam));
            var landed=router.Step(new EarthActionRouterFrame(1.3f,grounded:true,stableSupport:true,modifierHeld:true,jumpHeld:true));
            Assert.That(landed.Owner,Is.EqualTo(EarthActionOwner.LandingSlam));Assert.That(landed.Phase,Is.EqualTo(EarthActionRoutePhase.Continue));
            var release=router.Step(new EarthActionRouterFrame(1.4f,grounded:true,stableSupport:true,modifierHeld:true,jumpReleased:true));
            Assert.That(release.Phase,Is.EqualTo(EarthActionRoutePhase.Cancel));Assert.That(router.Owner,Is.EqualTo(EarthActionOwner.None));
        }
        [Test] public void EitherKeyReleasedDuringFallCancelsWithoutCommitting()
        {
            foreach(bool keepShift in new[]{true,false})
            {
                var router=new EarthActionRouter();router.Step(new EarthActionRouterFrame(1,modifierHeld:true,jumpHeld:true));
                var release=router.Step(new EarthActionRouterFrame(2,modifierHeld:keepShift,jumpHeld:!keepShift));
                Assert.That(release.Phase,Is.EqualTo(EarthActionRoutePhase.Cancel));Assert.That(release.Intent,Is.EqualTo(EarthActionIntentKind.Cancel));
            }
        }
        [Test] public void GroundedWaveAndActiveArmorRetainTheirOwnGrammar()
        {
            var router=new EarthActionRouter();
            var grounded=router.Step(new EarthActionRouterFrame(1,grounded:true,stableSupport:true,modifierHeld:true,jumpPressed:true,jumpHeld:true));
            Assert.That(grounded.Owner,Is.EqualTo(EarthActionOwner.ShiftSpaceChord));
            router.Reset();router.Step(new EarthActionRouterFrame(1,modifierHeld:true,fieldPressed:true,fieldHeld:true));
            var armor=router.Step(new EarthActionRouterFrame(2,modifierHeld:true,jumpHeld:true,fieldHeld:true));
            Assert.That(armor.Owner,Is.EqualTo(EarthActionOwner.Armor));
        }
    }
}
