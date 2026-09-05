using UnityEngine;

namespace Elemental.Presentation.Animation
{
    public readonly struct EarthStableArmIkSample
    {
        public EarthStableArmIkSample(Vector3 target, Vector3 elbow, Vector3 poleDirection)
        {
            Target = target;
            Elbow = elbow;
            PoleDirection = poleDirection;
        }

        public Vector3 Target { get; }
        public Vector3 Elbow { get; }
        public Vector3 PoleDirection { get; }
    }

    /// <summary>Pure analytic geometry shared by the rig job and EditMode tests.</summary>
    public static class EarthStableArmIkGeometry
    {
        private const float Epsilon = 0.000001f;

        public static EarthStableArmIkSample Resolve(
            Vector3 root,
            Vector3 requestedTarget,
            Vector3 pole,
            float upperLength,
            float lowerLength,
            float maximumReachFraction)
        {
            float upper = Mathf.Max(Epsilon, SanitizeLength(upperLength));
            float lower = Mathf.Max(Epsilon, SanitizeLength(lowerLength));
            float maximum = Mathf.Max(Epsilon,
                (upper + lower) * Mathf.Clamp(maximumReachFraction, .65f, .96f));
            float minimum = Mathf.Min(maximum, Mathf.Abs(upper - lower) + Epsilon);

            Vector3 targetDelta = IsFinite(requestedTarget) ? requestedTarget - root : Vector3.forward;
            Vector3 targetDirection = targetDelta.sqrMagnitude > Epsilon
                ? targetDelta.normalized
                : Vector3.forward;
            float distance = Mathf.Clamp(targetDelta.magnitude, minimum, maximum);
            Vector3 target = root + targetDirection * distance;

            Vector3 poleDelta = IsFinite(pole) ? pole - root : Vector3.zero;
            Vector3 planarPole = Vector3.ProjectOnPlane(poleDelta, targetDirection);
            if (planarPole.sqrMagnitude <= Epsilon)
            {
                Vector3 reference = Mathf.Abs(Vector3.Dot(targetDirection, Vector3.up)) < .92f
                    ? Vector3.up
                    : Vector3.right;
                planarPole = Vector3.ProjectOnPlane(reference, targetDirection);
            }
            Vector3 poleDirection = planarPole.normalized;

            float along = (distance * distance + upper * upper - lower * lower) /
                          Mathf.Max(Epsilon, 2f * distance);
            float perpendicular = Mathf.Sqrt(Mathf.Max(0f, upper * upper - along * along));
            Vector3 elbow = root + targetDirection * along + poleDirection * perpendicular;
            return new EarthStableArmIkSample(target, elbow, poleDirection);
        }

        public static Quaternion BlendRotation(Quaternion source, Quaternion solved, float weight) =>
            Quaternion.Slerp(source, solved, Mathf.Clamp01(weight));

        private static float SanitizeLength(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? Epsilon : Mathf.Abs(value);

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
