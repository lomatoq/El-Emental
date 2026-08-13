using System;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Input.Gestures
{
    public readonly struct PointerStrokeSample
    {
        public PointerStrokeSample(float2 viewportPosition01, float time, float pressure01 = 1f)
        {
            ViewportPosition01 = math.saturate(viewportPosition01);
            Time = math.isfinite(time) ? time : 0f;
            Pressure01 = math.saturate(pressure01);
        }

        public float2 ViewportPosition01 { get; }
        public float Time { get; }
        public float Pressure01 { get; }
    }

    public enum EarthGestureKind : byte
    {
        Invalid = 0,
        Line = 1,
        Pull = 2,
        Flick = 3,
        Arc = 4,
        ClosedContour = 5
    }

    [Flags]
    public enum EarthGestureTemplateMask : byte
    {
        None = 0,
        Line = 1 << 0,
        Pull = 1 << 1,
        Flick = 1 << 2,
        Arc = 1 << 3,
        ClosedContour = 1 << 4,
        Structures = Line | Arc | ClosedContour,
        Manipulation = Pull | Flick,
        All = Line | Pull | Flick | Arc | ClosedContour
    }

    public readonly struct EarthGestureFeatures
    {
        public EarthGestureFeatures(
            int sampleCount,
            float pathLength,
            float directDistance,
            float straightness,
            float2 direction,
            float totalCurvature,
            float signedArea,
            float closureRatio,
            float speed,
            float duration,
            float aspectRatio,
            int selfIntersections,
            uint geometryDigest)
        {
            SampleCount = sampleCount;
            PathLength = pathLength;
            DirectDistance = directDistance;
            Straightness = straightness;
            Direction = direction;
            TotalCurvature = totalCurvature;
            SignedArea = signedArea;
            ClosureRatio = closureRatio;
            Speed = speed;
            Duration = duration;
            AspectRatio = aspectRatio;
            SelfIntersections = selfIntersections;
            GeometryDigest = geometryDigest;
        }

        public int SampleCount { get; }
        public float PathLength { get; }
        public float DirectDistance { get; }
        public float Straightness { get; }
        public float2 Direction { get; }
        public float TotalCurvature { get; }
        public float SignedArea { get; }
        public float ClosureRatio { get; }
        public float Speed { get; }
        public float Duration { get; }
        public float AspectRatio { get; }
        public int SelfIntersections { get; }
        public uint GeometryDigest { get; }
        public bool IsValid => SampleCount >= 2 && PathLength > 0.0001f;
    }

    public readonly struct EarthGestureResult
    {
        public EarthGestureResult(
            EarthGestureKind best,
            EarthGestureKind secondBest,
            float confidence01,
            float ambiguityGap,
            EarthGestureFeatures features,
            bool accepted)
        {
            Best = best;
            SecondBest = secondBest;
            Confidence01 = math.saturate(confidence01);
            AmbiguityGap = math.saturate(ambiguityGap);
            Features = features;
            Accepted = accepted;
        }

        public EarthGestureKind Best { get; }
        public EarthGestureKind SecondBest { get; }
        public float Confidence01 { get; }
        public float AmbiguityGap { get; }
        public EarthGestureFeatures Features { get; }
        public bool Accepted { get; }

        public static EarthGestureResult Invalid(EarthGestureFeatures features = default) =>
            new EarthGestureResult(
                EarthGestureKind.Invalid,
                EarthGestureKind.Invalid,
                0f,
                0f,
                features,
                false);
    }

    public readonly struct EarthGestureSettings
    {
        public EarthGestureSettings(
            int resampleCount,
            float smoothing,
            float minimumPathLength,
            float closureRatio,
            float minimumConfidence,
            float minimumAmbiguityGap)
        {
            ResampleCount = math.clamp(resampleCount, 16, 64);
            Smoothing = math.saturate(smoothing);
            MinimumPathLength = math.max(0.002f, minimumPathLength);
            ClosureRatio = math.clamp(closureRatio, 0.02f, 0.5f);
            MinimumConfidence = math.saturate(minimumConfidence);
            MinimumAmbiguityGap = math.saturate(minimumAmbiguityGap);
        }

        public int ResampleCount { get; }
        public float Smoothing { get; }
        public float MinimumPathLength { get; }
        public float ClosureRatio { get; }
        public float MinimumConfidence { get; }
        public float MinimumAmbiguityGap { get; }

        public static EarthGestureSettings Default => new EarthGestureSettings(
            32, 0.18f, 0.025f, 0.16f, 0.58f, 0.075f);
    }

    [CreateAssetMenu(menuName = "Elemental/Input/Earth Gesture Profile", fileName = "EarthGestureProfile")]
    public sealed class EarthGestureProfile : ScriptableObject
    {
        [SerializeField, Range(16, 64)] private int resampleCount = 32;
        [SerializeField, Range(0f, 0.5f)] private float smoothing = 0.18f;
        [SerializeField, Range(0.002f, 0.2f)] private float minimumPathLength = 0.025f;
        [SerializeField, Range(0.02f, 0.5f)] private float closureRatio = 0.16f;
        [SerializeField, Range(0f, 1f)] private float minimumConfidence = 0.58f;
        [SerializeField, Range(0f, 0.5f)] private float minimumAmbiguityGap = 0.075f;

        public EarthGestureSettings Settings => new EarthGestureSettings(
            resampleCount,
            smoothing,
            minimumPathLength,
            closureRatio,
            minimumConfidence,
            minimumAmbiguityGap);
    }
}
