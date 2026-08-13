using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Elemental.Input.Gestures
{
    public enum GestureKind : byte
    {
        Invalid = 0,
        Line = 1,
        Pull = 2,
        Flick = 3
    }

    public static class GestureRecognizer
    {
        public static GestureKind Recognize(IReadOnlyList<float2> points, float durationSeconds)
        {
            if (points == null || points.Count < 2 || !float.IsFinite(durationSeconds))
            {
                return GestureKind.Invalid;
            }

            float pathLength = 0f;
            for (int index = 1; index < points.Count; index++)
            {
                pathLength += math.distance(points[index - 1], points[index]);
            }

            float2 displacement = points[points.Count - 1] - points[0];
            float directDistance = math.length(displacement);
            if (durationSeconds <= 0.45f && directDistance >= 90f)
            {
                return GestureKind.Flick;
            }

            if (displacement.y >= 30f && pathLength <= 180f)
            {
                return GestureKind.Pull;
            }

            if (pathLength >= 40f && directDistance / math.max(pathLength, 0.001f) >= 0.55f)
            {
                return GestureKind.Line;
            }

            return GestureKind.Invalid;
        }
    }
}
