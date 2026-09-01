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
        [SerializeField] private EarthBodyRegion bodyRegion = EarthBodyRegion.Unassigned;
        [SerializeField, Min(0f)] private float spring = 900f;
        [SerializeField, Min(0f)] private float damping = 65f;
        [SerializeField, Min(0f)] private float maximumForce = 1400f;
        [SerializeField, Range(1f, 90f)] private float angularLimit = 45f;

        private Quaternion _initialLocalRotation;
        private bool _initialized;
        private float _poweredDriveWeight;
        private float _lastAppliedAngularLimit = -1f;
        private bool _reportedMissingBodyRegion;

        public float JointErrorDegrees { get; private set; }
        public float LastAppliedTorqueEstimate { get; private set; }
        public Rigidbody Body => targetBody;
        public Transform TargetPose => targetPose;
        public EarthBodyRegion BodyRegion => bodyRegion;
        public bool HasConfiguredBodyRegion =>
            (int)bodyRegion >= (int)EarthBodyRegion.Pelvis &&
            (int)bodyRegion <= (int)EarthBodyRegion.Leg;

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

        public bool ConfigureBodyRegion(EarthBodyRegion configuredRegion)
        {
            int regionValue = (int)configuredRegion;
            if (regionValue < (int)EarthBodyRegion.Pelvis ||
                regionValue > (int)EarthBodyRegion.Leg)
            {
                bodyRegion = EarthBodyRegion.Unassigned;
                Debug.LogError(
                    $"{nameof(ActiveRagdollJoint)} on '{name}' requires an explicit " +
                    "Pelvis/Spine/Chest/Head/Arm/Leg binding.",
                    this);
                return false;
            }

            bodyRegion = configuredRegion;
            _reportedMissingBodyRegion = false;
            return true;
        }

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
            if (!HasConfiguredBodyRegion)
            {
                DisablePoweredPose();
                if (!_reportedMissingBodyRegion)
                {
                    _reportedMissingBodyRegion = true;
                    Debug.LogError(
                        $"{nameof(ActiveRagdollJoint)} on '{name}' disabled powered assist " +
                        "because its body region is unassigned.",
                        this);
                }
                return;
            }
            if (tuning.Frequency <= 0f ||
                tuning.TorqueCap <= 0f ||
                tuning.DriveWeight <= 0f)
            {
                DisablePoweredPose();
                return;
            }

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

        public void DisablePoweredPose()
        {
            if (!_initialized) return;
            _poweredDriveWeight = 0f;
            ZeroAllDriveChannels(targetJoint);
            LastAppliedTorqueEstimate = 0f;
        }

        private static void ZeroAllDriveChannels(ConfigurableJoint joint)
        {
            JointDrive disabledDrive = default;
            joint.xDrive = disabledDrive;
            joint.yDrive = disabledDrive;
            joint.zDrive = disabledDrive;
            joint.angularXDrive = disabledDrive;
            joint.angularYZDrive = disabledDrive;
            joint.slerpDrive = disabledDrive;
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
