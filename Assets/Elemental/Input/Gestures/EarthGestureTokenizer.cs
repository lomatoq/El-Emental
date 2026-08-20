using System.Collections.Generic;
using Elemental.Simulation.Bending;
using Unity.Mathematics;

namespace Elemental.Input.Gestures
{
    public sealed class EarthGestureTokenizer
    {
        private readonly EarthTemplateRecognizer _recognizer = new EarthTemplateRecognizer();
        private float _lastTapAt = -100f;
        private float2 _lastTapPoint;

        public EarthGestureToken Tokenize(
            IReadOnlyList<PointerStrokeSample> samples,
            float commitTime,
            in EarthGestureSettings settings,
            in EarthGestureTargetContext pointerDownTarget,
            in EarthGestureTargetContext commitTarget)
        {
            EarthGestureResult recognized = _recognizer.Recognize(
                samples, EarthGestureTemplateMask.All, in settings);
            EarthGestureFeatures f = recognized.Features;
            if (samples == null || samples.Count == 0) return default;
            float peakSpeed = 0f;
            float peakAcceleration = 0f;
            float previousSpeed = 0f;
            int reversals = 0;
            float2 previousDirection = float2.zero;
            for (int index = 1; index < samples.Count; index++)
            {
                float dt = math.max(0.0001f, samples[index].Time - samples[index - 1].Time);
                float2 delta = samples[index].ViewportPosition01 - samples[index - 1].ViewportPosition01;
                float speed = math.length(delta) / dt;
                peakSpeed = math.max(peakSpeed, speed);
                if (index > 1)
                    peakAcceleration = math.max(peakAcceleration, math.abs(speed - previousSpeed) / dt);
                previousSpeed = speed;
                float2 direction = math.normalizesafe(delta);
                if (math.lengthsq(previousDirection) > 0.5f && math.dot(previousDirection, direction) < -0.45f)
                    reversals++;
                if (math.lengthsq(direction) > 0.5f) previousDirection = direction;
            }

            EarthGestureTokenKind kind;
            float confidence;
            float2 lastPoint = samples[samples.Count - 1].ViewportPosition01;
            if (f.Duration >= 0.32f && f.PathLength <= 0.012f)
            {
                kind = EarthGestureTokenKind.BraceStillness;
                confidence = math.saturate((f.Duration - 0.25f) / 0.35f + 0.55f);
            }
            else if (f.Duration <= 0.19f && f.PathLength <= 0.018f)
            {
                bool doubleTap = commitTime - _lastTapAt <= 0.34f &&
                                 math.distance(lastPoint, _lastTapPoint) <= 0.035f;
                kind = doubleTap ? EarthGestureTokenKind.DoubleTap : EarthGestureTokenKind.Tap;
                confidence = 1f - math.saturate(f.PathLength / 0.018f) * 0.25f;
                _lastTapAt = commitTime;
                _lastTapPoint = lastPoint;
            }
            else if (f.Duration >= 0.20f && f.PathLength <= 0.025f)
            {
                kind = EarthGestureTokenKind.Hold;
                confidence = math.saturate(0.65f + (f.Duration - 0.2f) * 0.8f);
            }
            else if (reversals > 0 && f.PathLength >= 0.035f)
            {
                kind = EarthGestureTokenKind.DirectionReversal;
                confidence = math.saturate(0.62f + reversals * 0.12f);
            }
            else if (peakSpeed >= 1.35f && f.Duration <= 0.42f && f.Straightness >= 0.58f)
            {
                kind = EarthGestureTokenKind.Flick;
                confidence = math.saturate(0.55f + (peakSpeed - 1.35f) * 0.18f);
            }
            else if (recognized.Best == EarthGestureKind.ClosedContour)
            {
                bool circleLike = f.TotalCurvature >= 4.2f && f.ClosureRatio <= settings.ClosureRatio * 1.35f;
                kind = circleLike
                    ? f.SignedArea < 0f ? EarthGestureTokenKind.CircleCW : EarthGestureTokenKind.CircleCCW
                    : EarthGestureTokenKind.ClosedLoop;
                confidence = recognized.Confidence01;
            }
            else if (recognized.Best == EarthGestureKind.Arc)
            {
                kind = EarthGestureTokenKind.DragArc;
                confidence = recognized.Confidence01;
            }
            else
            {
                kind = EarthGestureTokenKind.DragLinear;
                confidence = math.max(recognized.Confidence01, math.saturate(f.Straightness));
            }
            var tokenFeatures = new EarthGestureTokenFeatures(
                f.Duration, f.PathLength, f.Straightness, f.TotalCurvature,
                f.SignedArea, f.Direction, f.ClosureRatio, f.GeometryDigest);
            return new EarthGestureToken(
                kind, confidence, in tokenFeatures, peakSpeed, peakAcceleration, reversals,
                in pointerDownTarget, in commitTarget, default);
        }

        public static EarthGestureToken FromScroll(
            in Elemental.Simulation.Bending.EarthScrollState scroll,
            in EarthGestureTargetContext target)
        {
            EarthGestureTokenKind kind = scroll.OverscrollConfirmed
                ? EarthGestureTokenKind.ScrollOverscrollConfirm
                : scroll.DirectionReversal
                    ? EarthGestureTokenKind.DirectionReversal
                    : scroll.FastFlick
                        ? scroll.NormalizedDelta >= 0f
                            ? EarthGestureTokenKind.ScrollFlickUp
                            : EarthGestureTokenKind.ScrollFlickDown
                        : scroll.NormalizedDelta > 0f
                            ? EarthGestureTokenKind.ScrollPulseUp
                            : scroll.NormalizedDelta < 0f
                                ? EarthGestureTokenKind.ScrollPulseDown
                                : EarthGestureTokenKind.Invalid;
            float confidence = kind == EarthGestureTokenKind.Invalid
                ? 0f
                : math.saturate(0.62f + math.abs(scroll.Velocity) * 0.035f);
            EarthGestureTokenFeatures features = default;
            return new EarthGestureToken(kind, confidence, in features,
                math.abs(scroll.Velocity), 0f, scroll.ReversalCount, in target, in target, default);
        }
    }
}
