using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    public readonly struct EarthAnimationGraphSettings
    {
        public EarthAnimationGraphSettings(
            bool usePlayablesAnimationGraph,
            bool usePoseInertialization,
            float positionHalfLifeSeconds = 0.075f,
            float rotationHalfLifeSeconds = 0.065f,
            float maximumDurationSeconds = 0.5f,
            float maximumPositionOffsetMeters = 0.5f,
            float maximumRotationOffsetDegrees = 150f,
            float maximumLinearVelocity = 15f,
            float maximumAngularVelocityRadians = 30f)
        {
            UsePlayablesAnimationGraph = usePlayablesAnimationGraph;
            UsePoseInertialization = usePoseInertialization;
            PositionHalfLifeSeconds = math.clamp(positionHalfLifeSeconds, 0.01f, 0.25f);
            RotationHalfLifeSeconds = math.clamp(rotationHalfLifeSeconds, 0.01f, 0.25f);
            MaximumDurationSeconds = math.clamp(maximumDurationSeconds, 0.05f, 1f);
            MaximumPositionOffsetMeters = math.clamp(maximumPositionOffsetMeters, 0.01f, 2f);
            MaximumRotationOffsetRadians = math.radians(math.clamp(maximumRotationOffsetDegrees, 1f, 180f));
            MaximumLinearVelocity = math.clamp(maximumLinearVelocity, 0.1f, 50f);
            MaximumAngularVelocityRadians = math.clamp(maximumAngularVelocityRadians, 0.1f, 80f);
        }

        public bool UsePlayablesAnimationGraph { get; }
        public bool UsePoseInertialization { get; }
        public float PositionHalfLifeSeconds { get; }
        public float RotationHalfLifeSeconds { get; }
        public float MaximumDurationSeconds { get; }
        public float MaximumPositionOffsetMeters { get; }
        public float MaximumRotationOffsetRadians { get; }
        public float MaximumLinearVelocity { get; }
        public float MaximumAngularVelocityRadians { get; }

        public static EarthAnimationGraphSettings Disabled =>
            new EarthAnimationGraphSettings(false, false);
    }

    [CreateAssetMenu(
        fileName = "EarthAnimationGraphProfile",
        menuName = "Elemental/Animation/Earth Animation Graph Profile")]
    public sealed class EarthAnimationGraphProfile : ScriptableObject
    {
        [Header("Feature Flags (default off until integration validation)")]
        [SerializeField] private bool usePlayablesAnimationGraph;
        [SerializeField] private bool usePoseInertialization;

        [Header("Bounded inertial decay")]
        [SerializeField, Range(0.01f, 0.25f)] private float positionHalfLifeSeconds = 0.075f;
        [SerializeField, Range(0.01f, 0.25f)] private float rotationHalfLifeSeconds = 0.065f;
        [SerializeField, Range(0.05f, 1f)] private float maximumDurationSeconds = 0.5f;
        [SerializeField, Range(0.01f, 2f)] private float maximumPositionOffsetMeters = 0.5f;
        [SerializeField, Range(1f, 180f)] private float maximumRotationOffsetDegrees = 150f;
        [SerializeField, Range(0.1f, 50f)] private float maximumLinearVelocity = 15f;
        [SerializeField, Range(0.1f, 80f)] private float maximumAngularVelocityRadians = 30f;

        public bool UsePlayablesAnimationGraph => usePlayablesAnimationGraph;
        public bool UsePoseInertialization => usePoseInertialization;

        public EarthAnimationGraphSettings Settings => new EarthAnimationGraphSettings(
            usePlayablesAnimationGraph,
            usePoseInertialization,
            positionHalfLifeSeconds,
            rotationHalfLifeSeconds,
            maximumDurationSeconds,
            maximumPositionOffsetMeters,
            maximumRotationOffsetDegrees,
            maximumLinearVelocity,
            maximumAngularVelocityRadians);
    }
}
