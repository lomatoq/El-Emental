using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    [Flags]
    public enum EarthAnimationMetadataIssue : ushort
    {
        None = 0,
        MissingCurve = 1 << 0,
        InvalidRange = 1 << 1,
        InvalidTime = 1 << 2,
        OverlappingContacts = 1 << 3,
        NoSafeExit = 1 << 4,
        NonFiniteValue = 1 << 5
    }

    public readonly struct EarthAnimationKinematicSample
    {
        public EarthAnimationKinematicSample(
            float time01,
            float3 leftFootPosition,
            float3 rightFootPosition,
            float3 pelvisPosition,
            float3 rootPosition)
        {
            Time01 = math.saturate(math.isfinite(time01) ? time01 : 0f);
            LeftFootPosition = SelectFinite(leftFootPosition);
            RightFootPosition = SelectFinite(rightFootPosition);
            PelvisPosition = SelectFinite(pelvisPosition);
            RootPosition = SelectFinite(rootPosition);
        }

        public float Time01 { get; }
        public float3 LeftFootPosition { get; }
        public float3 RightFootPosition { get; }
        public float3 PelvisPosition { get; }
        public float3 RootPosition { get; }

        private static float3 SelectFinite(float3 value) =>
            math.select(float3.zero, value, math.isfinite(value));
    }

    public readonly struct EarthAnimationMetadataSample
    {
        public EarthAnimationMetadataSample(
            float time01,
            float leftFootContact,
            float rightFootContact,
            float leftFootPhase,
            float rightFootPhase,
            float landContact,
            float canExit,
            float pelvisCompression,
            float rootEffort)
        {
            Time01 = time01;
            LeftFootContact = leftFootContact;
            RightFootContact = rightFootContact;
            LeftFootPhase = leftFootPhase;
            RightFootPhase = rightFootPhase;
            LandContact = landContact;
            CanExit = canExit;
            PelvisCompression = pelvisCompression;
            RootEffort = rootEffort;
        }

        public float Time01 { get; }
        public float LeftFootContact { get; }
        public float RightFootContact { get; }
        public float LeftFootPhase { get; }
        public float RightFootPhase { get; }
        public float LandContact { get; }
        public float CanExit { get; }
        public float PelvisCompression { get; }
        public float RootEffort { get; }

        public float CurveValue(int index) => index switch
        {
            0 => LeftFootContact,
            1 => RightFootContact,
            2 => LeftFootPhase,
            3 => RightFootPhase,
            4 => LandContact,
            5 => CanExit,
            6 => PelvisCompression,
            7 => RootEffort,
            _ => 0f
        };
    }

    /// <summary>
    /// Stable names and deterministic analysis for continuous animation metadata.
    /// The output is presentation guidance only; physics remains landing/support
    /// authority and animation events are intentionally excluded.
    /// </summary>
    public static class EarthAnimationClipMetadata
    {
        public const int CurveCount = 8;
        public const string LeftFootContact = "LeftFootContact";
        public const string RightFootContact = "RightFootContact";
        public const string LeftFootPhase = "LeftFootPhase";
        public const string RightFootPhase = "RightFootPhase";
        public const string LandContact = "LandContact";
        public const string CanExit = "CanExit";
        public const string PelvisCompression = "PelvisCompression";
        public const string RootEffort = "RootEffort";

        public static string CurveName(int index) => index switch
        {
            0 => LeftFootContact,
            1 => RightFootContact,
            2 => LeftFootPhase,
            3 => RightFootPhase,
            4 => LandContact,
            5 => CanExit,
            6 => PelvisCompression,
            7 => RootEffort,
            _ => string.Empty
        };

        public static EarthAnimationMetadataSample[] Analyze(
            IReadOnlyList<EarthAnimationKinematicSample> source,
            bool looping,
            bool landing)
        {
            int count = source != null ? source.Count : 0;
            if (count < 2) return Array.Empty<EarthAnimationMetadataSample>();

            var result = new EarthAnimationMetadataSample[count];
            float minimumFootHeight = float.PositiveInfinity;
            float minimumPelvisHeight = float.PositiveInfinity;
            float maximumPelvisHeight = float.NegativeInfinity;
            for (int index = 0; index < count; index++)
            {
                EarthAnimationKinematicSample sample = source[index];
                minimumFootHeight = math.min(
                    minimumFootHeight,
                    math.min(sample.LeftFootPosition.y, sample.RightFootPosition.y));
                minimumPelvisHeight = math.min(minimumPelvisHeight, sample.PelvisPosition.y);
                maximumPelvisHeight = math.max(maximumPelvisHeight, sample.PelvisPosition.y);
            }

            float pelvisRange = math.max(0.04f, maximumPelvisHeight - minimumPelvisHeight);
            for (int index = 0; index < count; index++)
            {
                int previousIndex = math.max(0, index - 1);
                int nextIndex = math.min(count - 1, index + 1);
                EarthAnimationKinematicSample sample = source[index];
                EarthAnimationKinematicSample previous = source[previousIndex];
                EarthAnimationKinematicSample next = source[nextIndex];
                float timeSpan = math.max(0.0001f, next.Time01 - previous.Time01);
                float3 leftVelocity = (next.LeftFootPosition - previous.LeftFootPosition) / timeSpan;
                float3 rightVelocity = (next.RightFootPosition - previous.RightFootPosition) / timeSpan;
                float3 rootVelocity = (next.RootPosition - previous.RootPosition) / timeSpan;
                float pelvisVelocityY = (next.PelvisPosition.y - previous.PelvisPosition.y) / timeSpan;

                float leftContact = ContactConfidence(
                    sample.LeftFootPosition.y - minimumFootHeight,
                    leftVelocity);
                float rightContact = ContactConfidence(
                    sample.RightFootPosition.y - minimumFootHeight,
                    rightVelocity);
                float contactUnion = math.max(leftContact, rightContact);
                float landingWindow = landing
                    ? math.smoothstep(0.45f, 0.86f, sample.Time01)
                    : 0f;
                float descending = math.saturate((-pelvisVelocityY + 0.12f) / 1.5f);
                float landContact = landingWindow * contactUnion * math.max(0.35f, descending);
                float canExit = looping
                    ? 1f
                    : math.smoothstep(0.72f, 0.92f, sample.Time01);
                float pelvisCompression = math.saturate(
                    (maximumPelvisHeight - sample.PelvisPosition.y) / pelvisRange);
                float rootEffort = math.saturate(math.length(rootVelocity.xz) / 5f);

                result[index] = new EarthAnimationMetadataSample(
                    sample.Time01,
                    leftContact,
                    rightContact,
                    math.frac(sample.Time01),
                    math.frac(sample.Time01 + 0.5f),
                    landContact,
                    canExit,
                    pelvisCompression,
                    rootEffort);
            }
            return result;
        }

        public static EarthAnimationMetadataIssue Validate(
            IReadOnlyList<bool> curvePresence,
            IReadOnlyList<EarthAnimationMetadataSample> samples,
            bool locomotion)
        {
            EarthAnimationMetadataIssue issues = EarthAnimationMetadataIssue.None;
            if (curvePresence == null || curvePresence.Count < CurveCount)
                issues |= EarthAnimationMetadataIssue.MissingCurve;
            else
                for (int index = 0; index < CurveCount; index++)
                    if (!curvePresence[index])
                        issues |= EarthAnimationMetadataIssue.MissingCurve;

            int count = samples != null ? samples.Count : 0;
            if (count < 2)
                return issues | EarthAnimationMetadataIssue.InvalidTime;

            float previousTime = -1f;
            float overlappingDuration = 0f;
            bool hasSafeExit = false;
            for (int sampleIndex = 0; sampleIndex < count; sampleIndex++)
            {
                EarthAnimationMetadataSample sample = samples[sampleIndex];
                if (!math.isfinite(sample.Time01) || sample.Time01 < previousTime ||
                    sample.Time01 < 0f || sample.Time01 > 1f)
                    issues |= EarthAnimationMetadataIssue.InvalidTime;
                float delta = sampleIndex > 0
                    ? math.max(0f, sample.Time01 - previousTime)
                    : 0f;
                previousTime = sample.Time01;
                for (int curveIndex = 0; curveIndex < CurveCount; curveIndex++)
                {
                    float value = sample.CurveValue(curveIndex);
                    if (!math.isfinite(value))
                        issues |= EarthAnimationMetadataIssue.NonFiniteValue;
                    else if (value < -0.001f || value > 1.001f)
                        issues |= EarthAnimationMetadataIssue.InvalidRange;
                }
                if (sample.CanExit >= 0.5f) hasSafeExit = true;
                if (locomotion && sample.RootEffort > 0.2f &&
                    sample.LeftFootContact > 0.85f && sample.RightFootContact > 0.85f)
                    overlappingDuration += delta;
            }
            if (!hasSafeExit) issues |= EarthAnimationMetadataIssue.NoSafeExit;
            if (overlappingDuration > 0.18f)
                issues |= EarthAnimationMetadataIssue.OverlappingContacts;
            return issues;
        }

        private static float ContactConfidence(float heightAboveFloor, float3 velocity)
        {
            float height = 1f - math.saturate(heightAboveFloor / 0.095f);
            float horizontal = 1f - math.saturate(math.length(velocity.xz) / 1.35f);
            float vertical = 1f - math.saturate(math.abs(velocity.y) / 0.95f);
            return math.saturate(height * math.lerp(0.35f, 1f, horizontal * vertical));
        }
    }
}
