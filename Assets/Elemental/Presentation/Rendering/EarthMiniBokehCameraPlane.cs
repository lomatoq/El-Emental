using MiniBokeh;
using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    /// <summary>
    /// Keeps MiniBokeh's stylized depth plane camera-relative without taking
    /// ownership of any artist-authored focus or blur settings.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-60)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.Camera), typeof(MiniBokehController))]
    public sealed class EarthMiniBokehCameraPlane : MonoBehaviour
    {
        [SerializeField] private MiniBokehController controller;
        [SerializeField] private Transform referencePlane;
        [SerializeField, Min(0.1f)] private float minimumPlaneDistance = 0.1f;

        public Transform ReferencePlane => referencePlane;

        public void Configure(
            MiniBokehController configuredController,
            Transform configuredReferencePlane)
        {
            controller = configuredController;
            referencePlane = configuredReferencePlane;
            SyncPlane();
        }

        private void OnEnable() => SyncPlane();

        private void OnValidate() => SyncPlane();

        private void LateUpdate() => SyncPlane();

        private void SyncPlane()
        {
            if (controller == null) controller = GetComponent<MiniBokehController>();
            if (controller == null || referencePlane == null) return;

            if (referencePlane.parent != transform)
                referencePlane.SetParent(transform, false);

            // MiniBokeh reads ReferencePlane.up as the plane normal. A +90 degree
            // local X rotation therefore aligns it exactly with camera.forward.
            referencePlane.localPosition = new Vector3(
                0f,
                0f,
                Mathf.Max(minimumPlaneDistance, controller.FocusDistance));
            referencePlane.localRotation = Quaternion.Euler(90f, 0f, 0f);
            referencePlane.localScale = Vector3.one;
            if (controller.ReferencePlane != referencePlane)
                controller.ReferencePlane = referencePlane;
        }
    }
}
