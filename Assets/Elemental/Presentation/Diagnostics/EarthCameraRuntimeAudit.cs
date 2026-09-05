using Elemental.Presentation.Camera;
using Elemental.Presentation.Rendering;
using Elemental.Simulation.Characters;
using UnityEngine;

namespace Elemental.Presentation.Diagnostics
{
    [DisallowMultipleComponent]
    public sealed class EarthCameraRuntimeAudit : MonoBehaviour
    {
        [SerializeField] private EarthCameraDirector director;
        [SerializeField] private EarthCinemachineCameraController controller;
        [SerializeField] private EarthCinematicDepthOfFieldController depthOfField;

        public bool WiringValid { get; private set; }
        public float ActualDistance { get; private set; }
        public float ExpectedDistance { get; private set; }
        public float ActualFieldOfView { get; private set; }
        public float ExpectedFieldOfView { get; private set; }
        public float SharpNearDistance { get; private set; }
        public float SharpFarDistance { get; private set; }
        public bool DepthOfFieldOwnsValidSubjects { get; private set; }

        public void Configure(
            EarthCameraDirector configuredDirector,
            EarthCinemachineCameraController configuredController,
            EarthCinematicDepthOfFieldController configuredDepthOfField)
        {
            director = configuredDirector;
            controller = configuredController;
            depthOfField = configuredDepthOfField;
        }

        private void LateUpdate()
        {
            EarthCameraState state = director != null
                ? director.State
                : EarthCameraState.Explore;
            EarthCameraStateProfile profile = EarthCameraStateProfile.Default(state);
            if (director != null && director.Profile != null)
                director.Profile.TryGet(state, out profile);
            ExpectedDistance = profile.Distance;
            ExpectedFieldOfView = profile.FieldOfView;
            ActualDistance = controller != null ? controller.CameraDistance : 0f;
            ActualFieldOfView = controller != null ? controller.FieldOfView : 0f;
            SharpNearDistance = depthOfField != null ? depthOfField.SharpNearDistance : 0f;
            SharpFarDistance = depthOfField != null ? depthOfField.SharpFarDistance : 0f;
            DepthOfFieldOwnsValidSubjects = depthOfField != null &&
                                              depthOfField.HasRequiredSubjects;
            WiringValid = director != null && director.Player != null &&
                          director.Profile != null && controller != null &&
                          depthOfField != null && depthOfField.PrimarySubject != null &&
                          depthOfField.SecondarySubject != null;
        }
    }
}
