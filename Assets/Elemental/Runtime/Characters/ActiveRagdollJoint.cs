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
        [SerializeField, Min(0f)] private float spring = 900f;
        [SerializeField, Min(0f)] private float damping = 65f;
        [SerializeField, Min(0f)] private float maximumForce = 1400f;
        [SerializeField, Range(1f, 90f)] private float angularLimit = 45f;

        private Quaternion _initialLocalRotation;
        private bool _initialized;

        public float JointErrorDegrees { get; private set; }
        public float LastAppliedTorqueEstimate { get; private set; }
        public Rigidbody Body => targetBody;
        public Transform TargetPose => targetPose;

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
            targetJoint.lowAngularXLimit = new SoftJointLimit { limit = -angularLimit };
            targetJoint.highAngularXLimit = new SoftJointLimit { limit = angularLimit };
            targetJoint.angularYLimit = new SoftJointLimit { limit = angularLimit };
            targetJoint.angularZLimit = new SoftJointLimit { limit = angularLimit };
            targetJoint.rotationDriveMode = RotationDriveMode.Slerp;
            targetJoint.projectionMode = JointProjectionMode.PositionAndRotation;
            targetJoint.projectionDistance = 0.08f;
            targetJoint.projectionAngle = 12f;
            targetJoint.enablePreprocessing = true;
            _initialLocalRotation = transform.localRotation;
            _initialized = true;
        }
    }
}
