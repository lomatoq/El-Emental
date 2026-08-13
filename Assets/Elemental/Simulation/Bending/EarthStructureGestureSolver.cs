using System.Collections.Generic;
using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public enum EarthStructureGestureKind : byte
    {
        Invalid = 0,
        Wall = 1,
        Platform = 2
    }

    public readonly struct EarthStructureGestureResult
    {
        public EarthStructureGestureResult(
            EarthStructureGestureKind kind,
            float straightness,
            float signedTurnRadians)
        {
            Kind = kind;
            Straightness = straightness;
            SignedTurnRadians = signedTurnRadians;
        }

        public EarthStructureGestureKind Kind { get; }
        public float Straightness { get; }
        public float SignedTurnRadians { get; }
    }

    public static class EarthStructureGestureSolver
    {
        public static EarthStructureGestureResult Classify(IReadOnlyList<float2> points)
        {
            if (points == null || points.Count < 2)
                return new EarthStructureGestureResult(EarthStructureGestureKind.Invalid, 0f, 0f);

            float pathLength = 0f;
            float signedTurn = 0f;
            float2 previousDirection = float2.zero;
            bool hasPreviousDirection = false;
            for (int index = 1; index < points.Count; index++)
            {
                float2 segment = points[index] - points[index - 1];
                float length = math.length(segment);
                if (!math.isfinite(length) || length <= 0.00001f) continue;
                pathLength += length;
                float2 direction = segment / length;
                if (hasPreviousDirection)
                {
                    float cross = (previousDirection.x * direction.y) -
                                  (previousDirection.y * direction.x);
                    float dot = math.clamp(math.dot(previousDirection, direction), -1f, 1f);
                    signedTurn += math.atan2(cross, dot);
                }
                previousDirection = direction;
                hasPreviousDirection = true;
            }

            float chord = math.distance(points[0], points[points.Count - 1]);
            if (pathLength <= 0.00001f)
                return new EarthStructureGestureResult(EarthStructureGestureKind.Invalid, 0f, 0f);
            float straightness = math.saturate(chord / pathLength);

            float2 a = points[0];
            float2 b = points[points.Count - 1];
            float2 ab = b - a;
            float abLength = math.length(ab);
            float maximumDeviation = 0f;
            if (abLength > 0.00001f)
            {
                for (int index = 1; index < points.Count - 1; index++)
                {
                    float2 ap = points[index] - a;
                    float distance = math.abs((ab.x * ap.y) - (ab.y * ap.x)) / abLength;
                    maximumDeviation = math.max(maximumDeviation, distance);
                }
            }

            bool line = straightness >= 0.91f &&
                        maximumDeviation <= math.max(6f, pathLength * 0.075f) &&
                        math.abs(signedTurn) <= 0.48f;
            return new EarthStructureGestureResult(
                line ? EarthStructureGestureKind.Wall : EarthStructureGestureKind.Platform,
                straightness,
                signedTurn);
        }
    }
}
