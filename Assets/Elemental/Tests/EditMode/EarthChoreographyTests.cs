using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Matter;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthChoreographyTests
    {
        [Test]
        public void HeavyWallCommitUsesRootedDialectAndBoundedPoseHold()
        {
            var request = new BendingPoseRequest(
                EarthTechniqueId.RaiseWall,
                EarthCastPhase.Strike,
                new float3(1f, 0.2f, 0.5f),
                new float3(0f, 1f, 0f),
                640f,
                0.92f,
                1f,
                0.35f,
                false,
                new EarthMatterId(7u, 1));

            EarthChoreographySample sample = EarthChoreographySolver.Solve(in request);

            Assert.That(sample.Dialect, Is.EqualTo(EarthBendingDialect.RootedPower));
            Assert.That(sample.StanceWidth01, Is.GreaterThan(0.7f));
            Assert.That(sample.PelvisCompression01, Is.GreaterThan(0.7f));
            Assert.That(sample.PoseHoldSeconds, Is.InRange(0.025f, 0.08f));
            Assert.That(math.abs(math.dot(request.ActionAxis, request.LocalUp)), Is.LessThan(0.0001f));
        }

        [Test]
        public void PrecisionGripUsesCompactDialectWithoutNonCommitHold()
        {
            var request = new BendingPoseRequest(
                EarthTechniqueId.PullStone,
                EarthCastPhase.Sustain,
                new float3(0f, 0f, 1f),
                new float3(0f, 1f, 0f),
                16f,
                0.62f,
                1f,
                0.95f,
                true,
                default);

            EarthChoreographySample sample = EarthChoreographySolver.Solve(in request);

            Assert.That(sample.Dialect, Is.EqualTo(EarthBendingDialect.CompactTactile));
            Assert.That(sample.PoseHoldSeconds, Is.Zero);
            Assert.That(sample.UpperBodyWeight01, Is.GreaterThan(0.5f));
        }

        [Test]
        public void VisualPoseConsumesEveryDeclaredChoreographyChannel()
        {
            EarthChoreographyPoseOffset baseline = EarthChoreographyVisualSolver.Solve(
                EarthTechniqueId.ThrowStone, EarthCastPhase.Strike,
                EarthBendingDialect.CompactTactile, .45f, .35f, .4f, .3f, false);

            AssertDifferent(baseline, EarthChoreographyVisualSolver.Solve(
                EarthTechniqueId.ThrowStone, EarthCastPhase.Strike,
                EarthBendingDialect.CompactTactile, .9f, .35f, .4f, .3f, false), "EarthEffort");
            AssertDifferent(baseline, EarthChoreographyVisualSolver.Solve(
                EarthTechniqueId.ThrowStone, EarthCastPhase.Strike,
                EarthBendingDialect.CompactTactile, .45f, .9f, .4f, .3f, false), "EarthBrace");
            AssertDifferent(baseline, EarthChoreographyVisualSolver.Solve(
                EarthTechniqueId.ThrowStone, EarthCastPhase.Strike,
                EarthBendingDialect.CompactTactile, .45f, .35f, .9f, .3f, false), "EarthGrounding");
            AssertDifferent(baseline, EarthChoreographyVisualSolver.Solve(
                EarthTechniqueId.ThrowStone, EarthCastPhase.Strike,
                EarthBendingDialect.CompactTactile, .45f, .35f, .4f, .9f, false), "EarthPrecision");
            AssertDifferent(baseline, EarthChoreographyVisualSolver.Solve(
                EarthTechniqueId.ThrowStone, EarthCastPhase.Load,
                EarthBendingDialect.CompactTactile, .45f, .35f, .4f, .3f, false), "EarthPhase");
            AssertDifferent(baseline, EarthChoreographyVisualSolver.Solve(
                EarthTechniqueId.ThrowStone, EarthCastPhase.Strike,
                EarthBendingDialect.RootedPower, .45f, .35f, .4f, .3f, false), "EarthDialect");
        }

        [Test]
        public void ElevenSemanticSlotsHaveFiniteDistinctBoundedUpperBodySignatures()
        {
            EarthTechniqueId[] techniques =
            {
                EarthTechniqueId.RaiseWall, EarthTechniqueId.RaisePlatform,
                EarthTechniqueId.PullStone, EarthTechniqueId.ThrowStone,
                EarthTechniqueId.VectorPush, EarthTechniqueId.Repair,
                EarthTechniqueId.Resonance, EarthTechniqueId.PillarJump,
                EarthTechniqueId.Armor, EarthTechniqueId.ArmorBarrage,
                EarthTechniqueId.MeteorFinish
            };
            var poses = new EarthChoreographyPoseOffset[techniques.Length];
            for (int index = 0; index < techniques.Length; index++)
            {
                var request = new BendingPoseRequest(
                    techniques[index], EarthCastPhase.Strike, new float3(0f, 0f, 1f),
                    new float3(0f, 1f, 0f), 120f, .82f, .9f, .64f,
                    (index & 1) == 0, default);
                EarthChoreographySample sample = EarthChoreographySolver.Solve(in request);
                poses[index] = EarthChoreographyVisualSolver.Solve(
                    request.Technique, request.Phase, sample.Dialect, request.Effort01,
                    sample.StanceWidth01, request.Grounding01, request.Precision01,
                    request.LeftDominant);
                Assert.That(poses[index].IsFinite, Is.True, techniques[index].ToString());
                Assert.That(math.cmax(math.abs(poses[index].ChestEuler)),
                    Is.LessThanOrEqualTo(EarthChoreographyVisualSolver.MaximumChestDegrees));
                Assert.That(math.cmax(math.abs(poses[index].HeadEuler)),
                    Is.LessThanOrEqualTo(EarthChoreographyVisualSolver.MaximumHeadDegrees));
                Assert.That(math.cmax(math.abs(poses[index].LeftShoulderEuler)),
                    Is.LessThanOrEqualTo(EarthChoreographyVisualSolver.MaximumShoulderDegrees));
                Assert.That(math.cmax(math.abs(poses[index].RightShoulderEuler)),
                    Is.LessThanOrEqualTo(EarthChoreographyVisualSolver.MaximumShoulderDegrees));
            }
            for (int left = 0; left < poses.Length; left++)
            for (int right = left + 1; right < poses.Length; right++)
                Assert.That(Distance(poses[left], poses[right]), Is.GreaterThan(.08f),
                    $"{techniques[left]} and {techniques[right]} collapsed to the same correction.");

            Assert.That(EarthChoreographyVisualSolver.Solve(
                EarthTechniqueId.None, EarthCastPhase.Idle, EarthBendingDialect.CompactTactile,
                1f, 1f, 1f, 1f, false).MaximumAbsDegrees, Is.Zero);
        }

        private static void AssertDifferent(
            in EarthChoreographyPoseOffset left,
            in EarthChoreographyPoseOffset right,
            string channel) => Assert.That(Distance(left, right), Is.GreaterThan(.01f),
                $"{channel} remains dead presentation data.");

        private static float Distance(
            in EarthChoreographyPoseOffset left,
            in EarthChoreographyPoseOffset right) =>
            math.length(left.ChestEuler - right.ChestEuler) +
            math.length(left.HeadEuler - right.HeadEuler) +
            math.length(left.LeftShoulderEuler - right.LeftShoulderEuler) +
            math.length(left.RightShoulderEuler - right.RightShoulderEuler);
    }
}
