using System.Collections.Generic;
using Elemental.Input.Gestures;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthInputGrammarV4Tests
    {
        [Test]
        public void DetentAndTrackpadNormalizeToSamePhysicalDirection()
        {
            var wheel = new EarthScrollAccumulator(EarthScrollDeviceProfile.DetentWheel);
            var trackpad = new EarthScrollAccumulator(EarthScrollDeviceProfile.SmoothTrackpad);
            EarthScrollState a = wheel.Step(120f, 1f / 60f, 1f);
            EarthScrollState b = trackpad.Step(10f, 1f / 60f, 1f);

            Assert.That(a.NormalizedDelta, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(b.NormalizedDelta, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(a.QuantizedSteps, Is.EqualTo(1));
            Assert.That(b.QuantizedSteps, Is.EqualTo(1));
        }

        [Test]
        public void WheelReversalAndConfirmedOverscrollAreDistinctTokens()
        {
            var scroll = new EarthScrollAccumulator();
            EarthScrollState up = scroll.Step(120f, 0.02f, 1f);
            EarthScrollState reverse = scroll.Step(-120f, 0.02f, 1.12f);
            scroll.Reset(1f);
            EarthScrollState arm = scroll.Step(120f, 0.02f, 2f, 0f, 1f);
            EarthScrollState confirm = scroll.Step(120f, 0.02f, 2.12f, 0f, 1f);

            Assert.That(up.DirectionReversal, Is.False);
            Assert.That(reverse.DirectionReversal, Is.True);
            Assert.That(arm.OverscrollConfirmed, Is.False);
            Assert.That(confirm.OverscrollConfirmed, Is.True);
        }

        [Test]
        public void TokenizerSeparatesTapDoubleTapFlickAndCircleDirection()
        {
            var tokenizer = new EarthGestureTokenizer();
            EarthGestureSettings settings = EarthGestureSettings.Default;
            EarthGestureTargetContext target = default;
            var tap = new List<PointerStrokeSample>
            {
                new PointerStrokeSample(new float2(0.5f), 0f),
                new PointerStrokeSample(new float2(0.502f, 0.5f), 0.08f)
            };
            EarthGestureToken first = tokenizer.Tokenize(tap, 0.08f, in settings, in target, in target);
            EarthGestureToken second = tokenizer.Tokenize(tap, 0.28f, in settings, in target, in target);
            var flick = new List<PointerStrokeSample>
            {
                new PointerStrokeSample(new float2(0.2f, 0.5f), 0f),
                new PointerStrokeSample(new float2(0.7f, 0.5f), 0.12f)
            };
            EarthGestureToken flickToken = tokenizer.Tokenize(flick, 1f, in settings, in target, in target);
            List<PointerStrokeSample> circle = Circle(clockwise: true);
            EarthGestureToken circleToken = tokenizer.Tokenize(circle, 2f, in settings, in target, in target);

            Assert.That(first.Kind, Is.EqualTo(EarthGestureTokenKind.Tap));
            Assert.That(second.Kind, Is.EqualTo(EarthGestureTokenKind.DoubleTap));
            Assert.That(flickToken.Kind, Is.EqualTo(EarthGestureTokenKind.Flick));
            Assert.That(circleToken.Kind, Is.EqualTo(EarthGestureTokenKind.CircleCW));
        }

        [Test]
        public void RankedIntentPrefersContinuityTarget()
        {
            EarthGestureTokenFeatures features = default;
            var down = new EarthGestureTargetContext(42u, 1u, (ushort)((1 << 0) | (1 << 4)));
            var token = new EarthGestureToken(
                EarthGestureTokenKind.Tap, 0.9f, in features, 0f, 0, in down, in down);
            var context = new EarthIntentContext(down.Capabilities, true, true, true, 42u);
            var candidates = new EarthIntentCandidate[8];

            int count = EarthRankedIntentResolver.ResolveNonAlloc(in token, in context, candidates);

            Assert.That(count, Is.GreaterThanOrEqualTo(2));
            Assert.That(candidates[0].Intent, Is.EqualTo(EarthActionIntentKind.FullBend));
            Assert.That(candidates[0].Score, Is.GreaterThan(candidates[1].Score));
        }

        [Test]
        public void TokenizerKeepsRawFlickAccelerationAndProjectedDirection()
        {
            var tokenizer = new EarthGestureTokenizer();
            EarthGestureSettings settings = EarthGestureSettings.Default;
            EarthGestureTargetContext target = default;
            var samples = new List<PointerStrokeSample>
            {
                new PointerStrokeSample(new float2(0.20f, 0.50f), 0.00f),
                new PointerStrokeSample(new float2(0.22f, 0.50f), 0.08f),
                new PointerStrokeSample(new float2(0.72f, 0.50f), 0.12f)
            };

            EarthGestureToken token = tokenizer.Tokenize(samples, 0.12f, in settings, in target, in target)
                .WithWorldProjectedDirection(new float3(2f, 0f, 0f));

            Assert.That(token.PeakAcceleration, Is.GreaterThan(0f));
            Assert.That(math.length(token.WorldProjectedDirection), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(token.WorldProjectedDirection.x, Is.GreaterThan(0.99f));
        }

        private static List<PointerStrokeSample> Circle(bool clockwise)
        {
            var output = new List<PointerStrokeSample>(33);
            for (int index = 0; index <= 32; index++)
            {
                float phase = index / 32f * math.PI * 2f * (clockwise ? -1f : 1f);
                output.Add(new PointerStrokeSample(
                    new float2(0.5f + math.cos(phase) * 0.18f, 0.5f + math.sin(phase) * 0.18f),
                    index * 0.018f));
            }
            return output;
        }
    }
}
