using Elemental.Simulation.Structures;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [CreateAssetMenu(menuName = "Elemental/Magic/Earth Repair Profile", fileName = "EarthRepairProfile")]
    public sealed class EarthRepairProfile : ScriptableObject
    {
        [Header("Selection")]
        [SerializeField] private EarthRepairAnchorMode anchorMode = EarthRepairAnchorMode.OriginalStructureFrame;
        [SerializeField, Min(1f)] private float maximumSelectedMass = 30000f;
        [Header("Capture and staging")]
        [SerializeField, Min(0.05f)] private float captureSettleTime = 0.08f;
        [SerializeField, Min(0.05f)] private float alignmentSettleTime = 0.08f;
        [SerializeField, Min(0.1f)] private float stagingDistance = 0.78f;
        [SerializeField, Min(0.05f)] private float stagingTolerance = 0.5f;
        [SerializeField, Min(0.05f)] private float maximumCaptureSeconds = 1f;
        [Header("Bounded PD")]
        [SerializeField, Range(0.2f, 2f)] private float dampingRatio = 1f;
        [SerializeField, Min(1f)] private float maximumAcceleration = 180f;
        [SerializeField, Min(1f)] private float maximumForce = 150000f;
        [SerializeField, Min(1f)] private float maximumAngularAcceleration = 240f;
        [SerializeField, Min(1f)] private float rotationStiffness = 90f;
        [SerializeField, Min(0f)] private float rotationDamping = 24f;
        [Header("Weld gate")]
        [SerializeField, Range(0.005f, 0.08f)] private float positionTolerance = 0.06f;
        [SerializeField, Range(0.5f, 8f)] private float angleToleranceDegrees = 6f;
        [SerializeField, Range(0.02f, 0.5f)] private float maximumRelativeSpeed = 0.45f;
        [SerializeField, Range(0.02f, 1f)] private float maximumRelativeAngularSpeed = 0.6f;
        [SerializeField, Range(0.05f, 0.4f)] private float settleDuration = 0.05f;
        [Header("Jam recovery")]
        [SerializeField, Range(0.15f, 2f)] private float jamDuration = 0.35f;
        [SerializeField, Range(0.0005f, 0.05f)] private float jamProgressEpsilon = 0.002f;
        [SerializeField, Range(0.02f, 0.8f)] private float retryDelay = 0.04f;

        public EarthRepairAnchorMode AnchorMode => anchorMode;
        public float MaximumSelectedMass => maximumSelectedMass;
        public float StagingDistance => stagingDistance;
        public float StagingTolerance => stagingTolerance;
        public float MaximumCaptureSeconds => maximumCaptureSeconds;

        public EarthReassemblyTuning ToTuning()
        {
            return new EarthReassemblyTuning
            {
                CaptureSettleTime = captureSettleTime,
                AlignmentSettleTime = alignmentSettleTime,
                DampingRatio = dampingRatio,
                MaximumAcceleration = maximumAcceleration,
                MaximumForce = maximumForce,
                MaximumAngularAcceleration = maximumAngularAcceleration,
                RotationStiffness = rotationStiffness,
                RotationDamping = rotationDamping,
                PositionTolerance = positionTolerance,
                AngleToleranceRadians = math.radians(angleToleranceDegrees),
                MaximumRelativeSpeed = maximumRelativeSpeed,
                MaximumRelativeAngularSpeed = maximumRelativeAngularSpeed,
                SettleDuration = settleDuration,
                JamDuration = jamDuration,
                JamProgressEpsilon = jamProgressEpsilon,
                RetryDelay = retryDelay
            };
        }
    }
}
