using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Elemental.Input.Gestures;
using Elemental.Simulation.Magic;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class GestureAndMagicTests
    {
        [Test]
        public void ResamplerProducesFixedCountAndPreservesEndpoints()
        {
            var input = new List<float2>
            {
                new float2(0f, 0f),
                new float2(25f, 0f),
                new float2(100f, 0f)
            };
            var output = new List<float2>();

            GestureResampler.Resample(input, 5, output);

            Assert.That(output.Count, Is.EqualTo(5));
            Assert.That(math.distance(output[0], input[0]), Is.LessThan(0.0001f));
            Assert.That(math.distance(output[4], input[2]), Is.LessThan(0.0001f));
            Assert.That(output[2].x, Is.EqualTo(50f).Within(0.001f));
        }

        [Test]
        public void RecognizerSeparatesLinePullAndFlick()
        {
            var line = new List<float2>
            {
                new float2(0f, 0f),
                new float2(50f, 5f),
                new float2(100f, 10f)
            };
            var pull = new List<float2>
            {
                new float2(0f, 0f),
                new float2(0f, 60f)
            };
            var flick = new List<float2>
            {
                new float2(0f, 0f),
                new float2(120f, 0f)
            };

            Assert.That(GestureRecognizer.Recognize(line, 0.8f), Is.EqualTo(GestureKind.Line));
            Assert.That(GestureRecognizer.Recognize(pull, 0.8f), Is.EqualTo(GestureKind.Pull));
            Assert.That(GestureRecognizer.Recognize(flick, 0.2f), Is.EqualTo(GestureKind.Flick));
        }

        [Test]
        public void EarthGesturePolicyAcceptsFastUsefulStrokesWithoutWeakeningThrowOrder()
        {
            Assert.That(MagicGesturePolicy.Matches(GestureKind.Flick, EarthAbilityIds.LineWall), Is.True);
            Assert.That(MagicGesturePolicy.Matches(GestureKind.Flick, EarthAbilityIds.PullRock), Is.True);
            Assert.That(MagicGesturePolicy.Matches(GestureKind.Line, EarthAbilityIds.PullRock), Is.True,
                "Once Pull Rock is selected, any deliberate surface drag should be usable.");
            Assert.That(MagicGesturePolicy.Matches(GestureKind.Line, EarthAbilityIds.FlickThrow), Is.False);
            Assert.That(MagicGesturePolicy.Matches(GestureKind.Invalid, EarthAbilityIds.LineWall), Is.False);
        }

        [Test]
        public void FlickKinematicsMapsGestureSpeedToBoundedIntensity()
        {
            var stroke = new List<float2>
            {
                new float2(100f, 200f),
                new float2(220f, 200f)
            };

            Assert.That(MagicGestureKinematics.PixelsPerSecond(stroke, 0.2f), Is.EqualTo(600f).Within(0.001f));
            Assert.That(MagicGestureKinematics.FlickIntensity(stroke, 0.2f), Is.EqualTo(380f / 1180f).Within(0.001f));
            Assert.That(MagicGestureKinematics.FlickIntensity(stroke, 2f), Is.Zero);
            Assert.That(MagicGestureKinematics.FlickIntensity(stroke, 0.05f), Is.EqualTo(1f));
        }

        [Test]
        public void WallHoldDurationMapsToBoundedHeightIntensity()
        {
            Assert.That(MagicGestureKinematics.WallHoldIntensity(0.1f), Is.EqualTo(0f));
            Assert.That(MagicGestureKinematics.WallHoldIntensity(0.715f), Is.EqualTo(0.404f).Within(0.01f));
            Assert.That(MagicGestureKinematics.WallHoldIntensity(2f), Is.EqualTo(0.805f).Within(0.01f));
            Assert.That(MagicGestureKinematics.WallHoldIntensity(5f), Is.GreaterThan(0.98f));
        }

        [Test]
        public void PushChargeHasImmediateTapAndBuildsTowardFullForce()
        {
            Assert.That(MagicInputController.PushCharge(0f), Is.EqualTo(0.18f).Within(0.001f));
            Assert.That(MagicInputController.PushCharge(0.7f), Is.GreaterThan(0.65f));
            Assert.That(MagicInputController.PushCharge(3f), Is.GreaterThan(0.98f));
        }

        [Test]
        public void PullRockExtractionUsesOneSharedBoundedVolume()
        {
            var path = new List<float3> { new float3(0f, 24f, 0f) };
            var command = new MagicCommand(
                71u, 1u, ElementId.Earth, EarthAbilityIds.PullRock,
                path[0], new float3(0f, 1f, 0f), path, 0.8f, 0u, 99u);

            EarthExtractionGeometry geometry = EarthGeometryBuilder.BuildExtraction(
                in command, float3.zero, 1.2f);

            Assert.That(geometry.SurfaceAnchor.y, Is.EqualTo(24f).Within(0.001f));
            Assert.That(geometry.Center.y, Is.EqualTo(24f - (1.2f * 0.62f)).Within(0.001f));
            Assert.That(geometry.EmergencePosition.y, Is.EqualTo(24f - (1.2f * 0.18f)).Within(0.001f));
            Assert.That(geometry.Radius, Is.EqualTo(1.2f));
        }

        [Test]
        public void AuthoredGestureCorpusMeetsSizeAccuracyIntentAndFixedRecognitionContract()
        {
            List<GestureSample> samples = LoadCorpus(
                "Assets/Elemental/Tests/Replays/EarthStarterGestureCorpus.csv");
            int[,] matrix = new int[4, 4];
            var resampled = new List<float2>(12);

            for (int index = 0; index < samples.Count; index++)
            {
                GestureSample sample = samples[index];
                GestureKind actual = GestureRecognitionPipeline.Recognize(
                    sample.Points, sample.Duration, 12, resampled);
                Assert.That(resampled.Count, Is.EqualTo(12));
                matrix[(int)sample.Expected, (int)actual]++;
            }

            for (int expected = 1; expected < 4; expected++)
            {
                int authored = 0;
                int predicted = 0;
                for (int actual = 0; actual < 4; actual++) authored += matrix[expected, actual];
                for (int source = 0; source < 4; source++) predicted += matrix[source, expected];
                float recall = matrix[expected, expected] / (float)authored;
                float precision = matrix[expected, expected] / (float)predicted;
                Assert.That(authored, Is.GreaterThanOrEqualTo(100));
                Assert.That(recall, Is.GreaterThanOrEqualTo(0.95f));
                Assert.That(precision, Is.GreaterThanOrEqualTo(0.95f));
            }
            Assert.That(matrix[(int)GestureKind.Invalid, (int)GestureKind.Invalid],
                Is.GreaterThanOrEqualTo(100));

            var inactiveSampler = new PointerPathSampler();
            for (int index = 0; index < samples.Count; index++)
                for (int point = 0; point < samples[index].Points.Count; point++)
                    inactiveSampler.Sample(samples[index].Points[point]);
            Assert.That(inactiveSampler.IsActive, Is.False);
            Assert.That(inactiveSampler.Points, Is.Empty,
                "Pointer motion without the explicit cast intent must never produce a cast path.");
        }

        [Test]
        public void RecipeCompilerProducesBoundedRuntimeRecipe()
        {
            var data = new AbilityRecipeData(
                EarthAbilityIds.PullRock,
                MagicSelectorKind.PlanetSurface,
                MagicGeometryKind.AnchorSphere,
                new[] { MagicOperatorKind.SubtractSolid, MagicOperatorKind.SpawnFragment },
                1.2f,
                1f);

            CompiledAbilityRecipe compiled = new AbilityCompiler().Compile(data);

            Assert.That(compiled.Id, Is.EqualTo(EarthAbilityIds.PullRock));
            Assert.That(compiled.Operators.Length, Is.EqualTo(2));
            Assert.That(compiled.Operators[0], Is.EqualTo(MagicOperatorKind.SubtractSolid));
            Assert.That(compiled.Operators[1], Is.EqualTo(MagicOperatorKind.SpawnFragment));
        }

        [Test]
        public void WallGeometryIsSharedAndDeterministic()
        {
            var path = new List<float3>
            {
                new float3(-1f, 8f, 0f),
                new float3(0f, 8f, 0f),
                new float3(1f, 8f, 0f)
            };
            var command = new MagicCommand(
                60u,
                1u,
                ElementId.Earth,
                EarthAbilityIds.LineWall,
                path[0],
                new float3(0f, 1f, 0f),
                path,
                1f,
                0u,
                123u);

            var first = EarthGeometryBuilder.BuildWallSegments(in command, float3.zero, 2.5f);
            var second = EarthGeometryBuilder.BuildWallSegments(in command, float3.zero, 2.5f);

            Assert.That(first.Length, Is.EqualTo(5));
            Assert.That(second.Length, Is.EqualTo(first.Length));
            for (int index = 0; index < first.Length; index++)
            {
                Assert.That(math.distance(first[index].Start, second[index].Start), Is.LessThan(0.0001f));
                Assert.That(math.distance(first[index].End, second[index].End), Is.LessThan(0.0001f));
            }
        }

        [Test]
        public void WallGeometryClipsLongStrokeToBoundedPhysicalLength()
        {
            var path = new List<float3>
            {
                new float3(0f, 8f, 0f),
                new float3(5f, 8f, 0f),
                new float3(10f, 8f, 0f)
            };
            var command = new MagicCommand(
                61u,
                1u,
                ElementId.Earth,
                EarthAbilityIds.LineWall,
                path[0],
                new float3(0f, 1f, 0f),
                path,
                1f,
                0u,
                124u);

            var segments = EarthGeometryBuilder.BuildWallSegments(in command, float3.zero, 2.5f, 6f);

            Assert.That(segments.Length, Is.EqualTo(5));
            Assert.That(segments[3].Start.x, Is.EqualTo(6f).Within(0.001f));
        }

        private static GestureSample Sample(GestureKind expected, float duration, params float2[] points)
        {
            return new GestureSample(expected, duration, points);
        }

        private static List<GestureSample> LoadCorpus(string path)
        {
            string[] lines = File.ReadAllLines(path);
            var samples = new List<GestureSample>(lines.Length - 1);
            for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                string[] columns = lines[lineIndex].Split(',');
                GestureKind expected = (GestureKind)System.Enum.Parse(typeof(GestureKind), columns[0]);
                float duration = float.Parse(columns[1], CultureInfo.InvariantCulture);
                string[] encodedPoints = columns[2].Split('|');
                var points = new List<float2>(encodedPoints.Length);
                for (int pointIndex = 0; pointIndex < encodedPoints.Length; pointIndex++)
                {
                    string[] coordinates = encodedPoints[pointIndex].Split(':');
                    points.Add(new float2(
                        float.Parse(coordinates[0], CultureInfo.InvariantCulture),
                        float.Parse(coordinates[1], CultureInfo.InvariantCulture)));
                }
                samples.Add(new GestureSample(expected, duration, points));
            }
            return samples;
        }

        private readonly struct GestureSample
        {
            public GestureSample(GestureKind expected, float duration, IReadOnlyList<float2> points)
            {
                Expected = expected;
                Duration = duration;
                Points = points;
            }

            public GestureKind Expected { get; }
            public float Duration { get; }
            public IReadOnlyList<float2> Points { get; }
        }
    }
}
