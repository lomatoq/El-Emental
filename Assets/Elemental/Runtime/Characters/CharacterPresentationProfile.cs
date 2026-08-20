using UnityEngine;

namespace Elemental.Runtime.Characters
{
    [CreateAssetMenu(menuName = "Elemental/Character/Presentation Profile", fileName = "CharacterPresentationProfile")]
    public sealed class CharacterPresentationProfile : ScriptableObject
    {
        [SerializeField] private GameObject humanoidPrefab;
        [SerializeField] private RuntimeAnimatorController animatorController;
        [SerializeField] private Avatar avatar;
        [SerializeField] private Vector3 localPosition = new Vector3(0f, -1.05f, 0f);
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField] private Vector3 localScale = Vector3.one * 1.08f;
        [SerializeField, Range(0.01f, 0.5f)] private float locomotionBlendSeconds = 0.12f;
        [SerializeField, Range(0.01f, 0.5f)] private float castingBlendSeconds = 0.1f;
        [SerializeField, Range(0f, 1f)] private float handIkWeight = 0.92f;

        [Header("Animation Rescue / Landing")]
        [SerializeField, Range(0.15f, 1.2f)] private float landingPredictionHorizon = 0.65f;
        [SerializeField, Range(2, 8)] private int landingPredictionSteps = 6;
        [SerializeField, Range(0.03f, 0.12f)] private float minimumLandingAnticipation = 0.06f;
        [SerializeField, Range(0.10f, 0.24f)] private float maximumLandingAnticipation = 0.18f;
        [SerializeField, Range(0f, 0.25f)] private float landingCandidateGrace = 0.12f;
        [SerializeField, Range(2f, 8f)] private float softLandingImpactSpeed = 4.5f;
        [SerializeField, Range(5f, 12f)] private float hardLandingImpactSpeed = 7.5f;
        [SerializeField, Range(0.3f, 4f)] private float movingLandingPlanarSpeed = 1.2f;
        [SerializeField, Range(0.04f, 0.14f)] private float movingLandingRecovery = 0.08f;
        [SerializeField, Range(0.08f, 0.22f)] private float softLandingRecovery = 0.16f;
        [SerializeField, Range(0.28f, 0.42f)] private float hardLandingRecovery = 0.34f;
        [SerializeField, Range(0.02f, 0.16f)] private float fixedTransitionSeconds = 0.065f;
        [SerializeField, Range(0.1f, 1.2f)] private float softLandingContactSeconds = 0.625f;
        [SerializeField, Range(0.1f, 1.2f)] private float movingLandingContactSeconds = 0.533f;
        [SerializeField, Range(0.1f, 1.2f)] private float hardLandingContactSeconds = 0.625f;

        [Header("Animation Rescue / Locomotion")]
        [SerializeField, Range(30f, 240f)] private float referenceYawRateDegrees = 145f;
        [SerializeField, Range(0f, 30f)] private float measuredYawFallbackThreshold = 7f;
        [SerializeField, Range(0f, 0.25f)] private float turnDeadZone = 0.055f;
        [SerializeField, Range(0.02f, 0.18f)] private float turnEnterSeconds = 0.065f;
        [SerializeField, Range(0.12f, 0.30f)] private float turnReleaseSeconds = 0.16f;
        [SerializeField, Range(0.02f, 0.18f)] private float speedAccelerationSeconds = 0.075f;
        [SerializeField, Range(0.04f, 0.24f)] private float speedDecelerationSeconds = 0.11f;

        [Header("Animation Rescue / Moving Support")]
        [SerializeField, Range(0.02f, 0.25f)] private float surfPelvisResponseSeconds = 0.085f;
        [SerializeField, Range(0.2f, 1.5f)] private float surfPelvisMaximumSpeed = 0.8f;

        public GameObject HumanoidPrefab => humanoidPrefab;
        public RuntimeAnimatorController AnimatorController => animatorController;
        public Avatar Avatar => avatar;
        public Vector3 LocalPosition => localPosition;
        public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);
        public Vector3 LocalScale => localScale;
        public float LocomotionBlendSeconds => locomotionBlendSeconds;
        public float CastingBlendSeconds => castingBlendSeconds;
        public float HandIkWeight => handIkWeight;
        public float LandingPredictionHorizon => landingPredictionHorizon;
        public int LandingPredictionSteps => landingPredictionSteps;
        public float MinimumLandingAnticipation => minimumLandingAnticipation;
        public float MaximumLandingAnticipation => maximumLandingAnticipation;
        public float LandingCandidateGrace => landingCandidateGrace;
        public float SoftLandingImpactSpeed => softLandingImpactSpeed;
        public float HardLandingImpactSpeed => hardLandingImpactSpeed;
        public float MovingLandingPlanarSpeed => movingLandingPlanarSpeed;
        public float MovingLandingRecovery => movingLandingRecovery;
        public float SoftLandingRecovery => softLandingRecovery;
        public float HardLandingRecovery => hardLandingRecovery;
        public float FixedTransitionSeconds => fixedTransitionSeconds;
        public float SoftLandingContactSeconds => softLandingContactSeconds;
        public float MovingLandingContactSeconds => movingLandingContactSeconds;
        public float HardLandingContactSeconds => hardLandingContactSeconds;
        public float ReferenceYawRateDegrees => referenceYawRateDegrees;
        public float MeasuredYawFallbackThreshold => measuredYawFallbackThreshold;
        public float TurnDeadZone => turnDeadZone;
        public float TurnEnterSeconds => turnEnterSeconds;
        public float TurnReleaseSeconds => turnReleaseSeconds;
        public float SpeedAccelerationSeconds => speedAccelerationSeconds;
        public float SpeedDecelerationSeconds => speedDecelerationSeconds;
        public float SurfPelvisResponseSeconds => surfPelvisResponseSeconds;
        public float SurfPelvisMaximumSpeed => surfPelvisMaximumSpeed;

        public void Configure(
            GameObject configuredPrefab,
            RuntimeAnimatorController configuredController,
            Avatar configuredAvatar,
            Vector3 configuredLocalPosition,
            Vector3 configuredLocalEulerAngles,
            Vector3 configuredLocalScale)
        {
            humanoidPrefab = configuredPrefab;
            animatorController = configuredController;
            avatar = configuredAvatar;
            localPosition = configuredLocalPosition;
            localEulerAngles = configuredLocalEulerAngles;
            localScale = configuredLocalScale;
        }
    }
}
