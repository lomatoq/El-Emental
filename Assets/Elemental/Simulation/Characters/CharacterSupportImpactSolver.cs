using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public static class CharacterSupportImpactSolver
    {
        public static bool IsSupportContact(
            float3 gravityUp,
            float3 centerOfMass,
            float3 point,
            float3 normal,
            bool otherBodyIsDynamic,
            float minimumSupportDot = 0.5f,
            float minimumBelowCenter = 0.18f,
            float maximumLateralDistance = 1.05f)
        {
            if (otherBodyIsDynamic ||
                !math.all(math.isfinite(gravityUp)) ||
                !math.all(math.isfinite(centerOfMass)) ||
                !math.all(math.isfinite(point)) ||
                !math.all(math.isfinite(normal))) return false;

            float3 up = math.normalizesafe(gravityUp, new float3(0f, 1f, 0f));
            float3 contactNormal = math.normalizesafe(normal, up);
            float3 centerToPoint = point - centerOfMass;
            float below = -math.dot(centerToPoint, up);
            float3 lateral = centerToPoint + (up * below);
            // Unity reports ContactPoint.normal from the other collider's frame, so
            // its sign depends on which participant owns the callback. Support is
            // identified by axis alignment plus a point below the center, not sign.
            return math.abs(math.dot(contactNormal, up)) >= math.clamp(minimumSupportDot, 0f, 1f) &&
                   below >= math.max(0f, minimumBelowCenter) &&
                   math.lengthsq(lateral) <= math.max(0.01f, maximumLateralDistance * maximumLateralDistance);
        }
    }
}
