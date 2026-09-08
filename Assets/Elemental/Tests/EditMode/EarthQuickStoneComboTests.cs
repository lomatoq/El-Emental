using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthQuickStoneComboTests
    {
        [Test]
        public void AcceptedCommandsRunTwoPunchesTwoLegsAndFinisherThenRepeat()
        {
            var combo = new EarthQuickStoneCombo();
            for (int i = 0; i < 12; i++)
            {
                Assert.That(combo.TryBegin(i, out var beat), Is.True);
                Assert.That(beat, Is.EqualTo((EarthQuickStoneBeat)(i % 5)));
                combo.Complete(i + .9f);
            }
        }

        [Test]
        public void SpamCannotRestartActiveBeatOrBuildUnboundedQueue()
        {
            var combo = new EarthQuickStoneCombo();
            combo.TryBegin(0f, out _);
            Assert.That(combo.TryBuffer(), Is.True);
            for (int i = 0; i < 100; i++)
            {
                Assert.That(combo.TryBuffer(), Is.False);
                Assert.That(combo.TryBegin(.1f, out _), Is.False);
            }
            Assert.That(combo.ConsumeBuffered(), Is.False);
            combo.Complete(.8f);
            Assert.That(combo.ConsumeBuffered(), Is.True);
            Assert.That(combo.ConsumeBuffered(), Is.False);
            combo.TryBegin(.8f, out var next);
            Assert.That(next, Is.EqualTo(EarthQuickStoneBeat.SecondPunch));
        }

        [Test]
        public void IdleWindowStartsAfterRecoveryAndPauseResetsToBoxing()
        {
            var combo = new EarthQuickStoneCombo();
            combo.TryBegin(0f, out _);
            combo.Complete(1f);
            combo.TryBegin(1.6f, out var second);
            Assert.That(second, Is.EqualTo(EarthQuickStoneBeat.SecondPunch));
            combo.Complete(2f);
            combo.TryBegin(2.66f, out var reset);
            Assert.That(reset, Is.EqualTo(EarthQuickStoneBeat.FirstPunch));
        }

        [Test]
        public void CancellationClearsBufferedCastAndComboProgress()
        {
            var combo = new EarthQuickStoneCombo();
            combo.TryBegin(0f, out _);
            combo.TryBuffer();
            combo.Reset();
            Assert.That(combo.Active, Is.False);
            Assert.That(combo.HasBufferedCommand, Is.False);
            combo.TryBegin(.2f, out var beat);
            Assert.That(beat, Is.EqualTo(EarthQuickStoneBeat.FirstPunch));
        }

        [TestCase(EarthQuickStoneBeat.LeftKick, EarthHumanoidPoseSlot.LeftKick)]
        [TestCase(EarthQuickStoneBeat.RightKick, EarthHumanoidPoseSlot.RightKick)]
        [TestCase(EarthQuickStoneBeat.SpinKick, EarthHumanoidPoseSlot.SpinKick)]
        public void KicksHaveDedicatedSemanticSlotsAndContactBeforeRecovery(
            EarthQuickStoneBeat beat, EarthHumanoidPoseSlot slot)
        {
            Assert.That(EarthHumanoidMotionResolver.Resolve(EarthQuickStoneCombo.Technique(beat)), Is.EqualTo(slot));
            Assert.That(EarthQuickStoneCombo.ContactSeconds(beat), Is.GreaterThan(0f));
            Assert.That(EarthQuickStoneCombo.ContactSeconds(beat), Is.LessThan(EarthQuickStoneCombo.Duration(beat)));
            Assert.That(EarthMagicClipClock.MaximumSpeedForSlot((int)slot), Is.GreaterThan(0f));
            Assert.That(EarthQuickStoneCombo.RadiusScale(beat), Is.GreaterThan(1f));
        }

        [TestCase(EarthQuickStoneBeat.FirstPunch, .47f)]
        [TestCase(EarthQuickStoneBeat.LeftKick, .5f)]
        [TestCase(EarthQuickStoneBeat.SpinKick, .6f)]
        public void AuthoritativeContactMatchesClipMarker(EarthQuickStoneBeat beat, float marker)
        {
            Assert.That(EarthQuickStoneCombo.ClipTime(beat, 0f), Is.EqualTo(0f));
            Assert.That(EarthQuickStoneCombo.ClipTime(beat, EarthQuickStoneCombo.ContactSeconds(beat)), Is.EqualTo(marker).Within(.0001f));
            Assert.That(EarthQuickStoneCombo.ClipTime(beat, EarthQuickStoneCombo.Duration(beat)), Is.EqualTo(1f).Within(.0001f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidClockDoesNotAdmitCommand(float time)
        {
            var combo = new EarthQuickStoneCombo();
            Assert.That(combo.TryBegin(time, out _), Is.False);
            Assert.That(combo.Active, Is.False);
        }
    }
}
