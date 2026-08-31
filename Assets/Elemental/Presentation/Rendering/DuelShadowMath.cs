using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    public struct DuelShadowBoundsState
    {
        public bool IsInitialized;
        public Vector2 LightSpaceCenter;
        public float HalfExtent;
        public float MinimumDepth;
        public float MaximumDepth;
        public Vector3 LightDirection;

        public void Reset()
        {
            this = default;
        }
    }

    public readonly struct DuelShadowFrame
    {
        public readonly Matrix4x4 ViewMatrix;
        public readonly Matrix4x4 ProjectionMatrix;
        public readonly Matrix4x4 WorldToShadowMatrix;
        public readonly Vector3 LightDirection;
        public readonly Vector3 LightSpaceRight;
        public readonly Vector3 LightSpaceUp;
        public readonly Vector2 SnappedCenter;
        public readonly float HalfExtent;
        public readonly float NearPlane;
        public readonly float FarPlane;
        public readonly float TexelWorldSize;

        public DuelShadowFrame(
            Matrix4x4 viewMatrix,
            Matrix4x4 projectionMatrix,
            Matrix4x4 worldToShadowMatrix,
            Vector3 lightDirection,
            Vector3 lightSpaceRight,
            Vector3 lightSpaceUp,
            Vector2 snappedCenter,
            float halfExtent,
            float nearPlane,
            float farPlane,
            float texelWorldSize)
        {
            ViewMatrix = viewMatrix;
            ProjectionMatrix = projectionMatrix;
            WorldToShadowMatrix = worldToShadowMatrix;
            LightDirection = lightDirection;
            LightSpaceRight = lightSpaceRight;
            LightSpaceUp = lightSpaceUp;
            SnappedCenter = snappedCenter;
            HalfExtent = halfExtent;
            NearPlane = nearPlane;
            FarPlane = farPlane;
            TexelWorldSize = texelWorldSize;
        }
    }

    public static class DuelShadowMath
    {
        private const float DirectionResetDot = 0.9995f;
        private const float MinimumNearPlane = 0.05f;

        public static Vector2 SnapCenterToTexels(
            Vector2 lightSpaceCenter,
            float coverageDiameter,
            int resolution)
        {
            if (!IsFinite(lightSpaceCenter) ||
                !IsFinite(coverageDiameter) ||
                coverageDiameter <= 0f ||
                resolution <= 0)
                return Vector2.zero;

            float texelSize = coverageDiameter / resolution;
            return new Vector2(
                Mathf.Round(lightSpaceCenter.x / texelSize) * texelSize,
                Mathf.Round(lightSpaceCenter.y / texelSize) * texelSize);
        }

        public static bool TryBuildFrame(
            Bounds worldCoverage,
            Vector3 lightDirection,
            Vector3 referenceUp,
            in DuelShadowStabilizationSettings settings,
            int resolution,
            ref DuelShadowBoundsState state,
            out DuelShadowFrame frame)
        {
            frame = default;
            if (!IsFinite(worldCoverage.center) ||
                !IsFinite(worldCoverage.extents) ||
                worldCoverage.extents.x < 0f ||
                worldCoverage.extents.y < 0f ||
                worldCoverage.extents.z < 0f ||
                !TryBuildBasis(
                    lightDirection,
                    referenceUp,
                    out Vector3 forward,
                    out Vector3 right,
                    out Vector3 up) ||
                resolution <= 0)
                return false;

            ProjectBounds(worldCoverage, right, up, forward, out Vector3 minimum, out Vector3 maximum);
            Vector2 rawCenter = new Vector2(
                (minimum.x + maximum.x) * 0.5f,
                (minimum.y + maximum.y) * 0.5f);

            bool directionChanged = state.IsInitialized &&
                Vector3.Dot(state.LightDirection, forward) < DirectionResetDot;
            if (directionChanged)
                state.Reset();

            Vector2 stabilizedCenter = rawCenter;
            if (state.IsInitialized)
            {
                if (Mathf.Abs(rawCenter.x - state.LightSpaceCenter.x) <=
                    settings.CenterHysteresis)
                    stabilizedCenter.x = state.LightSpaceCenter.x;
                if (Mathf.Abs(rawCenter.y - state.LightSpaceCenter.y) <=
                    settings.CenterHysteresis)
                    stabilizedCenter.y = state.LightSpaceCenter.y;
            }

            float requiredHalfExtent = RequiredHalfExtent(
                minimum,
                maximum,
                stabilizedCenter) + settings.WorldPadding;
            float minimumHalfExtent = settings.MinimumCoverageDiameter * 0.5f;
            float maximumHalfExtent = settings.MaximumCoverageDiameter * 0.5f;
            if (requiredHalfExtent > maximumHalfExtent)
                return false;

            float quantizedHalfExtent = QuantizeUp(
                Mathf.Max(minimumHalfExtent, requiredHalfExtent),
                settings.CoverageQuantum);
            float halfExtent = quantizedHalfExtent;
            if (state.IsInitialized && requiredHalfExtent <= state.HalfExtent)
            {
                bool canContract = state.HalfExtent - quantizedHalfExtent >=
                    settings.CoverageContractionHysteresis;
                halfExtent = canContract ? quantizedHalfExtent : state.HalfExtent;
            }

            Vector2 snappedCenter = SnapCenterToTexels(
                stabilizedCenter,
                halfExtent * 2f,
                resolution);
            requiredHalfExtent = RequiredHalfExtent(minimum, maximum, snappedCenter) +
                settings.WorldPadding;
            if (requiredHalfExtent > halfExtent)
            {
                halfExtent = QuantizeUp(requiredHalfExtent, settings.CoverageQuantum);
                if (halfExtent > maximumHalfExtent)
                    return false;
                snappedCenter = SnapCenterToTexels(
                    stabilizedCenter,
                    halfExtent * 2f,
                    resolution);
            }

            float targetMinimumDepth = QuantizeDown(
                minimum.z - settings.DepthPadding,
                settings.DepthQuantum);
            float targetMaximumDepth = QuantizeUp(
                maximum.z + settings.DepthPadding,
                settings.DepthQuantum);
            float minimumDepth = targetMinimumDepth;
            float maximumDepth = targetMaximumDepth;
            if (state.IsInitialized)
            {
                minimumDepth = StabilizeMinimum(
                    state.MinimumDepth,
                    targetMinimumDepth,
                    settings.DepthContractionHysteresis);
                maximumDepth = StabilizeMaximum(
                    state.MaximumDepth,
                    targetMaximumDepth,
                    settings.DepthContractionHysteresis);
            }

            float depthRange = maximumDepth - minimumDepth;
            if (!IsFinite(depthRange) || depthRange <= MinimumNearPlane)
                return false;

            Vector3 cameraPosition =
                right * snappedCenter.x +
                up * snappedCenter.y +
                forward * minimumDepth;
            Matrix4x4 view = Matrix4x4.LookAt(
                cameraPosition,
                cameraPosition + forward,
                up);
            Matrix4x4 projection = Matrix4x4.Ortho(
                -halfExtent,
                halfExtent,
                -halfExtent,
                halfExtent,
                MinimumNearPlane,
                depthRange);
            Matrix4x4 worldToShadow = BuildWorldToShadow(projection, view);

            if (!IsFinite(view) || !IsFinite(projection) || !IsFinite(worldToShadow))
                return false;

            state.IsInitialized = true;
            state.LightSpaceCenter = stabilizedCenter;
            state.HalfExtent = halfExtent;
            state.MinimumDepth = minimumDepth;
            state.MaximumDepth = maximumDepth;
            state.LightDirection = forward;
            frame = new DuelShadowFrame(
                view,
                projection,
                worldToShadow,
                forward,
                right,
                up,
                snappedCenter,
                halfExtent,
                MinimumNearPlane,
                depthRange,
                halfExtent * 2f / resolution);
            return true;
        }

        public static bool IsFinite(Matrix4x4 matrix)
        {
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    if (!IsFinite(matrix[row, column]))
                        return false;
                }
            }

            return true;
        }

        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        public static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float RequiredHalfExtent(
            Vector3 minimum,
            Vector3 maximum,
            Vector2 center)
        {
            float horizontal = Mathf.Max(
                Mathf.Abs(minimum.x - center.x),
                Mathf.Abs(maximum.x - center.x));
            float vertical = Mathf.Max(
                Mathf.Abs(minimum.y - center.y),
                Mathf.Abs(maximum.y - center.y));
            return Mathf.Max(horizontal, vertical);
        }

        private static float StabilizeMinimum(
            float previous,
            float target,
            float contractionHysteresis)
        {
            if (target < previous)
                return target;
            return target - previous >= contractionHysteresis ? target : previous;
        }

        private static float StabilizeMaximum(
            float previous,
            float target,
            float contractionHysteresis)
        {
            if (target > previous)
                return target;
            return previous - target >= contractionHysteresis ? target : previous;
        }

        private static float QuantizeUp(float value, float quantum)
        {
            return Mathf.Ceil(value / quantum) * quantum;
        }

        private static float QuantizeDown(float value, float quantum)
        {
            return Mathf.Floor(value / quantum) * quantum;
        }

        private static bool TryBuildBasis(
            Vector3 lightDirection,
            Vector3 referenceUp,
            out Vector3 forward,
            out Vector3 right,
            out Vector3 up)
        {
            forward = Vector3.zero;
            right = Vector3.zero;
            up = Vector3.zero;
            if (!IsFinite(lightDirection) || lightDirection.sqrMagnitude < 0.000001f)
                return false;

            forward = lightDirection.normalized;
            Vector3 upCandidate = IsFinite(referenceUp) &&
                referenceUp.sqrMagnitude > 0.000001f
                ? referenceUp.normalized
                : Vector3.up;
            upCandidate -= forward * Vector3.Dot(upCandidate, forward);
            if (upCandidate.sqrMagnitude < 0.000001f)
            {
                upCandidate = Mathf.Abs(Vector3.Dot(forward, Vector3.forward)) < 0.95f
                    ? Vector3.forward
                    : Vector3.right;
                upCandidate -= forward * Vector3.Dot(upCandidate, forward);
            }

            up = upCandidate.normalized;
            right = Vector3.Cross(up, forward).normalized;
            up = Vector3.Cross(forward, right).normalized;
            return IsFinite(right) && IsFinite(up);
        }

        private static void ProjectBounds(
            Bounds bounds,
            Vector3 right,
            Vector3 up,
            Vector3 forward,
            out Vector3 minimum,
            out Vector3 maximum)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int index = 0; index < 8; index++)
            {
                Vector3 corner = center + new Vector3(
                    (index & 1) == 0 ? -extents.x : extents.x,
                    (index & 2) == 0 ? -extents.y : extents.y,
                    (index & 4) == 0 ? -extents.z : extents.z);
                Vector3 projected = new Vector3(
                    Vector3.Dot(corner, right),
                    Vector3.Dot(corner, up),
                    Vector3.Dot(corner, forward));
                minimum = Vector3.Min(minimum, projected);
                maximum = Vector3.Max(maximum, projected);
            }
        }

        private static Matrix4x4 BuildWorldToShadow(
            Matrix4x4 projection,
            Matrix4x4 view)
        {
            if (SystemInfo.usesReversedZBuffer)
            {
                projection.m20 = -projection.m20;
                projection.m21 = -projection.m21;
                projection.m22 = -projection.m22;
                projection.m23 = -projection.m23;
            }

            Matrix4x4 scaleAndBias = Matrix4x4.identity;
            scaleAndBias.m00 = 0.5f;
            scaleAndBias.m11 = 0.5f;
            scaleAndBias.m22 = 0.5f;
            scaleAndBias.m03 = 0.5f;
            scaleAndBias.m13 = 0.5f;
            scaleAndBias.m23 = 0.5f;
            return scaleAndBias * projection * view;
        }
    }
}
