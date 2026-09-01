using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthAnimationClipMetadataTests
    {
        [Test]
        public void Contract_UsesTheEightStableContinuousCurveNames()
        {
            Assert.That(EarthAnimationClipMetadata.CurveCount, Is.EqualTo(8));
            Assert.That(EarthAnimationClipMetadata.CurveName(0), Is.EqualTo("LeftFootContact"));
            Assert.That(EarthAnimationClipMetadata.CurveName(7), Is.EqualTo("RootEffort"));
        }

        [Test]
        public void Analyzer_ProducesFiniteBoundedMetadataAndSafeExit()
        {
            EarthAnimationMetadataSample[] result = EarthAnimationClipMetadata.Analyze(
                BuildWalkSamples(),
                false,
                false);

            Assert.That(result, Has.Length.EqualTo(5));
            Assert.That(result[0].LeftFootContact, Is.GreaterThan(result[0].RightFootContact));
            Assert.That(result[2].RightFootContact, Is.GreaterThan(result[2].LeftFootContact));
            Assert.That(result[4].CanExit, Is.EqualTo(1f).Within(0.001f));
            Assert.That(EarthAnimationClipMetadata.Validate(
                AllCurvesPresent(), result, false), Is.EqualTo(EarthAnimationMetadataIssue.None));
        }

        [Test]
        public void Analyzer_RemovesIndependentRigHeightOffsetsAndDerivesOpposedFootPhases()
        {
            EarthAnimationKinematicSample[] source = BuildWalkSamples();
            var offset = new EarthAnimationKinematicSample[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                EarthAnimationKinematicSample sample = source[index];
                offset[index] = new EarthAnimationKinematicSample(
                    sample.Time01,
                    sample.LeftFootPosition + new float3(0f, 0.18f, 0f),
                    sample.RightFootPosition,
                    sample.PelvisPosition,
                    sample.RootPosition);
            }

            EarthAnimationMetadataSample[] result = EarthAnimationClipMetadata.Analyze(
                offset,
                true,
                false);

            Assert.That(result[0].LeftFootContact, Is.GreaterThan(result[0].RightFootContact));
            Assert.That(result[2].RightFootContact, Is.GreaterThan(result[2].LeftFootContact));
            Assert.That(CircularDistance01(result[0].LeftFootPhase, 0f), Is.LessThan(0.13f));
            Assert.That(CircularDistance01(result[2].RightFootPhase, 0f), Is.LessThan(0.13f));
        }

        [Test]
        public void Validator_ReportsMissingCurvesInvalidRangesAndNoExit()
        {
            var samples = new[]
            {
                Sample(0f, 0.5f, 0f),
                new EarthAnimationMetadataSample(1f, 2f, 0f, 0f, 0.5f, 0f, 0f, 0f, 0f)
            };
            bool[] presence = AllCurvesPresent();
            presence[4] = false;

            EarthAnimationMetadataIssue issue = EarthAnimationClipMetadata.Validate(
                presence,
                samples,
                false);

            Assert.That(issue.HasFlag(EarthAnimationMetadataIssue.MissingCurve), Is.True);
            Assert.That(issue.HasFlag(EarthAnimationMetadataIssue.InvalidRange), Is.True);
            Assert.That(issue.HasFlag(EarthAnimationMetadataIssue.NoSafeExit), Is.True);
        }

        [Test]
        public void Validator_RejectsImpossibleLongDoublePlantDuringLocomotion()
        {
            var samples = new[]
            {
                Sample(0f, 1f, 1f, 1f, 1f),
                Sample(0.25f, 1f, 1f, 1f, 1f),
                Sample(0.5f, 0f, 1f, 1f, 1f),
                Sample(1f, 0f, 1f, 1f, 1f)
            };

            EarthAnimationMetadataIssue issue = EarthAnimationClipMetadata.Validate(
                AllCurvesPresent(),
                samples,
                true);

            Assert.That(issue.HasFlag(EarthAnimationMetadataIssue.OverlappingContacts), Is.True);
        }

        private static EarthAnimationKinematicSample[] BuildWalkSamples() => new[]
        {
            Kinematic(0f, 0f, 0.14f, 1.0f, 0f),
            Kinematic(0.25f, 0.03f, 0.08f, 0.96f, 0.25f),
            Kinematic(0.5f, 0.14f, 0f, 0.92f, 0.5f),
            Kinematic(0.75f, 0.08f, 0.03f, 0.96f, 0.75f),
            Kinematic(1f, 0f, 0.14f, 1.0f, 1f)
        };

        private static EarthAnimationKinematicSample Kinematic(
            float time,
            float leftHeight,
            float rightHeight,
            float pelvisHeight,
            float rootZ) =>
            new EarthAnimationKinematicSample(
                time,
                new float3(0f, leftHeight, time),
                new float3(0f, rightHeight, 1f - time),
                new float3(0f, pelvisHeight, rootZ),
                new float3(0f, 0f, rootZ));

        private static EarthAnimationMetadataSample Sample(
            float time,
            float leftContact,
            float canExit,
            float rightContact = 0f,
            float rootEffort = 0f) =>
            new EarthAnimationMetadataSample(
                time,
                leftContact,
                rightContact,
                time,
                math.frac(time + 0.5f),
                0f,
                canExit,
                0f,
                rootEffort);

        private static bool[] AllCurvesPresent() =>
            new[] { true, true, true, true, true, true, true, true };

        private static float CircularDistance01(float a, float b)
        {
            float difference = math.abs(a - b);
            return math.min(difference, 1f - difference);
        }
    }
}
