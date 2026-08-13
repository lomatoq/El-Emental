using System.Collections.Generic;
using Unity.Mathematics;

namespace Elemental.Input.Gestures
{
    public static class GestureRecognitionPipeline
    {
        public static GestureKind Recognize(
            IReadOnlyList<float2> rawPoints,
            float durationSeconds,
            int resampleCount,
            List<float2> scratch)
        {
            if (rawPoints == null || rawPoints.Count < 2 || scratch == null)
                return GestureKind.Invalid;
            GestureResampler.Resample(rawPoints, resampleCount, scratch);
            return GestureRecognizer.Recognize(scratch, durationSeconds);
        }
    }
}
