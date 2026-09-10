using Elemental.Simulation.Rendering;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthChargeFeedbackTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
        public void EveryChargeOwnerProducesTheSameCompleteTensionEnvelope(int owner)
        {
            var input = new EarthChargeFeedbackInput { Allowed = true };
            switch (owner)
            {
                case 0: input.Bend = .8f; break;
                case 1: input.PushOrGroundWave = .8f; break;
                case 2: input.VectorField = .8f; break;
                case 3: input.GravityThrow = .8f; break;
                case 4: input.FractureThrow = .8f; break;
                case 5: input.Resonance = .8f; break;
                case 6: input.Pillar = .8f; break;
                case 7: input.PillarWave = .8f; break;
                case 8: input.PillarCrest = .8f; break;
                case 9: input.Accumulation = .8f; break;
                case 10: input.WallPush = .8f; break;
                case 11: input.FireRing = .8f; break;
            }
            float charge = EarthChargeFeedback.Resolve(in input);
            Assert.That(charge, Is.EqualTo(.8f));
            var result = EarthChargeFeedback.Solve(charge, 1f, 1f, false);
            Assert.That(result.FieldOfViewDelta, Is.InRange(9f, 10f));
            Assert.That(result.PositionShake, Is.InRange(.001f, .0065f));
            Assert.That(result.RotationShake, Is.InRange(.04f, .14f));
            Assert.That(result.ChromaticAberration, Is.InRange(.25f, .3f));
            input.Allowed = false;
            Assert.That(EarthChargeFeedback.Resolve(in input), Is.Zero,
                "KO, suspended input and round transitions override every stored charge.");
        }

        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void EnvelopeRampsAndReleasesAtTheSameWallClockTime(int fps)
        {
            float current = 0f;
            for (int frame = 0; frame < fps / 2; frame++)
            {
                float next = EarthChargeFeedback.Step(current, 1f, 1f / fps);
                Assert.That(next, Is.GreaterThanOrEqualTo(current).And.LessThanOrEqualTo(1f));
                current = next;
            }
            Assert.That(current, Is.EqualTo(.993262f).Within(.00002f));
            for (int frame = 0; frame < fps * 2; frame++)
                current = EarthChargeFeedback.Step(current, 0f, 1f / fps);
            Assert.That(current, Is.Zero);
        }

        [Test]
        public void BriefAccumulationHasVisibleLensOnsetAndNoResidualAfterCancellation()
        {
            var input = new EarthChargeFeedbackInput { Allowed = true, Accumulation = .2f };
            var output = EarthChargeFeedback.Solve(EarthChargeFeedback.Resolve(in input), 1f, 1f, false);
            Assert.That(output.FieldOfViewDelta, Is.GreaterThan(3f));
            Assert.That(output.ChromaticAberration, Is.GreaterThan(.1f));
            input.Accumulation = 0f;
            Assert.That(EarthChargeFeedback.Resolve(in input), Is.Zero);
        }

        [Test]
        public void SimultaneousOwnersCannotStackBeyondTheStrongestCharge()
        {
            var input = new EarthChargeFeedbackInput { Allowed = true, Bend = .7f,
                PushOrGroundWave = .8f, GravityThrow = .9f, Pillar = .6f };
            Assert.That(EarthChargeFeedback.Resolve(in input), Is.EqualTo(.9f));
        }

        [Test]
        public void AccessibilityAndNeutralStateClearLensAndColorOffsets()
        {
            var reduced = EarthChargeFeedback.Solve(1f, .2f, 1f, true);
            Assert.That(reduced.FieldOfViewDelta, Is.Zero);
            Assert.That(reduced.ChromaticAberration, Is.Zero);
            Assert.That(reduced.RotationShake, Is.EqualTo(.028f).Within(.00001f));
            var disabled = EarthChargeFeedback.Solve(1f, 0f, 0f, false);
            Assert.That(disabled.PositionShake, Is.Zero);
            Assert.That(disabled.FieldOfViewDelta, Is.Zero);
            var neutral = EarthChargeFeedback.Solve(0f, 1f, 1f, false);
            Assert.That(neutral.FieldOfViewDelta + neutral.PositionShake +
                neutral.RotationShake + neutral.ChromaticAberration, Is.Zero);
        }

        [Test]
        public void CorruptSnapshotsDoNotPoisonTheCameraAndHitchesCannotOvershoot()
        {
            var input = new EarthChargeFeedbackInput { Allowed = true, Bend = float.NaN,
                GravityThrow = float.PositiveInfinity, Pillar = 2f };
            Assert.That(EarthChargeFeedback.Resolve(in input), Is.EqualTo(1f));
            Assert.That(EarthChargeFeedback.Step(float.NaN, 1f, 3f), Is.EqualTo(1f).Within(.00001f));
            Assert.That(EarthChargeFeedback.Step(.5f, 1f, float.NaN), Is.EqualTo(.5f));
        }

        [TestCase(false)] [TestCase(true)]
        public void CrestUsesCanonicalGestureGrowthAndClearsOnCommitOrCancel(bool cancel)
        {
            var solver = new DualMouseEarthGestureSolver();
            solver.Step(new DualMouseEarthGestureFrame(0f, true, true, false,
                true, true, false, new float2(.5f)));
            Assert.That(solver.CrestCharge01, Is.Zero, "Quick chord does not pump the camera.");
            solver.Step(new DualMouseEarthGestureFrame(.3f, false, true, false,
                false, true, false, new float2(.5f, .7f)));
            Assert.That(solver.CrestCharge01, Is.GreaterThan(.8f));
            solver.Step(new DualMouseEarthGestureFrame(.4f, false, false, true,
                false, false, true, new float2(.5f, .7f), cancel));
            Assert.That(solver.CrestCharge01, Is.Zero);
        }
    }
}
