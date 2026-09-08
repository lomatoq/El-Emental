using Elemental.Simulation.Bending;
using Elemental.Simulation.Networking;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthWallPushRoutingTests
    {
        [Test] public void ControlUpgradesExistingVectorFieldAndHoldsOnlyOneOwner()
        {
            var router = new EarthActionRouter();
            Assert.That(router.Step(new EarthActionRouterFrame(0, forcePressed:true, forceHeld:true)).Owner, Is.EqualTo(EarthActionOwner.VectorField));
            var begin = router.Step(new EarthActionRouterFrame(.1f, forceHeld:true, wallPushModifierHeld:true));
            Assert.That(begin.Owner, Is.EqualTo(EarthActionOwner.WallPush));
            Assert.That(begin.Phase, Is.EqualTo(EarthActionRoutePhase.Begin));
            Assert.That(begin.Consumes(EarthInputConsumption.Force | EarthInputConsumption.WallPushModifier), Is.True);
            Assert.That(begin.Consumes(EarthInputConsumption.Modifier), Is.False);
            for (int i=0;i<100;i++)
                Assert.That(router.Step(new EarthActionRouterFrame(.2f+i*.02f, forceHeld:true, wallPushModifierHeld:true)).Phase, Is.EqualTo(EarthActionRoutePhase.Continue));
        }
        [TestCase(true)] [TestCase(false)] public void ControlReleaseCancelsButMouseReleaseCommits(bool control)
        {
            var router = new EarthActionRouter();
            router.Step(new EarthActionRouterFrame(0, forceHeld:true, wallPushModifierHeld:true));
            var release = router.Step(new EarthActionRouterFrame(.1f, forceHeld:control, wallPushModifierHeld:!control));
            Assert.That(release.Phase, Is.EqualTo(control ? EarthActionRoutePhase.Cancel : EarthActionRoutePhase.Commit));
            Assert.That(router.Owner, Is.EqualTo(EarthActionOwner.None));
        }
        [Test] public void HeldChargeOnlyCommitsOnceOnMouseRelease()
        {
            var router=new EarthActionRouter();
            router.Step(new EarthActionRouterFrame(0,forceHeld:true,wallPushModifierHeld:true));
            for(int i=1;i<=60;i++)Assert.That(router.Step(new EarthActionRouterFrame(i*.02f,forceHeld:true,wallPushModifierHeld:true)).Phase,Is.EqualTo(EarthActionRoutePhase.Continue));
            Assert.That(router.Step(new EarthActionRouterFrame(1.3f,forceReleased:true,wallPushModifierHeld:true)).Phase,Is.EqualTo(EarthActionRoutePhase.Commit));
            Assert.That(router.Step(new EarthActionRouterFrame(1.4f,wallPushModifierHeld:true)).Phase,Is.EqualTo(EarthActionRoutePhase.None));
        }
        [Test] public void CancellationCannotRepeatUntilChordReleased()
        {
            var router = new EarthActionRouter();
            router.Step(new EarthActionRouterFrame(0, forceHeld:true, wallPushModifierHeld:true));
            router.Step(new EarthActionRouterFrame(.1f, cancelPressed:true, forceHeld:true, wallPushModifierHeld:true));
            router.Reset(); // Runtime stun/disable path also resets ownership.
            Assert.That(router.Step(new EarthActionRouterFrame(.2f, forceHeld:true, wallPushModifierHeld:true)).Owner, Is.EqualTo(EarthActionOwner.None));
            router.Step(new EarthActionRouterFrame(.3f));
            Assert.That(router.Step(new EarthActionRouterFrame(.4f, forceHeld:true, wallPushModifierHeld:true)).Phase, Is.EqualTo(EarthActionRoutePhase.Begin));
        }
        [Test] public void OrdinaryForceAndActiveTechniquesKeepTheirMeaning()
        {
            var router = new EarthActionRouter();
            Assert.That(router.Step(new EarthActionRouterFrame(0,forcePressed:true,forceHeld:true)).Owner, Is.EqualTo(EarthActionOwner.VectorField));
            router.Reset();
            router.Step(new EarthActionRouterFrame(.1f,primaryPressed:true,primaryHeld:true));
            Assert.That(router.Step(new EarthActionRouterFrame(.2f,primaryHeld:true,forceHeld:true,wallPushModifierHeld:true)).Owner, Is.EqualTo(EarthActionOwner.Primary));
        }
        [Test] public void ShiftIsNotControlAndVolleyCannotBeStolen()
        {
            var router = new EarthActionRouter();
            Assert.That(router.Step(new EarthActionRouterFrame(0,modifierHeld:true,forcePressed:true,forceHeld:true,wallPushModifierHeld:true)).Owner, Is.Not.EqualTo(EarthActionOwner.WallPush));
            router.Reset();
            Assert.That(router.Step(new EarthActionRouterFrame(.1f,forceHeld:true,wallPushModifierHeld:true,resonanceVolleyActive:true)).Owner, Is.Not.EqualTo(EarthActionOwner.WallPush));
        }
        [Test] public void SemanticFrameAcceptsDistinctControlBitButRejectsUnknownBits()
        {
            var frame = new EarthSemanticInputFrame {Sequence=1, Held=EarthInputBits.WallPushModifier|EarthInputBits.Force,
                CameraRotation=quaternion.identity, FieldOfView=60, Aspect=1.6f, PointerViewport=new float2(.5f)};
            Assert.That(frame.Valid, Is.True);
            Assert.That((frame.Held & EarthInputBits.Modifier), Is.EqualTo(EarthInputBits.None));
            frame.Held |= (EarthInputBits)4096;
            Assert.That(frame.Valid, Is.False);
        }
        [Test] public void OnlyPendingSingleForceCanYieldFromDualMouse()
        {
            var solver = new DualMouseEarthGestureSolver();
            solver.Step(new DualMouseEarthGestureFrame(0,false,false,false,true,true,false,new float2(.5f)));
            Assert.That(solver.CanYieldPendingForce, Is.True);
            solver.Step(new DualMouseEarthGestureFrame(.01f,true,true,false,false,true,false,new float2(.5f)));
            Assert.That(solver.CanYieldPendingForce, Is.False);
        }
    }
}
