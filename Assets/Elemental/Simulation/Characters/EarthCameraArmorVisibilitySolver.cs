using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    /// <summary>
    /// Pure geometric gate for presentation-only armor visibility. Gameplay collision
    /// remains untouched; a plate is hidden only while it blocks a protected camera
    /// sightline to the avatar.
    /// </summary>
    public static class EarthCameraArmorVisibilitySolver
    {
        public const float DefaultReleaseMargin = 0.08f;

        public static bool ShouldSuppress(
            float3 cameraPosition,
            float3 focusPosition,
            float3 plateCenter,
            float plateRadius,
            float corridorRadius,
            bool wasSuppressed,
            float releaseMargin = DefaultReleaseMargin)
        {
            if (!math.all(math.isfinite(cameraPosition)) ||
                !math.all(math.isfinite(focusPosition)) ||
                !math.all(math.isfinite(plateCenter)))
                return false;

            float3 segment = focusPosition - cameraPosition;
            float segmentLengthSquared = math.lengthsq(segment);
            if (segmentLengthSquared <= 1e-5f) return false;

            float along = math.dot(plateCenter - cameraPosition, segment) / segmentLengthSquared;
            if (along <= 0.01f || along >= 0.995f) return false;

            float3 closest = cameraPosition + segment * along;
            float threshold = math.max(0f, plateRadius) + math.max(0f, corridorRadius);
            if (wasSuppressed) threshold += math.max(0f, releaseMargin);
            return math.lengthsq(plateCenter - closest) <= threshold * threshold;
        }

        public static float ResolveCameraDistance(
            float authoredDistance,
            bool armorActive,
            float armorPhase01)
        {
            float baseline = math.clamp(authoredDistance, 6.6f, 8.5f);
            if (!armorActive) return baseline;
            float phase = math.saturate(armorPhase01);
            float shellBonus = math.lerp(0.85f, 1.75f, phase);
            return math.clamp(baseline + shellBonus, 7.45f, 10.25f);
        }

        public static float ResolveBodyCoverageScale(bool readabilityRegion, float3 localDirection)
        {
            // Armor is a protection state, not a readability costume. Earlier
            // camera-facing head/chest tiles were shrunk to 38%, leaving a large
            // visible hole in exactly the direction an opponent attacks from.
            return 1f;
        }
    }
}
