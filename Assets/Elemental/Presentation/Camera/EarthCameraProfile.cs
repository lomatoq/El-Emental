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
                distance = structure ? 8.2f : airborne ? 8.05f : heavy ? 7.9f : 7.65f,
                height = structure ? 3.2f : airborne ? 3.25f : heavy ? 2.95f : 2.7f,
                shoulderOffset = structure ? 0.62f : 0.74f,
                fieldOfView = structure ? 63f : airborne ? 64f : heavy ? 59f :
                    state == EarthCameraState.Explore ? 60f : 60f,
                playerFocusWeight = state == EarthCameraState.Explore ? 1.35f : 1.25f,
                aimFocusWeight = state == EarthCameraState.Explore ? 0.75f : 0.7f,
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
        [SerializeField, Min(0.5f)] private float maximumFocusDistance = 4.5f;
        [SerializeField, Min(0.1f)] private float pullInSpeed = 24f;
        [SerializeField, Min(0.1f)] private float releaseSpeed = 4.5f;
        [SerializeField, Min(0f)] private float occlusionReleaseDelay = 0.12f;
        [Header("Pointer intent")]
        [SerializeField] private Vector2 pointerDeadZoneHalfExtents = new Vector2(0.2f, 0.18f);
        [SerializeField, Min(0f)] private float pointerHorizontalFocusMeters = 2.15f;
        [SerializeField, Min(0f)] private float pointerNearGroundDistance = 4.4f;
        [SerializeField, Min(0f)] private float pointerFarGroundDistance = 11.4f;
        [SerializeField] private float pointerLowerAimElevation = -0.65f;
        [SerializeField] private float pointerUpperAimElevation = 2.35f;
        [SerializeField, Min(0.1f)] private float maximumFocusSpeed = 18f;
        [SerializeField, Min(0.5f)] private float springResetDistance = 8f;
        [Header("Accessibility")]
        [SerializeField, Range(0f, 1f)] private float shakeIntensity = 1f;
        [SerializeField, Range(0f, 1f)] private float cameraLag = 1f;
        [SerializeField, Range(0f, 1f)] private float fieldOfViewMotion = 1f;
        [SerializeField] private bool reducedMotion;

        public float MaximumFocusDistance => maximumFocusDistance;
        public float PullInSpeed => pullInSpeed;
        public float ReleaseSpeed => releaseSpeed;
        public float OcclusionReleaseDelay => occlusionReleaseDelay;
        public Vector2 PointerDeadZoneHalfExtents => pointerDeadZoneHalfExtents;
        public float PointerHorizontalFocusMeters => pointerHorizontalFocusMeters;
        public float PointerNearGroundDistance => pointerNearGroundDistance;
        public float PointerFarGroundDistance => pointerFarGroundDistance;
        public float PointerLowerAimElevation => pointerLowerAimElevation;
        public float PointerUpperAimElevation => pointerUpperAimElevation;
        public float MaximumFocusSpeed => maximumFocusSpeed;
        public float SpringResetDistance => springResetDistance;
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
