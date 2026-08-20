using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public enum ConstructionOrientationMode : byte
    {
        FollowPlanetGravity = 0,
        PreserveAuthoredFrame = 1,
        FollowSupportFrame = 2
    }

    public readonly struct EarthConstructionFrame
    {
        public EarthConstructionFrame(
            uint supportId,
            uint supportGeneration,
            float3 origin,
            float3 normal,
            float3 tangent,
            quaternion authoredRotation,
            quaternion localRotationToSupport,
            ConstructionOrientationMode orientationMode)
        {
            SupportId = supportId;
            SupportGeneration = supportGeneration;
            Origin = origin;
            Normal = math.normalizesafe(normal, new float3(0f, 1f, 0f));
            float3 planarTangent = tangent - Normal * math.dot(tangent, Normal);
            Tangent = math.normalizesafe(planarTangent, OrthonormalTangent(Normal));
            Bitangent = math.normalizesafe(math.cross(Normal, Tangent), new float3(0f, 0f, 1f));
            AuthoredRotation = authoredRotation;
            LocalRotationToSupport = localRotationToSupport;
            OrientationMode = orientationMode;
        }

        public uint SupportId { get; }
        public uint SupportGeneration { get; }
        public float3 Origin { get; }
        public float3 Normal { get; }
        public float3 Tangent { get; }
        public float3 Bitangent { get; }
        public quaternion AuthoredRotation { get; }
        public quaternion LocalRotationToSupport { get; }
        public ConstructionOrientationMode OrientationMode { get; }
        public bool HasSupport => SupportId != 0u && SupportGeneration != 0u;

        private static float3 OrthonormalTangent(float3 normal)
        {
            float3 reference = math.abs(normal.y) < 0.92f
                ? new float3(0f, 1f, 0f)
                : new float3(1f, 0f, 0f);
            return math.normalizesafe(math.cross(reference, normal), new float3(1f, 0f, 0f));
        }
    }

    public static class EarthCantileverPlatformSolver
    {
        public static EarthPlatformGeometry Build(
            float3 supportPoint,
            float3 supportNormal,
            float3 supportTangent,
            float3 planetCenter,
            float horizontalGestureSpan,
            float verticalGestureSpan,
            float deckThickness,
            float rootEmbed = 0.34f)
        {
            float3 gravityUp = math.normalizesafe(supportPoint - planetCenter, new float3(0f, 1f, 0f));
            float3 outward = supportNormal - gravityUp * math.dot(supportNormal, gravityUp);
            outward = math.normalizesafe(outward, OrthonormalTangent(gravityUp));
            float3 right = math.normalizesafe(math.cross(gravityUp, outward), OrthonormalTangent(gravityUp));
            float3 requestedRight = supportTangent - gravityUp * math.dot(supportTangent, gravityUp);
            if (math.lengthsq(requestedRight) > 0.25f && math.dot(right, requestedRight) < 0f)
                right = -right;
            outward = math.normalizesafe(math.cross(right, gravityUp), outward);

            float width = math.clamp(horizontalGestureSpan, 1.2f, 9f);
            float overhang = math.clamp(1.35f + verticalGestureSpan * 0.72f, 1.35f, 5.8f);
            float embedded = math.clamp(rootEmbed, 0.18f, math.min(0.75f, overhang * 0.38f));
            float totalDepth = overhang + embedded;
            float halfWidth = width * 0.5f;
            float halfDepth = totalDepth * 0.5f;
            float shoulder = math.min(width * 0.08f, 0.34f);

            // Six-sided deck: a broad embedded root and a slightly tapered chipped
            // leading edge. It remains a closed stable support, not a decorative
            // plane or a stretched box.
            var polygon = new[]
            {
                new float2(-halfWidth, -halfDepth),
                new float2(halfWidth, -halfDepth),
                new float2(halfWidth + shoulder, halfDepth * 0.18f),
                new float2(halfWidth - shoulder * 0.55f, halfDepth),
                new float2(-halfWidth + shoulder * 0.35f, halfDepth),
                new float2(-halfWidth - shoulder * 0.7f, halfDepth * 0.08f)
            };
            float area = PolygonArea(polygon);
            float3 deckCenter = supportPoint + outward * ((overhang - embedded) * 0.5f) -
                                gravityUp * math.max(0.1f, deckThickness);
            float radius = math.length(deckCenter - planetCenter);
            return new EarthPlatformGeometry(
                deckCenter,
                gravityUp,
                right,
                outward,
                polygon,
                area,
                radius);
        }

        private static float PolygonArea(float2[] polygon)
        {
            float twiceArea = 0f;
            for (int index = 0; index < polygon.Length; index++)
            {
                float2 a = polygon[index];
                float2 b = polygon[(index + 1) % polygon.Length];
                twiceArea += a.x * b.y - a.y * b.x;
            }
            return math.abs(twiceArea) * 0.5f;
        }

        private static float3 OrthonormalTangent(float3 normal)
        {
            float3 reference = math.abs(normal.y) < 0.92f
                ? new float3(0f, 1f, 0f)
                : new float3(1f, 0f, 0f);
            return math.normalizesafe(math.cross(reference, normal), new float3(1f, 0f, 0f));
        }
    }
}
