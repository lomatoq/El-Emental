using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    public readonly struct CapsuleShadowProxy
    {
        public CapsuleShadowProxy(
            Vector3 startWorld,
            Vector3 endWorld,
            float radius,
            float softness)
        {
            StartWorld = startWorld;
            EndWorld = endWorld;
            Radius = Mathf.Max(0f, radius);
            Softness = Mathf.Max(0.001f, softness);
        }

        public Vector3 StartWorld { get; }
        public Vector3 EndWorld { get; }
        public float Radius { get; }
        public float Softness { get; }
        public float WorldDiameter => Vector3.Distance(StartWorld, EndWorld) + Radius * 2f;
        public bool IsValid =>
            DuelShadowMath.IsFinite(StartWorld) &&
            DuelShadowMath.IsFinite(EndWorld) &&
            DuelShadowMath.IsFinite(Radius) &&
            DuelShadowMath.IsFinite(Softness) &&
            Radius > 0f;
    }

    /// <summary>
    /// CPU reference for the bounded analytic receiver used by the HLSL include.
    /// It exists to verify contact distance, bias, and numerical behavior without
    /// requiring a rendered scene.
    /// </summary>
    public static class CapsuleContactShadowMath
    {
        private const float Epsilon = 0.000001f;

        public static float Evaluate(
            in CapsuleShadowProxy proxy,
            Vector3 receiverPositionWorld,
            Vector3 receiverNormalWorld,
            Vector3 directionToLightWorld,
            float maximumContactDistance,
            float surfaceBias,
            float normalBias,
            float shadowStrength)
        {
            if (!proxy.IsValid ||
                !DuelShadowMath.IsFinite(receiverPositionWorld) ||
                !TryNormalize(receiverNormalWorld, out Vector3 normal) ||
                !TryNormalize(directionToLightWorld, out Vector3 directionToLight) ||
                !DuelShadowMath.IsFinite(maximumContactDistance) ||
                maximumContactDistance <= 0f)
                return 1f;

            float clampedSurfaceBias = Mathf.Clamp(
                surfaceBias,
                0.001f,
                maximumContactDistance * 0.25f);
            float clampedNormalBias = Mathf.Clamp(
                normalBias,
                0f,
                maximumContactDistance * 0.25f);
            Vector3 rayStart = receiverPositionWorld +
                normal * clampedNormalBias +
                directionToLight * clampedSurfaceBias;
            Vector3 rayEnd = rayStart + directionToLight * maximumContactDistance;
            ClosestSegmentParameters(
                rayStart,
                rayEnd,
                proxy.StartWorld,
                proxy.EndWorld,
                out float rayParameter,
                out float capsuleParameter);
            Vector3 rayPoint = Vector3.LerpUnclamped(rayStart, rayEnd, rayParameter);
            Vector3 capsulePoint = Vector3.LerpUnclamped(
                proxy.StartWorld,
                proxy.EndWorld,
                capsuleParameter);
            float distance = Vector3.Distance(rayPoint, capsulePoint);
            float coverage = 1f - SmoothStep(
                Mathf.Max(0f, proxy.Radius - proxy.Softness),
                proxy.Radius + proxy.Softness,
                distance);
            float rayDistance = rayParameter * maximumContactDistance;
            float startGate = SmoothStep(
                clampedSurfaceBias,
                clampedSurfaceBias * 2f,
                rayDistance);
            float distanceFade = 1f - Mathf.Clamp01(rayParameter);
            float occlusion = Mathf.Clamp01(coverage * startGate * distanceFade);
            return Mathf.Clamp01(1f - occlusion * Mathf.Clamp01(shadowStrength));
        }

        private static void ClosestSegmentParameters(
            Vector3 firstStart,
            Vector3 firstEnd,
            Vector3 secondStart,
            Vector3 secondEnd,
            out float firstParameter,
            out float secondParameter)
        {
            Vector3 firstDirection = firstEnd - firstStart;
            Vector3 secondDirection = secondEnd - secondStart;
            Vector3 separation = firstStart - secondStart;
            float firstLengthSquared = Vector3.Dot(firstDirection, firstDirection);
            float secondLengthSquared = Vector3.Dot(secondDirection, secondDirection);
            float secondProjection = Vector3.Dot(secondDirection, separation);

            if (firstLengthSquared <= Epsilon && secondLengthSquared <= Epsilon)
            {
                firstParameter = 0f;
                secondParameter = 0f;
                return;
            }

            if (firstLengthSquared <= Epsilon)
            {
                firstParameter = 0f;
                secondParameter = Mathf.Clamp01(secondProjection / secondLengthSquared);
                return;
            }

            float firstProjection = Vector3.Dot(firstDirection, separation);
            if (secondLengthSquared <= Epsilon)
            {
                secondParameter = 0f;
                firstParameter = Mathf.Clamp01(-firstProjection / firstLengthSquared);
                return;
            }

            float crossProjection = Vector3.Dot(firstDirection, secondDirection);
            float denominator = firstLengthSquared * secondLengthSquared -
                crossProjection * crossProjection;
            firstParameter = Mathf.Abs(denominator) > Epsilon
                ? Mathf.Clamp01(
                    (crossProjection * secondProjection -
                     firstProjection * secondLengthSquared) / denominator)
                : 0f;
            secondParameter =
                (crossProjection * firstParameter + secondProjection) /
                secondLengthSquared;
            if (secondParameter < 0f)
            {
                secondParameter = 0f;
                firstParameter = Mathf.Clamp01(-firstProjection / firstLengthSquared);
            }
            else if (secondParameter > 1f)
            {
                secondParameter = 1f;
                firstParameter = Mathf.Clamp01(
                    (crossProjection - firstProjection) / firstLengthSquared);
            }
        }

        private static bool TryNormalize(Vector3 value, out Vector3 normalized)
        {
            float lengthSquared = value.sqrMagnitude;
            if (!DuelShadowMath.IsFinite(value) || lengthSquared <= Epsilon)
            {
                normalized = default;
                return false;
            }
            normalized = value / Mathf.Sqrt(lengthSquared);
            return true;
        }

        private static float SmoothStep(float minimum, float maximum, float value)
        {
            if (maximum <= minimum)
                return value >= maximum ? 1f : 0f;
            float t = Mathf.Clamp01((value - minimum) / (maximum - minimum));
            return t * t * (3f - 2f * t);
        }
    }
}
