using Elemental.Simulation.Networking;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class OnlinePauseInputTests
    {
        private static EarthSemanticInputFrame ActiveFrame => new EarthSemanticInputFrame
        {
            Sequence = 81, Tick = 400, Held = EarthInputBits.Primary | EarthInputBits.Jump | EarthInputBits.Modifier,
            Pressed = EarthInputBits.Primary | EarthInputBits.Jump, Move = new float2(.7f, .2f), Scroll = 120,
            PointerViewport = new float2(.2f, .7f), CameraPosition = new float3(2, 60, 3),
            CameraRotation = quaternion.Euler(.1f, .2f, .3f), FieldOfView = 70, Aspect = 16f / 9
        };
        [Test]
        public void OpeningPauseReleasesHeldControlsAndCancelsWithoutTurningReleaseIntoTapJump()
        {
            var before = ActiveFrame;
            var paused = EarthSemanticInputSuppression.Apply(before, before.Held, true);
            Assert.That(paused.Valid, Is.True);
            Assert.That(paused.Held, Is.EqualTo(EarthInputBits.None));
            Assert.That(paused.Pressed, Is.EqualTo(EarthInputBits.Cancel));
            Assert.That(paused.Released, Is.EqualTo(before.Held));
            Assert.That(paused.Move, Is.EqualTo(float2.zero)); Assert.That(paused.Scroll, Is.Zero);
            Assert.That(paused.CameraPosition, Is.EqualTo(before.CameraPosition));
            Assert.That(paused.CameraRotation, Is.EqualTo(before.CameraRotation));
            Assert.That(paused.Sequence, Is.EqualTo(before.Sequence)); Assert.That(paused.Tick, Is.EqualTo(before.Tick));
            Assert.That(before.Held, Is.Not.EqualTo(EarthInputBits.None), "Suppression cannot mutate the already captured source input.");
        }
        [Test]
        public void ContinuedPausedTrafficCarriesNoReplayedCancelReleaseOrMovement()
        {
            var before = ActiveFrame;
            var paused = EarthSemanticInputSuppression.Apply(before, before.Held, false);
            Assert.That(paused.Valid, Is.True);
            Assert.That(paused.Held | paused.Pressed | paused.Released, Is.EqualTo(EarthInputBits.None));
            Assert.That(paused.Move, Is.EqualTo(float2.zero)); Assert.That(paused.Scroll, Is.Zero);
        }
    }
}

