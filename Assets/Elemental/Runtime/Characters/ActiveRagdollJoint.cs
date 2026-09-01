using Elemental.Simulation.Characters;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(ConfigurableJoint))]
    public sealed class ActiveRagdollJoint : MonoBehaviour
    {
        [SerializeField] private Rigidbody targetBody;
        [SerializeField] private ConfigurableJoint targetJoint;
        [SerializeField] private Transform targetPose;
        [SerializeField] private EarthBodyRegion bodyRegion = EarthBodyRegion.Chest;
        [SerializeField, Min(0f)] private float spring = 900f;
        [SerializeField, Min(0f)] private float damping = 65f;
        [SerializeField, Min(0f)] private float maximumForce = 1400f;
        [SerializeField, Range(1f, 90f)] private float angularLimit = 45f;

        private Quaternion _initialLocalRotation;
        private bool _initialized;
        private float _poweredDriveWeight;
        private float _lastAppliedAngularLimit = -1f;

        public float JointErrorDegrees { get; private set; }
        public float LastAppliedTorqueEstimate { get; private set; }
        public Rigidbody Body => targetBody;
        public Transform TargetPose => targetPose;
        public EarthBodyRegion BodyRegion => bodyRegion;

        public void Configure(
            Rigidbody body,
            ConfigurableJoint joint,
            Transform poseTarget,
            float configuredSpring,
            float configuredDamping,
            float configuredMaximumForce,
            float configuredAngularLimit)
        {
            targetBody = body;
            targetJoint = joint;
            targetPose = poseTarget;
            spring = Mathf.Max(0f, configuredSpring);
            damping = Mathf.Max(0f, configuredDamping);
            maximumForce = Mathf.Max(0f, configuredMaximumForce);
            angularLimit = Mathf.Clamp(configuredAngularLimit, 1f, 90f);
            InitializeJoint();
        }

        public void ConfigureBodyRegion(EarthBodyRegion configuredRegion) =>
            bodyRegion = configuredRegion;

        private void Awake()
        {
            InitializeJoint();
        }

        public void ApplyPose(float muscleStrength)
        {
            if (!_initialized || targetPose == null)
            {
                return;
            }

            float muscle = Mathf.Clamp01(muscleStrength);
            ApplyAngularLimits(angularLimit);
            JointDrive drive = targetJoint.slerpDrive;
            drive.positionSpring = spring * muscle;
            drive.positionDamper = damping * Mathf.Lerp(0.35f, 1f, muscle);
            drive.maximumForce = maximumForce * muscle;
            targetJoint.slerpDrive = drive;
            targetJoint.targetRotation = Quaternion.Inverse(targetPose.localRotation) * _initialLocalRotation;

            JointErrorDegrees = Quaternion.Angle(targetBody.rotation, targetPose.rotation);
            LastAppliedTorqueEstimate = Mathf.Min(
                maximumForce * muscle,
                JointErrorDegrees * Mathf.Deg2Rad * spring * muscle);
        }

        public void ApplyPoweredPose(
            in EarthMuscleRegionTuning tuning,
            float responseWeight,
            float deltaTime)
        {
            if (!_initialized || targetPose == null) return;

            float targetWeight = tuning.DriveWeight * Mathf.Lerp(
                0.65f,
                1f,
                Mathf.Clamp01(responseWeight));
            _poweredDriveWeight = targetWeight <= 0f
                ? 0f
                : EarthMuscleProfiles.StepDriveWeight(
                    _poweredDriveWeight,
                    targetWeight,
                    tuning.RecoveryRate,
                    deltaTime);
            float omega = 2f * Mathf.PI * tuning.Frequency;
            JointDrive drive = targetJoint.slerpDrive;
            drive.positionSpring = omega * omega * _poweredDriveWeight;
            drive.positionDamper = 2f * tuning.Damping * omega * _poweredDriveWeight;
            drive.maximumForce = tuning.TorqueCap * _poweredDriveWeight;
            targetJoint.slerpDrive = drive;
            ApplyAngularLimits(tuning.AngularLimitDegrees);
            Quaternion desired = Quaternion.Inverse(targetPose.localRotation) *
                                 _initialLocalRotation;
            targetJoint.targetRotation = Quaternion.Slerp(
                Quaternion.identity,
                desired,
                tuning.TransferWeight);

            JointErrorDegrees = Quaternion.Angle(targetBody.rotation, targetPose.rotation);
            LastAppliedTorqueEstimate = Mathf.Min(
                drive.maximumForce,
                omega * omega * JointErrorDegrees * Mathf.Deg2Rad *
                _poweredDriveWeight * tuning.TransferWeight);
        }

        private void InitializeJoint()
        {
            if (targetBody == null)
            {
                targetBody = GetComponent<Rigidbody>();
            }

            if (targetJoint == null)
            {
                targetJoint = GetComponent<ConfigurableJoint>();
            }

            if (targetBody == null || targetJoint == null)
            {
                return;
            }

            targetJoint.xMotion = ConfigurableJointMotion.Locked;
            targetJoint.yMotion = ConfigurableJointMotion.Locked;
            targetJoint.zMotion = ConfigurableJointMotion.Locked;
            targetJoint.angularXMotion = ConfigurableJointMotion.Limited;
            targetJoint.angularYMotion = ConfigurableJointMotion.Limited;
            targetJoint.angularZMotion = ConfigurableJointMotion.Limited;
            ApplyAngularLimits(angularLimit);
            targetJoint.rotationDriveMode = RotationDriveMode.Slerp;
            targetJoint.projectionMode = JointProjectionMode.PositionAndRotation;
            targetJoint.projectionDistance = 0.08f;
            targetJoint.projectionAngle = 12f;
            targetJoint.enablePreprocessing = true;
            _initialLocalRotation = transform.localRotation;
            _initialized = true;
        }

        private void ApplyAngularLimits(float requestedLimit)
        {
            float limit = Mathf.Clamp(requestedLimit, 1f, 90f);
            if (Mathf.Abs(limit - _lastAppliedAngularLimit) <= 0.001f) return;
            targetJoint.lowAngularXLimit = new SoftJointLimit { limit = -limit };
            targetJoint.highAngularXLimit = new SoftJointLimit { limit = limit };
            targetJoint.angularYLimit = new SoftJointLimit { limit = limit };
            targetJoint.angularZLimit = new SoftJointLimit { limit = limit };
            _lastAppliedAngularLimit = limit;
        }
    }
}
