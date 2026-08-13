using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Elemental.Input.Gestures
{
    public static class GestureResampler
    {
        public static void Resample(
            IReadOnlyList<float2> input,
            int outputCount,
            List<float2> output)
        {
            if (input == null || input.Count < 2)
            {
                throw new ArgumentException("At least two input points are required.", nameof(input));
            }

            if (outputCount < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(outputCount));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            float totalLength = 0f;
            for (int index = 1; index < input.Count; index++)
            {
                totalLength += math.distance(input[index - 1], input[index]);
            }

            output.Clear();
            if (totalLength <= 0.0001f)
            {
                for (int index = 0; index < outputCount; index++)
                {
                    output.Add(input[0]);
                }

                return;
            }

            output.Add(input[0]);
            float spacing = totalLength / (outputCount - 1);
            float traversed = 0f;
            int segmentIndex = 1;
            float2 segmentStart = input[0];

            for (int sampleIndex = 1; sampleIndex < outputCount - 1; sampleIndex++)
            {
                float targetDistance = spacing * sampleIndex;
                while (segmentIndex < input.Count)
                {
                    float2 segmentEnd = input[segmentIndex];
                    float segmentLength = math.distance(segmentStart, segmentEnd);
                    if (traversed + segmentLength >= targetDistance)
                    {
                        float t = (targetDistance - traversed) / math.max(segmentLength, 0.0001f);
                        output.Add(math.lerp(segmentStart, segmentEnd, t));
                        break;
                    }

                    traversed += segmentLength;
                    segmentStart = segmentEnd;
                    segmentIndex++;
                }
            }

            output.Add(input[input.Count - 1]);
        }
    }
}
