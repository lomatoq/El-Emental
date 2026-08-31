using Elemental.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthMiniBokehFocusTests
    {
        [TestCase(12f, 0.75f, 0.85f)]
        [TestCase(8f, 1.35f, 1.10f)]
        [TestCase(4f, 1.95f, 1.50f)]
        public void DistanceCurveIsContinuousAndBounded(
            float distance,
            float expectedStrength,
            float expectedRadius)
        {
            EarthMiniBokehFocus.EvaluateDistanceCurve(
                distance,
                out float strength,
                out float radius);

            Assert.That(strength, Is.EqualTo(expectedStrength).Within(0.001f));
            Assert.That(radius, Is.EqualTo(expectedRadius).Within(0.001f));
            Assert.That(strength, Is.LessThanOrEqualTo(2.1f));
            Assert.That(radius, Is.LessThanOrEqualTo(1.6f));
        }

        [Test]
        public void FocusDistanceIntersectsReferencePlaneUnderPlayerScreenPoint()
        {
            GameObject cameraObject = new GameObject("MiniBokeh Test Camera");
            GameObject planeObject = new GameObject("MiniBokeh Test Plane");
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 6f, -10f);
                camera.transform.LookAt(Vector3.zero);
                planeObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                Vector3 playerChest = new Vector3(2f, 1.1f, 0f);
                bool resolved = EarthMiniBokehFocus.TryResolvePlanarFocusDistance(
                    camera,
                    planeObject.transform,
                    playerChest,
                    out float focusDistance);

                Vector3 viewport = camera.WorldToViewportPoint(playerChest);
                Ray focusRay = camera.ViewportPointToRay(
                    new Vector3(viewport.x, viewport.y, 0f));
                var plane = new Plane(Vector3.up, Vector3.zero);
                Assert.That(plane.Raycast(focusRay, out float expectedDistance), Is.True);
                Assert.That(resolved, Is.True);
                Assert.That(focusDistance, Is.EqualTo(expectedDistance).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(planeObject);
            }
        }
    }
}
