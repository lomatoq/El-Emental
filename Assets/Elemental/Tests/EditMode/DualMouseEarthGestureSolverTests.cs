using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class DualMouseEarthGestureSolverTests
    {
        [Test]
        public void FirstButtonIsBufferedAndFallsBackAfterEightyMilliseconds()
        {
            var solver = new DualMouseEarthGestureSolver();
            DualMouseEarthGestureResult pending = solver.Step(new DualMouseEarthGestureFrame(
                0f, true, true, false, false, false, false, new float2(0.5f)));
            DualMouseEarthGestureResult fallback = solver.Step(new DualMouseEarthGestureFrame(
                0.081f, false, true, false, false, false, false, new float2(0.5f)));
            Assert.That(pending.Kind, Is.EqualTo(DualMouseEarthResultKind.Pending));
            Assert.That(fallback.Kind, Is.EqualTo(DualMouseEarthResultKind.FallbackPrimary));
        }

        [Test]
        public void QuickChordReleaseCommitsStompStone()
        {
            var solver = new DualMouseEarthGestureSolver();
            solver.Step(new DualMouseEarthGestureFrame(
                0f, true, true, false, false, false, false, new float2(0.5f)));
            solver.Step(new DualMouseEarthGestureFrame(
                0.04f, false, true, false, true, true, false, new float2(0.505f, 0.504f)));
            DualMouseEarthGestureResult result = solver.Step(new DualMouseEarthGestureFrame(
                0.16f, false, false, true, false, false, true, new float2(0.51f, 0.51f)));
            Assert.That(result.Kind, Is.EqualTo(DualMouseEarthResultKind.StompStone));
        }

        [TestCase(0.08f, 1)]
        [TestCase(0.12f, 3)]
        [TestCase(0.20f, 5)]
        [TestCase(0.28f, 7)]
        public void UpwardHeldGestureMapsToOddCrestCounts(float travel, int expected)
        {
            var solver = new DualMouseEarthGestureSolver();
            solver.Step(new DualMouseEarthGestureFrame(
                0f, true, true, false, true, true, false, new float2(0.5f)));
            solver.Step(new DualMouseEarthGestureFrame(
                0.22f, false, true, false, false, true, false, new float2(0.5f, 0.5f + travel)));
            DualMouseEarthGestureResult result = solver.Step(new DualMouseEarthGestureFrame(
                0.24f, false, false, true, false, false, true, new float2(0.5f, 0.5f + travel)));
            Assert.That(result.Kind, Is.EqualTo(DualMouseEarthResultKind.PillarCrest));
            Assert.That(result.CrestCount, Is.EqualTo(expected));
        }

        [Test]
        public void HeldGestureCommitsCrestAtAnyDirection()
        {
            var solver = new DualMouseEarthGestureSolver();
            solver.Step(new DualMouseEarthGestureFrame(
                0f, true, true, false, true, true, false, new float2(0.5f)));
            DualMouseEarthGestureResult result = solver.Step(new DualMouseEarthGestureFrame(
                0.25f, false, false, true, false, false, true, new float2(0.58f, 0.55f)));
            Assert.That(result.Kind, Is.EqualTo(DualMouseEarthResultKind.PillarCrest));
            Assert.That(result.Direction.x, Is.GreaterThan(0.7f));
            Assert.That(result.StartPointer, Is.EqualTo(new float2(0.5f)));
            Assert.That(result.EndPointer, Is.EqualTo(new float2(0.58f, 0.55f)));
        }

        [Test]
        public void UpwardSwipeStillCommitsAfterSmallPointerSettle()
        {
            var solver = new DualMouseEarthGestureSolver();
            solver.Step(new DualMouseEarthGestureFrame(
                0f, true, true, false, true, true, false, new float2(0.5f)));
            solver.Step(new DualMouseEarthGestureFrame(
                0.18f, false, true, false, false, true, false, new float2(0.52f, 0.62f)));
            DualMouseEarthGestureResult result = solver.Step(new DualMouseEarthGestureFrame(
                0.21f, false, false, true, false, false, true, new float2(0.515f, 0.59f)));
            Assert.That(result.Kind, Is.EqualTo(DualMouseEarthResultKind.PillarCrest));
            Assert.That(result.CrestCount, Is.EqualTo(3));
        }

        [Test]
        public void StompStoneRisesHoversThenLaunchesOnAuthoredTiming()
        {
            Assert.That(
                EarthStompStoneSequenceSolver.Evaluate(0.27f).Phase,
                Is.EqualTo(EarthStompStonePhase.Rising));
            Assert.That(
                EarthStompStoneSequenceSolver.Evaluate(0.28f).Phase,
                Is.EqualTo(EarthStompStonePhase.Hovering));
            Assert.That(
                EarthStompStoneSequenceSolver.Evaluate(0.52f).Phase,
                Is.EqualTo(EarthStompStonePhase.Hovering));
            Assert.That(
                EarthStompStoneSequenceSolver.Evaluate(0.53f).Phase,
                Is.EqualTo(EarthStompStonePhase.Launch));
        }

        [Test]
        public void CrestPillarsOverlapAndRiseNearestToFarthest()
        {
            EarthPillarCrestLayoutSample first = EarthPillarCrestLayoutSolver.Sample(0, 5);
            EarthPillarCrestLayoutSample second = EarthPillarCrestLayoutSolver.Sample(1, 5);
            EarthPillarCrestLayoutSample last = EarthPillarCrestLayoutSolver.Sample(4, 5);
            Assert.That(second.ForwardOffset - first.ForwardOffset, Is.LessThan(first.Width));
            Assert.That(first.StartDelay, Is.Zero);
            Assert.That(second.StartDelay, Is.GreaterThan(first.StartDelay));
            Assert.That(last.StartDelay, Is.GreaterThan(second.StartDelay));
        }
    }
}
