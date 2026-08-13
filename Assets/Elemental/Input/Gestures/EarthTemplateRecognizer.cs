using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Profiling;

namespace Elemental.Input.Gestures
{
    public sealed class EarthTemplateRecognizer
    {
        private static readonly ProfilerMarker RecognizeMarker =
            new ProfilerMarker("Elemental.Earth.Gesture.Recognize");

        private readonly List<float2> _filtered = new List<float2>(192);
        private readonly List<float2> _resampled = new List<float2>(64);

        public EarthGestureResult Recognize(
            IReadOnlyList<PointerStrokeSample> samples,
            EarthGestureTemplateMask relevantTemplates,
            in EarthGestureSettings settings)
        {
            using (RecognizeMarker.Auto())
            {
                EarthGestureFeatures features = EarthGestureFeatureExtractor.Extract(
                    samples, in settings, _filtered, _resampled);
                if (!features.IsValid || features.PathLength < settings.MinimumPathLength ||
                    relevantTemplates == EarthGestureTemplateMask.None)
                    return EarthGestureResult.Invalid(features);

                EarthGestureKind best = EarthGestureKind.Invalid;
                EarthGestureKind second = EarthGestureKind.Invalid;
                float bestScore = 0f;
                float secondScore = 0f;
                Score(EarthGestureKind.Line, EarthGestureTemplateMask.Line, LineScore(features), relevantTemplates,
                    ref best, ref second, ref bestScore, ref secondScore);
                Score(EarthGestureKind.Pull, EarthGestureTemplateMask.Pull, PullScore(features), relevantTemplates,
                    ref best, ref second, ref bestScore, ref secondScore);
                Score(EarthGestureKind.Flick, EarthGestureTemplateMask.Flick, FlickScore(features), relevantTemplates,
                    ref best, ref second, ref bestScore, ref secondScore);
                Score(EarthGestureKind.Arc, EarthGestureTemplateMask.Arc, ArcScore(features), relevantTemplates,
                    ref best, ref second, ref bestScore, ref secondScore);
                Score(EarthGestureKind.ClosedContour, EarthGestureTemplateMask.ClosedContour,
                    ClosedScore(features, settings.ClosureRatio), relevantTemplates,
                    ref best, ref second, ref bestScore, ref secondScore);

                float gap = math.max(0f, bestScore - secondScore);
                bool accepted = best != EarthGestureKind.Invalid &&
                                bestScore >= settings.MinimumConfidence &&
                                gap >= settings.MinimumAmbiguityGap;
                return new EarthGestureResult(best, second, bestScore, gap, features, accepted);
            }
        }

        private static void Score(
            EarthGestureKind kind,
            EarthGestureTemplateMask flag,
            float score,
            EarthGestureTemplateMask mask,
            ref EarthGestureKind best,
            ref EarthGestureKind second,
            ref float bestScore,
            ref float secondScore)
        {
            if ((mask & flag) == 0) return;
            score = math.saturate(score);
            if (score > bestScore)
            {
                second = best;
                secondScore = bestScore;
                best = kind;
                bestScore = score;
            }
            else if (score > secondScore)
            {
                second = kind;
                secondScore = score;
            }
        }

        private static float LineScore(in EarthGestureFeatures f)
        {
            float length = math.saturate((f.PathLength - 0.018f) / 0.08f);
            float straight = math.smoothstep(0.72f, 0.98f, f.Straightness);
            float curvature = 1f - math.saturate(f.TotalCurvature / 1.35f);
            return 0.44f * straight + 0.31f * curvature + 0.25f * length;
        }

        private static float PullScore(in EarthGestureFeatures f)
        {
            float upward = math.saturate((f.Direction.y + 0.05f) / 0.95f);
            float straight = math.smoothstep(0.62f, 0.96f, f.Straightness);
            float length = math.saturate((f.DirectDistance - 0.02f) / 0.13f);
            float deliberate = 1f - math.saturate((f.Speed - 1.2f) / 2.8f);
            return 0.34f * upward + 0.28f * straight + 0.23f * length + 0.15f * deliberate;
        }

        private static float FlickScore(in EarthGestureFeatures f)
        {
            float shortDuration = 1f - math.saturate((f.Duration - 0.12f) / 0.42f);
            float speed = math.saturate((f.Speed - 0.18f) / 1.7f);
            float straight = math.smoothstep(0.55f, 0.94f, f.Straightness);
            return 0.34f * shortDuration + 0.42f * speed + 0.24f * straight;
        }

        private static float ArcScore(in EarthGestureFeatures f)
        {
            float curved = math.saturate((f.TotalCurvature - 0.22f) / 2.1f);
            float open = math.saturate((f.ClosureRatio - 0.12f) / 0.55f);
            float nonLine = 1f - math.smoothstep(0.82f, 0.98f, f.Straightness);
            float clean = 1f - math.saturate(f.SelfIntersections);
            return 0.36f * curved + 0.28f * nonLine + 0.21f * open + 0.15f * clean;
        }

        private static float ClosedScore(in EarthGestureFeatures f, float closureThreshold)
        {
            float closed = 1f - math.saturate(f.ClosureRatio / math.max(0.02f, closureThreshold));
            float area = math.saturate(math.abs(f.SignedArea) / 0.018f);
            float curved = math.saturate(f.TotalCurvature / 4.5f);
            float clean = 1f - math.saturate(f.SelfIntersections * 0.5f);
            return 0.42f * closed + 0.28f * area + 0.18f * curved + 0.12f * clean;
        }
    }
}
