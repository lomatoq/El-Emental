using Elemental.Presentation.VFX;
using Elemental.Simulation.Capabilities;
using MiniBokeh;
using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    /// <summary>
    /// Presentation-only MiniBokeh driver. It follows a stable enemy proxy in
    /// screen space and never changes Cinemachine targets or combat authority.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.Camera), typeof(MiniBokehController))]
    public sealed class EarthMiniBokehFocus : MonoBehaviour
    {
        [Header("Master")]
        [SerializeField] private bool effectEnabled = true;
        [SerializeField] private EarthChargeCameraLookdevV2 clarity;
        [SerializeField] private MiniBokehController controller;
        [SerializeField] private Transform referencePlane;
        [SerializeField] private Transform enemyFocusProxy;
        [Header("Base Bokeh")]
        [SerializeField, Range(0f, 10f)] private float baseBokehStrength = 1.35f;
        [SerializeField, Range(0.1f, 5f)] private float baseMaxBlurRadius = 1.1f;
        [SerializeField, Range(0f, 1f)] private float boundaryFade = 0.12f;
        [Header("Focus Smoothing")]
        [SerializeField, Range(0.01f, 0.3f)] private float focusSmoothTime = 0.15f;
        [SerializeField, Range(0.1f, 0.5f)] private float tuningSmoothTime = 0.30f;
        [Header("Reference Plane")]
        [SerializeField] private bool followEnemyGround = true;
        [SerializeField, Range(0f, 2f)] private float enemyGroundOffset = 1.075f;
        [SerializeField, Range(0f, 60f)] private float referencePlaneTiltDegrees = 12f;
        [SerializeField, Range(1f, 30f)] private float referencePlaneFollowSharpness = 10f;
        [Header("Dynamic Blur Curve")]
        [SerializeField] private bool dynamicBlur = true;
        [SerializeField, Min(0f)] private float farDistance = 10f;
        [SerializeField, Min(0f)] private float middleDistance = 8f;
        [SerializeField, Min(0f)] private float nearDistance = 6f;
        [SerializeField, Range(0f, 2.1f)] private float farStrength = 0.75f;
        [SerializeField, Range(0f, 2.1f)] private float middleStrength = 1.35f;
        [SerializeField, Range(0f, 2.1f)] private float nearStrength = 1.95f;
        [SerializeField, Range(0.1f, 1.6f)] private float farRadius = 0.85f;
        [SerializeField, Range(0.1f, 1.6f)] private float middleRadius = 1.10f;
        [SerializeField, Range(0.1f, 1.6f)] private float nearRadius = 1.50f;

        private UnityEngine.Camera _camera;
        private float _focusVelocity;
        private float _strengthVelocity;
        private float _radiusVelocity;
        private float _currentStrength;
        private float _currentRadius;

        public bool EffectEnabled => effectEnabled;
        public float FocusDistance => controller != null ? controller.FocusDistance : 0f;
        public bool IsMiniBokehActive => controller != null && controller.enabled;
        public float CurrentBokehStrength => _currentStrength;
        public float CurrentMaxBlurRadius => _currentRadius;
        public Transform EnemyFocusProxy => enemyFocusProxy;
        public Transform ReferencePlane => referencePlane;

        public void Configure(
            Transform configuredEnemyFocusProxy,
            MiniBokehController configuredController,
            Transform configuredReferencePlane)
        {
            enemyFocusProxy = configuredEnemyFocusProxy;
            controller = configuredController;
            referencePlane = configuredReferencePlane;
            clarity = GetComponent<EarthChargeCameraLookdevV2>();
            _camera = GetComponent<UnityEngine.Camera>();
            _currentStrength = baseBokehStrength;
            _currentRadius = baseMaxBlurRadius;
            if (isActiveAndEnabled) ApplyStaticSettings();
        }

        public void SetEffectEnabled(bool enabled)
        {
            effectEnabled = enabled;
            if (controller != null && !enabled) controller.enabled = false;
        }

        private void Awake()
        {
            _camera = GetComponent<UnityEngine.Camera>();
            if (clarity == null) clarity = GetComponent<EarthChargeCameraLookdevV2>();
            if (controller == null) controller = GetComponent<MiniBokehController>();
            _currentStrength = baseBokehStrength;
            _currentRadius = baseMaxBlurRadius;
            // A disabled manual-focus driver must not overwrite the artist's
            // MiniBokehController values merely because Play Mode invoked Awake.
            if (enabled) ApplyStaticSettings();
        }

        private void LateUpdate()
        {
            if (_camera == null || controller == null) return;

            bool nativeHigh = clarity == null ||
                clarity.Capability == CapabilityProfileKind.NativeHigh;
#if UNITY_WEBGL
            nativeHigh = false;
#endif
            controller.enabled = effectEnabled && nativeHigh && referencePlane != null &&
                enemyFocusProxy != null;
            if (!controller.enabled) return;

            if (followEnemyGround) UpdateReferencePlane();

            if (TryResolvePlanarFocusDistance(
                    _camera, referencePlane, enemyFocusProxy.position, out float desiredFocus))
            {
                controller.FocusDistance = Mathf.SmoothDamp(
                    Mathf.Max(0.1f, controller.FocusDistance),
                    desiredFocus,
                    ref _focusVelocity,
                    focusSmoothTime,
                    120f,
                    Mathf.Max(0.0001f, Time.unscaledDeltaTime));
            }

            float enemyDistance = Vector3.Distance(
                _camera.transform.position, enemyFocusProxy.position);
            float desiredStrength = baseBokehStrength;
            float desiredRadius = baseMaxBlurRadius;
            if (dynamicBlur)
                EvaluateConfiguredDistanceCurve(
                    enemyDistance,
                    out desiredStrength,
                    out desiredRadius);
            _currentStrength = Mathf.SmoothDamp(
                _currentStrength,
                desiredStrength,
                ref _strengthVelocity,
                tuningSmoothTime,
                8f,
                Mathf.Max(0.0001f, Time.unscaledDeltaTime));
            _currentRadius = Mathf.SmoothDamp(
                _currentRadius,
                desiredRadius,
                ref _radiusVelocity,
                tuningSmoothTime,
                4f,
                Mathf.Max(0.0001f, Time.unscaledDeltaTime));
            controller.BokehStrength = Mathf.Clamp(_currentStrength, 0f, 2.1f);
            controller.MaxBlurRadius = Mathf.Clamp(_currentRadius, 0.1f, 1.6f);
        }

        private void UpdateReferencePlane()
        {
            Transform enemyRoot = enemyFocusProxy != null ? enemyFocusProxy.parent : null;
            if (enemyRoot == null || referencePlane == null || _camera == null) return;

            Vector3 groundNormal = enemyRoot.up.normalized;
            Vector3 targetPosition = enemyRoot.position - groundNormal * enemyGroundOffset;
            Vector3 towardCamera = (_camera.transform.position - targetPosition).normalized;
            Vector3 tiltedNormal = Vector3.RotateTowards(
                groundNormal,
                towardCamera,
                referencePlaneTiltDegrees * Mathf.Deg2Rad,
                0f).normalized;
            Quaternion targetRotation = Quaternion.FromToRotation(Vector3.up, tiltedNormal);
            float blend = 1f - Mathf.Exp(
                -referencePlaneFollowSharpness * Mathf.Max(0.0001f, Time.unscaledDeltaTime));
            referencePlane.position = Vector3.Lerp(referencePlane.position, targetPosition, blend);
            referencePlane.rotation = Quaternion.Slerp(referencePlane.rotation, targetRotation, blend);
        }

        private void EvaluateConfiguredDistanceCurve(
            float distance,
            out float bokehStrength,
            out float maxBlurRadius)
        {
            float far = Mathf.Max(farDistance, middleDistance + 0.01f);
            float middle = Mathf.Max(middleDistance, nearDistance + 0.01f);
            float near = Mathf.Min(nearDistance, middle - 0.01f);
            if (distance >= middle)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(far, middle, distance));
                bokehStrength = Mathf.Lerp(farStrength, middleStrength, t);
                maxBlurRadius = Mathf.Lerp(farRadius, middleRadius, t);
            }
            else
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(middle, near, distance));
                bokehStrength = Mathf.Lerp(middleStrength, nearStrength, t);
                maxBlurRadius = Mathf.Lerp(middleRadius, nearRadius, t);
            }
            bokehStrength = Mathf.Clamp(bokehStrength, 0f, 2.1f);
            maxBlurRadius = Mathf.Clamp(maxBlurRadius, 0.1f, 1.6f);
        }

        private void ApplyStaticSettings()
        {
            if (controller == null) return;
            controller.ReferencePlane = referencePlane;
            controller.AutoFocus = false;
            controller.BokehStrength = baseBokehStrength;
            controller.MaxBlurRadius = baseMaxBlurRadius;
            controller.BoundaryFade = boundaryFade;
            controller.DownsampleMode = MiniBokehController.ResolutionMode.Half;
            controller.BokehMode = MiniBokehController.BokehType.Circular;
        }

        public static void EvaluateDistanceCurve(
            float distance,
            out float bokehStrength,
            out float maxBlurRadius)
        {
            if (distance >= 8f)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(10f, 8f, distance));
                bokehStrength = Mathf.Lerp(0.75f, 1.35f, t);
                maxBlurRadius = Mathf.Lerp(0.85f, 1.10f, t);
            }
            else
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8f, 6f, distance));
                bokehStrength = Mathf.Lerp(1.35f, 1.95f, t);
                maxBlurRadius = Mathf.Lerp(1.10f, 1.50f, t);
            }

            bokehStrength = Mathf.Clamp(bokehStrength, 0f, 2.1f);
            maxBlurRadius = Mathf.Clamp(maxBlurRadius, 0.1f, 1.6f);
        }

        public static bool TryResolvePlanarFocusDistance(
            UnityEngine.Camera camera,
            Transform planeTransform,
            Vector3 worldFocusPoint,
            out float focusDistance)
        {
            focusDistance = 0f;
            if (camera == null || planeTransform == null) return false;

            Vector3 viewport = camera.WorldToViewportPoint(worldFocusPoint);
            if (viewport.z <= 0f) return false;

            Ray focusRay = camera.ViewportPointToRay(
                new Vector3(viewport.x, viewport.y, 0f));
            var plane = new Plane(planeTransform.up, planeTransform.position);
            if (!plane.Raycast(focusRay, out float distance) || distance <= 0.1f)
                return false;

            focusDistance = distance;
            return true;
        }
    }
}
