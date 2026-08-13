using System;
using Elemental.Simulation.Characters;
using UnityEngine;

namespace Elemental.Presentation.Camera
{
    [Serializable]
    public struct EarthCameraStateProfile
    {
        [SerializeField] private EarthCameraState state;
        [SerializeField, Min(0.1f)] private float distance;
        [SerializeField, Min(0f)] private float height;
        [SerializeField] private float shoulderOffset;
        [SerializeField, Range(35f, 85f)] private float fieldOfView;
        [SerializeField, Range(0f, 2f)] private float playerFocusWeight;
        [SerializeField, Range(0f, 2f)] private float aimFocusWeight;
        [SerializeField, Range(0f, 2f)] private float heldFocusWeight;
        [SerializeField, Range(0f, 2f)] private float constructFocusWeight;
        [SerializeField, Min(0f)] private float velocityLookAhead;
        [SerializeField, Min(0.01f)] private float positionDamping;
        [SerializeField, Min(0.01f)] private float rotationDamping;
        [SerializeField, Min(0f)] private float enterHysteresis;
        [SerializeField, Min(0f)] private float exitHysteresis;
        [SerializeField, Min(0f)] private float occlusionRadius;
        [SerializeField, Min(0f)] private float impulseGain;
        [SerializeField, Range(0f, 12f)] private float maximumRoll;
        [SerializeField, Min(0f)] private float returnDelay;

        public EarthCameraState State => state;
        public float Distance => distance;
        public float Height => height;
        public float ShoulderOffset => shoulderOffset;
        public float FieldOfView => fieldOfView;
        public float PlayerFocusWeight => playerFocusWeight;
        public float AimFocusWeight => aimFocusWeight;
        public float HeldFocusWeight => heldFocusWeight;
        public float ConstructFocusWeight => constructFocusWeight;
        public float VelocityLookAhead => velocityLookAhead;
        public float PositionDamping => positionDamping;
        public float RotationDamping => rotationDamping;
        public float EnterHysteresis => enterHysteresis;
        public float ExitHysteresis => exitHysteresis;
        public float OcclusionRadius => occlusionRadius;
        public float ImpulseGain => impulseGain;
        public float MaximumRoll => maximumRoll;
        public float ReturnDelay => returnDelay;

        public static EarthCameraStateProfile Default(EarthCameraState state)
        {
            bool heavy = state == EarthCameraState.BendHeavy || state == EarthCameraState.HoldMass ||
                         state == EarthCameraState.Impact;
            bool structure = state == EarthCameraState.DrawStructure;
            bool airborne = state == EarthCameraState.Airborne;
            return new EarthCameraStateProfile
            {
                state = state,
                distance = structure ? 7f : airborne ? 6.8f : heavy ? 6.25f : 5.9f,
                height = structure ? 2.55f : airborne ? 2.7f : 2.15f,
                shoulderOffset = structure ? 0.72f : 0.9f,
                fieldOfView = structure ? 66f : airborne ? 65f : heavy ? 59f :
                    state == EarthCameraState.Explore ? 64f : 61f,
                playerFocusWeight = 1f,
                aimFocusWeight = state == EarthCameraState.Explore ? 1.75f : 0.7f,
                heldFocusWeight = state == EarthCameraState.HoldMass ? 1.15f : 0.15f,
                constructFocusWeight = structure ? 1f : 0.12f,
                velocityLookAhead = airborne ? 2.1f : 1.15f,
                positionDamping = heavy ? 0.065f : 0.1f,
                rotationDamping = heavy ? 0.12f : 0.09f,
                enterHysteresis = 0.06f,
                exitHysteresis = 0.16f,
                occlusionRadius = 0.28f,
                impulseGain = heavy ? 1f : 0.72f,
                maximumRoll = heavy ? 3.5f : 2f,
                returnDelay = 0.14f
            };
        }
    }

    [CreateAssetMenu(menuName = "Elemental/Camera/Earth Camera Profile", fileName = "EarthCameraProfile")]
    public sealed class EarthCameraProfile : ScriptableObject
    {
        [SerializeField] private EarthCameraStateProfile[] states = CreateDefaults();
        [Header("Composition")]
        [SerializeField, Min(0.5f)] private float maximumFocusDistance = 7.5f;
        [SerializeField, Min(0.1f)] private float pullInSpeed = 24f;
        [SerializeField, Min(0.1f)] private float releaseSpeed = 4.5f;
        [SerializeField, Min(0f)] private float occlusionReleaseDelay = 0.12f;
        [Header("Accessibility")]
        [SerializeField, Range(0f, 1f)] private float shakeIntensity = 1f;
        [SerializeField, Range(0f, 1f)] private float cameraLag = 1f;
        [SerializeField, Range(0f, 1f)] private float fieldOfViewMotion = 1f;
        [SerializeField] private bool reducedMotion;

        public float MaximumFocusDistance => maximumFocusDistance;
        public float PullInSpeed => pullInSpeed;
        public float ReleaseSpeed => releaseSpeed;
        public float OcclusionReleaseDelay => occlusionReleaseDelay;
        public EarthCameraAccessibilitySettings Accessibility =>
            new EarthCameraAccessibilitySettings(shakeIntensity, cameraLag, fieldOfViewMotion, reducedMotion);
        public float ShakeIntensity => Accessibility.EffectiveShake;
        public float CameraLag => Accessibility.EffectiveLag;
        public float FieldOfViewMotion => Accessibility.EffectiveFieldOfViewMotion;
        public bool ReducedMotion => reducedMotion;

        public bool TryGet(EarthCameraState state, out EarthCameraStateProfile value)
        {
            if (states != null)
                for (int index = 0; index < states.Length; index++)
                    if (states[index].State == state) { value = states[index]; return true; }
            value = EarthCameraStateProfile.Default(state);
            return false;
        }

        private void OnValidate()
        {
            if (states == null || states.Length != 9) states = CreateDefaults();
        }

        private static EarthCameraStateProfile[] CreateDefaults()
        {
            var values = new EarthCameraStateProfile[9];
            for (int index = 0; index < values.Length; index++)
                values[index] = EarthCameraStateProfile.Default((EarthCameraState)index);
            return values;
        }
    }
}
