using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class SeptemberPivotContactTests
    {
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void RealContinuousPivotConfidenceTransfersWithoutRequiringAnAbsentZero(int fps)
        {
            EarthFootContactState left = default, right = default;
            var l = Foot(true, .81f, 0f, .02f, fps);
            var r = Foot(false, .54f, 0f, .02f, fps);
            EarthFootContactSolver.ResolvePair(ref left, ref right, in l, in r);
            Assert.That(left.Locked, Is.True);
            // Actual source curve at phase .60: first foot still .65 (> generic
            // .22 release), but the opposite eligible foot has taken the load.
            l = Foot(true, .65f, 0f, .02f, fps);
            r = Foot(false, .84f, 0f, .02f, fps);
            var transferred = EarthFootContactSolver.ResolvePair(ref left, ref right, in l, in r);
            Assert.That(transferred.Left.Locked, Is.False);
            Assert.That(transferred.Left.TargetWeight, Is.Zero);
            Assert.That(transferred.Right.Locked, Is.True);
        }

        [Test]
        public void MirroredNamedFootChannelsSwapContactsAndIndependentPhasesTogether()
        {
            float lp = .1f, rp = .6f, lc = .8f, rc = .3f;
            EarthAnimationClipMetadata.ResolveMirroredFootChannels(true, ref lp, ref rp, ref lc, ref rc);
            Assert.That(lp, Is.EqualTo(.6f)); Assert.That(rp, Is.EqualTo(.1f));
            Assert.That(lc, Is.EqualTo(.3f)); Assert.That(rc, Is.EqualTo(.8f));
        }

        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void PivotTransfersPlantWhenAuthoredFootLifts(int fps)
        {
            EarthFootContactState left = default, right = default;
            var l = Foot(true, 1f, 0f, .02f, fps);
            var r = Foot(false, 0f, 0f, .02f, fps);
            var first = EarthFootContactSolver.ResolvePair(ref left, ref right, in l, in r);
            Assert.That(first.Left.Locked, Is.True);
            l = Foot(true, 0f, 0f, .18f, fps);
            r = Foot(false, 1f, 0f, .02f, fps);
            var next = EarthFootContactSolver.ResolvePair(ref left, ref right, in l, in r);
            Assert.That(next.Left.Locked, Is.False, "An authored swing must not remain pinned during a turn.");
            Assert.That(next.Right.Locked, Is.True);
        }

        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void PivotReleasesBeforeAnchorCrossesLegReach(int fps)
        {
            EarthFootContactState left = default, right = default;
            var l = Foot(true, 1f, 0f, .02f, fps);
            var r = Foot(false, 0f, 0f, .02f, fps);
            EarthFootContactSolver.ResolvePair(ref left, ref right, in l, in r);
            l = Foot(true, 1f, .45f, .02f, fps);
            var next = EarthFootContactSolver.ResolvePair(ref left, ref right, in l, in r);
            Assert.That(next.Left.Locked, Is.False, "Old pivot exemption pinned an arbitrarily distant ankle.");
        }

        private static EarthFootContactInput Foot(bool left, float contact, float travel, float clearance, int fps) =>
            new EarthFootContactInput(left, true, true, true, false, true, clearance, 0f,
                left ? 1f : 0f, 0f, new float3(left ? -.1f : .1f, 0f, travel),
                math.up(), new float3(left ? -.1f : .1f, clearance, travel), math.up(),
                11u, 1u, 1f / fps, contact);
    }
}
