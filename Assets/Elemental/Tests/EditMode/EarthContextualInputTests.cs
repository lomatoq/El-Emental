using System.Collections.Generic;
using System.IO;
using Elemental.Input.Gestures;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthContextualInputTests
    {
        private readonly EarthTemplateRecognizer _recognizer = new EarthTemplateRecognizer();
        private readonly List<PointerStrokeSample> _samples = new List<PointerStrokeSample>(64);

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3440, 1440)]
        public void SameWallGestureIsResolutionIndependentAndFixedCount(int width, int height)
        {
            BuildScreenStroke(width, height, 0.8f,
                new float2(0.18f, 0.42f),
                new float2(0.38f, 0.43f),
                new float2(0.62f, 0.45f),
                new float2(0.84f, 0.46f));

            EarthGestureSettings settings = EarthGestureSettings.Default;
            EarthGestureResult result = _recognizer.Recognize(
                _samples, EarthGestureTemplateMask.Structures, in settings);

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Best, Is.EqualTo(EarthGestureKind.Line));
            Assert.That(result.Features.SampleCount, Is.EqualTo(32));
            Assert.That(result.Confidence01, Is.GreaterThanOrEqualTo(settings.MinimumConfidence));
        }

        [Test]
        public void ArcAndClosedContourResolveToPlatform()
        {
            BuildNormalizedStroke(1.05f,
                new float2(0.2f, 0.3f),
                new float2(0.25f, 0.56f),
                new float2(0.5f, 0.72f),
                new float2(0.75f, 0.56f),
                new float2(0.8f, 0.3f));
            EarthGestureSettings settings = EarthGestureSettings.Default;
            EarthGestureResult arc = _recognizer.Recognize(
                _samples, EarthGestureTemplateMask.Structures, in settings);
            EarthInputContext context = TerrainPrimary();
            EarthResolvedIntent arcIntent = EarthIntentResolver.Resolve(in context, in arc);
            Assert.That(arc.Accepted, Is.True);
            Assert.That(arc.Best, Is.EqualTo(EarthGestureKind.Arc));
            Assert.That(arcIntent.Kind, Is.EqualTo(EarthIntentKind.RaisePlatform));

            BuildNormalizedStroke(1.2f,
                new float2(0.3f, 0.3f),
                new float2(0.7f, 0.3f),
                new float2(0.7f, 0.7f),
                new float2(0.3f, 0.7f),
                new float2(0.3f, 0.3f));
            EarthGestureResult closed = _recognizer.Recognize(
                _samples, EarthGestureTemplateMask.Structures, in settings);
            EarthResolvedIntent closedIntent = EarthIntentResolver.Resolve(in context, in closed);
            Assert.That(closed.Accepted, Is.True);
            Assert.That(closed.Best, Is.EqualTo(EarthGestureKind.ClosedContour));
            Assert.That(closedIntent.Kind, Is.EqualTo(EarthIntentKind.RaisePlatform));
        }

        [Test]
        public void AmbiguousNBestResultRejectsWithoutIntent()
        {
            BuildNormalizedStroke(0.7f,
                new float2(0.42f, 0.2f),
                new float2(0.44f, 0.45f),
                new float2(0.46f, 0.75f));
            var strict = new EarthGestureSettings(32, 0.18f, 0.02f, 0.16f, 0.45f, 0.5f);
            EarthGestureResult result = _recognizer.Recognize(
                _samples,
                EarthGestureTemplateMask.Line | EarthGestureTemplateMask.Pull,
                in strict);

            Assert.That(result.Best, Is.Not.EqualTo(EarthGestureKind.Invalid));
            Assert.That(result.Accepted, Is.False);
            Assert.That(result.AmbiguityGap, Is.LessThan(strict.MinimumAmbiguityGap));
            EarthInputContext context = TerrainPrimary();
            EarthResolvedIntent intent = EarthIntentResolver.Resolve(in context, in result);
            Assert.That(intent.Accepted, Is.False);
            Assert.That(intent.Kind, Is.EqualTo(EarthIntentKind.None));
            Assert.That(intent.Reticle, Is.EqualTo(EarthReticleState.Ambiguous));
        }

        [Test]
        public void ContextGateResolvesSessionsAndFieldBeforeTemplates()
        {
            var active = new EarthInputContext(
                EarthSourceKind.Rock, true, true, false, false, false);
            Assert.That(EarthIntentResolver.NeedsGestureRecognition(in active), Is.False);
            EarthGestureResult invalid = EarthGestureResult.Invalid();
            EarthResolvedIntent manipulation = EarthIntentResolver.Resolve(in active, in invalid);
            Assert.That(manipulation.Kind, Is.EqualTo(EarthIntentKind.Manipulate));

            var repair = new EarthInputContext(
                EarthSourceKind.BrokenStructure, false, false, false, true, false);
            Assert.That(EarthIntentResolver.NeedsGestureRecognition(in repair), Is.False);
            EarthResolvedIntent repairIntent = EarthIntentResolver.Resolve(in repair, in invalid);
            Assert.That(repairIntent.Kind, Is.EqualTo(EarthIntentKind.Repair));
        }

        [Test]
        public void InvalidOrObstructedSourceRejectsSafely()
        {
            EarthGestureResult invalidGesture = EarthGestureResult.Invalid();
            var invalid = new EarthInputContext(
                EarthSourceKind.Invalid, false, true, false, false, false);
            var obstructed = new EarthInputContext(
                EarthSourceKind.Terrain, false, true, false, false, false, false, true);

            EarthResolvedIntent first = EarthIntentResolver.Resolve(in invalid, in invalidGesture);
            EarthResolvedIntent second = EarthIntentResolver.Resolve(in obstructed, in invalidGesture);
            Assert.That(first.Accepted, Is.False);
            Assert.That(first.Reticle, Is.EqualTo(EarthReticleState.Invalid));
            Assert.That(second.Accepted, Is.False);
            Assert.That(second.Reticle, Is.EqualTo(EarthReticleState.Obstructed));
        }

        [Test]
        public void StrokeSamplerIgnoresHoverAndStoresOnlyNormalizedSamples()
        {
            var sampler = new EarthStrokeSampler();
            sampler.Sample(new float2(600f, -20f), 0f);
            Assert.That(sampler.Samples, Is.Empty);

            sampler.Begin(new float2(1.5f, -0.4f), 1f);
            sampler.Sample(new float2(0.6f, 0.5f), 1.2f);
            sampler.End(new float2(0.8f, 0.7f), 1.4f);

            Assert.That(sampler.Samples.Count, Is.EqualTo(3));
            for (int index = 0; index < sampler.Samples.Count; index++)
            {
                float2 point = sampler.Samples[index].ViewportPosition01;
                Assert.That(point.x, Is.InRange(0f, 1f));
                Assert.That(point.y, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void RecognitionLoopAllocatesNothingAfterWarmup()
        {
            BuildNormalizedStroke(0.8f,
                new float2(0.1f, 0.4f),
                new float2(0.4f, 0.42f),
                new float2(0.7f, 0.44f),
                new float2(0.9f, 0.45f));
            EarthGestureSettings settings = EarthGestureSettings.Default;
            for (int index = 0; index < 16; index++)
                _recognizer.Recognize(_samples, EarthGestureTemplateMask.Structures, in settings);

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            uint digest = 0u;
            for (int index = 0; index < 256; index++)
                digest ^= _recognizer.Recognize(
                    _samples, EarthGestureTemplateMask.Structures, in settings).Features.GeometryDigest;
            long after = System.GC.GetAllocatedBytesForCurrentThread();

            Assert.That(digest, Is.EqualTo(0u));
            Assert.That(after - before, Is.Zero);
        }

        [Test]
        public void ResolvedReplayCommandUsesQuantizedGeometryWithoutRawStroke()
        {
            BuildNormalizedStroke(0.8f,
                new float2(0.12f, 0.31f),
                new float2(0.5f, 0.42f),
                new float2(0.88f, 0.54f));
            var geometry = new List<uint2>(32);
            EarthInputCommandQuantizer.QuantizeViewportGeometry(_samples, geometry);
            var command = new EarthResolvedInputCommand(
                EarthIntentKind.RaiseWall,
                17u,
                3u,
                geometry,
                EarthInputCommandQuantizer.Quantize01(0.75f),
                EarthInputCommandQuantizer.Quantize01(0.4f),
                EarthInputModifierFlags.Modifier,
                100u,
                120u,
                912u,
                42u);

            Assert.That(command.QuantizedGeometry.Count, Is.EqualTo(3));
            Assert.That(command.QuantizedGeometry[0].x, Is.EqualTo((uint)math.round(0.12f * 65535f)));
            Assert.That(command.SourceStableId, Is.EqualTo(17u));
            Assert.That(command.SourceGeneration, Is.EqualTo(3u));
            Assert.That(command.ReleaseTick, Is.EqualTo(120u));
            Assert.That(command.Modifiers, Is.EqualTo(EarthInputModifierFlags.Modifier));
        }

        [Test]
        public void CanonicalActionMapAndDeviceBoundaryStayExplicit()
        {
            string actions = File.ReadAllText("Assets/Elemental/Input/Actions/Gameplay.inputactions");
            string[] required =
            {
                "BendPrimary", "BendForce", "BendField", "BendModifier",
                "JumpOrStomp", "BendParameter", "ShoulderSwap", "Cancel"
            };
            for (int index = 0; index < required.Length; index++)
                StringAssert.Contains($"\"name\": \"{required[index]}\"", actions);

            string[] runtimeFiles = Directory.GetFiles(
                "Assets/Elemental", "*.cs", SearchOption.AllDirectories);
            for (int index = 0; index < runtimeFiles.Length; index++)
            {
                string path = runtimeFiles[index].Replace('\\', '/');
                if (path.EndsWith("EarthInputAdapter.cs") ||
                    path.Contains("/Tests/") ||
                    path.Contains("/Authoring/Editor/")) continue;
                string source = File.ReadAllText(runtimeFiles[index]);
                StringAssert.DoesNotContain("Mouse.current", source, path);
                StringAssert.DoesNotContain("Keyboard.current", source, path);
                StringAssert.DoesNotContain("Gamepad.current", source, path);
                StringAssert.DoesNotContain("Touchscreen.current", source, path);
            }
        }

        private void BuildScreenStroke(int width, int height, float duration, params float2[] viewportPoints)
        {
            _samples.Clear();
            for (int index = 0; index < viewportPoints.Length; index++)
            {
                float2 pixels = viewportPoints[index] * new float2(width, height);
                float2 normalized = pixels / new float2(width, height);
                float time = duration * index / math.max(1, viewportPoints.Length - 1);
                _samples.Add(new PointerStrokeSample(normalized, time));
            }
        }

        private void BuildNormalizedStroke(float duration, params float2[] points)
        {
            _samples.Clear();
            for (int index = 0; index < points.Length; index++)
            {
                float time = duration * index / math.max(1, points.Length - 1);
                _samples.Add(new PointerStrokeSample(points[index], time));
            }
        }

        private static EarthInputContext TerrainPrimary() => new EarthInputContext(
            EarthSourceKind.Terrain, false, true, false, false, false);
    }
}
