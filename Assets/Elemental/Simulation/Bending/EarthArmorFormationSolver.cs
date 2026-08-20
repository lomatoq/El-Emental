using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public readonly struct EarthArmorFormationSample
    {
        public EarthArmorFormationSample(float3 direction, float radiusMultiplier, float scaleMultiplier, int layer)
        {
            Direction = math.normalizesafe(direction, new float3(0f, 0f, 1f));
            RadiusMultiplier = math.max(0.1f, radiusMultiplier);
            ScaleMultiplier = math.max(0.1f, scaleMultiplier);
            Layer = layer;
        }

        public float3 Direction { get; }
        public float RadiusMultiplier { get; }
        public float ScaleMultiplier { get; }
        public int Layer { get; }
    }

    public static class EarthArmorFormationSolver
    {
        private const float GoldenAngle = 2.39996323f;

        public static EarthArmorFormationSample DirectedDome(
            int index,
            int count,
            float3 aim,
            float3 localUp,
            uint seed = 0u)
        {
            count = math.max(1, count);
            float3 up = math.normalizesafe(localUp, new float3(0f, 1f, 0f));
            float3 forward = math.normalizesafe(aim - up * math.dot(aim, up), OrthonormalTangent(up));
            float3 right = math.normalizesafe(math.cross(up, forward), OrthonormalTangent(up));
            int layer = math.abs(index) % 3;
            float radial01 = math.sqrt(math.saturate((index + 0.5f) / count));
            float angle = index * GoldenAngle + Hash01((uint)index + seed * 17u) * 0.42f;
            float lateral = math.cos(angle) * radial01;
            float vertical = math.sin(angle) * radial01;
            // An elliptical cap, biased toward the threat/aim direction. Rear slots
            // remain sparse and the three radius layers give the shield real depth.
            float3 direction = math.normalizesafe(
                forward * math.lerp(1.35f, 0.42f, radial01) +
                right * lateral * 0.88f +
                up * (vertical * 0.66f + 0.16f),
                forward);
            float radius = 0.88f + layer * 0.075f +
                           (Hash01((uint)index + seed * 31u + 7u) - 0.5f) * 0.045f;
            float scale = math.lerp(1.18f, 0.82f, radial01) *
                          math.lerp(0.92f, 1.11f, Hash01((uint)index + seed * 47u + 11u));
            return new EarthArmorFormationSample(direction, radius, scale, layer);
        }

        public static EarthArmorFormationSample BrokenOrbit(
            int index,
            int count,
            float time,
            float3 aim,
            float3 localUp,
            uint seed = 0u)
        {
            count = math.max(1, count);
            float3 up = math.normalizesafe(localUp, new float3(0f, 1f, 0f));
            float3 forward = math.normalizesafe(aim - up * math.dot(aim, up), OrthonormalTangent(up));
            float3 right = math.normalizesafe(math.cross(up, forward), OrthonormalTangent(up));
            int band = math.abs(index) % 5;
            float phase = index * GoldenAngle + band * 0.71f + seed * 0.13f;
            float directionSign = (band & 1) == 0 ? 1f : -1f;
            float speed = 0.42f + band * 0.085f;
            float angle = phase + time * speed * directionSign;
            float tilt = math.radians(-32f + band * 16f);
            float3 bandUp = math.normalizesafe(
                up * math.cos(tilt) + right * math.sin(tilt), up);
            float3 bandRight = math.normalizesafe(math.cross(bandUp, forward), right);
            float3 bandForward = math.normalizesafe(math.cross(bandRight, bandUp), forward);
            float eccentricity = 0.72f + band * 0.055f;
            float3 rawDirection = math.normalizesafe(
                bandRight * math.cos(angle) +
                bandForward * math.sin(angle) * eccentricity +
                bandUp * math.sin(angle * 0.5f + band) * 0.16f,
                forward);
            // Broken bands still belong to the hero's upper defensive hemisphere.
            // Letting a tilted ellipse continue below the tangent plane sent plates
            // through the planet and made the formation look like a full cage.
            float vertical = math.dot(rawDirection, up);
            rawDirection += up * (math.max(0.045f, vertical) - vertical);
            float3 direction = math.normalizesafe(rawDirection, forward);
            float radius = 0.82f + band * 0.055f +
                           (Hash01((uint)index + seed * 23u + 19u) - 0.5f) * 0.08f;
            float scale = math.lerp(0.78f, 1.24f, Hash01((uint)index + seed * 59u + 29u));
            return new EarthArmorFormationSample(direction, radius, scale, band);
        }

        private static float3 OrthonormalTangent(float3 normal)
        {
            float3 reference = math.abs(normal.y) < 0.92f
                ? new float3(0f, 1f, 0f)
                : new float3(1f, 0f, 0f);
            return math.normalizesafe(math.cross(reference, normal), new float3(1f, 0f, 0f));
        }

        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }
    }
}
