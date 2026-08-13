using Unity.Collections;
using Unity.Mathematics;

namespace Elemental.Simulation.Magic
{
    public readonly struct EarthExtractionGeometry
    {
        public EarthExtractionGeometry(float3 surfaceAnchor, float3 center, float3 emergencePosition, float radius)
        {
            SurfaceAnchor = surfaceAnchor;
            Center = center;
            EmergencePosition = emergencePosition;
            Radius = radius;
        }

        public float3 SurfaceAnchor { get; }
        public float3 Center { get; }
        public float3 EmergencePosition { get; }
        public float Radius { get; }
    }

    public readonly struct MagicSegment
    {
        public MagicSegment(float3 start, float3 end)
        {
            Start = start;
            End = end;
        }

        public float3 Start { get; }
        public float3 End { get; }
    }

    public static class EarthGeometryBuilder
    {
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        public static FixedList4096Bytes<float3> BuildWallFootprint(
            in MagicCommand command,
            float maxPathLength = float.PositiveInfinity)
        {
            FixedList4096Bytes<float3> footprint = default;
            if (command.Path.Length == 0) return footprint;

            float3 previous = command.Path[0];
            footprint.Add(previous);
            float remaining = math.max(0f, maxPathLength);
            for (int index = 1; index < command.Path.Length && remaining > 0f; index++)
            {
                float3 requestedPoint = command.Path[index];
                float distance = math.distance(previous, requestedPoint);
                if (distance <= 0.0001f) continue;
                float usedDistance = math.min(distance, remaining);
                previous = math.lerp(previous, requestedPoint, usedDistance / distance);
                footprint.Add(previous);
                remaining -= usedDistance;
            }

            return footprint;
        }

        public static FixedList4096Bytes<MagicSegment> BuildWallSegments(
            in MagicCommand command,
            float3 planetCenter,
            float height,
            float maxPathLength = float.PositiveInfinity)
        {
            FixedList4096Bytes<MagicSegment> segments = default;
            FixedList4096Bytes<float3> footprint = BuildWallFootprint(in command, maxPathLength);
            if (footprint.Length == 0)
            {
                return segments;
            }

            float3 previous = footprint[0];
            float3 previousUp = math.normalizesafe(previous - planetCenter, command.Aim);
            float3 previousTop = previous + (previousUp * height);
            segments.Add(new MagicSegment(previous, previousTop));

            for (int index = 1; index < footprint.Length; index++)
            {
                float3 basePoint = footprint[index];
                float3 up = math.normalizesafe(basePoint - planetCenter, command.Aim);
                float3 topPoint = basePoint + (up * height);
                segments.Add(new MagicSegment(basePoint, topPoint));
                segments.Add(new MagicSegment(previousTop, topPoint));
                previous = basePoint;
                previousTop = topPoint;
            }

            return segments;
        }

        public static ulong ComputeFootprintHash(FixedList4096Bytes<float3> footprint)
        {
            ulong hash = FnvOffset;
            hash = Mix(hash, (uint)footprint.Length);
            for (int index = 0; index < footprint.Length; index++)
            {
                uint3 bits = math.asuint(footprint[index]);
                hash = Mix(hash, bits.x);
                hash = Mix(hash, bits.y);
                hash = Mix(hash, bits.z);
            }
            return hash;
        }

        public static float3 GetAnchor(in MagicCommand command)
        {
            return command.Path.Length > 0 ? command.Path[0] : command.Origin;
        }

        public static EarthExtractionGeometry BuildExtraction(
            in MagicCommand command,
            float3 planetCenter,
            float radius)
        {
            float3 surfaceAnchor = GetAnchor(in command);
            float3 up = math.normalizesafe(surfaceAnchor - planetCenter, command.Aim);
            float3 center = surfaceAnchor - (up * radius * 0.62f);
            // The runtime fragment begins partially below the original surface and is
            // pulled through the same subtracted volume. It must never pop into existence
            // already floating above otherwise flat ground.
            float3 emergence = surfaceAnchor - (up * radius * 0.18f);
            return new EarthExtractionGeometry(surfaceAnchor, center, emergence, radius);
        }

        public static float ExtractionRadius(float authoredRadius, float amount01)
        {
            return math.max(0.05f, authoredRadius) * math.lerp(0.55f, 1.25f, math.saturate(amount01));
        }

        private static ulong Mix(ulong hash, uint value)
        {
            hash ^= value;
            hash *= FnvPrime;
            return hash;
        }
    }
}
