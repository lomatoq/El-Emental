using System.Collections;
using Elemental.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class RenderingGateLifecycleTests
    {
        [UnityTest]
        public IEnumerator MovingCameraResamplesAndKeepsBothFighterBoundsSharp()
        {
            var cameraObject = new GameObject("Moving Gate Camera");
            var primary = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var secondary = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            try
            {
                cameraObject.AddComponent<UnityEngine.Camera>();
                EarthCinematicDepthOfFieldController controller =
                    cameraObject.AddComponent<EarthCinematicDepthOfFieldController>();
                primary.name = "Primary Fighter";
                secondary.name = "Secondary Fighter";
                primary.transform.SetPositionAndRotation(
                    new Vector3(-1.5f, 0f, 8f), Quaternion.identity);
                secondary.transform.SetPositionAndRotation(
                    new Vector3(2.5f, 0.5f, 14f), Quaternion.identity);
                controller.ConfigureSubjects(primary.transform, secondary.transform);
                controller.SetCaptureOverride(
                    true,
                    EarthCinematicDepthOfFieldDebugView.SignedCircleOfConfusion);

                yield return null;
                Assert.That(controller.TryGetRenderSettings(out _), Is.True);
                Assert.That(controller.TryGetSharpEnvelopeEvidence(
                    out EarthCinematicDepthOfFieldEvidence initial), Is.True);
                Assert.That(initial.BothSubjectsSharp, Is.True);

                Vector3 movedPosition = new Vector3(3f, 1.2f, -2f);
                Vector3 midpoint = (primary.transform.position + secondary.transform.position) * 0.5f;
                Quaternion movedRotation = Quaternion.LookRotation(
                    midpoint - movedPosition,
                    Vector3.up);
                cameraObject.transform.SetPositionAndRotation(movedPosition, movedRotation);

                Assert.That(controller.TryGetRenderSettings(
                    out EarthCinematicDepthOfFieldSettings settings), Is.True);
                Assert.That(controller.TryGetSharpEnvelopeEvidence(
                    out EarthCinematicDepthOfFieldEvidence moved), Is.True);
                Assert.That(moved.CameraPosition, Is.EqualTo(movedPosition));
                Assert.That(Quaternion.Angle(moved.CameraRotation, movedRotation),
                    Is.LessThan(0.001f));
                Assert.That(moved.BothSubjectsSharp, Is.True);
                Assert.That(moved.PrimaryNearDistance,
                    Is.GreaterThanOrEqualTo(settings.SharpNearDistance - 0.001f));
                Assert.That(moved.PrimaryFarDistance,
                    Is.LessThanOrEqualTo(settings.SharpFarDistance + 0.001f));
                Assert.That(moved.SecondaryNearDistance,
                    Is.GreaterThanOrEqualTo(settings.SharpNearDistance - 0.001f));
                Assert.That(moved.SecondaryFarDistance,
                    Is.LessThanOrEqualTo(settings.SharpFarDistance + 0.001f));
            }
            finally
            {
                Object.Destroy(cameraObject);
                Object.Destroy(primary);
                Object.Destroy(secondary);
            }
        }

        [UnityTest]
        public IEnumerator MissingSecondFighterCannotPassDualSubjectEvidence()
        {
            var cameraObject = new GameObject("Incomplete Gate Camera");
            var primary = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            try
            {
                cameraObject.AddComponent<UnityEngine.Camera>();
                EarthCinematicDepthOfFieldController controller =
                    cameraObject.AddComponent<EarthCinematicDepthOfFieldController>();
                primary.transform.position = new Vector3(0f, 0f, 8f);
                controller.ConfigureSubjects(primary.transform, null);

                yield return null;
                Assert.That(controller.TryGetSharpEnvelopeEvidence(
                    out EarthCinematicDepthOfFieldEvidence evidence), Is.True);
                Assert.That(evidence.HasBothSubjects, Is.False);
                Assert.That(evidence.BothSubjectsSharp, Is.False);
            }
            finally
            {
                Object.Destroy(cameraObject);
                Object.Destroy(primary);
            }
        }
    }
}
