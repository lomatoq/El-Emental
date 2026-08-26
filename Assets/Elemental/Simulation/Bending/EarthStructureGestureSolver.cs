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
            float signedTurnRadians,
            float pathLength = 0f,
            float closureDistance = 0f,
            float enclosedArea = 0f)
        {
            Kind = kind;
            Straightness = straightness;
            SignedTurnRadians = signedTurnRadians;
            PathLength = pathLength;
            ClosureDistance = closureDistance;
            EnclosedArea = enclosedArea;
        }

        public EarthStructureGestureKind Kind { get; }
        public float Straightness { get; }
        public float SignedTurnRadians { get; }
        public float PathLength { get; }
        public float ClosureDistance { get; }
        public float EnclosedArea { get; }
    }

    public static class EarthStructureGestureSolver
    {
        public const float MinimumStrokeLengthViewport = 0.012f;
        public const float PlatformClosureViewport = 0.025f;
        public const float PlatformMinimumPathViewport = 0.10f;
        public const float PlatformMinimumAreaViewport = 0.0012f;

        public static EarthStructureGestureResult Classify(IReadOnlyList<float2> points) =>
            Classify(points, new float2(1f, 1f));

        public static EarthStructureGestureResult Classify(
            IReadOnlyList<float2> points,
            float2 coordinateExtent)
        {
            if (points == null || points.Count < 2)
                return new EarthStructureGestureResult(EarthStructureGestureKind.Invalid, 0f, 0f);

            float pathLength = 0f;
            float signedTurn = 0f;
            float signedAreaTwice = 0f;
            float2 safeExtent = math.max(coordinateExtent, new float2(0.0001f));
            float2 previousDirection = float2.zero;
            bool hasPreviousDirection = false;
            for (int index = 1; index < points.Count; index++)
            {
                float2 previous = points[index - 1] / safeExtent;
                float2 current = points[index] / safeExtent;
                float2 segment = current - previous;
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
                signedAreaTwice += previous.x * current.y - current.x * previous.y;
            }

            float2 first = points[0] / safeExtent;
            float2 last = points[points.Count - 1] / safeExtent;
            float chord = math.distance(first, last);
            if (pathLength <= 0.00001f)
                return new EarthStructureGestureResult(EarthStructureGestureKind.Invalid, 0f, 0f);
            float straightness = math.saturate(chord / pathLength);

            float2 a = first;
            float2 b = last;
            float2 ab = b - a;
            float abLength = math.length(ab);
            float maximumDeviation = 0f;
            if (abLength > 0.00001f)
            {
                for (int index = 1; index < points.Count - 1; index++)
                {
                    float2 ap = points[index] / safeExtent - a;
                    float distance = math.abs((ab.x * ap.y) - (ab.y * ap.x)) / abLength;
                    maximumDeviation = math.max(maximumDeviation, distance);
                }
            }

            float enclosedArea = math.abs(signedAreaTwice + last.x * first.y - first.x * last.y) * 0.5f;
            if (pathLength < MinimumStrokeLengthViewport)
                return new EarthStructureGestureResult(
                    EarthStructureGestureKind.Invalid,
                    straightness,
                    signedTurn,
                    pathLength,
                    chord,
                    enclosedArea);
            bool closedPlatform = chord <= PlatformClosureViewport &&
                                  pathLength >= PlatformMinimumPathViewport &&
                                  enclosedArea >= PlatformMinimumAreaViewport;
            return new EarthStructureGestureResult(
                closedPlatform ? EarthStructureGestureKind.Platform : EarthStructureGestureKind.Wall,
                straightness,
                signedTurn,
                pathLength,
                chord,
                enclosedArea);
        }
    }
}
